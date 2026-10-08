using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHN.Power.API.DTOs.Payroll;
using NHN.Power.API.Repositories;
using NHN.Power.API.Services;

namespace NHN.Power.API.Functions
{
    public class PayrollFunction
    {
        private readonly ILogger _logger;
        private readonly IPayrollCalculatorService _payrollCalculatorService;
        private readonly IEmployeePaySlipPdfService _paySlipPdfService;
        private readonly ISmsService _smsService;
        private readonly IEmployeeRepository _employeeRepository;

        public PayrollFunction(
            ILoggerFactory loggerFactory,
            IPayrollCalculatorService payrollCalculatorService,
            IEmployeePaySlipPdfService paySlipPdfService,
            ISmsService smsService,
            IEmployeeRepository employeeRepository)
        {
            _logger = loggerFactory.CreateLogger<PayrollFunction>();
            _payrollCalculatorService = payrollCalculatorService;
            _paySlipPdfService = paySlipPdfService;
            _smsService = smsService;
            _employeeRepository = employeeRepository;
        }

        [Function("PayrollPreview")]
        public async Task<HttpResponseData> Preview(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "payroll/preview")] HttpRequestData req)
        {
            _logger.LogInformation("Generating Payroll Preview");
            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<EmployeePayrollPreviewRequestDto>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data == null || data.EmployeeId == Guid.Empty)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteAsJsonAsync(new { error = "EmployeeId is required" });
                    return badReq;
                }

                var preview = await _payrollCalculatorService.CalculatePayrollAsync(data);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(preview);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating payroll preview: {Message}", ex.Message);
                var errResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResponse.WriteStringAsync($"Error: {ex.Message}");
                return errResponse;
            }
        }

        [Function("PayrollPdf")]
        public async Task<HttpResponseData> GeneratePdf(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "payroll/pdf")] HttpRequestData req)
        {
            _logger.LogInformation("Generating Pay Slip PDF");
            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<PaySlipPdfRequestDto>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data == null || data.Preview == null || data.Business == null)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteAsJsonAsync(new { error = "Preview and Business details are required" });
                    return badReq;
                }

                byte[] pdfBytes = _paySlipPdfService.GeneratePdf(data);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/pdf");
                string paySlipNo = string.IsNullOrWhiteSpace(data.PaySlipNo) ? "DRAFT" : data.PaySlipNo;
                response.Headers.Add("Content-Disposition", $"inline; filename=\"PaySlip_{paySlipNo}.pdf\"");
                await response.WriteBytesAsync(pdfBytes);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Pay Slip PDF: {Message}", ex.Message);
                var errResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResponse.WriteStringAsync($"Error: {ex.Message}");
                return errResponse;
            }
        }

        [Function("PayrollSendEmail")]
        public async Task<HttpResponseData> SendEmail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "payroll/send-email")] HttpRequestData req)
        {
            _logger.LogInformation("Sending Pay Slip Email");
            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<SendPaySlipEmailRequestDto>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data == null || data.PdfRequest == null)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteAsJsonAsync(new { error = "PdfRequest is required" });
                    return badReq;
                }

                var (success, message, r2Path) = await _paySlipPdfService.SendPaySlipEmailAsync(data);
                var response = req.CreateResponse(success ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
                await response.WriteAsJsonAsync(new { success, message, r2Path });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Pay Slip email: {Message}", ex.Message);
                var errResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResponse.WriteStringAsync($"Error: {ex.Message}");
                return errResponse;
            }
        }

        [Function("PayrollSendSms")]
        public async Task<HttpResponseData> SendSms(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "payroll/send-sms")] HttpRequestData req)
        {
            _logger.LogInformation("Sending Payroll Transfer SMS");
            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<SendTransferSmsRequestDto>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data == null || data.EmployeeId == Guid.Empty)
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteAsJsonAsync(new { error = "EmployeeId is required" });
                    return badReq;
                }

                var employee = await _employeeRepository.GetByIdAsync(data.EmployeeId);
                if (employee == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "Employee not found" });
                    return notFound;
                }

                string targetPhone = !string.IsNullOrWhiteSpace(data.CustomPhone) ? data.CustomPhone : (employee.Phone ?? string.Empty);
                if (string.IsNullOrWhiteSpace(targetPhone))
                {
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteAsJsonAsync(new { error = "Employee has no phone number configured" });
                    return badReq;
                }

                string periodDesc = string.IsNullOrWhiteSpace(data.PeriodDescription) ? "近期" : data.PeriodDescription;
                string smsMessage = $"尊敬的 {employee.Name}，您在 {periodDesc} 的工资 ${data.Amount:F2} 已转账完成。-- NHN Power";

                bool sent = await _smsService.SendSmsAsync(targetPhone, smsMessage, "NHNPOWER");

                var response = req.CreateResponse(sent ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
                await response.WriteAsJsonAsync(new
                {
                    success = sent,
                    message = sent ? $"Transfer SMS sent to {targetPhone}" : "Failed to send SMS via ClickSend",
                    phone = targetPhone
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending payroll SMS: {Message}", ex.Message);
                var errResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResponse.WriteStringAsync($"Error: {ex.Message}");
                return errResponse;
            }
        }
    }
}
