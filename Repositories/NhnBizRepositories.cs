using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NHN.Power.API.Data;
using NHN.Power.API.Models;

namespace NHN.Power.API.Repositories
{
    public class ClientRepository : IClientRepository
    {
        private readonly AppDbContext _context;

        public ClientRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Client>> GetAllActiveAsync()
        {
            return await _context.Clients
                .Where(c => c.IsActive && c.DeletedAt == null)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Client?> GetByIdAsync(Guid id)
        {
            return await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive && c.DeletedAt == null);
        }

        public async Task<Client> AddAsync(Client client)
        {
            client.Id = Guid.NewGuid();
            client.CreatedAt = DateTimeOffset.UtcNow;
            client.UpdatedAt = DateTimeOffset.UtcNow;
            
            _context.Clients.Add(client);
            await _context.SaveChangesAsync();
            return client;
        }

        public async Task UpdateAsync(Client client)
        {
            client.UpdatedAt = DateTimeOffset.UtcNow;
            _context.Clients.Update(client);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(Guid id)
        {
            var client = await GetByIdAsync(id);
            if (client != null)
            {
                client.IsActive = false;
                client.DeletedAt = DateTimeOffset.UtcNow;
                _context.Clients.Update(client);
                await _context.SaveChangesAsync();
            }
        }
    }

    public class SiteRepository : ISiteRepository
    {
        private readonly AppDbContext _context;

        public SiteRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Site>> GetByClientIdAsync(Guid clientId)
        {
            return await _context.Sites
                .Where(s => s.ClientId == clientId && s.IsActive && s.DeletedAt == null)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<Site?> GetByIdAsync(Guid id)
        {
            return await _context.Sites
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive && s.DeletedAt == null);
        }

        public async Task<Site> AddAsync(Site site)
        {
            site.Id = Guid.NewGuid();
            site.CreatedAt = DateTimeOffset.UtcNow;
            site.UpdatedAt = DateTimeOffset.UtcNow;
            
            _context.Sites.Add(site);
            await _context.SaveChangesAsync();
            return site;
        }

        public async Task UpdateAsync(Site site)
        {
            site.UpdatedAt = DateTimeOffset.UtcNow;
            _context.Sites.Update(site);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(Guid id)
        {
            var site = await GetByIdAsync(id);
            if (site != null)
            {
                site.IsActive = false;
                site.DeletedAt = DateTimeOffset.UtcNow;
                _context.Sites.Update(site);
                await _context.SaveChangesAsync();
            }
        }
    }

    public class BillingRuleRepository : IBillingRuleRepository
    {
        private readonly AppDbContext _context;

        public BillingRuleRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<BillingRule>> GetBySiteIdAsync(Guid siteId)
        {
            return await _context.BillingRules
                .Where(b => b.SiteId == siteId && b.IsActive && b.DeletedAt == null)
                .OrderBy(b => b.DayOfWeek)
                .ToListAsync();
        }

        public async Task<BillingRule?> GetByIdAsync(Guid id)
        {
            return await _context.BillingRules
                .FirstOrDefaultAsync(b => b.Id == id && b.IsActive && b.DeletedAt == null);
        }

        public async Task<BillingRule> AddAsync(BillingRule rule)
        {
            rule.Id = Guid.NewGuid();
            rule.CreatedAt = DateTimeOffset.UtcNow;
            rule.UpdatedAt = DateTimeOffset.UtcNow;
            
            _context.BillingRules.Add(rule);
            await _context.SaveChangesAsync();
            return rule;
        }

        public async Task UpdateAsync(BillingRule rule)
        {
            rule.UpdatedAt = DateTimeOffset.UtcNow;
            _context.BillingRules.Update(rule);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(Guid id)
        {
            var rule = await GetByIdAsync(id);
            if (rule != null)
            {
                rule.IsActive = false;
                rule.DeletedAt = DateTimeOffset.UtcNow;
                _context.BillingRules.Update(rule);
                await _context.SaveChangesAsync();
            }
        }
    }

    public class ScheduleRuleRepository : IScheduleRuleRepository
    {
        private readonly AppDbContext _context;

        public ScheduleRuleRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ServiceScheduleRule>> GetBySiteIdAsync(Guid siteId)
        {
            return await _context.ServiceScheduleRules
                .Where(r => r.SiteId == siteId && r.IsActive && r.DeletedAt == null)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<ServiceScheduleRule?> GetByIdAsync(Guid id)
        {
            return await _context.ServiceScheduleRules
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive && r.DeletedAt == null);
        }

        public async Task<ServiceScheduleRule> AddAsync(ServiceScheduleRule rule)
        {
            rule.Id = Guid.NewGuid();
            rule.CreatedAt = DateTimeOffset.UtcNow;
            rule.UpdatedAt = DateTimeOffset.UtcNow;
            
            _context.ServiceScheduleRules.Add(rule);
            await _context.SaveChangesAsync();
            return rule;
        }

        public async Task UpdateAsync(ServiceScheduleRule rule)
        {
            rule.UpdatedAt = DateTimeOffset.UtcNow;
            _context.ServiceScheduleRules.Update(rule);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(Guid id)
        {
            var rule = await GetByIdAsync(id);
            if (rule != null)
            {
                rule.IsActive = false;
                rule.DeletedAt = DateTimeOffset.UtcNow;
                _context.ServiceScheduleRules.Update(rule);
                await _context.SaveChangesAsync();
            }
        }
    }
}
