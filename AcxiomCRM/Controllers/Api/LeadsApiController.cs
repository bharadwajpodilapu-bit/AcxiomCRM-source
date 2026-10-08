using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs.Lead;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers.Api;

[Route("api/leads")]
[Authorize]
public class LeadsApiController : BaseApiController
{
    private readonly ILeadService _leadService;

    public LeadsApiController(ILeadService leadService)
    {
        _leadService = leadService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLeads(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? assignedTo,
        [FromQuery] string? sortBy = "CreatedDate",
        [FromQuery] bool sortDesc = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _leadService.GetLeadsAsync(
            User, search, status, priority, assignedTo, sortBy, sortDesc, page, pageSize);

        var dtoList = result.Items.Select(MapToDto).ToList();

        return ApiSuccess(new
        {
            items = dtoList,
            pageIndex = result.PageIndex,
            pageSize = result.PageSize,
            totalCount = result.TotalCount,
            totalPages = result.TotalPages,
            hasPreviousPage = result.HasPreviousPage,
            hasNextPage = result.HasNextPage
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetLead(int id)
    {
        var lead = await _leadService.GetLeadByIdAsync(id, User);
        if (lead == null)
        {
            if (await _leadService.LeadExistsAsync(id))
            {
                return ApiForbidden("You do not have permission to view this lead.");
            }
            return ApiNotFound($"Lead with ID {id} not found.");
        }

        return ApiSuccess(MapToDto(lead));
    }

    [HttpPost]
    public async Task<IActionResult> CreateLead([FromBody] CreateLeadDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ApiBadRequest("Validation failed.", GetModelErrors());
        }

        var lead = new Lead
        {
            LeadName = dto.LeadName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Source = dto.Source,
            Priority = dto.Priority,
            ExpectedValue = dto.ExpectedValue,
            AssignedTo = dto.AssignedTo
        };

        var result = await _leadService.CreateLeadAsync(lead, User);
        if (!result.Success)
        {
            return ApiValidationError(result.Errors, result.Message);
        }

        return ApiCreated($"/api/leads/{result.Data!.LeadId}", MapToDto(result.Data), result.Message);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateLead(int id, [FromBody] UpdateLeadDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ApiBadRequest("Validation failed.", GetModelErrors());
        }

        if (!await _leadService.LeadExistsAsync(id))
        {
            return ApiNotFound($"Lead with ID {id} not found.");
        }

        var lead = new Lead
        {
            LeadId = id,
            LeadName = dto.LeadName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Source = dto.Source,
            Status = dto.Status,
            Priority = dto.Priority,
            ExpectedValue = dto.ExpectedValue,
            AssignedTo = dto.AssignedTo
        };

        var result = await _leadService.UpdateLeadAsync(lead, User);
        if (result.IsUnauthorized)
        {
            return ApiForbidden(result.Message);
        }
        if (result.IsNotFound)
        {
            return ApiNotFound(result.Message);
        }
        if (!result.Success)
        {
            return ApiValidationError(result.Errors, result.Message);
        }

        return ApiSuccess(MapToDto(result.Data!), result.Message);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteLead(int id)
    {
        var result = await _leadService.DeleteLeadAsync(id, User);
        if (result.IsUnauthorized)
        {
            return ApiForbidden(result.Message);
        }
        if (result.IsNotFound)
        {
            return ApiNotFound(result.Message);
        }
        if (!result.Success)
        {
            return ApiBadRequest(result.Message);
        }

        return ApiSuccess(new { id }, result.Message);
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] string newStatus)
    {
        var result = await _leadService.ChangeStatusAsync(id, newStatus, User);
        if (result.IsUnauthorized)
        {
            return ApiForbidden(result.Message);
        }
        if (result.IsNotFound)
        {
            return ApiNotFound(result.Message);
        }
        if (!result.Success)
        {
            return ApiValidationError(result.Errors, result.Message);
        }

        return ApiSuccess(MapToDto(result.Data!), result.Message);
    }

    private static LeadDto MapToDto(Lead l) => new()
    {
        LeadId = l.LeadId,
        LeadCode = l.LeadCode,
        LeadName = l.LeadName,
        Email = l.Email,
        Phone = l.Phone,
        CompanyName = l.CompanyName,
        Source = l.Source,
        Status = l.Status,
        Priority = l.Priority,
        ExpectedValue = l.ExpectedValue,
        AssignedTo = l.AssignedTo,
        AssignedUserName = l.AssignedUser?.FullName,
        CreatedDate = l.CreatedDate,
        ConvertedCustomerId = l.ConvertedCustomerId,
        ConvertedOpportunityId = l.ConvertedOpportunityId
    };

    private Dictionary<string, List<string>> GetModelErrors()
    {
        var dict = new Dictionary<string, List<string>>();
        foreach (var (key, val) in ModelState)
        {
            if (val.Errors.Any())
            {
                dict[key] = val.Errors.Select(e => e.ErrorMessage).ToList();
            }
        }
        return dict;
    }
}
