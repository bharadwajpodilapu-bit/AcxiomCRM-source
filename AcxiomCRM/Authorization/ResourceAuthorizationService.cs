using System.Security.Claims;
using AcxiomCRM.Models;

namespace AcxiomCRM.Authorization;

public class ResourceAuthorizationService : IResourceAuthorizationService
{
    public bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(AppRoles.Admin);
    public bool IsManager(ClaimsPrincipal user) => user.IsInRole(AppRoles.Manager);
    public bool IsSalesExecutive(ClaimsPrincipal user) => user.IsInRole(AppRoles.SalesExecutive);

    public string? GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool CanAccessCustomer(ClaimsPrincipal user, Customer? customer)
    {
        if (customer == null) return false;
        if (IsAdmin(user) || IsManager(user)) return true;
        var userId = GetUserId(user);
        return !string.IsNullOrEmpty(userId) && customer.OwnerId == userId;
    }

    public bool CanAccessLead(ClaimsPrincipal user, Lead? lead)
    {
        if (lead == null) return false;
        if (IsAdmin(user) || IsManager(user)) return true;
        var userId = GetUserId(user);
        return !string.IsNullOrEmpty(userId) && (lead.AssignedTo == userId || lead.CreatedBy == user.Identity?.Name);
    }

    public bool CanAccessOpportunity(ClaimsPrincipal user, Opportunity? opportunity)
    {
        if (opportunity == null) return false;
        if (IsAdmin(user) || IsManager(user)) return true;
        var userId = GetUserId(user);
        return !string.IsNullOrEmpty(userId) && opportunity.OwnerId == userId;
    }

    public bool CanAccessFollowUp(ClaimsPrincipal user, FollowUp? followUp)
    {
        if (followUp == null) return false;
        if (IsAdmin(user) || IsManager(user)) return true;
        var userId = GetUserId(user);
        return !string.IsNullOrEmpty(userId) && followUp.AssignedTo == userId;
    }

    public bool CanAccessActivity(ClaimsPrincipal user, Activity? activity)
    {
        if (activity == null) return false;
        if (IsAdmin(user) || IsManager(user)) return true;
        var userId = GetUserId(user);
        return !string.IsNullOrEmpty(userId) && activity.AssignedTo == userId;
    }

    public IQueryable<Customer> ScopeCustomers(IQueryable<Customer> query, ClaimsPrincipal user)
    {
        if (IsAdmin(user) || IsManager(user)) return query;
        var userId = GetUserId(user) ?? string.Empty;
        return query.Where(c => c.OwnerId == userId);
    }

    public IQueryable<Lead> ScopeLeads(IQueryable<Lead> query, ClaimsPrincipal user)
    {
        if (IsAdmin(user) || IsManager(user)) return query;
        var userId = GetUserId(user) ?? string.Empty;
        var userName = user.Identity?.Name ?? string.Empty;
        return query.Where(l => l.AssignedTo == userId || l.CreatedBy == userName);
    }

    public IQueryable<Opportunity> ScopeOpportunities(IQueryable<Opportunity> query, ClaimsPrincipal user)
    {
        if (IsAdmin(user) || IsManager(user)) return query;
        var userId = GetUserId(user) ?? string.Empty;
        return query.Where(o => o.OwnerId == userId);
    }

    public IQueryable<FollowUp> ScopeFollowUps(IQueryable<FollowUp> query, ClaimsPrincipal user)
    {
        if (IsAdmin(user) || IsManager(user)) return query;
        var userId = GetUserId(user) ?? string.Empty;
        return query.Where(f => f.AssignedTo == userId);
    }

    public IQueryable<Activity> ScopeActivities(IQueryable<Activity> query, ClaimsPrincipal user)
    {
        if (IsAdmin(user) || IsManager(user)) return query;
        var userId = GetUserId(user) ?? string.Empty;
        return query.Where(a => a.AssignedTo == userId);
    }
}
