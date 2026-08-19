using System.Data;
using Microsoft.Extensions.Options;
using Npgsql;
using NHN.Power.API.Models;

namespace NHN.Power.API.Infrastructure
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
    }

    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly AppSettings _appSettings;

        public DbConnectionFactory(IOptions<AppSettings> appSettings)
        {
            _appSettings = appSettings.Value;
        }

        public IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(_appSettings.SupabaseConnectionString);
        }
    }
}
