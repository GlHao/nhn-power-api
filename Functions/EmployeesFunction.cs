using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs.Employee;
using NHN.Power.API.Services;

namespace NHN.Power.API.Functions
{
    public class EmployeesFunction
    {
        private readonly ILogger _logger;
        private readonly IEmployeeService _employeeService;

        public EmployeesFunction(ILoggerFactory loggerFactory, IEmployeeService employeeService)
        {
            _logger = loggerFactory.CreateLogger<EmployeesFunction>();
            _employeeService = employeeService;
        }

        [Function("GetEmployees")]
        public async Task<HttpResponseData> GetEmployees(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "employees")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all active employees");
            try
            {
                var employees = await _employeeService.GetAllEmployeesAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(employees);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employees: {Message}", ex.Message);
                var errResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResponse.WriteStringAsync($"Error: {ex.Message}");
                return errResponse;
            }
        }

        [Function("GetEmployeeById")]
        public async Task<HttpResponseData> GetEmployeeById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "employees/{id:guid}")] HttpRequestData req, Guid id)
        {
            _logger.LogInformation("Getting employee by id: {Id}", id);
            try
            {
                var employee = await _employeeService.GetEmployeeByIdAsync(id);
                if (employee == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(employee);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employee by id: {Message}", ex.Message);
                var errResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResponse.WriteStringAsync($"Error: {ex.Message}");
                return errResponse;
            }
        }

        [Function("CreateEmployee")]
        public async Task<HttpResponseData> CreateEmployee(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "employees")] HttpRequestData req)
        {
            _logger.LogInformation("Creating employee");
            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<CreateEmployeeRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data == null || string.IsNullOrWhiteSpace(data.Name))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Name is required" });
                    return badRequest;
                }

                var created = await _employeeService.CreateEmployeeAsync(data);
                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(created);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating employee: {Message}", ex.Message);
                var errResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResponse.WriteStringAsync($"Error: {ex.Message}");
                return errResponse;
            }
        }

        [Function("UpdateEmployee")]
        public async Task<HttpResponseData> UpdateEmployee(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "employees/{id:guid}")] HttpRequestData req, Guid id)
        {
            _logger.LogInformation("Updating employee id: {Id}", id);
            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<UpdateEmployeeRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data == null || string.IsNullOrWhiteSpace(data.Name))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Name is required" });
                    return badRequest;
                }

                var updated = await _employeeService.UpdateEmployeeAsync(id, data);
                if (updated == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(updated);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee: {Message}", ex.Message);
                var errResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResponse.WriteStringAsync($"Error: {ex.Message}");
                return errResponse;
            }
        }

        [Function("DeleteEmployee")]
        public async Task<HttpResponseData> DeleteEmployee(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "employees/{id:guid}")] HttpRequestData req, Guid id)
        {
            _logger.LogInformation("Deleting employee id: {Id}", id);
            try
            {
                var success = await _employeeService.DeleteEmployeeAsync(id);
                if (!success)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                return req.CreateResponse(HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting employee: {Message}", ex.Message);
                var errResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResponse.WriteStringAsync($"Error: {ex.Message}");
                return errResponse;
            }
        }
    }
}
