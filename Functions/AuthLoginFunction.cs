using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs;
using NHN.Power.API.Models;
using NHN.Power.API.Repositories;
using NHN.Power.API.Services;

namespace NHN.Power.API.Functions
{
    public class AuthLoginFunction
    {
        private readonly ILogger _logger;
        private readonly IUserRepository _userRepository;
        private readonly IPasswordService _passwordService;
        private readonly IJwtService _jwtService;

        public AuthLoginFunction(
            ILoggerFactory loggerFactory, 
            IUserRepository userRepository, 
            IPasswordService passwordService, 
            IJwtService jwtService)
        {
            _logger = loggerFactory.CreateLogger<AuthLoginFunction>();
            _userRepository = userRepository;
            _passwordService = passwordService;
            _jwtService = jwtService;
        }

        [Function("AuthLogin")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/login")] HttpRequestData req)
        {
            _logger.LogInformation("Processing login request.");

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var loginRequest = JsonSerializer.Deserialize<LoginRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (loginRequest == null || string.IsNullOrWhiteSpace(loginRequest.Email) || string.IsNullOrWhiteSpace(loginRequest.Password))
            {
                return await CreateResponseAsync(req, HttpStatusCode.BadRequest, ApiResponse<object>.CreateError("Invalid login request. Email and password are required."));
            }

            var user = await _userRepository.GetByEmailAsync(loginRequest.Email.ToLower().Trim());

            if (user == null || !_passwordService.VerifyPassword(loginRequest.Password, user.PasswordHash))
            {
                return await CreateResponseAsync(req, HttpStatusCode.Unauthorized, ApiResponse<object>.CreateError("Invalid email or password."));
            }

            if (!user.IsActive)
            {
                return await CreateResponseAsync(req, HttpStatusCode.Forbidden, ApiResponse<object>.CreateError("Account is inactive."));
            }

            var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email, user.Role);

            var loginResponse = new LoginResponse
            {
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = user.Role
                }
            };

            return await CreateResponseAsync(req, HttpStatusCode.OK, ApiResponse<LoginResponse>.CreateSuccess(loginResponse));
        }

        private static async Task<HttpResponseData> CreateResponseAsync<T>(HttpRequestData req, HttpStatusCode statusCode, ApiResponse<T> apiResponse)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            
            var json = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await response.WriteStringAsync(json);
            
            return response;
        }
    }
}
