using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NHN.Power.API.DTOs.Finance;

namespace NHN.Power.API.Services
{
    public interface IFinanceService
    {
        /// <summary>
        /// 获取财务大盘综合统计数据（多生意月度收入对比与合并、9类核心开支走势与占比、Jani-King月度应收）
        /// </summary>
        Task<FinancialAnalyticsDto> GetFinancialAnalyticsAsync();

        /// <summary>
        /// 根据应税收入测算 ATO 个人所得税、Medicare Levy 及税后净收入
        /// </summary>
        Task<TaxCalculateResultDto> CalculateTaxAsync(TaxCalculateRequestDto request);

        /// <summary>
        /// 查询税阶税率配置列表
        /// </summary>
        Task<List<TaxBracketDto>> GetTaxBracketsAsync(string? financialYear = null);

        /// <summary>
        /// 新增税阶配置
        /// </summary>
        Task<TaxBracketDto> CreateTaxBracketAsync(CreateTaxBracketDto dto);

        /// <summary>
        /// 更新税阶配置
        /// </summary>
        Task<TaxBracketDto> UpdateTaxBracketAsync(Guid id, UpdateTaxBracketDto dto);

        /// <summary>
        /// 删除税阶配置
        /// </summary>
        Task<bool> DeleteTaxBracketAsync(Guid id);
    }
}
