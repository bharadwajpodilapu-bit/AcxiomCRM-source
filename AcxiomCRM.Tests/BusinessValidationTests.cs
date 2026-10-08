using Xunit;
using AcxiomCRM.Models;
using AcxiomCRM.Validators;

namespace AcxiomCRM.Tests;

public class BusinessValidationTests
{
    [Fact]
    public async Task Customer_Requires_Name_Email_Phone_And_Owner()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(Customer_Requires_Name_Email_Phone_And_Owner));
        var validator = new CrmBusinessValidator(db);

        var emptyCustomer = new Customer
        {
            CustomerName = "",
            Email = "",
            Phone = "",
            OwnerId = ""
        };

        var result = await validator.ValidateCustomerAsync(emptyCustomer);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey(nameof(Customer.CustomerName)));
        Assert.True(result.Errors.ContainsKey(nameof(Customer.Email)));
        Assert.True(result.Errors.ContainsKey(nameof(Customer.Phone)));
        Assert.True(result.Errors.ContainsKey(nameof(Customer.OwnerId)));
    }

    [Fact]
    public async Task Customer_Name_Cannot_Exceed_150_Characters()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(Customer_Name_Cannot_Exceed_150_Characters));
        var validator = new CrmBusinessValidator(db);

        var customer = new Customer
        {
            CustomerName = new string('A', 151),
            Email = "valid@acxiom.com",
            Phone = "+1-555-010-0000",
            OwnerId = "user1"
        };

        var result = await validator.ValidateCustomerAsync(customer);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors[nameof(Customer.CustomerName)], e => e.Contains("cannot exceed 150 characters"));
    }

    [Fact]
    public async Task Customer_Rejects_Invalid_Email_Format()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(Customer_Rejects_Invalid_Email_Format));
        var validator = new CrmBusinessValidator(db);

        var customer = new Customer
        {
            CustomerName = "Acme Corp",
            Email = "invalid-email-format",
            Phone = "+1-555-010-0000",
            OwnerId = "user1"
        };

        var result = await validator.ValidateCustomerAsync(customer);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors[nameof(Customer.Email)], e => e.Contains("Enter a valid email address"));
    }

    [Fact]
    public async Task Customer_Prevents_Duplicate_Email()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(Customer_Prevents_Duplicate_Email));
        var activeUser = new ApplicationUser { Id = "user-1", UserName = "u1@test.com", Email = "u1@test.com", IsActive = true };
        db.Users.Add(activeUser);

        db.Customers.Add(new Customer
        {
            CustomerCode = "CUST-0001",
            CustomerName = "First Client",
            Email = "duplicate@acxiom.com",
            Phone = "+1-555-011-1111",
            OwnerId = activeUser.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var validator = new CrmBusinessValidator(db);

        var duplicateCustomer = new Customer
        {
            CustomerName = "Second Client",
            Email = "DUPLICATE@ACXIOM.COM", // tests case-insensitivity
            Phone = "+1-555-012-2222",
            OwnerId = activeUser.Id
        };

        var result = await validator.ValidateCustomerAsync(duplicateCustomer);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors[nameof(Customer.Email)], e => e.Contains("already exists"));
    }

    [Fact]
    public async Task Customer_Prevents_Duplicate_Phone()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(Customer_Prevents_Duplicate_Phone));
        var activeUser = new ApplicationUser { Id = "user-1", UserName = "u1@test.com", Email = "u1@test.com", IsActive = true };
        db.Users.Add(activeUser);

        db.Customers.Add(new Customer
        {
            CustomerCode = "CUST-0001",
            CustomerName = "First Client",
            Email = "first@acxiom.com",
            Phone = "+1-555-011-9999",
            OwnerId = activeUser.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var validator = new CrmBusinessValidator(db);

        var duplicatePhoneCust = new Customer
        {
            CustomerName = "Second Client",
            Email = "second@acxiom.com",
            Phone = "+1-555-011-9999",
            OwnerId = activeUser.Id
        };

        var result = await validator.ValidateCustomerAsync(duplicatePhoneCust);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors[nameof(Customer.Phone)], e => e.Contains("already exists"));
    }

    [Fact]
    public async Task Lead_Requires_Name_And_Validates_Status()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(Lead_Requires_Name_And_Validates_Status));
        var validator = new CrmBusinessValidator(db);

        var lead = new Lead
        {
            LeadName = "",
            Status = "InvalidStatus",
            ExpectedValue = -500
        };

        var result = await validator.ValidateLeadAsync(lead);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey(nameof(Lead.LeadName)));
        Assert.True(result.Errors.ContainsKey(nameof(Lead.Status)));
        Assert.True(result.Errors.ContainsKey(nameof(Lead.ExpectedValue)));
    }

    [Theory]
    [InlineData("New", "Contacted", true)]
    [InlineData("Contacted", "Qualified", true)]
    [InlineData("Contacted", "Unqualified", true)]
    [InlineData("Qualified", "Converted", true)]
    [InlineData("Qualified", "Lost", true)]
    [InlineData("Unqualified", "Contacted", true)]
    [InlineData("Unqualified", "Lost", true)]
    [InlineData("New", "Converted", false)]     // Cannot skip straight from New to Converted
    [InlineData("New", "Qualified", false)]     // Cannot skip Contacted
    [InlineData("Converted", "New", false)]     // Terminal state
    [InlineData("Converted", "Contacted", false)]
    [InlineData("Lost", "New", false)]          // Terminal state
    [InlineData("Lost", "Qualified", false)]
    public void Lead_Status_Transition_Rules_Are_Enforced(string currentStatus, string newStatus, bool expectedValid)
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var validator = new CrmBusinessValidator(db);

        var result = validator.ValidateLeadStatusTransition(currentStatus, newStatus);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public async Task Opportunity_Validates_Amount_Probability_And_Future_Close_Date()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(Opportunity_Validates_Amount_Probability_And_Future_Close_Date));
        var validator = new CrmBusinessValidator(db);

        var opp = new Opportunity
        {
            OpportunityName = "Big Deal",
            Amount = 0, // Must be > 0
            Probability = 150, // Must be <= 100
            ExpectedCloseDate = DateTime.UtcNow.AddDays(-10), // In past
            Stage = "Qualification",
            Status = "Open",
            CustomerId = 999, // Customer does not exist
            OwnerId = "user1"
        };

        var result = await validator.ValidateOpportunityAsync(opp);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey(nameof(Opportunity.Amount)));
        Assert.True(result.Errors.ContainsKey(nameof(Opportunity.Probability)));
        Assert.True(result.Errors.ContainsKey(nameof(Opportunity.ExpectedCloseDate)));
        Assert.True(result.Errors.ContainsKey(nameof(Opportunity.CustomerId)));
    }

    [Fact]
    public async Task FollowUp_Requires_Parent_Relation_And_Future_Date_For_Planned()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(FollowUp_Requires_Parent_Relation_And_Future_Date_For_Planned));
        var validator = new CrmBusinessValidator(db);

        var followUp = new FollowUp
        {
            Subject = "Catch up call",
            FollowUpType = "Call",
            CustomerId = null,
            LeadId = null,
            OpportunityId = null,
            FollowUpDate = DateTime.UtcNow.AddDays(-5), // Past date
            Status = "Planned"
        };

        var result = await validator.ValidateFollowUpAsync(followUp, isNew: true);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey("Relations"));
        Assert.True(result.Errors.ContainsKey(nameof(FollowUp.FollowUpDate)));
    }

    [Fact]
    public async Task Activity_Requires_Subject_And_Parent_Relation()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(Activity_Requires_Subject_And_Parent_Relation));
        var validator = new CrmBusinessValidator(db);

        var activity = new Activity
        {
            Subject = "",
            ActivityType = "Email",
            CustomerId = null,
            LeadId = null,
            OpportunityId = null
        };

        var result = await validator.ValidateActivityAsync(activity);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey(nameof(Activity.Subject)));
        Assert.True(result.Errors.ContainsKey("Relations"));
    }
}
