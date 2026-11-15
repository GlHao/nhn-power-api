using System;
using System.Collections.Generic;

namespace NHN.Power.API.DTOs.Finance
{
    // =========================================================================
    // 1. Financial Analytics & Charts DTOs
    // =========================================================================

    public class FinancialAnalyticsDto
    {
        // 1. 双生意收入月度对比与合并
        public List<MonthlySalesItemDto> MonthlySales { get; set; } = new();

        // 2. 9 类重点支出月度走势
        public List<MonthlyExpenseTrendDto> MonthlyExpenses { get; set; } = new();

        // 3. 9 类重点支出类别总额与占比
        public List<ExpenseCategoryPieItemDto> ExpenseCategoryBreakdown { get; set; } = new();

        // 4. Jani-King 月度实际应收走势
        public List<JanikingRevenueItemDto> JanikingRevenue { get; set; } = new();

        // 汇总统计 KPI
        public decimal TotalNhnPowerSales { get; set; }
        public decimal TotalNiShaSales { get; set; }
        public decimal TotalCombinedSales { get; set; }
        public decimal TotalKeyExpenses { get; set; }
        public decimal TotalJanikingExpected { get; set; }
    }

    public class MonthlySalesItemDto
    {
        public string Month { get; set; } = string.Empty; // e.g. "2024-07"
        public decimal NhnPowerSales { get; set; }
        public decimal NiShaSales { get; set; }
        public decimal CombinedSales { get; set; }
    }

    public class MonthlyExpenseTrendDto
    {
        public string Month { get; set; } = string.Empty; // e.g. "2024-07"
        public decimal TotalAmount { get; set; }
        public Dictionary<string, decimal> CategoryAmounts { get; set; } = new();
    }

    public class ExpenseCategoryPieItemDto
    {
        public string Category { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal Percentage { get; set; } // e.g. 35.5% -> 35.5
        public int TransactionCount { get; set; }
    }

    public class JanikingRevenueItemDto
    {
        public string Month { get; set; } = string.Empty; // e.g. "2026-05"
        public decimal ExpectedAmount { get; set; }
        public decimal DueToFranchisee { get; set; }
        public decimal ChargeBackAmount { get; set; }
    }

    // =========================================================================
    // 2. ATO Personal Income Tax Calculator DTOs
    // =========================================================================

    public class TaxCalculateRequestDto
    {
        public decimal TaxableIncome { get; set; }
        public string FinancialYear { get; set; } = "2025-2026";
    }

    public class TaxCalculateResultDto
    {
        public decimal TaxableIncome { get; set; }
        public string FinancialYear { get; set; } = "2025-2026";
        public decimal BaseIncomeTax { get; set; }
        public decimal MedicareLevy { get; set; }
        public decimal TotalTaxPayable { get; set; }
        public decimal NetTakeHomePay { get; set; }
        public decimal EffectiveTaxRate { get; set; } // e.g. 23.45% -> 23.45
        public decimal TopMarginalRate { get; set; }  // e.g. 30.00% -> 30.00
        public List<TaxBracketCalculationDetailDto> BracketsBreakdown { get; set; } = new();
    }

    public class TaxBracketCalculationDetailDto
    {
        public string RangeLabel { get; set; } = string.Empty; // e.g. "$18,201 - $45,000"
        public decimal TaxableInBracket { get; set; }
        public decimal Rate { get; set; } // e.g. 0.16
        public decimal TaxAmount { get; set; }
    }

    // =========================================================================
    // 3. Tax Bracket Configuration DTOs
    // =========================================================================

    public class TaxBracketDto
    {
        public Guid Id { get; set; }
        public string FinancialYear { get; set; } = string.Empty;
        public decimal ThresholdMin { get; set; }
        public decimal? ThresholdMax { get; set; }
        public decimal BaseTax { get; set; }
        public decimal TaxRate { get; set; }
        public decimal MedicareRate { get; set; }
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CreateTaxBracketDto
    {
        public string FinancialYear { get; set; } = "2025-2026";
        public decimal ThresholdMin { get; set; }
        public decimal? ThresholdMax { get; set; }
        public decimal BaseTax { get; set; }
        public decimal TaxRate { get; set; }
        public decimal MedicareRate { get; set; } = 0.0200m;
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
    }

    public class UpdateTaxBracketDto
    {
        public decimal? ThresholdMin { get; set; }
        public decimal? ThresholdMax { get; set; }
        public decimal? BaseTax { get; set; }
        public decimal? TaxRate { get; set; }
        public decimal? MedicareRate { get; set; }
        public string? Notes { get; set; }
        public int? SortOrder { get; set; }
    }
}
