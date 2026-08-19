using System.Threading.Tasks;
using Dapper;
using NHN.Power.API.Infrastructure;
using NHN.Power.API.Models;

namespace NHN.Power.API.Repositories
{
    public interface IUserRepository
    {
        Task<AppUser?> GetByEmailAsync(string email);
    }

    public class UserRepository : IUserRepository
    {
        private readonly IDbConnectionFactory _dbConnectionFactory;

        public UserRepository(IDbConnectionFactory dbConnectionFactory)
        {
            _dbConnectionFactory = dbConnectionFactory;
        }

        public async Task<AppUser?> GetByEmailAsync(string email)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            
            // Note: Postgres columns are usually snake_case. 
            // Dapper auto-maps if we use AS or we can use DefaultTypeMap.MatchNamesWithUnderscores = true;
            // Best practice is to alias them explicitly or set the Dapper setting globally.
            // Let's set the global setting in Program.cs later, or just map explicitly here.
            
            const string sql = @"
                SELECT 
                    id AS Id, 
                    email AS Email, 
                    password_hash AS PasswordHash, 
                    full_name AS FullName, 
                    role AS Role, 
                    is_active AS IsActive, 
                    created_at AS CreatedAt, 
                    updated_at AS UpdatedAt 
                FROM app_users 
                WHERE email = @Email";

            return await connection.QuerySingleOrDefaultAsync<AppUser>(sql, new { Email = email });
        }
    }
}
