using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using NHN.Power.API.DTOs;
using NHN.Power.API.Infrastructure;

namespace NHN.Power.API.Repositories
{
    public interface IQuoteRepository
    {
        Task<QuoteSubmitResponse> SubmitQuoteAsync(QuoteSubmitRequest request, decimal? reviewedAmount);
        Task<IEnumerable<QuoteListDto>> GetQuotesAsync(int limit = 50);
        Task<QuoteDetailDto?> GetQuoteDetailAsync(Guid id);
        Task<bool> UpdateQuoteStatusAsync(Guid id, string newStatus, string oldStatus);
        Task<bool> UpdateQuoteReviewedAmountAsync(Guid id, decimal reviewedAmount, Guid reviewedBy, decimal? oldReviewedAmount);
        Task<IEnumerable<QuoteNoteDto>> GetQuoteNotesAsync(Guid quoteId);
        Task<bool> AddQuoteNoteAsync(Guid quoteId, string note, Guid adminId);
        Task<IEnumerable<QuoteTimelineDto>> GetQuoteTimelineAsync(Guid quoteId);
        Task<Guid> CreateQuotePhotoMetadataAsync(string fileName, string fileKey, string contentType, long fileSize);
        Task<DashboardDto> GetDashboardSummaryAsync();
        Task UpdateNotificationLogStatusAsync(Guid quoteId, string status, string? errorMsg = null);
    }

    public class QuoteRepository : IQuoteRepository
    {
        private readonly IDbConnectionFactory _dbConnectionFactory;

        public QuoteRepository(IDbConnectionFactory dbConnectionFactory)
        {
            _dbConnectionFactory = dbConnectionFactory;
        }

        public async Task<QuoteSubmitResponse> SubmitQuoteAsync(QuoteSubmitRequest request, decimal? reviewedAmount)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. Generate Quote Number
                var todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
                var countSql = "SELECT COUNT(*) FROM quote_requests WHERE CAST(created_at AS DATE) = CURRENT_DATE";
                var count = await connection.ExecuteScalarAsync<int>(countSql, transaction: transaction);
                var sequence = (count + 1).ToString("D4");
                var quoteNumber = $"Q-{todayStr}-{sequence}";

                // 2. Resolve LeadSourceId and QrCampaignId
                Guid? leadSourceId = null;
                if (!string.IsNullOrEmpty(request.LeadSourceCode))
                {
                    leadSourceId = await connection.ExecuteScalarAsync<Guid?>(
                        "SELECT id FROM lead_sources WHERE code = @Code", 
                        new { Code = request.LeadSourceCode }, transaction: transaction);
                }

                Guid? qrCampaignId = null;
                if (!string.IsNullOrEmpty(request.QrCampaignCode))
                {
                    qrCampaignId = await connection.ExecuteScalarAsync<Guid?>(
                        "SELECT id FROM qr_campaigns WHERE campaign_code = @Code", 
                        new { Code = request.QrCampaignCode }, transaction: transaction);
                }

                // 3. Insert into quote_requests
                var insertQuoteSql = @"
                    INSERT INTO quote_requests (
                        quote_number, customer_name, customer_phone, customer_email,
                        property_address, suburb, postcode, property_type, bedrooms, bathrooms,
                        living_areas, kitchens, has_pets, parking_available, preferred_date,
                        customer_message, website_estimate_amount, reviewed_amount, status,
                        lead_source_id, qr_campaign_id
                    ) VALUES (
                        @QuoteNumber, @CustomerName, @CustomerPhone, @CustomerEmail,
                        @PropertyAddress, @Suburb, @Postcode, @PropertyType, @Bedrooms, @Bathrooms,
                        @LivingAreas, @Kitchens, @HasPets, @ParkingAvailable, @PreferredDate,
                        @CustomerMessage, @WebsiteEstimateAmount, @ReviewedAmount, 'new',
                        @LeadSourceId, @QrCampaignId
                    ) RETURNING id;";
                
                var quoteId = await connection.ExecuteScalarAsync<Guid>(insertQuoteSql, new {
                    QuoteNumber = quoteNumber,
                    request.CustomerName,
                    request.CustomerPhone,
                    request.CustomerEmail,
                    request.PropertyAddress,
                    request.Suburb,
                    request.Postcode,
                    request.PropertyType,
                    request.Bedrooms,
                    request.Bathrooms,
                    request.LivingAreas,
                    request.Kitchens,
                    request.HasPets,
                    request.ParkingAvailable,
                    request.PreferredDate,
                    request.CustomerMessage,
                    request.WebsiteEstimateAmount,
                    ReviewedAmount = reviewedAmount,
                    LeadSourceId = leadSourceId,
                    QrCampaignId = qrCampaignId
                }, transaction: transaction);

