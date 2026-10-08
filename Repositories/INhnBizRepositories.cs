using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NHN.Power.API.Models;

namespace NHN.Power.API.Repositories
{
    public interface IClientRepository
    {
        Task<IEnumerable<Client>> GetAllActiveAsync();
        Task<Client?> GetByIdAsync(Guid id);
        Task<Client> AddAsync(Client client);
        Task UpdateAsync(Client client);
        Task SoftDeleteAsync(Guid id);
    }

    public interface ISiteRepository
    {
        Task<IEnumerable<Site>> GetByClientIdAsync(Guid clientId);
        Task<Site?> GetByIdAsync(Guid id);
        Task<Site> AddAsync(Site site);
        Task UpdateAsync(Site site);
        Task SoftDeleteAsync(Guid id);
    }

    public interface IBillingRuleRepository
    {
        Task<IEnumerable<BillingRule>> GetBySiteIdAsync(Guid siteId);
        Task<IEnumerable<BillingRule>> GetByEmployeeIdAsync(Guid employeeId);
        Task<BillingRule?> GetByIdAsync(Guid id);
        Task<BillingRule> AddAsync(BillingRule rule);
        Task UpdateAsync(BillingRule rule);
        Task SoftDeleteAsync(Guid id);
    }

    public interface IScheduleRuleRepository
    {
        Task<IEnumerable<ServiceScheduleRule>> GetBySiteIdAsync(Guid siteId);
        Task<ServiceScheduleRule?> GetByIdAsync(Guid id);
        Task<ServiceScheduleRule> AddAsync(ServiceScheduleRule rule);
        Task UpdateAsync(ServiceScheduleRule rule);
        Task SoftDeleteAsync(Guid id);
    }

    public interface IEmployeeRepository
    {
        Task<IEnumerable<Employee>> GetAllActiveAsync();
        Task<Employee?> GetByIdAsync(Guid id);
        Task<Employee> AddAsync(Employee employee);
        Task UpdateAsync(Employee employee);
        Task SoftDeleteAsync(Guid id);
    }
}
