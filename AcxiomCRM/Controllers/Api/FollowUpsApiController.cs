using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs.FollowUp;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers.Api;

[Route("api/followups")]
[Authorize]
public class FollowUpsApiController : BaseApiController
{
    private readonly IFollowUpService _followUpService;

    public FollowUpsApiController(IFollowUpService followUpService)
    {
        _followUpService = followUpService;
    }

    [HttpGet]
    public async Task<IActionResult> GetFollowUps(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? type,
        [FromQuery] string? assignedTo,
        [FromQuery] bool? overdueOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _followUpService.GetFollowUpsAsync(
            User, search, status, type, assignedTo, overdueOnly, null, null, null, page, pageSize);

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
    public async Task<IActionResult> GetFollowUp(int id)
    {
        var followUp = await _followUpService.GetFollowUpByIdAsync(id, User);
        if (followUp == null)
        {
            if (await _followUpService.FollowUpExistsAsync(id))
            {
                return ApiForbidden("You do not have permission to view this follow-up.");
            }
            return ApiNotFound($"Follow-up with ID {id} not found.");
        }

        return ApiSuccess(MapToDto(followUp));
    }

    [HttpPost]
    public async Task<IActionResult> CreateFollowUp([FromBody] CreateFollowUpDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ApiBadRequest("Validation failed.", GetModelErrors());
        }

        var followUp = new FollowUp
        {
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            OpportunityId = dto.OpportunityId,
            FollowUpDate = dto.FollowUpDate,
            FollowUpType = dto.FollowUpType,
            Subject = dto.Subject,
            Remarks = dto.Remarks,
            AssignedTo = dto.AssignedTo ?? string.Empty
        };

        var result = await _followUpService.CreateFollowUpAsync(followUp, User);
        if (!result.Success)
        {
            return ApiValidationError(result.Errors, result.Message);
        }

        return ApiCreated($"/api/followups/{result.Data!.FollowUpId}", MapToDto(result.Data), result.Message);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateFollowUp(int id, [FromBody] CreateFollowUpDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ApiBadRequest("Validation failed.", GetModelErrors());
        }

        if (!await _followUpService.FollowUpExistsAsync(id))
        {
            return ApiNotFound($"Follow-up with ID {id} not found.");
        }

        var followUp = new FollowUp
        {
            FollowUpId = id,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            OpportunityId = dto.OpportunityId,
            FollowUpDate = dto.FollowUpDate,
            FollowUpType = dto.FollowUpType,
            Subject = dto.Subject,
            Remarks = dto.Remarks,
            AssignedTo = dto.AssignedTo ?? string.Empty
        };

        var result = await _followUpService.UpdateFollowUpAsync(followUp, User);
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
    public async Task<IActionResult> DeleteFollowUp(int id)
    {
        var result = await _followUpService.DeleteFollowUpAsync(id, User);
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

    private static FollowUpDto MapToDto(FollowUp f) => new()
    {
        FollowUpId = f.FollowUpId,
        CustomerId = f.CustomerId,
        CustomerName = f.Customer?.CustomerName,
        LeadId = f.LeadId,
        LeadName = f.Lead?.LeadName,
        OpportunityId = f.OpportunityId,
        FollowUpDate = f.FollowUpDate,
        FollowUpType = f.FollowUpType,
        Subject = f.Subject,
        Remarks = f.Remarks,
        Status = f.Status,
        AssignedTo = f.AssignedTo,
        AssignedUserName = f.AssignedUser?.FullName,
        IsOverdue = f.IsOverdue
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
