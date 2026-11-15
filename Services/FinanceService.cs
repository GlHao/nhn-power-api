using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NHN.Power.API.Data;
using NHN.Power.API.DTOs.Finance;
using NHN.Power.API.Models;

namespace NHN.Power.API.Services
{
    public class FinanceService : IFinanceService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<FinanceService> _logger;

        // 9 类核心经营开支规范名称
        private static readonly string[] KeyExpenseCategories = new[]
        {
            "Parking",
            "fuel",
            "车保养",
            "过路费",
            "police check",
            "车贷款",
            "PAYROLLER",
            "车保险",
            "会计费"
        };

        public FinanceService(AppDbContext dbContext, ILogger<FinanceService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<FinancialAnalyticsDto> GetFinancialAnalyticsAsync()
        {
            _logger.LogInformation("Generating comprehensive financial analytics report.");

            var analytics = new FinancialAnalyticsDto();

            // 1. 加载所有流水及关联 Quarter
            var transactions = await _dbContext.LedgerTransactions
                .Include(t => t.Quarter)
                .AsNoTracking()
                .ToListAsync();

            // -------------------------------------------------------------
            // 2. 统计月度双生意 Sales 收入
            // -------------------------------------------------------------
            var salesTransactions = transactions
                .Where(t => IsSalesTransaction(t.Notes))
                .ToList();

            var salesByMonth = salesTransactions
                .GroupBy(t => t.TransactionDate.ToString("yyyy-MM"))
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var monthGroup in salesByMonth)
            {
                var monthKey = monthGroup.Key;

                // NHN POWER
                var nhnSales = monthGroup
                    .Where(t => IsNhnPowerBusiness(t.Quarter?.Business))
                    .Sum(t => Math.Abs(t.Amount));

                // NI SHA AND HAO WANG
                var niShaSales = monthGroup
                    .Where(t => IsNiShaBusiness(t.Quarter?.Business))
                    .Sum(t => Math.Abs(t.Amount));

                var combinedSales = nhnSales + niShaSales;

                analytics.MonthlySales.Add(new MonthlySalesItemDto
                {
                    Month = monthKey,
                    NhnPowerSales = Math.Round(nhnSales, 2),
                    NiShaSales = Math.Round(niShaSales, 2),
                    CombinedSales = Math.Round(combinedSales, 2)
                });
            }

            analytics.TotalNhnPowerSales = analytics.MonthlySales.Sum(s => s.NhnPowerSales);
            analytics.TotalNiShaSales = analytics.MonthlySales.Sum(s => s.NiShaSales);
            analytics.TotalCombinedSales = analytics.TotalNhnPowerSales + analytics.TotalNiShaSales;

            // -------------------------------------------------------------
            // 3. 统计 9 类核心经营支出月度走势与类别占比
            // -------------------------------------------------------------
            var categorizedExpenses = new List<(NhnLedgerTransaction Tx, string Category)>();

            foreach (var tx in transactions)
            {
                var cat = MatchKeyExpenseCategory(tx.Notes);
                if (cat != null)
                {
                    categorizedExpenses.Add((tx, cat));
                }
            }

            // 按月分组
            var expensesByMonth = categorizedExpenses
                .GroupBy(item => item.Tx.TransactionDate.ToString("yyyy-MM"))
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var monthGroup in expensesByMonth)
            {
                var monthDto = new MonthlyExpenseTrendDto
                {
                    Month = monthGroup.Key
                };

                // 初始化 9 个类别金额为 0
                foreach (var cat in KeyExpenseCategories)
                {
                    monthDto.CategoryAmounts[cat] = 0m;
                }

                foreach (var item in monthGroup)
                {
                    var absAmt = Math.Abs(item.Tx.Amount);
                    monthDto.CategoryAmounts[item.Category] += absAmt;
                }

                // 四舍五入保留2位小数并计算当月总支出
                decimal monthTotal = 0m;
                foreach (var cat in KeyExpenseCategories)
                {
                    monthDto.CategoryAmounts[cat] = Math.Round(monthDto.CategoryAmounts[cat], 2);
                    monthTotal += monthDto.CategoryAmounts[cat];
                }
                monthDto.TotalAmount = Math.Round(monthTotal, 2);

                analytics.MonthlyExpenses.Add(monthDto);
            }

