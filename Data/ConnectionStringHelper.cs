using System;

namespace NHN.Power.API.Data
{
    public static class ConnectionStringHelper
    {
        public static string GetValidConnectionString(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            if (input.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || 
                input.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(input);
                var userInfo = uri.UserInfo.Split(':');
                var username = userInfo.Length > 0 ? userInfo[0] : "";
                var password = userInfo.Length > 1 ? userInfo[1] : "";
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 5432;
                var database = uri.LocalPath.TrimStart('/');

                return $"Host={host};Port={port};Database={database};Username={username};Password={password};Ssl Mode=Require;Trust Server Certificate=true;";
            }

            return input;
        }
    }
}