                // 4. Insert into quote_services
                if (request.Services != null && request.Services.Any())
                {
                    var insertServiceSql = @"
                        INSERT INTO quote_services (
                            quote_request_id, service_code, service_name, quantity, unit, estimated_amount
                        ) VALUES (
                            @QuoteRequestId, @ServiceCode, @ServiceName, @Quantity, @Unit, @EstimatedAmount
                        );";
                    
                    var servicesToInsert = request.Services.Select(s => new {
                        QuoteRequestId = quoteId,
                        s.ServiceCode,
                        s.ServiceName,
                        s.Quantity,
                        s.Unit,
                        s.EstimatedAmount
                    });

                    await connection.ExecuteAsync(insertServiceSql, servicesToInsert, transaction: transaction);
                }

                // 5. Link Photos
                if (request.PhotoKeys != null && request.PhotoKeys.Any())
                {
                    var updatePhotosSql = @"
                        UPDATE quote_photos 
                        SET quote_request_id = @QuoteRequestId 
                        WHERE file_key = ANY(@PhotoKeys);";
                    
                    await connection.ExecuteAsync(updatePhotosSql, new {
                        QuoteRequestId = quoteId,
                        PhotoKeys = request.PhotoKeys.ToArray()
                    }, transaction: transaction);
                }

                // 6. Insert timeline event
                var insertTimelineSql = @"
                    INSERT INTO quote_timeline (quote_request_id, event_type, event_title, event_description) 
                    VALUES (@QuoteRequestId, 'quote_created', 'Quote Created', 'A new quote request was submitted from the website.');";
                await connection.ExecuteAsync(insertTimelineSql, new { QuoteRequestId = quoteId }, transaction: transaction);

                // 7. Insert Notification Log
                if (!string.IsNullOrEmpty(request.CustomerEmail))
                {
                    var insertNotificationSql = @"
                        INSERT INTO notification_logs (quote_request_id, notification_type, recipient, subject, status) 
                        VALUES (@QuoteRequestId, 'email', @Recipient, 'Your NHN Power Quote Request', 'pending');";
                    await connection.ExecuteAsync(insertNotificationSql, new { 
                        QuoteRequestId = quoteId,
                        Recipient = request.CustomerEmail
                    }, transaction: transaction);
                }

                transaction.Commit();

                return new QuoteSubmitResponse
                {
                    QuoteRequestId = quoteId,
                    QuoteNumber = quoteNumber
                };
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<QuoteListDto>> GetQuotesAsync(int limit = 50)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            var sql = @"
                SELECT 
                    id AS Id, 
                    quote_number AS QuoteNumber, 
                    customer_name AS CustomerName, 
                    suburb AS Suburb, 
                    status AS Status, 
                    created_at AS CreatedAt, 
                    website_estimate_amount AS WebsiteEstimateAmount
                FROM quote_requests
                ORDER BY created_at DESC
                LIMIT @Limit;";

            return await connection.QueryAsync<QuoteListDto>(sql, new { Limit = limit });
        }

        public async Task<QuoteDetailDto?> GetQuoteDetailAsync(Guid id)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            var sqlQuote = @"
                SELECT 
                    q.id AS Id, q.quote_number AS QuoteNumber, q.customer_name AS CustomerName, 
                    q.customer_phone AS CustomerPhone, q.customer_email AS CustomerEmail, 
                    q.property_address AS PropertyAddress, q.suburb AS Suburb, q.postcode AS Postcode, 
                    q.property_type AS PropertyType, q.bedrooms AS Bedrooms, q.bathrooms AS Bathrooms, 
                    q.living_areas AS LivingAreas, q.kitchens AS Kitchens, q.has_pets AS HasPets, 
                    q.parking_available AS ParkingAvailable, q.preferred_date AS PreferredDate, 
                    q.customer_message AS CustomerMessage, q.website_estimate_amount AS WebsiteEstimateAmount, 
                    q.reviewed_amount AS ReviewedAmount, q.status AS Status, q.created_at AS CreatedAt,
                    l.name AS LeadSourceName, c.name AS QrCampaignName
                FROM quote_requests q
                LEFT JOIN lead_sources l ON q.lead_source_id = l.id
                LEFT JOIN qr_campaigns c ON q.qr_campaign_id = c.id
                WHERE q.id = @Id;";

            var quote = await connection.QueryFirstOrDefaultAsync<QuoteFlatDto>(sqlQuote, new { Id = id });
            
            if (quote == null) return null;

