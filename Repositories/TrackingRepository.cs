using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using NHN.Power.API.DTOs;
using NHN.Power.API.Infrastructure;

namespace NHN.Power.API.Repositories
{
    public class TrackingRepository : ITrackingRepository
    {
        private readonly IDbConnectionFactory _dbConnectionFactory;

        public TrackingRepository(IDbConnectionFactory dbConnectionFactory)
        {
            _dbConnectionFactory = dbConnectionFactory;
        }

        public async Task<Guid?> GetLeadSourceByCodeAsync(string code)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            var sql = "SELECT id FROM lead_sources WHERE code = @Code AND is_active = true;";
            return await connection.ExecuteScalarAsync<Guid?>(sql, new { Code = code });
        }

        public async Task<Guid?> GetQrCampaignByCodeAsync(string campaignCode)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            var sql = "SELECT id FROM qr_campaigns WHERE campaign_code = @Code AND is_active = true;";
            return await connection.ExecuteScalarAsync<Guid?>(sql, new { Code = campaignCode });
        }

        public async Task<QrCampaignTargetDto?> GetCampaignTargetByCodeAsync(string campaignCode)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            var sql = @"
                SELECT id AS Id, target_url AS TargetUrl 
                FROM qr_campaigns 
                WHERE campaign_code = @Code AND is_active = true;";
            return await connection.QueryFirstOrDefaultAsync<QrCampaignTargetDto>(sql, new { Code = campaignCode });
        }

        public async Task<Guid> CreateQrScanAsync(CreateQrScanRecord record)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            var sql = @"
                INSERT INTO qr_scans (
                    qr_campaign_id, lead_source_id, ip_address, user_agent, 
                    referrer, landing_url, scanned_at, created_at
                ) VALUES (
                    @QrCampaignId, @LeadSourceId, @IpAddress, @UserAgent, 
                    @Referrer, @LandingUrl, now(), now()
                ) RETURNING id;";

            return await connection.ExecuteScalarAsync<Guid>(sql, record);
        }

        public async Task<IEnumerable<LeadSourceDto>> GetActiveLeadSourcesAsync()
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            var sql = @"
                SELECT id AS Id, code AS Code, name AS Name 
                FROM lead_sources 
                WHERE is_active = true 
                ORDER BY created_at ASC;";

            return await connection.QueryAsync<LeadSourceDto>(sql);
        }
    }
}
