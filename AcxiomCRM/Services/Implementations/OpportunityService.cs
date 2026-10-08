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

public class OpportunityService : IOpportunityService
{
    private readonly ApplicationDbContext _db;
    private readonly ICrmBusinessValidator _validator;
    private readonly IResourceAuthorizationService _authService;
    private readonly IAuditService _auditService;
    private readonly ILogger<OpportunityService> _logger;

    public OpportunityService(
        ApplicationDbContext db,
        ICrmBusinessValidator validator,
        IResourceAuthorizationService authService,
        IAuditService auditService,
        ILogger<OpportunityService> logger)
    {
        _db = db;
        _validator = validator;
        _authService = authService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<PagedResult<Opportunity>> GetOpportunitiesAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? stage = null,
        string? status = null,
        string? ownerId = null,
        int? customerId = null,
        string? sortBy = "CreatedDate",
        bool sortDesc = true,
        int pageIndex = 1,
        int pageSize = 10)
    {
        var query = _db.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Owner)
            .Include(o => o.Lead)
            .AsNoTracking();

        query = _authService.ScopeOpportunities(query, user);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(o =>
                o.OpportunityName.ToLower().Contains(term) ||
                o.Customer!.CustomerName.ToLower().Contains(term) ||
                (o.Lead != null && o.Lead.LeadName.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }

        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(ownerId) && (_authService.IsAdmin(user) || _authService.IsManager(user)))
        {
            query = query.Where(o => o.OwnerId == ownerId);
        }

        query = sortBy?.ToLower() switch
        {
            "name" or "opportunityname" => sortDesc ? query.OrderByDescending(o => o.OpportunityName) : query.OrderBy(o => o.OpportunityName),
            "amount" => sortDesc ? query.OrderByDescending(o => o.Amount) : query.OrderBy(o => o.Amount),
            "probability" => sortDesc ? query.OrderByDescending(o => o.Probability) : query.OrderBy(o => o.Probability),
            "stage" => sortDesc ? query.OrderByDescending(o => o.Stage) : query.OrderBy(o => o.Stage),
            "expectedclosedate" => sortDesc ? query.OrderByDescending(o => o.ExpectedCloseDate) : query.OrderBy(o => o.ExpectedCloseDate),
            _ => sortDesc ? query.OrderByDescending(o => o.CreatedDate) : query.OrderBy(o => o.CreatedDate)
        };

        var total = await query.CountAsync();
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Opportunity>
        {
            Items = items,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<Opportunity?> GetOpportunityByIdAsync(int id, ClaimsPrincipal user, bool includeRelated = false)
    {
        IQueryable<Opportunity> query = _db.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Owner)
            .Include(o => o.Lead);

        if (includeRelated)
        {
            query = query
                .Include(o => o.FollowUps)
                .Include(o => o.Activities);
        }

        var opp = await query.FirstOrDefaultAsync(o => o.OpportunityId == id);
        if (opp == null) return null;

        if (!_authService.CanAccessOpportunity(user, opp))
        {
            return null;
        }

        return opp;
    }

    public async Task<bool> OpportunityExistsAsync(int id)
    {
        return await _db.Opportunities.AnyAsync(o => o.OpportunityId == id);
    }

    public async Task<ServiceResult<Opportunity>> CreateOpportunityAsync(Opportunity opportunity, ClaimsPrincipal user)
    {
        var currentUserId = _authService.GetUserId(user);
        if (string.IsNullOrWhiteSpace(opportunity.OwnerId))
        {
            opportunity.OwnerId = currentUserId ?? string.Empty;
        }

        opportunity.OpportunityName = opportunity.OpportunityName?.Trim() ?? string.Empty;
        opportunity.CreatedBy = user.Identity?.Name ?? "System";
        opportunity.CreatedDate = DateTime.UtcNow;

        // Sync Status based on Stage
        if (opportunity.Stage == "Won")
        {
            opportunity.Status = "Won";
            opportunity.Probability = 100;
        }
        else if (opportunity.Stage == "Lost")
        {
            opportunity.Status = "Lost";
            opportunity.Probability = 0;
        }
        else
        {
            opportunity.Status = "Open";
        }

        var validationResult = await _validator.ValidateOpportunityAsync(opportunity);
        if (!validationResult.IsValid)
        {
            return ServiceResult<Opportunity>.Fail("Validation failed.", validationResult.Errors);
        }

        _db.Opportunities.Add(opportunity);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            currentUserId,
            "Create",
            "Opportunity",
            nameof(Opportunity),
            opportunity.OpportunityId.ToString(),
            null,
            JsonSerializer.Serialize(new { opportunity.OpportunityId, opportunity.OpportunityName, opportunity.Amount, opportunity.Stage, opportunity.Probability, opportunity.OwnerId }),
            "Success",
            $"Created opportunity {opportunity.OpportunityName} for amount {opportunity.Amount:C}");

