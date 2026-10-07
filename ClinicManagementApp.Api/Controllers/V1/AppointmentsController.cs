using ClinicManagementApp.Api.Models.Common;
using ClinicManagementApp.Api.Models.DTOs.Appointments;
using ClinicManagementApp.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicManagementApp.Api.Controllers.V1;

[ApiController]
[Route("api/v1/appointments")]
[Authorize]
public class AppointmentsController(IAppointmentService appointmentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAppointments([FromQuery] AppointmentFilterParams filter)
    {
        var result = await appointmentService.GetAppointmentsAsync(filter);
        return Ok(result);
    }

    [HttpGet("upcoming")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcoming([FromQuery] int limit = 10)
    {
        var result = await appointmentService.GetUpcomingAppointmentsAsync(limit);
        return Ok(result);
    }

    [HttpGet("patient/{patientId:int}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientAppointments(int patientId)
    {
        var result = await appointmentService.GetPatientAppointmentsAsync(patientId);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Schedule([FromBody] CreateAppointmentRequest request)
    {
        var result = await appointmentService.ScheduleAppointmentAsync(request);
        return result.Success ? StatusCode(StatusCodes.Status201Created, result) : BadRequest(result);
    }

    [HttpPut("{id:int}/reschedule")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reschedule(int id, [FromBody] RescheduleAppointmentRequest request)
    {
        var result = await appointmentService.RescheduleAppointmentAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:int}/cancel")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await appointmentService.CancelAppointmentAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:int}/complete")]
    [ProducesResponseType(typeof(ApiResponse<CompleteAppointmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CompleteAppointmentResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Complete(int id)
    {
        var result = await appointmentService.CompleteAppointmentAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("send-sms-reminder")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendSmsReminder([FromBody] SendReminderRequest request)
    {
        var result = await appointmentService.TriggerSmsReminderAsync(request.AppointmentId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

