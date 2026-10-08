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

public class FollowUpService : IFollowUpService
{
    private readonly ApplicationDbContext _db;
    private readonly ICrmBusinessValidator _validator;
    private readonly IResourceAuthorizationService _authService;
    private readonly IAuditService _auditService;
    private readonly ILogger<FollowUpService> _logger;

    public FollowUpService(
        ApplicationDbContext db,
        ICrmBusinessValidator validator,
        IResourceAuthorizationService authService,
        IAuditService auditService,
        ILogger<FollowUpService> logger)
    {
        _db = db;
        _validator = validator;
        _authService = authService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<PagedResult<FollowUp>> GetFollowUpsAsync(
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
        int pageSize = 10)
    {
        var query = _db.FollowUps
            .Include(f => f.AssignedUser)
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.Opportunity)
            .AsNoTracking();

        query = _authService.ScopeFollowUps(query, user);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(f =>
                f.Subject.ToLower().Contains(term) ||
                (f.Remarks != null && f.Remarks.ToLower().Contains(term)) ||
                (f.Customer != null && f.Customer.CustomerName.ToLower().Contains(term)) ||
                (f.Lead != null && f.Lead.LeadName.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(f => f.FollowUpType == type);
        }

        if (!string.IsNullOrWhiteSpace(assignedTo) && (_authService.IsAdmin(user) || _authService.IsManager(user)))
        {
            query = query.Where(f => f.AssignedTo == assignedTo);
        }

        if (overdueOnly == true)
        {
            var today = DateTime.UtcNow.Date;
            query = query.Where(f => f.Status == "Planned" && f.FollowUpDate.Date < today);
        }

        if (customerId.HasValue) query = query.Where(f => f.CustomerId == customerId.Value);
        if (leadId.HasValue) query = query.Where(f => f.LeadId == leadId.Value);
        if (opportunityId.HasValue) query = query.Where(f => f.OpportunityId == opportunityId.Value);

        query = query.OrderBy(f => f.FollowUpDate);

        var total = await query.CountAsync();
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<FollowUp>
        {
            Items = items,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<FollowUp?> GetFollowUpByIdAsync(int id, ClaimsPrincipal user)
    {
        var item = await _db.FollowUps
            .Include(f => f.AssignedUser)
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.Opportunity)
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (item == null) return null;
        if (!_authService.CanAccessFollowUp(user, item)) return null;

        return item;
    }

    public async Task<bool> FollowUpExistsAsync(int id)
    {
        return await _db.FollowUps.AnyAsync(f => f.FollowUpId == id);
    }

    public async Task<ServiceResult<FollowUp>> CreateFollowUpAsync(FollowUp followUp, ClaimsPrincipal user)
    {
        var currentUserId = _authService.GetUserId(user);
        if (string.IsNullOrWhiteSpace(followUp.AssignedTo))
        {
            followUp.AssignedTo = currentUserId ?? string.Empty;
        }

        followUp.Subject = followUp.Subject?.Trim() ?? string.Empty;
        followUp.Status = "Planned";
        followUp.CreatedBy = user.Identity?.Name ?? "System";
        followUp.CreatedDate = DateTime.UtcNow;

        var validationResult = await _validator.ValidateFollowUpAsync(followUp, isNew: true);
        if (!validationResult.IsValid)
        {
            return ServiceResult<FollowUp>.Fail("Validation failed.", validationResult.Errors);
        }

        _db.FollowUps.Add(followUp);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            currentUserId,
            "Create",
            "FollowUp",
            nameof(FollowUp),
            followUp.FollowUpId.ToString(),
            null,
            JsonSerializer.Serialize(new { followUp.FollowUpId, followUp.Subject, followUp.FollowUpDate, followUp.FollowUpType, followUp.AssignedTo }),
            "Success",
            $"Scheduled {followUp.FollowUpType} follow-up '{followUp.Subject}' on {followUp.FollowUpDate:d}");

        return ServiceResult<FollowUp>.Ok(followUp, "Follow-up scheduled successfully.");
    }

    public async Task<ServiceResult<FollowUp>> UpdateFollowUpAsync(FollowUp followUp, ClaimsPrincipal user)
    {
        var existing = await _db.FollowUps.FindAsync(followUp.FollowUpId);
        if (existing == null)
        {
            return ServiceResult<FollowUp>.NotFound("Follow-up not found.");
        }

        if (!_authService.CanAccessFollowUp(user, existing))
        {
            return ServiceResult<FollowUp>.Unauthorized("Unauthorized to edit this follow-up.");
        }

        followUp.Subject = followUp.Subject?.Trim() ?? string.Empty;

        var validationResult = await _validator.ValidateFollowUpAsync(followUp, isNew: false);
        if (!validationResult.IsValid)
        {
            return ServiceResult<FollowUp>.Fail("Validation failed.", validationResult.Errors);
        }

        var oldSnapshot = JsonSerializer.Serialize(new { existing.Subject, existing.FollowUpDate, existing.FollowUpType, existing.Status, existing.AssignedTo });

        existing.Subject = followUp.Subject;
        existing.FollowUpType = followUp.FollowUpType;
        existing.FollowUpDate = followUp.FollowUpDate;
        existing.Status = followUp.Status;
        existing.Remarks = followUp.Remarks?.Trim();
        existing.CustomerId = followUp.CustomerId;
        existing.LeadId = followUp.LeadId;
        existing.OpportunityId = followUp.OpportunityId;
        if (_authService.IsAdmin(user) || _authService.IsManager(user))
        {
            existing.AssignedTo = followUp.AssignedTo;
        }
        existing.ModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var newSnapshot = JsonSerializer.Serialize(new { existing.Subject, existing.FollowUpDate, existing.FollowUpType, existing.Status, existing.AssignedTo });
        var currentUserId = _authService.GetUserId(user);

        await _auditService.LogAsync(
            currentUserId,
            "Update",
            "FollowUp",
            nameof(FollowUp),
            existing.FollowUpId.ToString(),
            oldSnapshot,
            newSnapshot,
            "Success",
            $"Updated follow-up '{existing.Subject}'");

        return ServiceResult<FollowUp>.Ok(existing, "Follow-up updated successfully.");
    }

    public async Task<ServiceResult<FollowUp>> ChangeStatusAsync(int id, string newStatus, string? remarks, ClaimsPrincipal user)
    {
        var existing = await _db.FollowUps.FindAsync(id);
        if (existing == null)
        {
            return ServiceResult<FollowUp>.NotFound("Follow-up not found.");
        }

        if (!_authService.CanAccessFollowUp(user, existing))
        {
            return ServiceResult<FollowUp>.Unauthorized("Unauthorized to update this follow-up.");
        }

        var oldStatus = existing.Status;
        existing.Status = newStatus;
        if (!string.IsNullOrWhiteSpace(remarks))
        {
            existing.Remarks = (existing.Remarks != null ? existing.Remarks + " | " : "") + remarks.Trim();
        }
        existing.ModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var currentUserId = _authService.GetUserId(user);
        await _auditService.LogAsync(
            currentUserId,
            newStatus, // Completed, Missed, Cancelled
            "FollowUp",
            nameof(FollowUp),
            id.ToString(),
            oldStatus,
            newStatus,
            "Success",
            $"Follow-up '{existing.Subject}' marked as {newStatus}");

        return ServiceResult<FollowUp>.Ok(existing, $"Follow-up status changed to {newStatus}.");
    }

    public async Task<ServiceResult<FollowUp>> RescheduleAsync(int id, DateTime newDate, string? remarks, ClaimsPrincipal user)
    {
        var existing = await _db.FollowUps.FindAsync(id);
        if (existing == null)
        {
            return ServiceResult<FollowUp>.NotFound("Follow-up not found.");
        }

        if (!_authService.CanAccessFollowUp(user, existing))
        {
            return ServiceResult<FollowUp>.Unauthorized("Unauthorized to reschedule this follow-up.");
        }

        if (newDate.Date < DateTime.UtcNow.Date)
        {
            return ServiceResult<FollowUp>.Fail("Follow-up date cannot be earlier than today.");
        }

        var oldDate = existing.FollowUpDate;
        existing.FollowUpDate = newDate;
        existing.Status = "Planned";
        if (!string.IsNullOrWhiteSpace(remarks))
        {
            existing.Remarks = (existing.Remarks != null ? existing.Remarks + " | " : "") + $"Rescheduled: {remarks.Trim()}";
        }
        existing.ModifiedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var currentUserId = _authService.GetUserId(user);
        await _auditService.LogAsync(
            currentUserId,
            "Reschedule",
            "FollowUp",
            nameof(FollowUp),
            id.ToString(),
            oldDate.ToString("o"),
            newDate.ToString("o"),
            "Success",
            $"Follow-up '{existing.Subject}' rescheduled from {oldDate:d} to {newDate:d}");

        return ServiceResult<FollowUp>.Ok(existing, "Follow-up rescheduled successfully.");
    }

    public async Task<ServiceResult> DeleteFollowUpAsync(int id, ClaimsPrincipal user)
    {
        var existing = await _db.FollowUps.FindAsync(id);
        if (existing == null)
        {
            return ServiceResult.NotFound("Follow-up not found.");
        }

        if (!_authService.IsAdmin(user) && !_authService.CanAccessFollowUp(user, existing))
        {
            return ServiceResult.Unauthorized("Unauthorized to delete this follow-up.");
        }

        _db.FollowUps.Remove(existing);
        await _db.SaveChangesAsync();

        var currentUserId = _authService.GetUserId(user);
        await _auditService.LogAsync(
            currentUserId,
            "Delete",
            "FollowUp",
            nameof(FollowUp),
            id.ToString(),
            existing.Subject,
            null,
            "Success",
            $"Deleted follow-up '{existing.Subject}'");

        return ServiceResult.Ok("Follow-up deleted successfully.");
    }
}