        return ServiceResult<Opportunity>.Ok(opportunity, "Opportunity created successfully.");
    }

    public async Task<ServiceResult<Opportunity>> UpdateOpportunityAsync(Opportunity opportunity, ClaimsPrincipal user)
    {
        var existing = await _db.Opportunities.FindAsync(opportunity.OpportunityId);
        if (existing == null)
        {
            return ServiceResult<Opportunity>.NotFound("Opportunity not found.");
        }

        if (!_authService.CanAccessOpportunity(user, existing))
        {
            return ServiceResult<Opportunity>.Unauthorized("Unauthorized to edit this opportunity.");
        }

        opportunity.OpportunityName = opportunity.OpportunityName?.Trim() ?? string.Empty;

        // If not admin/manager, retain existing owner
        if (!_authService.IsAdmin(user) && !_authService.IsManager(user))
        {
            opportunity.OwnerId = existing.OwnerId;
        }

        // Sync Status based on Stage
        if (opportunity.Stage == "Won")
        {
            opportunity.Status = "Won";
            opportunity.Probability = 100;
        }
        else if (opportunity.Stage == "Lost")
        {
            opportunity.Status = "Lost";
            opportunity.Probability = 0;
        }
        else
        {
            opportunity.Status = "Open";
        }

        var validationResult = await _validator.ValidateOpportunityAsync(opportunity, opportunity.OpportunityId);
        if (!validationResult.IsValid)
        {
            return ServiceResult<Opportunity>.Fail("Validation failed.", validationResult.Errors);
        }

        var oldSnapshot = JsonSerializer.Serialize(new { existing.OpportunityName, existing.Amount, existing.Stage, existing.Probability, existing.Status, existing.OwnerId });

        existing.OpportunityName = opportunity.OpportunityName;
        existing.CustomerId = opportunity.CustomerId;
        existing.LeadId = opportunity.LeadId;
        existing.Amount = opportunity.Amount;
        existing.Stage = opportunity.Stage;
        existing.Probability = opportunity.Probability;
        existing.ExpectedCloseDate = opportunity.ExpectedCloseDate;
        existing.Status = opportunity.Status;
        if (_authService.IsAdmin(user) || _authService.IsManager(user))
        {
            existing.OwnerId = opportunity.OwnerId;
        }
        existing.Notes = opportunity.Notes?.Trim();
        existing.ModifiedDate = DateTime.UtcNow;
        existing.ModifiedBy = user.Identity?.Name ?? "System";

        await _db.SaveChangesAsync();

        var newSnapshot = JsonSerializer.Serialize(new { existing.OpportunityName, existing.Amount, existing.Stage, existing.Probability, existing.Status, existing.OwnerId });
        var currentUserId = _authService.GetUserId(user);

        await _auditService.LogAsync(
            currentUserId,
            "Update",
            "Opportunity",
            nameof(Opportunity),
            existing.OpportunityId.ToString(),
            oldSnapshot,
            newSnapshot,
            "Success",
            $"Updated opportunity {existing.OpportunityName}");

        return ServiceResult<Opportunity>.Ok(existing, "Opportunity updated successfully.");
    }

    public async Task<ServiceResult<Opportunity>> ChangeStageAsync(int id, string newStage, ClaimsPrincipal user)
    {
        var existing = await _db.Opportunities.FindAsync(id);
        if (existing == null)
        {
            return ServiceResult<Opportunity>.NotFound("Opportunity not found.");
        }

        if (!_authService.CanAccessOpportunity(user, existing))
        {
            return ServiceResult<Opportunity>.Unauthorized("Unauthorized to edit this opportunity.");
        }

        var oldStage = existing.Stage;
        existing.Stage = newStage;
        if (newStage == "Won")
        {
            existing.Status = "Won";
            existing.Probability = 100;
        }
        else if (newStage == "Lost")
        {
            existing.Status = "Lost";
            existing.Probability = 0;
        }
        else
        {
            existing.Status = "Open";
        }
        existing.ModifiedDate = DateTime.UtcNow;
        existing.ModifiedBy = user.Identity?.Name ?? "System";

        await _db.SaveChangesAsync();

        var currentUserId = _authService.GetUserId(user);
        await _auditService.LogAsync(
            currentUserId,
            "StageChange",
            "Opportunity",
            nameof(Opportunity),
            id.ToString(),
            oldStage,
            newStage,
            "Success",
            $"Opportunity {existing.OpportunityName} stage changed from {oldStage} to {newStage}");

        return ServiceResult<Opportunity>.Ok(existing, "Opportunity stage updated.");
    }

    public async Task<ServiceResult> DeleteOpportunityAsync(int id, ClaimsPrincipal user)
    {
        var existing = await _db.Opportunities
            .Include(o => o.FollowUps)
            .Include(o => o.Activities)
            .FirstOrDefaultAsync(o => o.OpportunityId == id);

        if (existing == null)
        {
            return ServiceResult.NotFound("Opportunity not found.");
        }

        if (!_authService.IsAdmin(user) && !_authService.CanAccessOpportunity(user, existing))
        {
            return ServiceResult.Unauthorized("Unauthorized to delete this opportunity.");
        }

        _db.Opportunities.Remove(existing);
        await _db.SaveChangesAsync();

        var currentUserId = _authService.GetUserId(user);
        await _auditService.LogAsync(
            currentUserId,
            "Delete",
            "Opportunity",
            nameof(Opportunity),
            id.ToString(),
            existing.OpportunityName,
            null,
            "Success",
            $"Deleted opportunity {existing.OpportunityName}");

        return ServiceResult.Ok("Opportunity deleted successfully.");
    }
}
