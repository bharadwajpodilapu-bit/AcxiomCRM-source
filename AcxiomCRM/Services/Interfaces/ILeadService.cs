using System.Security.Claims;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Common;

namespace AcxiomCRM.Services.Interfaces;

public interface ILeadService
{
    Task<PagedResult<Lead>> GetLeadsAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? status = null,
        string? priority = null,
        string? assignedTo = null,
        string? sortBy = "CreatedDate",
        bool sortDesc = true,
        int pageIndex = 1,
        int pageSize = 10);

    Task<Lead?> GetLeadByIdAsync(int id, ClaimsPrincipal user, bool includeRelated = false);
    Task<bool> LeadExistsAsync(int id);
    Task<ServiceResult<Lead>> CreateLeadAsync(Lead lead, ClaimsPrincipal user);
    Task<ServiceResult<Lead>> UpdateLeadAsync(Lead lead, ClaimsPrincipal user);
    Task<ServiceResult<Lead>> ChangeStatusAsync(int id, string newStatus, ClaimsPrincipal user);
    Task<ServiceResult<Customer>> ConvertLeadAsync(
        int leadId,
        bool createOpportunity,
        decimal? opportunityAmount,
        DateTime? expectedCloseDate,
        ClaimsPrincipal user);
    Task<ServiceResult> DeleteLeadAsync(int id, ClaimsPrincipal user);
}
