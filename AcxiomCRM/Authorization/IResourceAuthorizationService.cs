using System.Security.Claims;
using AcxiomCRM.Models;

namespace AcxiomCRM.Authorization;

public interface IResourceAuthorizationService
{
    bool IsAdmin(ClaimsPrincipal user);
    bool IsManager(ClaimsPrincipal user);
    bool IsSalesExecutive(ClaimsPrincipal user);
    string? GetUserId(ClaimsPrincipal user);

    bool CanAccessCustomer(ClaimsPrincipal user, Customer? customer);
    bool CanAccessLead(ClaimsPrincipal user, Lead? lead);
    bool CanAccessOpportunity(ClaimsPrincipal user, Opportunity? opportunity);
    bool CanAccessFollowUp(ClaimsPrincipal user, FollowUp? followUp);
    bool CanAccessActivity(ClaimsPrincipal user, Activity? activity);

    IQueryable<Customer> ScopeCustomers(IQueryable<Customer> query, ClaimsPrincipal user);
    IQueryable<Lead> ScopeLeads(IQueryable<Lead> query, ClaimsPrincipal user);
    IQueryable<Opportunity> ScopeOpportunities(IQueryable<Opportunity> query, ClaimsPrincipal user);
    IQueryable<FollowUp> ScopeFollowUps(IQueryable<FollowUp> query, ClaimsPrincipal user);
    IQueryable<Activity> ScopeActivities(IQueryable<Activity> query, ClaimsPrincipal user);
}