            quote.Source = new QuoteSourceDto
            {
                LeadSourceName = quote.LeadSourceName,
                QrCampaignName = quote.QrCampaignName
            };

            var sqlServices = @"
                SELECT service_code AS ServiceCode, service_name AS ServiceName, 
                       quantity AS Quantity, unit AS Unit, estimated_amount AS EstimatedAmount
                FROM quote_services
                WHERE quote_request_id = @Id;";
            
            quote.Services = (await connection.QueryAsync<QuoteServiceDto>(sqlServices, new { Id = id })).ToList();

            var sqlPhotos = @"
                SELECT id AS Id, file_name AS FileName, file_key AS FileKey, 
                       file_url AS FileUrl, uploaded_at AS UploadedAt
                FROM quote_photos
                WHERE quote_request_id = @Id;";
                
            quote.Photos = (await connection.QueryAsync<QuotePhotoDto>(sqlPhotos, new { Id = id })).ToList();

            return quote;
        }

        public async Task<bool> UpdateQuoteStatusAsync(Guid id, string newStatus, string oldStatus)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                var sqlUpdate = @"
                    UPDATE quote_requests 
                    SET status = @NewStatus, updated_at = now()
                    WHERE id = @Id AND status = @OldStatus;";

                var rowsAffected = await connection.ExecuteAsync(sqlUpdate, new { Id = id, NewStatus = newStatus, OldStatus = oldStatus }, transaction);
                
                if (rowsAffected == 0)
                {
                    transaction.Rollback();
                    return false; // Not found or status changed concurrently
                }

                var sqlTimeline = @"
                    INSERT INTO quote_timeline (quote_request_id, event_type, event_title, event_description, old_value, new_value)
                    VALUES (@QuoteRequestId, @EventType, @EventTitle, @EventDescription, @OldValue, @NewValue);";

                await connection.ExecuteAsync(sqlTimeline, new
                {
                    QuoteRequestId = id,
                    EventType = "status_changed",
                    EventTitle = "Status Updated",
                    EventDescription = $"Status updated from {oldStatus} to {newStatus}",
                    OldValue = oldStatus,
                    NewValue = newStatus
                }, transaction);

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> UpdateQuoteReviewedAmountAsync(Guid id, decimal reviewedAmount, Guid reviewedBy, decimal? oldReviewedAmount)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                var sqlUpdate = @"
                    UPDATE quote_requests 
                    SET reviewed_amount = @ReviewedAmount, 
                        reviewed_by = @ReviewedBy, 
                        reviewed_at = now(), 
                        updated_at = now()
                    WHERE id = @Id;";

                var rowsAffected = await connection.ExecuteAsync(sqlUpdate, new 
                { 
                    Id = id, 
                    ReviewedAmount = reviewedAmount, 
                    ReviewedBy = reviewedBy 
                }, transaction);
                
                if (rowsAffected == 0)
                {
                    transaction.Rollback();
                    return false;
                }

                var sqlTimeline = @"
                    INSERT INTO quote_timeline (quote_request_id, event_type, event_title, event_description, old_value, new_value, created_by)
                    VALUES (@QuoteRequestId, @EventType, @EventTitle, @EventDescription, @OldValue, @NewValue, @CreatedBy);";

                var oldValStr = oldReviewedAmount?.ToString("F2") ?? "0.00";
                var newValStr = reviewedAmount.ToString("F2");

                await connection.ExecuteAsync(sqlTimeline, new
                {
                    QuoteRequestId = id,
                    EventType = "price_updated",
                    EventTitle = "Price Reviewed/Updated",
                    EventDescription = $"Reviewed amount updated from {oldValStr} to {newValStr}",
                    OldValue = oldValStr,
                    NewValue = newValStr,
                    CreatedBy = reviewedBy
                }, transaction);

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<QuoteNoteDto>> GetQuoteNotesAsync(Guid quoteId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            var sql = @"
                SELECT 
                    n.id AS Id, 
                    n.quote_request_id AS QuoteRequestId, 
                    n.note AS Note, 
                    n.created_by AS CreatedBy, 
                    u.full_name AS CreatedByName, 
                    n.created_at AS CreatedAt
                FROM quote_internal_notes n
                LEFT JOIN app_users u ON n.created_by = u.id
                WHERE n.quote_request_id = @QuoteId
                ORDER BY n.created_at ASC;";

            return await connection.QueryAsync<QuoteNoteDto>(sql, new { QuoteId = quoteId });
        }

        public async Task<bool> AddQuoteNoteAsync(Guid quoteId, string note, Guid adminId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                // First verify quote exists to avoid foreign key violation
                var quoteExists = await connection.ExecuteScalarAsync<bool>(
                    "SELECT EXISTS(SELECT 1 FROM quote_requests WHERE id = @Id)", 
                    new { Id = quoteId }, transaction);

                if (!quoteExists)
                {
                    transaction.Rollback();
                    return false;
                }

                var sqlInsertNote = @"
                    INSERT INTO quote_internal_notes (quote_request_id, note, created_by)
                    VALUES (@QuoteId, @Note, @AdminId);";

                await connection.ExecuteAsync(sqlInsertNote, new { QuoteId = quoteId, Note = note, AdminId = adminId }, transaction);

                var sqlTimeline = @"
                    INSERT INTO quote_timeline (quote_request_id, event_type, event_title, event_description, new_value, created_by)
                    VALUES (@QuoteRequestId, @EventType, @EventTitle, @EventDescription, @NewValue, @CreatedBy);";

                await connection.ExecuteAsync(sqlTimeline, new
                {
                    QuoteRequestId = quoteId,
                    EventType = "note_added",
                    EventTitle = "Note Added",
                    EventDescription = "An internal note was added.",
                    NewValue = note,
                    CreatedBy = adminId
                }, transaction);

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<QuoteTimelineDto>> GetQuoteTimelineAsync(Guid quoteId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            var sql = @"
                SELECT 
                    t.id AS Id, 
                    t.quote_request_id AS QuoteRequestId, 
                    t.event_type AS EventType, 
                    t.event_title AS EventTitle, 
                    t.event_description AS EventDescription, 
                    t.old_value AS OldValue, 
                    t.new_value AS NewValue, 
                    t.created_by AS CreatedBy, 
                    u.full_name AS CreatedByName, 
                    t.created_at AS CreatedAt
                FROM quote_timeline t
                LEFT JOIN app_users u ON t.created_by = u.id
                WHERE t.quote_request_id = @QuoteId
                ORDER BY t.created_at DESC;";

            return await connection.QueryAsync<QuoteTimelineDto>(sql, new { QuoteId = quoteId });
        }

        public async Task<Guid> CreateQuotePhotoMetadataAsync(string fileName, string fileKey, string contentType, long fileSize)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            var sql = @"
                INSERT INTO quote_photos (file_name, file_key, content_type, file_size)
                VALUES (@FileName, @FileKey, @ContentType, @FileSize)
                RETURNING id;";

            return await connection.ExecuteScalarAsync<Guid>(sql, new 
            { 
                FileName = fileName, 
                FileKey = fileKey, 
                ContentType = contentType, 
                FileSize = fileSize 
            });
        }

        public async Task<DashboardDto> GetDashboardSummaryAsync()
        {
            using var connection = _dbConnectionFactory.CreateConnection();

            var kpiSql = @"
                SELECT 
                    COUNT(*) AS TotalQuotes,
                    COUNT(*) FILTER (WHERE status = 'new') AS NewQuotes,
                    COUNT(*) FILTER (WHERE status = 'reviewing') AS ReviewingQuotes,
                    COUNT(*) FILTER (WHERE status = 'approved') AS ApprovedQuotes
                FROM quote_requests;";
            
            var kpi = await connection.QueryFirstOrDefaultAsync<DashboardKpiDto>(kpiSql) ?? new DashboardKpiDto();

            var recentSql = @"
                SELECT 
                    q.id AS Id, 
                    q.quote_number AS QuoteNumber, 
                    q.customer_name AS CustomerName, 
                    q.status AS Status, 
                    q.created_at AS CreatedAt, 
                    l.name AS LeadSourceName
                FROM quote_requests q
                LEFT JOIN lead_sources l ON q.lead_source_id = l.id
                ORDER BY q.created_at DESC
                LIMIT 10;";
            
            var recentQuotes = await connection.QueryAsync<DashboardQuoteDto>(recentSql);

            return new DashboardDto
            {
                Kpi = kpi,
                RecentQuotes = recentQuotes.ToList()
            };
        }

        public async Task UpdateNotificationLogStatusAsync(Guid quoteId, string status, string? errorMsg = null)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            var sql = @"
                UPDATE notification_logs 
                SET status = @Status, error_message = @ErrorMsg, sent_at = CASE WHEN @Status = 'sent' THEN now() ELSE sent_at END
                WHERE quote_request_id = @QuoteId AND notification_type = 'email';";
            
            await connection.ExecuteAsync(sql, new { QuoteId = quoteId, Status = status, ErrorMsg = errorMsg });
        }
    }

    internal class QuoteFlatDto : QuoteDetailDto
    {
        public string? LeadSourceName { get; set; }
        public string? QrCampaignName { get; set; }
    }
}
