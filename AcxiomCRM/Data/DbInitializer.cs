using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        // Ensure database exists
        await context.Database.EnsureCreatedAsync();

        // 1. Seed Roles
        foreach (var roleName in AppRoles.AllRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // 2. Seed Users — only when a valid default password is configured (Development-only)
        var defaultPassword = configuration["SeedData:DefaultPassword"];
        if (string.IsNullOrWhiteSpace(defaultPassword))
        {
            // No seed password configured — skip user/data seeding (production mode).
            return;
        }

        var adminUser = await EnsureUserAsync(
            userManager,
            "admin@acxiomcrm.com",
            "Admin User",
            defaultPassword,
            AppRoles.Admin);

        var managerUser = await EnsureUserAsync(
            userManager,
            "manager@acxiomcrm.com",
            "Sarah Manager",
            defaultPassword,
            AppRoles.Manager);

        var salesUser = await EnsureUserAsync(
            userManager,
            "sales@acxiomcrm.com",
            "Alex Sales",
            defaultPassword,
            AppRoles.SalesExecutive);

        // 3. Seed Customers
        if (!await context.Customers.AnyAsync())
        {
            var customers = new List<Customer>
            {
                new()
                {
                    CustomerCode = "CUST-2026-0001",
                    CustomerName = "Apex Global Technologies",
                    Email = "contact@apextech.io",
                    Phone = "+1-555-019-2831",
                    CompanyName = "Apex Global Technologies Inc.",
                    Address = "100 Innovation Way, Suite 400",
                    City = "San Jose",
                    State = "CA",
                    Status = "Active",
                    OwnerId = salesUser.Id,
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddMonths(-3),
                    IsActive = true
                },
                new()
                {
                    CustomerCode = "CUST-2026-0002",
                    CustomerName = "BlueHorizon Logistics",
                    Email = "ops@bluehorizon.net",
                    Phone = "+1-555-018-7744",
                    CompanyName = "BlueHorizon Logistics Corp",
                    Address = "500 Harbor Blvd",
                    City = "Seattle",
                    State = "WA",
                    Status = "Active",
                    OwnerId = managerUser.Id,
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddMonths(-2),
                    IsActive = true
                },
                new()
                {
                    CustomerCode = "CUST-2026-0003",
                    CustomerName = "Nexus Financial Partners",
                    Email = "info@nexusfin.com",
                    Phone = "+1-555-014-9922",
                    CompanyName = "Nexus Financial Partners LLC",
                    Address = "25 Wall Street",
                    City = "New York",
                    State = "NY",
                    Status = "Active",
                    OwnerId = salesUser.Id,
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddMonths(-1),
                    IsActive = true
                },
                new()
                {
                    CustomerCode = "CUST-2026-0004",
                    CustomerName = "Vanguard Medical Systems",
                    Email = "procurement@vanguardmed.org",
                    Phone = "+1-555-013-4411",
                    CompanyName = "Vanguard Medical Systems",
                    Address = "742 Evergreen Terrace",
                    City = "Boston",
                    State = "MA",
                    Status = "Inactive",
                    OwnerId = adminUser.Id,
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddMonths(-4),
                    IsActive = false
                }
            };

            await context.Customers.AddRangeAsync(customers);
            await context.SaveChangesAsync();
        }

        var apexCust = await context.Customers.FirstAsync(c => c.CustomerCode == "CUST-2026-0001");
        var blueCust = await context.Customers.FirstAsync(c => c.CustomerCode == "CUST-2026-0002");
        var nexusCust = await context.Customers.FirstAsync(c => c.CustomerCode == "CUST-2026-0003");

        // 4. Seed Leads
        if (!await context.Leads.AnyAsync())
        {
            var leads = new List<Lead>
            {
                new()
                {
                    LeadCode = "LEAD-2026-0001",
                    LeadName = "David Miller",
                    Email = "david.miller@zenithenergy.com",
                    Phone = "+1-555-011-3322",
                    CompanyName = "Zenith Energy",
                    Source = "Website",
                    Status = "New",
                    Priority = "High",
                    ExpectedValue = 45000m,
                    AssignedTo = salesUser.Id,
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-10)
                },
                new()
                {
                    LeadCode = "LEAD-2026-0002",
                    LeadName = "Emily Watson",
                    Email = "e.watson@cloudscale.co",
                    Phone = "+1-555-012-6655",
                    CompanyName = "CloudScale Networks",
                    Source = "Referral",
                    Status = "Contacted",
                    Priority = "Medium",
                    ExpectedValue = 28000m,
                    AssignedTo = salesUser.Id,
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-20)
                },
                new()
                {
                    LeadCode = "LEAD-2026-0003",
                    LeadName = "Marcus Brody",
                    Email = "brody@solarsphere.org",
                    Phone = "+1-555-015-8833",
                    CompanyName = "SolarSphere Solutions",
                    Source = "Cold Call",
                    Status = "Qualified",
                    Priority = "High",
                    ExpectedValue = 95000m,
                    AssignedTo = managerUser.Id,
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-30)
                },
                new()
                {
                    LeadCode = "LEAD-2026-0004",
                    LeadName = "Laura Croft",
                    Email = "laura@relicmining.com",
                    Phone = "+1-555-019-1122",
                    CompanyName = "Relic Mining Corp",
                    Source = "Campaign",
                    Status = "Unqualified",
                    Priority = "Low",
                    ExpectedValue = 12000m,
                    AssignedTo = salesUser.Id,
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-40)
                },
                new()
                {
                    LeadCode = "LEAD-2026-0005",
                    LeadName = "Apex Lead (Historic)",
                    Email = "contact@apextech.io",
                    Phone = "+1-555-019-2831",
                    CompanyName = "Apex Global Technologies Inc.",
                    Source = "Website",
                    Status = "Converted",
                    Priority = "High",
                    ExpectedValue = 85000m,
                    AssignedTo = salesUser.Id,
                    ConvertedCustomerId = apexCust.CustomerId,
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddMonths(-3)
                },
                new()
                {
                    LeadCode = "LEAD-2026-0006",
                    LeadName = "Thomas Sterling",
                    Email = "thomas@sterlingmetals.net",
                    Phone = "+1-555-017-7722",
                    CompanyName = "Sterling Metals",
                    Source = "Other",
                    Status = "Lost",
                    Priority = "Medium",
                    ExpectedValue = 30000m,
                    AssignedTo = salesUser.Id,
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddMonths(-2)
                }
            };

            await context.Leads.AddRangeAsync(leads);
            await context.SaveChangesAsync();
        }

        // 5. Seed Opportunities in all stages
        if (!await context.Opportunities.AnyAsync())
        {
            var opportunities = new List<Opportunity>
            {
                new()
                {
                    OpportunityName = "Apex Cloud Migration Enterprise",
                    CustomerId = apexCust.CustomerId,
                    Amount = 85000m,
                    Stage = "Won",
                    Probability = 100,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(-15),
                    Status = "Won",
                    OwnerId = salesUser.Id,
                    Notes = "Signed 3-year subscription enterprise agreement.",
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddMonths(-2)
                },
                new()
                {
                    OpportunityName = "BlueHorizon Fleet Tracking Integration",
                    CustomerId = blueCust.CustomerId,
                    Amount = 120000m,
                    Stage = "Negotiation",
                    Probability = 75,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(25),
                    Status = "Open",
                    OwnerId = managerUser.Id,
                    Notes = "Contract revision under legal review.",
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-45)
                },
                new()
                {
                    OpportunityName = "Nexus Financial Security Gateway",
                    CustomerId = nexusCust.CustomerId,
                    Amount = 65000m,
                    Stage = "Proposal",
                    Probability = 50,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(40),
                    Status = "Open",
                    OwnerId = salesUser.Id,
                    Notes = "RFP presentation scheduled for next week.",
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-20)
                },
                new()
                {
                    OpportunityName = "Apex AI Analytics Module Addon",
                    CustomerId = apexCust.CustomerId,
                    Amount = 40000m,
                    Stage = "Qualification",
                    Probability = 25,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(60),
                    Status = "Open",
                    OwnerId = salesUser.Id,
                    Notes = "Initial interest during quarterly review.",
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-5)
                },
                new()
                {
                    OpportunityName = "Nexus Old Legacy Upgrade",
                    CustomerId = nexusCust.CustomerId,
                    Amount = 25000m,
                    Stage = "Lost",
                    Probability = 0,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(-30),
                    Status = "Lost",
                    OwnerId = salesUser.Id,
                    Notes = "Competitor selected due to internal budget constraints.",
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddMonths(-3)
                }
            };

            await context.Opportunities.AddRangeAsync(opportunities);
            await context.SaveChangesAsync();
        }

        var apexOpp = await context.Opportunities.FirstAsync(o => o.OpportunityName == "Apex Cloud Migration Enterprise");
        var lead1 = await context.Leads.FirstAsync(l => l.LeadCode == "LEAD-2026-0001");

        // 6. Seed FollowUps (Planned, Completed, Overdue)
        if (!await context.FollowUps.AnyAsync())
        {
            var followUps = new List<FollowUp>
            {
                new()
                {
                    CustomerId = apexCust.CustomerId,
                    Subject = "Quarterly Business Review Meeting",
                    FollowUpType = "Meeting",
                    FollowUpDate = DateTime.UtcNow.AddDays(3),
                    Status = "Planned",
                    AssignedTo = salesUser.Id,
                    Remarks = "Review roadmap and expansion scope.",
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-2)
                },
                new()
                {
                    LeadId = lead1.LeadId,
                    Subject = "Introductory Demo Call with David Miller",
                    FollowUpType = "Call",
                    FollowUpDate = DateTime.UtcNow.AddDays(-2), // Overdue planned follow-up
                    Status = "Planned",
                    AssignedTo = salesUser.Id,
                    Remarks = "Needs architecture overview before decision.",
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-5)
                },
                new()
                {
                    CustomerId = blueCust.CustomerId,
                    Subject = "Deliver revised SLA contract",
                    FollowUpType = "Email",
                    FollowUpDate = DateTime.UtcNow.AddDays(-10),
                    Status = "Completed",
                    AssignedTo = managerUser.Id,
                    Remarks = "Sent updated terms agreed during negotiation.",
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-12)
                }
            };

            await context.FollowUps.AddRangeAsync(followUps);
            await context.SaveChangesAsync();
        }

        // 7. Seed Activities
        if (!await context.Activities.AnyAsync())
        {
            var activities = new List<Activity>
            {
                new()
                {
                    ActivityType = "Call",
                    Subject = "Discovery call with Apex CTO",
                    Description = "Discussed current cloud limitations and performance bottlenecks.",
                    ActivityDate = DateTime.UtcNow.AddDays(-25),
                    CustomerId = apexCust.CustomerId,
                    AssignedTo = salesUser.Id,
                    Status = "Completed",
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-25)
                },
                new()
                {
                    ActivityType = "Meeting",
                    Subject = "Technical Architecture Workshop",
                    Description = "Presented proposed microservice architecture and security safeguards.",
                    ActivityDate = DateTime.UtcNow.AddDays(-18),
                    OpportunityId = apexOpp.OpportunityId,
                    AssignedTo = salesUser.Id,
                    Status = "Completed",
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-18)
                },
                new()
                {
                    ActivityType = "Email",
                    Subject = "Pricing proposal sent to BlueHorizon",
                    Description = "Dispatched tier 1 commercial quote.",
                    ActivityDate = DateTime.UtcNow.AddDays(-14),
                    CustomerId = blueCust.CustomerId,
                    AssignedTo = managerUser.Id,
                    Status = "Completed",
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-14)
                }
            };

            await context.Activities.AddRangeAsync(activities);
            await context.SaveChangesAsync();
        }

        // 8. Seed Audit Logs
        if (!await context.AuditLogs.AnyAsync())
        {
            var auditLogs = new List<AuditLog>
            {
                new()
                {
                    UserId = adminUser.Id,
                    Action = "SystemInit",
                    Module = "System",
                    EntityName = "Database",
                    Result = "Success",
                    Details = "Initial database seed completed with roles and demo accounts.",
                    CreatedDate = DateTime.UtcNow.AddDays(-30)
                },
                new()
                {
                    UserId = salesUser.Id,
                    Action = "Login",
                    Module = "Auth",
                    EntityName = "User",
                    RecordId = salesUser.Id,
                    Result = "Success",
                    Details = $"User {salesUser.Email} logged in successfully.",
                    CreatedDate = DateTime.UtcNow.AddDays(-1)
                }
            };

            await context.AuditLogs.AddRangeAsync(auditLogs);
            await context.SaveChangesAsync();
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string password,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                IsActive = true,
                EmailConfirmed = true,
                CreatedDate = DateTime.UtcNow
            };

            var createRes = await userManager.CreateAsync(user, password);
            if (!createRes.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed user {email}: {string.Join(", ", createRes.Errors.Select(e => e.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }
}
