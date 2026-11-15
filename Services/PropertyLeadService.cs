using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NHN.Power.API.Data;
using NHN.Power.API.DTOs.PropertyLead;
using NHN.Power.API.Models;

namespace NHN.Power.API.Services
{
    public class PropertyLeadService : IPropertyLeadService
    {
        private readonly AppDbContext _dbContext;
        private readonly IAzureDocumentIntelligenceService _ocrService;
        private readonly IStorageService _storageService;
        private readonly ILogger<PropertyLeadService> _logger;

        private static readonly string[] StreetSuffixes = new[]
        {
            "Street", "St", "Road", "Rd", "Avenue", "Ave", "Drive", "Dr",
            "Way", "Court", "Ct", "Place", "Pl", "Close", "Cl", "Boulevard",
            "Blvd", "Lane", "Ln", "Parade", "Pde", "Circuit", "Cct", "Terrace",
            "Tce", "Crescent", "Cres", "Highway", "Hwy", "Grove", "Gr",
            "Esplanade", "Esp", "Walk", "Rise", "Track", "Alley"
        };

        public PropertyLeadService(
            AppDbContext dbContext,
            IAzureDocumentIntelligenceService ocrService,
            IStorageService storageService,
            ILogger<PropertyLeadService> logger)
        {
            _dbContext = dbContext;
            _ocrService = ocrService;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<List<PropertyLeadDto>> GetPropertyLeadsAsync(bool? isAdvertised = null, string? status = null)
        {
            var query = _dbContext.PropertyLeads.AsNoTracking().AsQueryable();

            if (isAdvertised.HasValue)
            {
                query = query.Where(p => p.IsAdvertised == isAdvertised.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(p => p.Status.ToLower() == status.Trim().ToLower());
            }

            var list = await query
                .OrderBy(p => p.IsAdvertised)
                .ThenByDescending(p => p.CreatedAt)
                .ToListAsync();

            return list.Select(MapToDto).ToList();
        }

        public async Task<BatchUploadPropertyLeadsResultDto> BatchUploadAndParseAsync(BatchUploadPropertyLeadsRequestDto request)
        {
            var result = new BatchUploadPropertyLeadsResultDto();
            if (request.Images == null || !request.Images.Any())
            {
                return result;
            }

            result.TotalImagesProcessed = request.Images.Count;

            foreach (var imgItem in request.Images)
            {
                var itemResult = new PropertyLeadProcessingItemResultDto
                {
                    FileName = imgItem.FileName
                };

                try
                {
                    if (string.IsNullOrWhiteSpace(imgItem.Base64Data))
                    {
                        itemResult.Action = "failed";
                        itemResult.Message = "Base64 image data is empty.";
                        result.FailedCount++;
                        result.Results.Add(itemResult);
                        continue;
                    }

                    // 1. 解码图片
                    string cleanBase64 = imgItem.Base64Data;
                    int commaIdx = cleanBase64.IndexOf(',');
                    if (commaIdx >= 0)
                    {
                        cleanBase64 = cleanBase64.Substring(commaIdx + 1);
                    }
                    byte[] imageBytes = Convert.FromBase64String(cleanBase64);

                    // 2. 备份上传至 Cloudflare R2（若配置）
                    string? r2Key = null;
                    try
                    {
                        string safeName = Path.GetFileNameWithoutExtension(imgItem.FileName).Replace(" ", "_");
                        r2Key = $"property_leads/{DateTime.UtcNow:yyyyMM}/{Guid.NewGuid()}_{safeName}.png";
                        await _storageService.UploadFileAsync(r2Key, imageBytes, "image/png");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to upload lead image to R2 for {FileName}", imgItem.FileName);
                    }

                    // 3. Azure Document Intelligence OCR 提取文本
                    string ocrText = await _ocrService.AnalyzeLayoutTextAsync(imageBytes);
                    if (string.IsNullOrWhiteSpace(ocrText))
                    {
                        itemResult.Action = "failed";
                        itemResult.Message = "OCR service returned empty text.";
                        result.FailedCount++;
                        result.Results.Add(itemResult);
                        continue;
                    }

                    // 4. 解析房产信息
                    var parsed = ParsePropertyLeadFromText(ocrText);
                    if (string.IsNullOrWhiteSpace(parsed.Address))
                    {
                        itemResult.Action = "failed";
                        itemResult.Message = "Could not identify valid property address from image.";
                        itemResult.Status = parsed.Status;
                        result.FailedCount++;
                        result.Results.Add(itemResult);
                        continue;
                    }

                    itemResult.Address = parsed.Address;
                    itemResult.Status = parsed.Status;

                    // 5. 数据库去重与更新智能比对
                    string normalizedAddr = parsed.Address.Trim().ToLowerInvariant();

                    var existing = await _dbContext.PropertyLeads
                        .FirstOrDefaultAsync(p => p.Address.Trim().ToLower() == normalizedAddr);

                    if (existing != null)
                    {
                        // 检查状态是否有变化
                        if (existing.Status.Trim().Equals(parsed.Status.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            itemResult.Action = "skipped";
                            itemResult.Message = $"Address already exists with identical status '{existing.Status}'. Skipped.";
                            itemResult.Lead = MapToDto(existing);
                            result.SkippedCount++;
                        }
                        else
                        {
                            string oldStatus = existing.Status;
                            existing.Status = parsed.Status;
                            existing.UpdatedAt = DateTimeOffset.UtcNow;
                            if (parsed.Bedrooms > 0 && existing.Bedrooms == 0) existing.Bedrooms = parsed.Bedrooms;
                            if (parsed.Bathrooms > 0 && existing.Bathrooms == 0) existing.Bathrooms = parsed.Bathrooms;
                            if (parsed.CarSpaces.HasValue && existing.CarSpaces == 0) existing.CarSpaces = parsed.CarSpaces;
                            if (!string.IsNullOrWhiteSpace(parsed.PriceGuide) && string.IsNullOrWhiteSpace(existing.PriceGuide))
                            {
                                existing.PriceGuide = parsed.PriceGuide;
                            }

                            await _dbContext.SaveChangesAsync();

                            itemResult.Action = "updated";
                            itemResult.Message = $"Status updated from '{oldStatus}' to '{parsed.Status}'.";
                            itemResult.Lead = MapToDto(existing);
                            result.UpdatedCount++;
                        }
                    }
                    else
                    {
                        // 新增线索
                        var newLead = new PropertyLead
                        {
                            Address = parsed.Address,
                            Bedrooms = parsed.Bedrooms,
                            Bathrooms = parsed.Bathrooms,
                            CarSpaces = parsed.CarSpaces,
                            Status = parsed.Status,
                            PriceGuide = parsed.PriceGuide,
                            IsAdvertised = false,
                            AdvertisedAt = null,
                            RawOcrText = ocrText,
                            ImageR2Key = r2Key,
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        };

                        _dbContext.PropertyLeads.Add(newLead);
                        await _dbContext.SaveChangesAsync();

                        itemResult.Action = "created";
                        itemResult.Message = "New property lead created successfully.";
                        itemResult.Lead = MapToDto(newLead);
                        result.CreatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing property lead image {FileName}", imgItem.FileName);
                    itemResult.Action = "failed";
                    itemResult.Message = ex.Message;
                    result.FailedCount++;
                }

                result.Results.Add(itemResult);
            }

            return result;
        }

        public async Task<PropertyLeadDto?> ToggleAdvertisedAsync(Guid id, bool? isAdvertised = null)
        {
            var lead = await _dbContext.PropertyLeads.FindAsync(id);
            if (lead == null) return null;

            if (isAdvertised.HasValue)
            {
                lead.IsAdvertised = isAdvertised.Value;
            }
            else
            {
                lead.IsAdvertised = !lead.IsAdvertised;
            }

            lead.AdvertisedAt = lead.IsAdvertised ? DateTimeOffset.UtcNow : null;
            lead.UpdatedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync();
            return MapToDto(lead);
        }

        public async Task<bool> DeletePropertyLeadAsync(Guid id)
        {
            var lead = await _dbContext.PropertyLeads.FindAsync(id);
            if (lead == null) return false;

            _dbContext.PropertyLeads.Remove(lead);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        // =========================================================================
        // OCR 文本解析辅助算法
        // =========================================================================

        private class ParsedLeadInfo
        {
            public string Address { get; set; } = string.Empty;
            public int Bedrooms { get; set; } = 0;
            public int Bathrooms { get; set; } = 0;
            public int? CarSpaces { get; set; } = 0;
            public string Status { get; set; } = "Under offer";
            public string? PriceGuide { get; set; }
        }

        private static ParsedLeadInfo ParsePropertyLeadFromText(string ocrText)
        {
            var info = new ParsedLeadInfo();

            var lines = ocrText
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            // 1. 解析 Status
            if (Regex.IsMatch(ocrText, @"\bunder\s*offer\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(ocrText, @"\bunder\s*contract\b", RegexOptions.IgnoreCase))
            {
                info.Status = "Under offer";
            }
            else if (Regex.IsMatch(ocrText, @"\bsold\b", RegexOptions.IgnoreCase))
            {
                info.Status = "Sold";
            }
            else if (Regex.IsMatch(ocrText, @"\bdeposit\s*taken\b", RegexOptions.IgnoreCase))
            {
                info.Status = "Deposit Taken";
            }
            else if (Regex.IsMatch(ocrText, @"\bfor\s*sale\b", RegexOptions.IgnoreCase))
            {
                info.Status = "For Sale";
            }
            else if (Regex.IsMatch(ocrText, @"\bauction\b", RegexOptions.IgnoreCase))
            {
                info.Status = "Auction";
            }

            // 2. 解析 Price Guide
            var priceMatch = Regex.Match(ocrText, @"\$[\d,]+(?:\s*-\s*\$[\d,]+)?");
            if (priceMatch.Success)
            {
                info.PriceGuide = priceMatch.Value.Trim();
            }

            // 3. 解析 Address
            string remainder = "";
            var addrMatch = Regex.Match(ocrText, @"(\b\d+[\w/-]*\s+[A-Za-z0-9\s'-]+?\s+(?:Street|St|Road|Rd|Avenue|Ave|Drive|Dr|Way|Court|Ct|Place|Pl|Close|Cl|Boulevard|Blvd|Lane|Ln|Parade|Pde|Circuit|Cct|Terrace|Tce|Crescent|Cres|Highway|Hwy|Grove|Gr|Esplanade|Esp|Walk|Rise)(?:,?\s+[A-Za-z]+(?:\s+[A-Za-z]+)?)?)", RegexOptions.IgnoreCase);
            if (addrMatch.Success)
            {
                info.Address = addrMatch.Value.Trim();
                int idx = addrMatch.Index + addrMatch.Length;
                remainder = ocrText.Substring(idx);
            }
            else
            {
                // 逐行降级查找以数字开头且包含街道后缀的非价格行
                foreach (var line in lines)
                {
                    if (line.Contains("$") || line.Length < 6) continue;
                    foreach (var suffix in StreetSuffixes)
                    {
                        if (Regex.IsMatch(line, $@"\b{suffix}\b", RegexOptions.IgnoreCase) &&
                            Regex.IsMatch(line, @"^\s*(?:Unit\s+\d+,?\s*)?\d+[\w/-]*\s+[A-Za-z]"))
                        {
                            info.Address = line.Trim();
                            break;
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(info.Address)) break;
                }
            }

            // 4. 解析 Bedrooms / Bathrooms / Car Spaces
            // 模式 A：从紧随地址后面的文本段提取规格数字
            if (!string.IsNullOrWhiteSpace(remainder))
            {
                var specsMatch = Regex.Match(remainder, @"(\d)\s*[^\d\r\n]*?\s*(\d)(?:\s*[^\d\r\n]*?\s*(\d+))?(?:\s*\d+(?:m²|m2|sqm))?");
                if (specsMatch.Success)
                {
                    if (int.TryParse(specsMatch.Groups[1].Value, out var sb)) info.Bedrooms = sb;
                    if (int.TryParse(specsMatch.Groups[2].Value, out var sba)) info.Bathrooms = sba;
                    if (specsMatch.Groups[3].Success && int.TryParse(specsMatch.Groups[3].Value, out var sc))
                    {
                        if (sc > 6) sc = sc % 10;
                        info.CarSpaces = sc;
                    }
                }
            }

            // 模式 B：带关键词 "3 bed 2 bath 2 car"
            if (info.Bedrooms == 0)
            {
                var bedKw = Regex.Match(ocrText, @"(\d+)\s*(?:bed|br|bedroom)", RegexOptions.IgnoreCase);
                var bathKw = Regex.Match(ocrText, @"(\d+)\s*(?:bath|ba|bathroom)", RegexOptions.IgnoreCase);
                var carKw = Regex.Match(ocrText, @"(\d+)\s*(?:car|garage|parking)", RegexOptions.IgnoreCase);

                if (bedKw.Success && int.TryParse(bedKw.Groups[1].Value, out var b)) info.Bedrooms = b;
                if (bathKw.Success && int.TryParse(bathKw.Groups[1].Value, out var ba)) info.Bathrooms = ba;
                if (carKw.Success && int.TryParse(carKw.Groups[1].Value, out var c)) info.CarSpaces = c;
            }

            // 模式 C：卡片独立数字行
            if (info.Bedrooms == 0)
            {
                foreach (var line in lines)
                {
                    var specMatch = Regex.Match(line, @"(?:^|\s)(\d)\s+(\d)(?:\s+(\d))?(?:\s+\d+(?:m²|sqm))?");
                    if (specMatch.Success)
                    {
                        if (int.TryParse(specMatch.Groups[1].Value, out var sb)) info.Bedrooms = sb;
                        if (int.TryParse(specMatch.Groups[2].Value, out var sba)) info.Bathrooms = sba;
                        if (specMatch.Groups[3].Success && int.TryParse(specMatch.Groups[3].Value, out var sc))
                        {
                            if (sc > 6) sc = sc % 10;
                            info.CarSpaces = sc;
                        }
                        break;
                    }
                }
            }

            return info;
        }

        private static PropertyLeadDto MapToDto(PropertyLead entity)
        {
            return new PropertyLeadDto
            {
                Id = entity.Id,
                Address = entity.Address,
                Bedrooms = entity.Bedrooms,
                Bathrooms = entity.Bathrooms,
                CarSpaces = entity.CarSpaces,
                Status = entity.Status,
                PriceGuide = entity.PriceGuide,
                IsAdvertised = entity.IsAdvertised,
                AdvertisedAt = entity.AdvertisedAt,
                RawOcrText = entity.RawOcrText,
                ImageR2Key = entity.ImageR2Key,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
