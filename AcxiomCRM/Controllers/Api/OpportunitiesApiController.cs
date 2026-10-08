using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs.Opportunity;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers.Api;

[Route("api/opportunities")]
[Authorize]
public class OpportunitiesApiController : BaseApiController
{
    private readonly IOpportunityService _opportunityService;

    public OpportunitiesApiController(IOpportunityService opportunityService)
    {
        _opportunityService = opportunityService;
    }

    [HttpGet]
    public async Task<IActionResult> GetOpportunities(
        [FromQuery] string? search,
        [FromQuery] string? stage,
        [FromQuery] string? status,
        [FromQuery] string? ownerId,
        [FromQuery] int? customerId,
        [FromQuery] string? sortBy = "CreatedDate",
        [FromQuery] bool sortDesc = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _opportunityService.GetOpportunitiesAsync(
            User, search, stage, status, ownerId, customerId, sortBy, sortDesc, page, pageSize);

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
    public async Task<IActionResult> GetOpportunity(int id)
    {
        var opp = await _opportunityService.GetOpportunityByIdAsync(id, User);
        if (opp == null)
        {
            if (await _opportunityService.OpportunityExistsAsync(id))
            {
                return ApiForbidden("You do not have permission to view this opportunity.");
            }
            return ApiNotFound($"Opportunity with ID {id} not found.");
        }

        return ApiSuccess(MapToDto(opp));
    }

    [HttpPost]
    public async Task<IActionResult> CreateOpportunity([FromBody] CreateOpportunityDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ApiBadRequest("Validation failed.", GetModelErrors());
        }

        var opp = new Opportunity
        {
            OpportunityName = dto.OpportunityName,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Amount = dto.Amount,
            Stage = dto.Stage,
            Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            OwnerId = dto.OwnerId ?? string.Empty,
            Notes = dto.Notes
        };

        var result = await _opportunityService.CreateOpportunityAsync(opp, User);
        if (!result.Success)
        {
            return ApiValidationError(result.Errors, result.Message);
        }

        return ApiCreated($"/api/opportunities/{result.Data!.OpportunityId}", MapToDto(result.Data), result.Message);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateOpportunity(int id, [FromBody] UpdateOpportunityDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ApiBadRequest("Validation failed.", GetModelErrors());
        }

        if (!await _opportunityService.OpportunityExistsAsync(id))
        {
            return ApiNotFound($"Opportunity with ID {id} not found.");
        }

        var opp = new Opportunity
        {
            OpportunityId = id,
            OpportunityName = dto.OpportunityName,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Amount = dto.Amount,
            Stage = dto.Stage,
            Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            OwnerId = dto.OwnerId ?? string.Empty,
            Notes = dto.Notes
        };

        var result = await _opportunityService.UpdateOpportunityAsync(opp, User);
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
    public async Task<IActionResult> DeleteOpportunity(int id)
    {
        var result = await _opportunityService.DeleteOpportunityAsync(id, User);
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

    [HttpPatch("{id:int}/stage")]
    public async Task<IActionResult> ChangeStage(int id, [FromBody] string newStage)
    {
        var result = await _opportunityService.ChangeStageAsync(id, newStage, User);
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

    private static OpportunityDto MapToDto(Opportunity o) => new()
    {
        OpportunityId = o.OpportunityId,
        OpportunityName = o.OpportunityName,
        CustomerId = o.CustomerId,
        CustomerName = o.Customer?.CustomerName,
        LeadId = o.LeadId,
        Amount = o.Amount,
        Stage = o.Stage,
        Probability = o.Probability,
        ExpectedCloseDate = o.ExpectedCloseDate,
        Status = o.Status,
        OwnerId = o.OwnerId,
        OwnerName = o.Owner?.FullName,
        Notes = o.Notes,
        CreatedDate = o.CreatedDate
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
