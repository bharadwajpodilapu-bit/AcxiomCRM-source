using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs.Common;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected string CurrentTraceId => Activity.Current?.Id ?? HttpContext.TraceIdentifier;

    protected IActionResult ApiSuccess<T>(T data, string message = "")
    {
        return Ok(ApiResponse<T>.Ok(data, message));
    }

    protected IActionResult ApiCreated<T>(string uri, T data, string message = "")
    {
        return Created(uri, ApiResponse<T>.Ok(data, message));
    }

    protected IActionResult ApiBadRequest(string message, Dictionary<string, List<string>>? errors = null)
    {
        return BadRequest(ApiResponse.Fail(message, errors, CurrentTraceId));
    }

    protected IActionResult ApiNotFound(string message = "Resource not found.")
    {
        return NotFound(ApiResponse.Fail(message, null, CurrentTraceId));
    }

    protected IActionResult ApiForbidden(string message = "You do not have permission to access this resource.")
    {
        return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(message, null, CurrentTraceId));
    }

    protected IActionResult ApiUnauthorized(string message = "Authentication is required.")
    {
        return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail(message, null, CurrentTraceId));
    }

    protected IActionResult ApiConflict(string message, Dictionary<string, List<string>>? errors = null)
    {
        return StatusCode(StatusCodes.Status409Conflict, ApiResponse.Fail(message, errors, CurrentTraceId));
    }

    protected IActionResult ApiValidationError(Dictionary<string, List<string>> errors, string message = "Validation failed.")
    {
        return StatusCode(StatusCodes.Status422UnprocessableEntity, ApiResponse.Fail(message, errors, CurrentTraceId));
    }
}
