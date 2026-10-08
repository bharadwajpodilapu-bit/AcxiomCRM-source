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

public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _db;
    private readonly ICrmBusinessValidator _validator;
    private readonly IResourceAuthorizationService _authService;
    private readonly IAuditService _auditService;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        ApplicationDbContext db,
        ICrmBusinessValidator validator,
        IResourceAuthorizationService authService,
        IAuditService auditService,
        ILogger<CustomerService> logger)
    {
        _db = db;
        _validator = validator;
        _authService = authService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<PagedResult<Customer>> GetCustomersAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? status = null,
        string? ownerId = null,
        string? sortBy = "CreatedDate",
        bool sortDesc = true,
        int pageIndex = 1,
        int pageSize = 10)
    {
        var query = _db.Customers
            .Include(c => c.Owner)
            .AsNoTracking();

        query = _authService.ScopeCustomers(query, user);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.CustomerName.ToLower().Contains(term) ||
                c.Email.ToLower().Contains(term) ||
                c.Phone.Contains(term) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(ownerId) && (_authService.IsAdmin(user) || _authService.IsManager(user)))
        {
            query = query.Where(c => c.OwnerId == ownerId);
        }

        query = sortBy?.ToLower() switch
        {
            "name" or "customername" => sortDesc ? query.OrderByDescending(c => c.CustomerName) : query.OrderBy(c => c.CustomerName),
            "email" => sortDesc ? query.OrderByDescending(c => c.Email) : query.OrderBy(c => c.Email),
            "company" or "companyname" => sortDesc ? query.OrderByDescending(c => c.CompanyName) : query.OrderBy(c => c.CompanyName),
            "status" => sortDesc ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            _ => sortDesc ? query.OrderByDescending(c => c.CreatedDate) : query.OrderBy(c => c.CreatedDate)
        };

        var total = await query.CountAsync();
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Customer>
        {
            Items = items,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<Customer?> GetCustomerByIdAsync(int id, ClaimsPrincipal user, bool includeRelated = false)
    {
        IQueryable<Customer> query = _db.Customers
            .Include(c => c.Owner);

        if (includeRelated)
        {
            query = query
                .Include(c => c.Opportunities)
                .Include(c => c.FollowUps)
                .Include(c => c.Activities)
                .Include(c => c.ConvertedFromLeads);
        }

        var customer = await query.FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer == null) return null;

        if (!_authService.CanAccessCustomer(user, customer))
        {
            return null;
        }

        return customer;
    }

    public async Task<bool> CustomerExistsAsync(int id)
    {
        return await _db.Customers.AnyAsync(c => c.CustomerId == id);
    }

    public async Task<ServiceResult<Customer>> CreateCustomerAsync(Customer customer, ClaimsPrincipal user)
    {
        // Enforce Owner assignment if not admin/manager
        var currentUserId = _authService.GetUserId(user);
        if (!_authService.IsAdmin(user) && !_authService.IsManager(user))
        {
            customer.OwnerId = currentUserId ?? string.Empty;
        }
        else if (string.IsNullOrWhiteSpace(customer.OwnerId))
        {
            customer.OwnerId = currentUserId ?? string.Empty;
        }

        customer.CustomerName = customer.CustomerName?.Trim() ?? string.Empty;
        customer.Email = customer.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        customer.Phone = customer.Phone?.Trim() ?? string.Empty;
        customer.CreatedBy = user.Identity?.Name ?? "System";
        customer.CreatedDate = DateTime.UtcNow;
        customer.IsActive = true;
        customer.Status = "Active";

        if (string.IsNullOrWhiteSpace(customer.CustomerCode))
        {
            var nextNum = (await _db.Customers.CountAsync()) + 1;
            customer.CustomerCode = $"CUST-{DateTime.UtcNow.Year}-{nextNum:D4}";
        }

        var validationResult = await _validator.ValidateCustomerAsync(customer);
        if (!validationResult.IsValid)
        {
            return ServiceResult<Customer>.Fail("Validation failed.", validationResult.Errors);
        }

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(
            currentUserId,
            "Create",
            "Customer",
            nameof(Customer),
            customer.CustomerId.ToString(),
            null,
            JsonSerializer.Serialize(new { customer.CustomerId, customer.CustomerName, customer.Email, customer.Phone, customer.OwnerId }),
            "Success",
            $"Created customer {customer.CustomerName} ({customer.CustomerCode})");

        return ServiceResult<Customer>.Ok(customer, "Customer created successfully.");
    }

    public async Task<ServiceResult<Customer>> UpdateCustomerAsync(Customer customer, ClaimsPrincipal user)
    {
        var existing = await _db.Customers.FindAsync(customer.CustomerId);
        if (existing == null)
        {
            return ServiceResult<Customer>.NotFound("Customer not found.");
        }

        if (!_authService.CanAccessCustomer(user, existing))
        {
            return ServiceResult<Customer>.Unauthorized("Unauthorized to edit this customer.");
        }

        // Validate changes
        customer.CustomerName = customer.CustomerName?.Trim() ?? string.Empty;
        customer.Email = customer.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        customer.Phone = customer.Phone?.Trim() ?? string.Empty;

        // If not admin/manager, do not allow reassigning owner
        if (!_authService.IsAdmin(user) && !_authService.IsManager(user))
        {
            customer.OwnerId = existing.OwnerId;
        }

        var validationResult = await _validator.ValidateCustomerAsync(customer, customer.CustomerId);
        if (!validationResult.IsValid)
        {
            return ServiceResult<Customer>.Fail("Validation failed.", validationResult.Errors);
        }

        var oldSnapshot = JsonSerializer.Serialize(new { existing.CustomerName, existing.Email, existing.Phone, existing.OwnerId, existing.Status });

        existing.CustomerName = customer.CustomerName;
        existing.Email = customer.Email;
        existing.Phone = customer.Phone;
        existing.CompanyName = customer.CompanyName?.Trim();
        existing.Address = customer.Address?.Trim();
        existing.City = customer.City?.Trim();
        existing.State = customer.State?.Trim();
        existing.Status = customer.Status;
        existing.IsActive = customer.Status == "Active";
        if (_authService.IsAdmin(user) || _authService.IsManager(user))
        {
            existing.OwnerId = customer.OwnerId;
        }
        existing.ModifiedDate = DateTime.UtcNow;
        existing.ModifiedBy = user.Identity?.Name ?? "System";

        await _db.SaveChangesAsync();

        var newSnapshot = JsonSerializer.Serialize(new { existing.CustomerName, existing.Email, existing.Phone, existing.OwnerId, existing.Status });
        var currentUserId = _authService.GetUserId(user);

        await _auditService.LogAsync(
            currentUserId,
            "Update",
            "Customer",
            nameof(Customer),
            existing.CustomerId.ToString(),
            oldSnapshot,
            newSnapshot,
            "Success",
            $"Updated customer {existing.CustomerName}");

        return ServiceResult<Customer>.Ok(existing, "Customer updated successfully.");
    }

    public async Task<ServiceResult> DeactivateCustomerAsync(int id, ClaimsPrincipal user)
    {
        var existing = await _db.Customers.FindAsync(id);
        if (existing == null)
        {
            return ServiceResult.NotFound("Customer not found.");
        }

        if (!_authService.CanAccessCustomer(user, existing))
        {
            return ServiceResult.Unauthorized("Unauthorized to deactivate this customer.");
        }

        var oldStatus = existing.Status;
        existing.Status = "Inactive";
        existing.IsActive = false;
        existing.ModifiedDate = DateTime.UtcNow;
        existing.ModifiedBy = user.Identity?.Name ?? "System";

        await _db.SaveChangesAsync();

        var currentUserId = _authService.GetUserId(user);
        await _auditService.LogAsync(
            currentUserId,
            "Deactivate",
            "Customer",
            nameof(Customer),
            existing.CustomerId.ToString(),
            oldStatus,
            "Inactive",
            "Success",
            $"Deactivated customer {existing.CustomerName}");

        return ServiceResult.Ok("Customer deactivated successfully.");
    }

    public async Task<ServiceResult> DeleteCustomerAsync(int id, ClaimsPrincipal user)
    {
        var existing = await _db.Customers
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (existing == null)
        {
            return ServiceResult.NotFound("Customer not found.");
        }

        if (!_authService.IsAdmin(user) && !_authService.CanAccessCustomer(user, existing))
        {
            return ServiceResult.Unauthorized("Unauthorized to delete this customer.");
        }

        // If customer has related records, perform safe deactivation instead of hard delete
        if (existing.Opportunities.Any() || existing.FollowUps.Any() || existing.Activities.Any())
        {
            return await DeactivateCustomerAsync(id, user);
        }

        _db.Customers.Remove(existing);
        await _db.SaveChangesAsync();

        var currentUserId = _authService.GetUserId(user);
        await _auditService.LogAsync(
            currentUserId,
            "Delete",
            "Customer",
            nameof(Customer),
            id.ToString(),
            existing.CustomerName,
            null,
            "Success",
            $"Permanently deleted customer {existing.CustomerName}");

        return ServiceResult.Ok("Customer deleted successfully.");
    }
}
