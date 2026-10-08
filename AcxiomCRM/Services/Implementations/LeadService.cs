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

public class LeadService : ILeadService
{
    private readonly ApplicationDbContext _db;
    private readonly ICrmBusinessValidator _validator;
    private readonly IResourceAuthorizationService _authService;
    private readonly IAuditService _auditService;
    private readonly ILogger<LeadService> _logger;

    public LeadService(
        ApplicationDbContext db,
        ICrmBusinessValidator validator,
        IResourceAuthorizationService authService,
        IAuditService auditService,
        ILogger<LeadService> logger)
    {
        _db = db;
        _validator = validator;
        _authService = authService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<PagedResult<Lead>> GetLeadsAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? status = null,
        string? priority = null,
        string? assignedTo = null,
        string? sortBy = "CreatedDate",
        bool sortDesc = true,
        int pageIndex = 1,
        int pageSize = 10)
    {
        var query = _db.Leads
            .Include(l => l.AssignedUser)
            .Include(l => l.ConvertedCustomer)
            .AsNoTracking();

        query = _authService.ScopeLeads(query, user);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(l =>
                l.LeadName.ToLower().Contains(term) ||
                (l.Email != null && l.Email.ToLower().Contains(term)) ||
                (l.Phone != null && l.Phone.Contains(term)) ||
                (l.CompanyName != null && l.CompanyName.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(l => l.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(assignedTo) && (_authService.IsAdmin(user) || _authService.IsManager(user)))
        {
            query = query.Where(l => l.AssignedTo == assignedTo);
        }

        query = sortBy?.ToLower() switch
        {
            "name" or "leadname" => sortDesc ? query.OrderByDescending(l => l.LeadName) : query.OrderBy(l => l.LeadName),
            "status" => sortDesc ? query.OrderByDescending(l => l.Status) : query.OrderBy(l => l.Status),
            "priority" => sortDesc ? query.OrderByDescending(l => l.Priority) : query.OrderBy(l => l.Priority),
            "value" or "expectedvalue" => sortDesc ? query.OrderByDescending(l => l.ExpectedValue) : query.OrderBy(l => l.ExpectedValue),
            _ => sortDesc ? query.OrderByDescending(l => l.CreatedDate) : query.OrderBy(l => l.CreatedDate)
        };

        var total = await query.CountAsync();
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Lead>
        {
            Items = items,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<Lead?> GetLeadByIdAsync(int id, ClaimsPrincipal user, bool includeRelated = false)
    {
        IQueryable<Lead> query = _db.Leads
            .Include(l => l.AssignedUser)
            .Include(l => l.ConvertedCustomer)
            .Include(l => l.ConvertedOpportunity);

        if (includeRelated)
        {
            query = query
                .Include(l => l.FollowUps)
                .Include(l => l.Activities);
        }

        var lead = await query.FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead == null) return null;

        if (!_authService.CanAccessLead(user, lead))
        {
            return null;
        }

        return lead;
    }

    public async Task<bool> LeadExistsAsync(int id)
    {
        return await _db.Leads.AnyAsync(l => l.LeadId == id);
    }

    public async Task<ServiceResult<Lead>> CreateLeadAsync(Lead lead, ClaimsPrincipal user)
    {
        var currentUserId = _authService.GetUserId(user);
        if (string.IsNullOrWhiteSpace(lead.AssignedTo))
        {
            lead.AssignedTo = currentUserId;
        }

        lead.LeadName = lead.LeadName?.Trim() ?? string.Empty;
        lead.Email = lead.Email?.Trim().ToLowerInvariant();
        lead.Phone = lead.Phone?.Trim();
        lead.CompanyName = lead.CompanyName?.Trim();
        lead.CreatedBy = user.Identity?.Name ?? "System";
        lead.CreatedDate = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(lead.LeadCode))
        {
            var nextNum = (await _db.Leads.CountAsync()) + 1;
            lead.LeadCode = $"LEAD-{DateTime.UtcNow.Year}-{nextNum:D4}";
        }

        var validationResult = await _validator.ValidateLeadAsync(lead);
        if (!validationResult.IsValid)
        {
            return ServiceResult<Lead>.Fail("Validation failed.", validationResult.Errors);
        }

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            currentUserId,
            "Create",
            "Lead",
            nameof(Lead),
            lead.LeadId.ToString(),
            null,
            JsonSerializer.Serialize(new { lead.LeadId, lead.LeadName, lead.Status, lead.Priority, lead.AssignedTo }),
            "Success",
            $"Created lead {lead.LeadName} ({lead.LeadCode})");

        return ServiceResult<Lead>.Ok(lead, "Lead created successfully.");
    }

    public async Task<ServiceResult<Lead>> UpdateLeadAsync(Lead lead, ClaimsPrincipal user)
    {
        var existing = await _db.Leads.FindAsync(lead.LeadId);
        if (existing == null)
        {
            return ServiceResult<Lead>.NotFound("Lead not found.");
        }

        if (!_authService.CanAccessLead(user, existing))
        {
            return ServiceResult<Lead>.Unauthorized("Unauthorized to edit this lead.");
        }

        // Validate status transition
        if (existing.Status != lead.Status)
        {
            var transitionResult = _validator.ValidateLeadStatusTransition(existing.Status, lead.Status);
            if (!transitionResult.IsValid)
            {
                return ServiceResult<Lead>.Fail("Invalid status transition.", transitionResult.Errors);
            }
        }

        lead.LeadName = lead.LeadName?.Trim() ?? string.Empty;
        lead.Email = lead.Email?.Trim().ToLowerInvariant();
        lead.Phone = lead.Phone?.Trim();

        var validationResult = await _validator.ValidateLeadAsync(lead, lead.LeadId);
        if (!validationResult.IsValid)
        {
            return ServiceResult<Lead>.Fail("Validation failed.", validationResult.Errors);
        }

        var oldSnapshot = JsonSerializer.Serialize(new { existing.LeadName, existing.Status, existing.Priority, existing.AssignedTo, existing.ExpectedValue });

        existing.LeadName = lead.LeadName;
        existing.Email = lead.Email;
        existing.Phone = lead.Phone;
        existing.CompanyName = lead.CompanyName?.Trim();
        existing.Source = lead.Source;
        existing.Status = lead.Status;
        existing.Priority = lead.Priority;
        existing.ExpectedValue = lead.ExpectedValue;
        if (_authService.IsAdmin(user) || _authService.IsManager(user))
        {
            existing.AssignedTo = lead.AssignedTo;
        }
        existing.ModifiedDate = DateTime.UtcNow;
        existing.ModifiedBy = user.Identity?.Name ?? "System";

        await _db.SaveChangesAsync();

        var newSnapshot = JsonSerializer.Serialize(new { existing.LeadName, existing.Status, existing.Priority, existing.AssignedTo, existing.ExpectedValue });
        var currentUserId = _authService.GetUserId(user);

        await _auditService.LogAsync(
            currentUserId,
            "Update",
            "Lead",
            nameof(Lead),
            existing.LeadId.ToString(),
            oldSnapshot,
            newSnapshot,
            "Success",
            $"Updated lead {existing.LeadName}");

        return ServiceResult<Lead>.Ok(existing, "Lead updated successfully.");
    }

    public async Task<ServiceResult<Lead>> ChangeStatusAsync(int id, string newStatus, ClaimsPrincipal user)
    {
        var existing = await _db.Leads.FindAsync(id);
        if (existing == null)
        {
            return ServiceResult<Lead>.NotFound("Lead not found.");
        }

        if (!_authService.CanAccessLead(user, existing))
        {
            return ServiceResult<Lead>.Unauthorized("Unauthorized to update this lead.");
        }

        var transitionResult = _validator.ValidateLeadStatusTransition(existing.Status, newStatus);
        if (!transitionResult.IsValid)
        {
            return ServiceResult<Lead>.Fail("Invalid status transition.", transitionResult.Errors);
        }

        var oldStatus = existing.Status;
        existing.Status = newStatus;
        existing.ModifiedDate = DateTime.UtcNow;
        existing.ModifiedBy = user.Identity?.Name ?? "System";

        await _db.SaveChangesAsync();

        var currentUserId = _authService.GetUserId(user);
        await _auditService.LogAsync(
            currentUserId,
            "StatusChange",
            "Lead",
            nameof(Lead),
            id.ToString(),
            oldStatus,
            newStatus,
            "Success",
            $"Lead {existing.LeadName} status changed from {oldStatus} to {newStatus}");

        return ServiceResult<Lead>.Ok(existing, "Lead status updated.");
    }

    public async Task<ServiceResult<Customer>> ConvertLeadAsync(
        int leadId,
        bool createOpportunity,
        decimal? opportunityAmount,
        DateTime? expectedCloseDate,
        ClaimsPrincipal user)
    {
        var lead = await _db.Leads.FindAsync(leadId);
        if (lead == null)
        {
            return ServiceResult<Customer>.NotFound("Lead not found.");
        }

        if (!_authService.CanAccessLead(user, lead))
        {
            return ServiceResult<Customer>.Unauthorized("Unauthorized to convert this lead.");
        }

        if (lead.Status == "Converted")
        {
            return ServiceResult<Customer>.Fail("This lead has already been converted.");
        }

        if (lead.Status == "Lost")
        {
            return ServiceResult<Customer>.Fail("Lost leads cannot be converted.");
        }

        var currentUserId = _authService.GetUserId(user) ?? lead.AssignedTo ?? string.Empty;

        // Check if customer already exists by email or phone to prevent duplicate customer
        Customer? customer = null;
        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == lead.Email.ToLower());
        }
        if (customer == null && !string.IsNullOrWhiteSpace(lead.Phone))
        {
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == lead.Phone);
        }

        if (customer == null)
        {
            var custCount = await _db.Customers.CountAsync();
            customer = new Customer
            {
                CustomerCode = $"CUST-{DateTime.UtcNow.Year}-{(custCount + 1):D4}",
                CustomerName = lead.LeadName,
                Email = string.IsNullOrWhiteSpace(lead.Email) ? $"lead{lead.LeadId}@noemail.local" : lead.Email,
                Phone = string.IsNullOrWhiteSpace(lead.Phone) ? "0000000000" : lead.Phone,
                CompanyName = lead.CompanyName,
                OwnerId = currentUserId,
                Status = "Active",
                IsActive = true,
                CreatedBy = user.Identity?.Name ?? "System",
                CreatedDate = DateTime.UtcNow
            };

            var customerValidation = await _validator.ValidateCustomerAsync(customer);
            if (!customerValidation.IsValid)
            {
                return ServiceResult<Customer>.Fail("Failed to convert lead: Customer validation failed.", customerValidation.Errors);
            }

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
        }

        Opportunity? opportunity = null;
        if (createOpportunity)
        {
            var amount = opportunityAmount ?? lead.ExpectedValue ?? 1000m;
            var closeDate = expectedCloseDate ?? DateTime.UtcNow.AddDays(30);

            opportunity = new Opportunity
            {
                OpportunityName = $"{lead.LeadName} - Opportunity",
                CustomerId = customer.CustomerId,
                LeadId = lead.LeadId,
                Amount = amount,
                Stage = "Qualification",
                Probability = 20,
                ExpectedCloseDate = closeDate,
                Status = "Open",
                OwnerId = currentUserId,
                CreatedBy = user.Identity?.Name ?? "System",
                CreatedDate = DateTime.UtcNow
            };

            var oppValidation = await _validator.ValidateOpportunityAsync(opportunity);
            if (!oppValidation.IsValid)
            {
                return ServiceResult<Customer>.Fail("Failed to create opportunity during conversion.", oppValidation.Errors);
            }

            _db.Opportunities.Add(opportunity);
            await _db.SaveChangesAsync();
        }

        // Update Lead status and references
        lead.Status = "Converted";
        lead.ConvertedCustomerId = customer.CustomerId;
        if (opportunity != null)
        {
            lead.ConvertedOpportunityId = opportunity.OpportunityId;
        }
        lead.ModifiedDate = DateTime.UtcNow;
        lead.ModifiedBy = user.Identity?.Name ?? "System";

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            currentUserId,
            "Convert",
            "Lead",
            nameof(Lead),
            lead.LeadId.ToString(),
            null,
            JsonSerializer.Serialize(new { customerId = customer.CustomerId, opportunityId = opportunity?.OpportunityId }),
            "Success",
            $"Converted lead {lead.LeadName} into Customer {customer.CustomerName}" + (opportunity != null ? $" and Opportunity {opportunity.OpportunityName}" : ""));

        return ServiceResult<Customer>.Ok(customer, "Lead successfully converted to Customer.");
    }

    public async Task<ServiceResult> DeleteLeadAsync(int id, ClaimsPrincipal user)
    {
        var existing = await _db.Leads
            .Include(l => l.FollowUps)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (existing == null)
        {
            return ServiceResult.NotFound("Lead not found.");
        }

        if (!_authService.IsAdmin(user) && !_authService.CanAccessLead(user, existing))
        {
            return ServiceResult.Unauthorized("Unauthorized to delete this lead.");
        }

        // If converted, prevent deletion to preserve history
        if (existing.Status == "Converted")
        {
            return ServiceResult.Fail("Cannot delete a converted lead as it is referenced by existing customer records.");
        }

        _db.Leads.Remove(existing);
        await _db.SaveChangesAsync();

        var currentUserId = _authService.GetUserId(user);
        await _auditService.LogAsync(
            currentUserId,
            "Delete",
            "Lead",
            nameof(Lead),
            id.ToString(),
            existing.LeadName,
            null,
            "Success",
            $"Deleted lead {existing.LeadName}");

        return ServiceResult.Ok("Lead deleted successfully.");
    }
}