            // 按类别聚合汇总
            var expensesByCategory = categorizedExpenses
                .GroupBy(item => item.Category)
                .ToList();

            decimal totalKeyExpenses = 0m;
            var categoryBreakdownRaw = new List<ExpenseCategoryPieItemDto>();

            foreach (var cat in KeyExpenseCategories)
            {
                var items = expensesByCategory.FirstOrDefault(g => g.Key.Equals(cat, StringComparison.OrdinalIgnoreCase));
                var catTotal = items != null ? items.Sum(i => Math.Abs(i.Tx.Amount)) : 0m;
                var catCount = items != null ? items.Count() : 0;

                totalKeyExpenses += catTotal;

                categoryBreakdownRaw.Add(new ExpenseCategoryPieItemDto
                {
                    Category = cat,
                    TotalAmount = Math.Round(catTotal, 2),
                    TransactionCount = catCount
                });
            }

            analytics.TotalKeyExpenses = Math.Round(totalKeyExpenses, 2);

            // 计算各类别的百分比占比
            foreach (var item in categoryBreakdownRaw)
            {
                item.Percentage = analytics.TotalKeyExpenses > 0
                    ? Math.Round((item.TotalAmount / analytics.TotalKeyExpenses) * 100m, 2)
                    : 0m;
            }

            // 优先按金额倒序排列
            analytics.ExpenseCategoryBreakdown = categoryBreakdownRaw
                .OrderByDescending(c => c.TotalAmount)
                .ToList();

            // -------------------------------------------------------------
            // 4. Jani-King 月度实际应收统计
            // -------------------------------------------------------------
            var janikingReports = await _dbContext.JanikingReports
                .AsNoTracking()
                .OrderBy(r => r.ReportMonth)
                .ToListAsync();

            foreach (var report in janikingReports)
            {
                analytics.JanikingRevenue.Add(new JanikingRevenueItemDto
                {
                    Month = report.ReportMonth,
                    ExpectedAmount = Math.Round(report.ExpectedAmount, 2),
                    DueToFranchisee = Math.Round(report.DueToFranchisee, 2),
                    ChargeBackAmount = Math.Round(report.ChargeBackAmount, 2)
                });
            }

            analytics.TotalJanikingExpected = Math.Round(analytics.JanikingRevenue.Sum(r => r.ExpectedAmount), 2);

