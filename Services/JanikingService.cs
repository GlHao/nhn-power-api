using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NHN.Power.API.Data;
using NHN.Power.API.DTOs.Janiking;
using NHN.Power.API.Models;

namespace NHN.Power.API.Services
{
    public class JanikingService : IJanikingService
    {
        private readonly AppDbContext _dbContext;
        private readonly IJanikingPdfParserService _parserService;
        private readonly IStorageService _storageService;
        private readonly ILogger<JanikingService> _logger;

        public JanikingService(
            AppDbContext dbContext,
            IJanikingPdfParserService parserService,
            IStorageService storageService,
            ILogger<JanikingService> logger)
        {
            _dbContext = dbContext;
            _parserService = parserService;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<JanikingUploadResultDto> UploadAndProcessReportAsync(Stream fileStream, string fileName)
        {
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms);
            var fileBytes = ms.ToArray();

            // 1. Parse PDF
            var parsedData = await _parserService.ParsePdfAsync(fileBytes);

            // 2. Deduplication check
            var existingReport = await _dbContext.JanikingReports
                .FirstOrDefaultAsync(r => r.ReportMonth == parsedData.ReportMonth);

            if (existingReport != null)
            {
                _logger.LogWarning("Jani-King report for {Month} already exists.", parsedData.ReportMonth);
                return new JanikingUploadResultDto
                {
                    Success = false,
                    Message = $"该月份（{parsedData.ReportMonth}）报表已存在，请勿重复导入。",
                    ReportMonth = parsedData.ReportMonth
                };
            }

            // 3. Check freshness against latest month in DB
            var latestReport = await _dbContext.JanikingReports
                .OrderByDescending(r => r.ReportMonth)
                .FirstOrDefaultAsync();

            bool isNewerMonth = latestReport == null || string.Compare(parsedData.ReportMonth, latestReport.ReportMonth, StringComparison.OrdinalIgnoreCase) >= 0;

            if (isNewerMonth)
            {
                // Full sync clients: add new, update existing, mark missing as inactive
                var existingClients = await _dbContext.JanikingClients.ToListAsync();
                var existingMap = existingClients.ToDictionary(c => c.CustomerCode, c => c, StringComparer.OrdinalIgnoreCase);
                var matchedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var pc in parsedData.Clients)
                {
                    matchedCodes.Add(pc.CustomerCode);
                    if (existingMap.TryGetValue(pc.CustomerCode, out var client))
                    {
                        client.Name = pc.Name;
                        client.ContractBilling = pc.ContractBilling;
                        client.FindersFeeRaw = pc.FindersFeeRaw;
                        client.FindersFeeEndMonth = pc.FindersFeeEndMonth;
                        client.FindersFeeAmount = pc.FindersFeeAmount;
                        client.ActualBilling = pc.ActualBilling;
                        client.IsActive = true;
                        client.LastSyncMonth = parsedData.ReportMonth;
                        client.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        var newClient = new JanikingClient
                        {
                            CustomerCode = pc.CustomerCode,
                            Name = pc.Name,
                            ContractBilling = pc.ContractBilling,
                            FindersFeeRaw = pc.FindersFeeRaw,
                            FindersFeeEndMonth = pc.FindersFeeEndMonth,
                            FindersFeeAmount = pc.FindersFeeAmount,
                            ActualBilling = pc.ActualBilling,
                            IsActive = true,
                            LastSyncMonth = parsedData.ReportMonth,
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        };
                        _dbContext.JanikingClients.Add(newClient);
                    }
                }

                // Missing clients become inactive
                foreach (var client in existingClients)
                {
                    if (!matchedCodes.Contains(client.CustomerCode))
                    {
                        client.IsActive = false;
                        client.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                }

                // Add Charge Backs
                foreach (var pcb in parsedData.ChargeBacks)
                {
                    _dbContext.JanikingChargeBacks.Add(new JanikingChargeBack
                    {
                        ReportMonth = pcb.ReportMonth,
                        InvoiceNumber = pcb.InvoiceNumber,
                        InvoiceDate = pcb.InvoiceDate,
                        CustomerName = pcb.CustomerName,
                        Description = pcb.Description,
                        TotalAmount = pcb.TotalAmount,
                        PaymentsAmount = pcb.PaymentsAmount,
                        ChargeBackAmount = pcb.ChargeBackAmount,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }
            }
            else
            {
                _logger.LogInformation("Imported report {Month} is older than current latest {LatestMonth}. Skipping clients table overwrite to protect active client profiles.",
                    parsedData.ReportMonth, latestReport?.ReportMonth);

                // Add ChargeBacks for historical month if any
                foreach (var pcb in parsedData.ChargeBacks)
                {
                    _dbContext.JanikingChargeBacks.Add(new JanikingChargeBack
                    {
                        ReportMonth = pcb.ReportMonth,
                        InvoiceNumber = pcb.InvoiceNumber,
                        InvoiceDate = pcb.InvoiceDate,
                        CustomerName = pcb.CustomerName,
                        Description = pcb.Description,
                        TotalAmount = pcb.TotalAmount,
                        PaymentsAmount = pcb.PaymentsAmount,
                        ChargeBackAmount = pcb.ChargeBackAmount,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }
            }

            // 4. Upload to Cloudflare R2
            string year = parsedData.ReportMonth.Length >= 4 ? parsedData.ReportMonth.Substring(0, 4) : DateTime.UtcNow.Year.ToString();
            string cleanFileName = Path.GetFileNameWithoutExtension(fileName);
            string r2Key = $"janiking/{year}/{parsedData.ReportMonth}_{Guid.NewGuid():N}.pdf";

            bool uploadSuccess = await _storageService.UploadFileAsync(r2Key, fileBytes, "application/pdf");
            if (!uploadSuccess)
            {
                _logger.LogWarning("Failed to upload Jani-King PDF to R2. Storing key without live upload.");
            }

            string r2Url = _storageService.GeneratePreSignedReadUrl(r2Key, TimeSpan.FromDays(7));

            // 5. Insert report record
            var report = new JanikingReport
            {
                ReportMonth = parsedData.ReportMonth,
                DueToFranchisee = parsedData.DueToFranchisee,
                ChargeBackAmount = parsedData.ChargeBackAmount,
                ExpectedAmount = parsedData.ExpectedAmount,
                TotalContractBilling = parsedData.TotalContractBilling,
                R2Key = r2Key,
                R2Url = r2Url,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.JanikingReports.Add(report);
            await _dbContext.SaveChangesAsync();

            return new JanikingUploadResultDto
            {
                Success = true,
                Message = $"成功导入 {parsedData.ReportMonth} 月度报表。",
                ReportMonth = parsedData.ReportMonth,
                DueToFranchisee = parsedData.DueToFranchisee,
                ChargeBackAmount = parsedData.ChargeBackAmount,
                ExpectedAmount = parsedData.ExpectedAmount,
                TotalContractBilling = parsedData.TotalContractBilling,
                ClientsCount = parsedData.Clients.Count,
                ChargeBacksCount = parsedData.ChargeBacks.Count,
                IsNewerMonth = isNewerMonth
            };
        }

        public async Task<JanikingClientsSummaryDto> GetClientsSummaryAsync()
        {
            var clients = await _dbContext.JanikingClients
                .OrderByDescending(c => c.IsActive)
                .ThenBy(c => c.Name)
                .ToListAsync();

            var activeClients = clients.Where(c => c.IsActive).ToList();

            var result = new JanikingClientsSummaryDto
            {
                Clients = clients.Select(c => new JanikingClientDto
                {
                    Id = c.Id,
                    CustomerCode = c.CustomerCode,
                    Name = c.Name,
                    ContractBilling = c.ContractBilling,
                    FindersFeeRaw = c.FindersFeeRaw,
                    FindersFeeEndMonth = c.FindersFeeEndMonth,
                    FindersFeeAmount = c.FindersFeeAmount,
                    ActualBilling = c.ActualBilling,
                    IsActive = c.IsActive,
                    LastSyncMonth = c.LastSyncMonth
                }).ToList(),
                TotalContractBilling = activeClients.Sum(c => c.ContractBilling),
                TotalFindersFee = activeClients.Sum(c => c.FindersFeeAmount),
                TotalActualBilling = activeClients.Sum(c => c.ActualBilling),
                ActiveClientCount = activeClients.Count,
                LatestReportMonth = activeClients.FirstOrDefault()?.LastSyncMonth
            };

            return result;
        }

        public async Task<List<JanikingChargeBackDto>> GetChargeBacksAsync()
        {
            var chargeBacks = await _dbContext.JanikingChargeBacks
                .OrderByDescending(cb => cb.ReportMonth)
                .ThenByDescending(cb => cb.InvoiceDate)
                .ToListAsync();

            return chargeBacks.Select(cb => new JanikingChargeBackDto
            {
                Id = cb.Id,
                ReportMonth = cb.ReportMonth,
                InvoiceNumber = cb.InvoiceNumber,
                InvoiceDate = cb.InvoiceDate,
                CustomerName = cb.CustomerName,
                Description = cb.Description,
                TotalAmount = cb.TotalAmount,
                PaymentsAmount = cb.PaymentsAmount,
                ChargeBackAmount = cb.ChargeBackAmount
            }).ToList();
        }

        public async Task<bool> DeleteChargeBackAsync(Guid id)
        {
            var item = await _dbContext.JanikingChargeBacks.FindAsync(id);
            if (item == null)
            {
                return false;
            }

            _dbContext.JanikingChargeBacks.Remove(item);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<List<JanikingReportDto>> GetReportsAsync()
        {
            var reports = await _dbContext.JanikingReports
                .OrderByDescending(r => r.ReportMonth)
                .ToListAsync();

            return reports.Select(r =>
            {
                string? url = r.R2Url;
                if (!string.IsNullOrEmpty(r.R2Key))
                {
                    url = _storageService.GeneratePreSignedReadUrl(r.R2Key, TimeSpan.FromDays(1));
                }

                return new JanikingReportDto
                {
                    Id = r.Id,
                    ReportMonth = r.ReportMonth,
                    DueToFranchisee = r.DueToFranchisee,
                    ChargeBackAmount = r.ChargeBackAmount,
                    ExpectedAmount = r.ExpectedAmount,
                    TotalContractBilling = r.TotalContractBilling,
                    R2Key = r.R2Key,
                    R2Url = url,
                    CreatedAt = r.CreatedAt
                };
            }).ToList();
        }
    }
}
