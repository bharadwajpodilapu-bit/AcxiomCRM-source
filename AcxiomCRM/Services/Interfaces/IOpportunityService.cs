using System.Security.Claims;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Common;

namespace AcxiomCRM.Services.Interfaces;

public interface IOpportunityService
{
    Task<PagedResult<Opportunity>> GetOpportunitiesAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? stage = null,
        string? status = null,
        string? ownerId = null,
        int? customerId = null,
        string? sortBy = "CreatedDate",
        bool sortDesc = true,
        int pageIndex = 1,
        int pageSize = 10);

    Task<Opportunity?> GetOpportunityByIdAsync(int id, ClaimsPrincipal user, bool includeRelated = false);
    Task<bool> OpportunityExistsAsync(int id);
    Task<ServiceResult<Opportunity>> CreateOpportunityAsync(Opportunity opportunity, ClaimsPrincipal user);
    Task<ServiceResult<Opportunity>> UpdateOpportunityAsync(Opportunity opportunity, ClaimsPrincipal user);
    Task<ServiceResult<Opportunity>> ChangeStageAsync(int id, string newStage, ClaimsPrincipal user);
    Task<ServiceResult> DeleteOpportunityAsync(int id, ClaimsPrincipal user);
}
