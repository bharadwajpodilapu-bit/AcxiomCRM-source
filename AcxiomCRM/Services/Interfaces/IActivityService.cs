using System.Security.Claims;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Common;

namespace AcxiomCRM.Services.Interfaces;

public interface IActivityService
{
    Task<PagedResult<Activity>> GetActivitiesAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? type = null,
        string? status = null,
        string? assignedTo = null,
        int? customerId = null,
        int? leadId = null,
        int? opportunityId = null,
        int pageIndex = 1,
        int pageSize = 10);

    Task<Activity?> GetActivityByIdAsync(int id, ClaimsPrincipal user);
    Task<bool> ActivityExistsAsync(int id);
    Task<ServiceResult<Activity>> CreateActivityAsync(Activity activity, ClaimsPrincipal user);
    Task<ServiceResult<Activity>> UpdateActivityAsync(Activity activity, ClaimsPrincipal user);
    Task<ServiceResult<Activity>> CompleteActivityAsync(int id, ClaimsPrincipal user);
    Task<ServiceResult> DeleteActivityAsync(int id, ClaimsPrincipal user);
}
