using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.Services.Implementations;
using AcxiomCRM.Validators;

namespace AcxiomCRM.Tests;

public class WorkflowAndSecurityTests
{
    private static ClaimsPrincipal CreateAdminPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "admin-1"),
            new(ClaimTypes.Name, "admin@acxiom.com"),
            new(ClaimTypes.Role, AppRoles.Admin)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task AuditService_Sanitizes_Passwords_Tokens_And_Secrets()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(AuditService_Sanitizes_Passwords_Tokens_And_Secrets));
        var auditService = new AuditService(db, NullLogger<AuditService>.Instance);

        await auditService.LogAsync(
            userId: "user-1",
            action: "PasswordReset",
            module: "Auth",
            entityName: "User",
            recordId: "user-1",
            oldValue: "{\"password\":\"Secret123!\",\"apiKey\":\"xyz\"}",
            newValue: "{\"password\":\"NewSecret456!\",\"token\":\"abc12345\"}",
            result: "Success",
            details: "User reset their password with reset token");

        var log = db.AuditLogs.First();

        Assert.Equal("[REDACTED]", log.OldValue);
        Assert.Equal("[REDACTED]", log.NewValue);
    }

    [Fact]
    public async Task Lead_Conversion_Creates_Customer_Opportunity_And_Marks_Lead_Converted()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(Lead_Conversion_Creates_Customer_Opportunity_And_Marks_Lead_Converted));
        var user = new ApplicationUser { Id = "admin-1", UserName = "admin@acxiom.com", Email = "admin@acxiom.com", IsActive = true };
        db.Users.Add(user);

        var lead = new Lead
        {
            LeadCode = "LEAD-001",
            LeadName = "Jane Prospect",
            Email = "jane@techcorp.com",
            Phone = "+1-555-019-9988",
            CompanyName = "TechCorp Inc.",
            Status = "Qualified",
            Priority = "High",
            ExpectedValue = 75000m,
            AssignedTo = user.Id,
            CreatedDate = DateTime.UtcNow
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        var authService = new ResourceAuthorizationService();
        var validator = new CrmBusinessValidator(db);
        var auditService = new AuditService(db, NullLogger<AuditService>.Instance);
        var leadService = new LeadService(db, validator, authService, auditService, NullLogger<LeadService>.Instance);

        var adminUser = CreateAdminPrincipal();
        var convertResult = await leadService.ConvertLeadAsync(
            leadId: lead.LeadId,
            createOpportunity: true,
            opportunityAmount: 75000m,
            expectedCloseDate: DateTime.UtcNow.AddDays(45),
            user: adminUser);

        Assert.True(convertResult.Success);
        Assert.NotNull(convertResult.Data);

        var createdCustomer = convertResult.Data;
        Assert.Equal("Jane Prospect", createdCustomer.CustomerName);
        Assert.Equal("jane@techcorp.com", createdCustomer.Email);

        // Verify Lead state is updated
        var updatedLead = await db.Leads.FindAsync(lead.LeadId);
        Assert.NotNull(updatedLead);
        Assert.Equal("Converted", updatedLead.Status);
        Assert.Equal(createdCustomer.CustomerId, updatedLead.ConvertedCustomerId);
        Assert.NotNull(updatedLead.ConvertedOpportunityId);

        // Verify Opportunity was created
        var opp = await db.Opportunities.FindAsync(updatedLead.ConvertedOpportunityId);
        Assert.NotNull(opp);
        Assert.Equal(75000m, opp.Amount);
        Assert.Equal("Open", opp.Status);
        Assert.Equal("Qualification", opp.Stage);
    }

    [Fact]
    public async Task Converted_Lead_Cannot_Be_Converted_Again()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(Converted_Lead_Cannot_Be_Converted_Again));
        var lead = new Lead
        {
            LeadCode = "LEAD-002",
            LeadName = "Converted Lead",
            Email = "converted@test.com",
            Status = "Converted",
            AssignedTo = "admin-1"
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        var authService = new ResourceAuthorizationService();
        var validator = new CrmBusinessValidator(db);
        var auditService = new AuditService(db, NullLogger<AuditService>.Instance);
        var leadService = new LeadService(db, validator, authService, auditService, NullLogger<LeadService>.Instance);

        var adminUser = CreateAdminPrincipal();
        var result = await leadService.ConvertLeadAsync(lead.LeadId, false, null, null, adminUser);

        Assert.False(result.Success);
        Assert.Contains("already been converted", result.Message);
    }

    [Fact]
    public async Task Converted_Lead_Cannot_Be_Deleted()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(Converted_Lead_Cannot_Be_Deleted));
        var lead = new Lead
        {
            LeadCode = "LEAD-003",
            LeadName = "Converted History Lead",
            Status = "Converted",
            AssignedTo = "admin-1"
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        var authService = new ResourceAuthorizationService();
        var validator = new CrmBusinessValidator(db);
        var auditService = new AuditService(db, NullLogger<AuditService>.Instance);
        var leadService = new LeadService(db, validator, authService, auditService, NullLogger<LeadService>.Instance);

        var adminUser = CreateAdminPrincipal();
        var deleteResult = await leadService.DeleteLeadAsync(lead.LeadId, adminUser);

        Assert.False(deleteResult.Success);
        Assert.Contains("Cannot delete a converted lead", deleteResult.Message);
    }

    [Fact]
    public async Task Opportunity_Stage_Change_Syncs_Status_And_Probability()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(Opportunity_Stage_Change_Syncs_Status_And_Probability));
        var user = new ApplicationUser { Id = "admin-1", UserName = "admin@acxiom.com", Email = "admin@acxiom.com", IsActive = true };
        db.Users.Add(user);

        var customer = new Customer
        {
            CustomerCode = "CUST-0099",
            CustomerName = "Target Corp",
            Email = "target@corp.com",
            Phone = "+1-555-010-9999",
            OwnerId = user.Id,
            IsActive = true
        };
        db.Customers.Add(customer);

        var opp = new Opportunity
        {
            OpportunityName = "ERP Integration",
            CustomerId = customer.CustomerId,
            Amount = 50000m,
            Stage = "Proposal",
            Probability = 50,
            Status = "Open",
            OwnerId = user.Id,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
        };
        db.Opportunities.Add(opp);
        await db.SaveChangesAsync();

        var authService = new ResourceAuthorizationService();
        var validator = new CrmBusinessValidator(db);
        var auditService = new AuditService(db, NullLogger<AuditService>.Instance);
        var oppService = new OpportunityService(db, validator, authService, auditService, NullLogger<OpportunityService>.Instance);

        var adminUser = CreateAdminPrincipal();

        // 1. Change to Won -> Status must be Won, Probability 100
        var wonResult = await oppService.ChangeStageAsync(opp.OpportunityId, "Won", adminUser);
        Assert.True(wonResult.Success);
        Assert.Equal("Won", wonResult.Data!.Status);
        Assert.Equal(100, wonResult.Data.Probability);

        // 2. Change to Lost -> Status must be Lost, Probability 0
        var lostResult = await oppService.ChangeStageAsync(opp.OpportunityId, "Lost", adminUser);
        Assert.True(lostResult.Success);
        Assert.Equal("Lost", lostResult.Data!.Status);
        Assert.Equal(0, lostResult.Data.Probability);
    }

    [Fact]
    public async Task FollowUp_Reschedule_Enforces_Future_Date()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(FollowUp_Reschedule_Enforces_Future_Date));
        var followUp = new FollowUp
        {
            Subject = "Demo Call",
            FollowUpType = "Call",
            FollowUpDate = DateTime.UtcNow.AddDays(1),
            Status = "Planned",
            AssignedTo = "admin-1"
        };
        db.FollowUps.Add(followUp);
        await db.SaveChangesAsync();

        var authService = new ResourceAuthorizationService();
        var validator = new CrmBusinessValidator(db);
        var auditService = new AuditService(db, NullLogger<AuditService>.Instance);
        var followUpService = new FollowUpService(db, validator, authService, auditService, NullLogger<FollowUpService>.Instance);

        var adminUser = CreateAdminPrincipal();

        // Try to reschedule to the past
        var pastResult = await followUpService.RescheduleAsync(
            followUp.FollowUpId,
            DateTime.UtcNow.AddDays(-2),
            "Rescheduled",
            adminUser);

        Assert.False(pastResult.Success);
        Assert.Contains("cannot be earlier than today", pastResult.Message);

        // Reschedule to valid future date
        var futureDate = DateTime.UtcNow.AddDays(7);
        var futureResult = await followUpService.RescheduleAsync(
            followUp.FollowUpId,
            futureDate,
            "Client requested next week",
            adminUser);

        Assert.True(futureResult.Success);
        Assert.Equal(futureDate, futureResult.Data!.FollowUpDate);
        Assert.Contains("Client requested next week", futureResult.Data.Remarks);
    }
}
