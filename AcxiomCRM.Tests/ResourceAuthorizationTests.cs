using System.Security.Claims;
using Xunit;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;

namespace AcxiomCRM.Tests;

public class ResourceAuthorizationTests
{
    private static ClaimsPrincipal CreatePrincipal(string userId, string role, string name = "testuser")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void Admin_Has_Full_Access_To_Any_Customer()
    {
        var authService = new ResourceAuthorizationService();
        var admin = CreatePrincipal("admin-id", AppRoles.Admin);
        var customer = new Customer { CustomerId = 1, OwnerId = "other-sales-rep" };

        var canAccess = authService.CanAccessCustomer(admin, customer);

        Assert.True(canAccess);
    }

    [Fact]
    public void Manager_Has_Access_To_Team_Customer()
    {
        var authService = new ResourceAuthorizationService();
        var manager = CreatePrincipal("mgr-id", AppRoles.Manager);
        var customer = new Customer { CustomerId = 1, OwnerId = "sales-rep-id" };

        var canAccess = authService.CanAccessCustomer(manager, customer);

        Assert.True(canAccess);
    }

    [Fact]
    public void SalesExecutive_Has_Access_To_Own_Customer()
    {
        var authService = new ResourceAuthorizationService();
        var salesUser = CreatePrincipal("sales-1", AppRoles.SalesExecutive);
        var customer = new Customer { CustomerId = 1, OwnerId = "sales-1" };

        var canAccess = authService.CanAccessCustomer(salesUser, customer);

        Assert.True(canAccess);
    }

    [Fact]
    public void SalesExecutive_Denied_Access_To_Other_User_Customer()
    {
        var authService = new ResourceAuthorizationService();
        var salesUser = CreatePrincipal("sales-1", AppRoles.SalesExecutive);
        var anotherRepCustomer = new Customer { CustomerId = 2, OwnerId = "sales-2" };

        // Attempting to access another user's customer by route ID / query string
        var canAccess = authService.CanAccessCustomer(salesUser, anotherRepCustomer);

        Assert.False(canAccess);
    }

    [Fact]
    public void SalesExecutive_Denied_Access_To_Other_User_Lead()
    {
        var authService = new ResourceAuthorizationService();
        var salesUser = CreatePrincipal("sales-1", AppRoles.SalesExecutive, "sales1@test.com");
        var otherLead = new Lead { LeadId = 5, AssignedTo = "sales-2", CreatedBy = "sales2@test.com" };

        var canAccess = authService.CanAccessLead(salesUser, otherLead);

        Assert.False(canAccess);
    }

    [Fact]
    public void SalesExecutive_Can_Access_Assigned_Or_Created_Lead()
    {
        var authService = new ResourceAuthorizationService();
        var salesUser = CreatePrincipal("sales-1", AppRoles.SalesExecutive, "sales1@test.com");
        var assignedLead = new Lead { LeadId = 1, AssignedTo = "sales-1", CreatedBy = "admin@test.com" };
        var createdLead = new Lead { LeadId = 2, AssignedTo = "unassigned", CreatedBy = "sales1@test.com" };

        Assert.True(authService.CanAccessLead(salesUser, assignedLead));
        Assert.True(authService.CanAccessLead(salesUser, createdLead));
    }

    [Fact]
    public void SalesExecutive_Denied_Access_To_Other_User_Opportunity()
    {
        var authService = new ResourceAuthorizationService();
        var salesUser = CreatePrincipal("sales-1", AppRoles.SalesExecutive);
        var otherOpp = new Opportunity { OpportunityId = 10, OwnerId = "sales-2" };

        var canAccess = authService.CanAccessOpportunity(salesUser, otherOpp);

        Assert.False(canAccess);
    }

    [Fact]
    public void SalesExecutive_Denied_Access_To_Other_User_FollowUp()
    {
        var authService = new ResourceAuthorizationService();
        var salesUser = CreatePrincipal("sales-1", AppRoles.SalesExecutive);
        var otherFollowUp = new FollowUp { FollowUpId = 20, AssignedTo = "sales-2" };

        var canAccess = authService.CanAccessFollowUp(salesUser, otherFollowUp);

        Assert.False(canAccess);
    }

    [Fact]
    public void Null_Entity_Access_Is_Denied()
    {
        var authService = new ResourceAuthorizationService();
        var admin = CreatePrincipal("admin-id", AppRoles.Admin);

        Assert.False(authService.CanAccessCustomer(admin, null));
        Assert.False(authService.CanAccessLead(admin, null));
        Assert.False(authService.CanAccessOpportunity(admin, null));
        Assert.False(authService.CanAccessFollowUp(admin, null));
        Assert.False(authService.CanAccessActivity(admin, null));
    }

    [Fact]
    public void ScopeCustomers_Filters_Query_Strictly_For_SalesExecutive()
    {
        var authService = new ResourceAuthorizationService();
        var salesUser = CreatePrincipal("sales-1", AppRoles.SalesExecutive);

        var data = new List<Customer>
        {
            new() { CustomerId = 1, CustomerName = "C1", OwnerId = "sales-1" },
            new() { CustomerId = 2, CustomerName = "C2", OwnerId = "sales-2" },
            new() { CustomerId = 3, CustomerName = "C3", OwnerId = "sales-1" },
            new() { CustomerId = 4, CustomerName = "C4", OwnerId = "admin-1" },
        }.AsQueryable();

        var scoped = authService.ScopeCustomers(data, salesUser).ToList();

        Assert.Equal(2, scoped.Count);
        Assert.All(scoped, c => Assert.Equal("sales-1", c.OwnerId));
    }

    [Fact]
    public void ScopeCustomers_Allows_All_For_Admin_And_Manager()
    {
        var authService = new ResourceAuthorizationService();
        var admin = CreatePrincipal("admin-1", AppRoles.Admin);
        var manager = CreatePrincipal("mgr-1", AppRoles.Manager);

        var data = new List<Customer>
        {
            new() { CustomerId = 1, CustomerName = "C1", OwnerId = "sales-1" },
            new() { CustomerId = 2, CustomerName = "C2", OwnerId = "sales-2" },
            new() { CustomerId = 3, CustomerName = "C3", OwnerId = "admin-1" },
        }.AsQueryable();

        var adminScoped = authService.ScopeCustomers(data, admin).ToList();
        var managerScoped = authService.ScopeCustomers(data, manager).ToList();

        Assert.Equal(3, adminScoped.Count);
        Assert.Equal(3, managerScoped.Count);
    }
}
