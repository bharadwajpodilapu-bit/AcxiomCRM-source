using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.Validators;
using AcxiomCRM.ViewModels.Common;

namespace AcxiomCRM.Services.Implementations;

public class ActivityService : IActivityService
{
    private readonly ApplicationDbContext _db;
    private readonly ICrmBusinessValidator _validator;
    private readonly IResourceAuthorizationService _authService;
    private readonly IAuditService _auditService;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(
        ApplicationDbContext db,
        ICrmBusinessValidator validator,
        IResourceAuthorizationService authService,
        IAuditService auditService,
        ILogger<ActivityService> logger)
    {
        _db = db;
        _validator = validator;
        _authService = authService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<PagedResult<Activity>> GetActivitiesAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? type = null,
        string? status = null,
        string? assignedTo = null,
        int? customerId = null,
        int? leadId = null,
        int? opportunityId = null,
        int pageIndex = 1,
        int pageSize = 10)
    {
        var query = _db.Activities
            .Include(a => a.AssignedUser)
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.Opportunity)
            .AsNoTracking();

        query = _authService.ScopeActivities(query, user);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.Subject.ToLower().Contains(term) ||
                (a.Description != null && a.Description.ToLower().Contains(term)) ||
                (a.Customer != null && a.Customer.CustomerName.ToLower().Contains(term)) ||
                (a.Lead != null && a.Lead.LeadName.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(a => a.ActivityType == type);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(assignedTo) && (_authService.IsAdmin(user) || _authService.IsManager(user)))
        {
            query = query.Where(a => a.AssignedTo == assignedTo);
        }

        if (customerId.HasValue) query = query.Where(a => a.CustomerId == customerId.Value);
        if (leadId.HasValue) query = query.Where(a => a.LeadId == leadId.Value);
        if (opportunityId.HasValue) query = query.Where(a => a.OpportunityId == opportunityId.Value);

        query = query.OrderByDescending(a => a.ActivityDate);

        var total = await query.CountAsync();
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Activity>
        {
            Items = items,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<Activity?> GetActivityByIdAsync(int id, ClaimsPrincipal user)
    {
        var item = await _db.Activities
            .Include(a => a.AssignedUser)
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.Opportunity)
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (item == null) return null;
        if (!_authService.CanAccessActivity(user, item)) return null;

        return item;
    }

    public async Task<bool> ActivityExistsAsync(int id)
    {
        return await _db.Activities.AnyAsync(a => a.ActivityId == id);
    }

    public async Task<ServiceResult<Activity>> CreateActivityAsync(Activity activity, ClaimsPrincipal user)
    {
        var currentUserId = _authService.GetUserId(user);
        if (string.IsNullOrWhiteSpace(activity.AssignedTo))
        {
            activity.AssignedTo = currentUserId ?? string.Empty;
        }

        activity.Subject = activity.Subject?.Trim() ?? string.Empty;
        activity.CreatedBy = user.Identity?.Name ?? "System";
        activity.CreatedDate = DateTime.UtcNow;

        var validationResult = await _validator.ValidateActivityAsync(activity);
        if (!validationResult.IsValid)
        {
            return ServiceResult<Activity>.Fail("Validation failed.", validationResult.Errors);
        }

        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            currentUserId,
            "Create",
            "Activity",
            nameof(Activity),
            activity.ActivityId.ToString(),
            null,
            JsonSerializer.Serialize(new { activity.ActivityId, activity.Subject, activity.ActivityType, activity.Status, activity.AssignedTo }),
            "Success",
            $"Logged {activity.ActivityType} activity '{activity.Subject}'");

        return ServiceResult<Activity>.Ok(activity, "Activity recorded successfully.");
    }

    public async Task<ServiceResult<Activity>> UpdateActivityAsync(Activity activity, ClaimsPrincipal user)
    {
        var existing = await _db.Activities.FindAsync(activity.ActivityId);
        if (existing == null)
        {
            return ServiceResult<Activity>.NotFound("Activity not found.");
        }

        if (!_authService.CanAccessActivity(user, existing))
        {
            return ServiceResult<Activity>.Unauthorized("Unauthorized to edit this activity.");
        }

        activity.Subject = activity.Subject?.Trim() ?? string.Empty;

        var validationResult = await _validator.ValidateActivityAsync(activity);
        if (!validationResult.IsValid)
        {
            return ServiceResult<Activity>.Fail("Validation failed.", validationResult.Errors);
        }

        var oldSnapshot = JsonSerializer.Serialize(new { existing.Subject, existing.ActivityType, existing.Status, existing.AssignedTo });

        existing.Subject = activity.Subject;
        existing.ActivityType = activity.ActivityType;
        existing.Description = activity.Description?.Trim();
        existing.ActivityDate = activity.ActivityDate;
        existing.Status = activity.Status;
        existing.CustomerId = activity.CustomerId;
        existing.LeadId = activity.LeadId;
        existing.OpportunityId = activity.OpportunityId;
        if (_authService.IsAdmin(user) || _authService.IsManager(user))
        {
            existing.AssignedTo = activity.AssignedTo;
        }

        await _db.SaveChangesAsync();

        var newSnapshot = JsonSerializer.Serialize(new { existing.Subject, existing.ActivityType, existing.Status, existing.AssignedTo });
        var currentUserId = _authService.GetUserId(user);

        await _auditService.LogAsync(
            currentUserId,
            "Update",
            "Activity",
            nameof(Activity),
            existing.ActivityId.ToString(),
            oldSnapshot,
            newSnapshot,
            "Success",
            $"Updated activity '{existing.Subject}'");

        return ServiceResult<Activity>.Ok(existing, "Activity updated successfully.");
    }

    public async Task<ServiceResult<Activity>> CompleteActivityAsync(int id, ClaimsPrincipal user)
    {
        var existing = await _db.Activities.FindAsync(id);
        if (existing == null)
        {
            return ServiceResult<Activity>.NotFound("Activity not found.");
        }

        if (!_authService.CanAccessActivity(user, existing))
        {
            return ServiceResult<Activity>.Unauthorized("Unauthorized to edit this activity.");
        }

        var oldStatus = existing.Status;
        existing.Status = "Completed";
        await _db.SaveChangesAsync();

        var currentUserId = _authService.GetUserId(user);
        await _auditService.LogAsync(
            currentUserId,
            "Complete",
            "Activity",
            nameof(Activity),
            id.ToString(),
            oldStatus,
            "Completed",
            "Success",
            $"Activity '{existing.Subject}' marked completed");

        return ServiceResult<Activity>.Ok(existing, "Activity marked as completed.");
    }

    public async Task<ServiceResult> DeleteActivityAsync(int id, ClaimsPrincipal user)
    {
        var existing = await _db.Activities.FindAsync(id);
        if (existing == null)
        {
            return ServiceResult.NotFound("Activity not found.");
        }

        if (!_authService.IsAdmin(user) && !_authService.CanAccessActivity(user, existing))
        {
            return ServiceResult.Unauthorized("Unauthorized to delete this activity.");
        }

        _db.Activities.Remove(existing);
        await _db.SaveChangesAsync();

        var currentUserId = _authService.GetUserId(user);
        await _auditService.LogAsync(
            currentUserId,
            "Delete",
            "Activity",
            nameof(Activity),
            id.ToString(),
            existing.Subject,
            null,
            "Success",
            $"Deleted activity '{existing.Subject}'");

        return ServiceResult.Ok("Activity deleted successfully.");
    }
}