            return analytics;
        }

        public async Task<TaxCalculateResultDto> CalculateTaxAsync(TaxCalculateRequestDto request)
        {
            var income = Math.Max(0m, request.TaxableIncome);
            var fy = string.IsNullOrWhiteSpace(request.FinancialYear) ? "2025-2026" : request.FinancialYear.Trim();

            _logger.LogInformation("Calculating ATO personal tax for income {Income:C} and FY {FY}", income, fy);

            // 查询指定财年的所有税阶并排序
            var brackets = await _dbContext.TaxBrackets
                .AsNoTracking()
                .Where(b => b.FinancialYear == fy)
                .OrderBy(b => b.SortOrder)
                .ThenBy(b => b.ThresholdMin)
                .ToListAsync();

            // 若数据库中无此财年税阶，尝试获取 2025-2026 或任一最新财年作为保底
            if (!brackets.Any())
            {
                brackets = await _dbContext.TaxBrackets
                    .AsNoTracking()
                    .OrderByDescending(b => b.FinancialYear)
                    .ThenBy(b => b.SortOrder)
                    .ToListAsync();
            }

            var result = new TaxCalculateResultDto
            {
                TaxableIncome = Math.Round(income, 2),
                FinancialYear = fy
            };

            if (income <= 0m || !brackets.Any())
            {
                result.NetTakeHomePay = 0m;
                return result;
            }

            // 1. 查找收入所处的主税阶计算基础所得税
            var currentBracket = brackets.FirstOrDefault(b =>
                income > b.ThresholdMin && (b.ThresholdMax == null || income <= b.ThresholdMax.Value));

            decimal baseTax = 0m;
            decimal topRate = 0m;

            if (currentBracket != null)
            {
                baseTax = currentBracket.BaseTax + (income - currentBracket.ThresholdMin) * currentBracket.TaxRate;
                topRate = currentBracket.TaxRate * 100m;
            }
            else
            {
                // 超出所有配置税阶，使用最高税阶
                var highestBracket = brackets.Last();
                baseTax = highestBracket.BaseTax + (income - highestBracket.ThresholdMin) * highestBracket.TaxRate;
                topRate = highestBracket.TaxRate * 100m;
            }

            result.BaseIncomeTax = Math.Round(baseTax, 2);
            result.TopMarginalRate = Math.Round(topRate, 2);

            // 2. Medicare Levy 测算 (澳洲低收入免征门槛约 $26,000，超过部分标准征收 2.0%)
            decimal medicareLevy = 0m;
            if (income > 26000m)
            {
                medicareLevy = income * 0.0200m;
            }
            result.MedicareLevy = Math.Round(medicareLevy, 2);

            // 3. 总应缴税额与净到手收入
            result.TotalTaxPayable = Math.Round(result.BaseIncomeTax + result.MedicareLevy, 2);
            result.NetTakeHomePay = Math.Max(0m, Math.Round(income - result.TotalTaxPayable, 2));
            result.EffectiveTaxRate = income > 0 ? Math.Round((result.TotalTaxPayable / income) * 100m, 2) : 0m;

            // 4. 计算各税阶分段详情明细
            foreach (var b in brackets)
            {
                decimal taxableInThisBracket = 0m;
                if (income > b.ThresholdMin)
                {
                    decimal ceiling = b.ThresholdMax ?? income;
                    taxableInThisBracket = Math.Min(income, ceiling) - b.ThresholdMin;
                }

                decimal bracketTax = Math.Round(taxableInThisBracket * b.TaxRate, 2);
                string rangeLabel = b.ThresholdMax.HasValue
                    ? $"${b.ThresholdMin:N0} - ${b.ThresholdMax.Value:N0}"
                    : $">${b.ThresholdMin:N0}";

                result.BracketsBreakdown.Add(new TaxBracketCalculationDetailDto
                {
                    RangeLabel = rangeLabel,
                    TaxableInBracket = Math.Round(taxableInThisBracket, 2),
                    Rate = b.TaxRate,
                    TaxAmount = bracketTax
                });
            }

            return result;
        }

        public async Task<List<TaxBracketDto>> GetTaxBracketsAsync(string? financialYear = null)
        {
            var query = _dbContext.TaxBrackets.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(financialYear))
            {
                query = query.Where(b => b.FinancialYear == financialYear.Trim());
            }

            var brackets = await query
                .OrderByDescending(b => b.FinancialYear)
                .ThenBy(b => b.SortOrder)
                .ThenBy(b => b.ThresholdMin)
                .ToListAsync();

            return brackets.Select(MapToDto).ToList();
        }

        public async Task<TaxBracketDto> CreateTaxBracketAsync(CreateTaxBracketDto dto)
        {
            var entity = new TaxBracket
            {
                FinancialYear = string.IsNullOrWhiteSpace(dto.FinancialYear) ? "2025-2026" : dto.FinancialYear.Trim(),
                ThresholdMin = dto.ThresholdMin,
                ThresholdMax = dto.ThresholdMax,
                BaseTax = dto.BaseTax,
                TaxRate = dto.TaxRate,
                MedicareRate = dto.MedicareRate,
                Notes = dto.Notes,
                SortOrder = dto.SortOrder,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.TaxBrackets.Add(entity);
            await _dbContext.SaveChangesAsync();

            return MapToDto(entity);
        }

        public async Task<TaxBracketDto> UpdateTaxBracketAsync(Guid id, UpdateTaxBracketDto dto)
        {
            var entity = await _dbContext.TaxBrackets.FindAsync(id);
            if (entity == null)
            {
                throw new KeyNotFoundException($"Tax bracket with ID {id} not found.");
            }

            if (dto.ThresholdMin.HasValue) entity.ThresholdMin = dto.ThresholdMin.Value;
            if (dto.ThresholdMax.HasValue) entity.ThresholdMax = dto.ThresholdMax.Value;
            if (dto.BaseTax.HasValue) entity.BaseTax = dto.BaseTax.Value;
            if (dto.TaxRate.HasValue) entity.TaxRate = dto.TaxRate.Value;
            if (dto.MedicareRate.HasValue) entity.MedicareRate = dto.MedicareRate.Value;
            if (dto.Notes != null) entity.Notes = dto.Notes;
            if (dto.SortOrder.HasValue) entity.SortOrder = dto.SortOrder.Value;

            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync();

            return MapToDto(entity);
        }

        public async Task<bool> DeleteTaxBracketAsync(Guid id)
        {
            var entity = await _dbContext.TaxBrackets.FindAsync(id);
            if (entity == null) return false;

            _dbContext.TaxBrackets.Remove(entity);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        // -------------------------------------------------------------
        // 辅助判断方法
        // -------------------------------------------------------------

        private static bool IsSalesTransaction(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return false;
            var trimmed = notes.Trim();
            return trimmed.Equals("sales", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.Equals("salse", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("sales", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNhnPowerBusiness(string? business)
        {
            if (string.IsNullOrWhiteSpace(business)) return true; // Default fallback to NHN
            var trimmed = business.Trim().ToUpperInvariant();
            return trimmed.Contains("NHN");
        }

        private static bool IsNiShaBusiness(string? business)
        {
            if (string.IsNullOrWhiteSpace(business)) return false;
            var trimmed = business.Trim().ToUpperInvariant();
            return trimmed.Contains("NI SHA") || trimmed.Contains("HAO WANG");
        }

        private static string? MatchKeyExpenseCategory(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return null;
            var trimmed = notes.Trim();

            // 精准匹配
            foreach (var cat in KeyExpenseCategories)
            {
                if (trimmed.Equals(cat, StringComparison.OrdinalIgnoreCase))
                    return cat;
            }

            // 宽容容错匹配
            if (trimmed.IndexOf("parking", StringComparison.OrdinalIgnoreCase) >= 0) return "Parking";
            if (trimmed.IndexOf("fuel", StringComparison.OrdinalIgnoreCase) >= 0) return "fuel";
            if (trimmed.Contains("车保养") || trimmed.Contains("车辆保养")) return "车保养";
            if (trimmed.Contains("过路费") || trimmed.IndexOf("toll", StringComparison.OrdinalIgnoreCase) >= 0) return "过路费";
            if (trimmed.IndexOf("police", StringComparison.OrdinalIgnoreCase) >= 0) return "police check";
            if (trimmed.Contains("车贷款") || trimmed.Contains("车辆贷款")) return "车贷款";
            if (trimmed.IndexOf("payroller", StringComparison.OrdinalIgnoreCase) >= 0) return "PAYROLLER";
            if (trimmed.Contains("车保险") || trimmed.Contains("车辆保险")) return "车保险";
            if (trimmed.Contains("会计费") || trimmed.Contains("会计")) return "会计费";

            return null;
        }

        private static TaxBracketDto MapToDto(TaxBracket entity)
        {
            return new TaxBracketDto
            {
                Id = entity.Id,
                FinancialYear = entity.FinancialYear,
                ThresholdMin = entity.ThresholdMin,
                ThresholdMax = entity.ThresholdMax,
                BaseTax = entity.BaseTax,
                TaxRate = entity.TaxRate,
                MedicareRate = entity.MedicareRate,
                Notes = entity.Notes,
                SortOrder = entity.SortOrder,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
