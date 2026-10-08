using System.Security.Claims;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Common;

namespace AcxiomCRM.Services.Interfaces;

public interface IFollowUpService
{
    Task<PagedResult<FollowUp>> GetFollowUpsAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? status = null,
        string? type = null,
        string? assignedTo = null,
        bool? overdueOnly = null,
        int? customerId = null,
        int? leadId = null,
        int? opportunityId = null,
        int pageIndex = 1,
        int pageSize = 10);

    Task<FollowUp?> GetFollowUpByIdAsync(int id, ClaimsPrincipal user);
    Task<bool> FollowUpExistsAsync(int id);
    Task<ServiceResult<FollowUp>> CreateFollowUpAsync(FollowUp followUp, ClaimsPrincipal user);
    Task<ServiceResult<FollowUp>> UpdateFollowUpAsync(FollowUp followUp, ClaimsPrincipal user);
    Task<ServiceResult<FollowUp>> ChangeStatusAsync(int id, string newStatus, string? remarks, ClaimsPrincipal user);
    Task<ServiceResult<FollowUp>> RescheduleAsync(int id, DateTime newDate, string? remarks, ClaimsPrincipal user);
    Task<ServiceResult> DeleteFollowUpAsync(int id, ClaimsPrincipal user);
}
