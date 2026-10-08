using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Validators;

public class CrmBusinessValidator : ICrmBusinessValidator
{
    private readonly ApplicationDbContext _db;
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PhoneRegex = new(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$|^[0-9]{10}$", RegexOptions.Compiled);

    public CrmBusinessValidator(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<BusinessRuleValidationResult> ValidateCustomerAsync(Customer customer, int? existingCustomerId = null)
    {
        var result = new BusinessRuleValidationResult();

        if (string.IsNullOrWhiteSpace(customer.CustomerName))
        {
            result.AddError(nameof(customer.CustomerName), "Customer Name is required.");
        }
        else if (customer.CustomerName.Trim().Length > 150)
        {
            result.AddError(nameof(customer.CustomerName), "Customer Name cannot exceed 150 characters.");
        }

        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            result.AddError(nameof(customer.Email), "Email is required.");
        }
        else
        {
            var cleanEmail = customer.Email.Trim().ToLowerInvariant();
            if (!EmailRegex.IsMatch(cleanEmail))
            {
                result.AddError(nameof(customer.Email), "Enter a valid email address.");
            }
            else
            {
                var emailExists = await _db.Customers
                    .AnyAsync(c => c.CustomerId != existingCustomerId && c.Email.ToLower() == cleanEmail);
                if (emailExists)
                {
                    result.AddError(nameof(customer.Email), "A customer with this email already exists.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(customer.Phone))
        {
            result.AddError(nameof(customer.Phone), "Phone number is required.");
        }
        else
        {
            var cleanPhone = customer.Phone.Trim();
            if (!PhoneRegex.IsMatch(cleanPhone) && cleanPhone.Length < 7)
            {
                result.AddError(nameof(customer.Phone), "Enter a valid phone number.");
            }
            else
            {
                var phoneExists = await _db.Customers
                    .AnyAsync(c => c.CustomerId != existingCustomerId && c.Phone == cleanPhone);
                if (phoneExists)
                {
                    result.AddError(nameof(customer.Phone), "A customer with this phone number already exists.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(customer.OwnerId))
        {
            result.AddError(nameof(customer.OwnerId), "Owner is required.");
        }
        else
        {
            var ownerExists = await _db.Users.AnyAsync(u => u.Id == customer.OwnerId && u.IsActive);
            if (!ownerExists)
            {
                result.AddError(nameof(customer.OwnerId), "Assigned owner must be an active system user.");
            }
        }

        return result;
    }

    public async Task<BusinessRuleValidationResult> ValidateLeadAsync(Lead lead, int? existingLeadId = null)
    {
        var result = new BusinessRuleValidationResult();

        if (string.IsNullOrWhiteSpace(lead.LeadName))
        {
            result.AddError(nameof(lead.LeadName), "Lead name is required.");
        }

        if (!string.IsNullOrWhiteSpace(lead.Email) && !EmailRegex.IsMatch(lead.Email.Trim().ToLowerInvariant()))
        {
            result.AddError(nameof(lead.Email), "Enter a valid email address.");
        }

        if (!string.IsNullOrWhiteSpace(lead.Phone))
        {
            var cleanPhone = lead.Phone.Trim();
            if (!PhoneRegex.IsMatch(cleanPhone) && cleanPhone.Length < 7)
            {
                result.AddError(nameof(lead.Phone), "Enter a valid phone number.");
            }
        }

        if (lead.ExpectedValue.HasValue && lead.ExpectedValue.Value < 0)
        {
            result.AddError(nameof(lead.ExpectedValue), "Expected value cannot be negative.");
        }

        var allowedStatuses = new[] { "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost" };
        if (!allowedStatuses.Contains(lead.Status))
        {
            result.AddError(nameof(lead.Status), $"Status must be one of: {string.Join(", ", allowedStatuses)}.");
        }

        if (!string.IsNullOrWhiteSpace(lead.AssignedTo))
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == lead.AssignedTo && u.IsActive);
            if (!userExists)
            {
                result.AddError(nameof(lead.AssignedTo), "Assigned user must be an active system user.");
            }
        }

        return result;
    }

    public BusinessRuleValidationResult ValidateLeadStatusTransition(string currentStatus, string newStatus)
    {
        var result = new BusinessRuleValidationResult();
        if (currentStatus == newStatus) return result;

        if (currentStatus == "Converted" || currentStatus == "Lost")
        {
            result.AddError("Status", $"Cannot change status of a {currentStatus} lead.");
            return result;
        }

        var valid = currentStatus switch
        {
            "New" => newStatus == "Contacted",
            "Contacted" => newStatus == "Qualified" || newStatus == "Unqualified",
            "Qualified" => newStatus == "Converted" || newStatus == "Lost",
            "Unqualified" => newStatus == "Contacted" || newStatus == "Lost",
            _ => false
        };

        if (!valid)
        {
            result.AddError("Status", $"Invalid status transition from '{currentStatus}' to '{newStatus}'.");
        }

        return result;
    }

    public async Task<BusinessRuleValidationResult> ValidateOpportunityAsync(Opportunity opportunity, int? existingOpportunityId = null)
    {
        var result = new BusinessRuleValidationResult();

        if (string.IsNullOrWhiteSpace(opportunity.OpportunityName))
        {
            result.AddError(nameof(opportunity.OpportunityName), "Opportunity name is required.");
        }

        if (opportunity.Amount < 0)
        {
            result.AddError(nameof(opportunity.Amount), "Opportunity Amount cannot be negative.");
        }

        bool isActive = opportunity.Status != "Lost" && opportunity.Stage != "Lost";
        if (isActive && opportunity.Amount <= 0)
        {
            result.AddError(nameof(opportunity.Amount), "Opportunity Amount must be greater than 0.");
        }

        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            result.AddError(nameof(opportunity.Probability), "Probability must be between 0 and 100.");
        }

        if (isActive && opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
        {
            result.AddError(nameof(opportunity.ExpectedCloseDate), "Expected Close Date cannot be in the past.");
        }

        var customerExists = await _db.Customers.AnyAsync(c => c.CustomerId == opportunity.CustomerId && c.IsActive);
        if (!customerExists)
        {
            result.AddError(nameof(opportunity.CustomerId), "Customer must exist and be active.");
        }

        if (string.IsNullOrWhiteSpace(opportunity.OwnerId))
        {
            result.AddError(nameof(opportunity.OwnerId), "Owner is required.");
        }
        else
        {
            var ownerExists = await _db.Users.AnyAsync(u => u.Id == opportunity.OwnerId && u.IsActive);
            if (!ownerExists)
            {
                result.AddError(nameof(opportunity.OwnerId), "Owner must be an active system user.");
            }
        }

        var validStages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
        if (!validStages.Contains(opportunity.Stage))
        {
            result.AddError(nameof(opportunity.Stage), "Invalid opportunity stage.");
        }

        return result;
    }

    public async Task<BusinessRuleValidationResult> ValidateFollowUpAsync(FollowUp followUp, bool isNew = false)
    {
        var result = new BusinessRuleValidationResult();

        if (string.IsNullOrWhiteSpace(followUp.Subject))
        {
            result.AddError(nameof(followUp.Subject), "Subject is required.");
        }

        if (followUp.CustomerId == null && followUp.LeadId == null && followUp.OpportunityId == null)
        {
            result.AddError("Relations", "At least one related CRM record (Customer, Lead, or Opportunity) must be supplied.");
        }

        if (isNew || followUp.Status == "Planned")
        {
            if (followUp.FollowUpDate.Date < DateTime.UtcNow.Date)
            {
                result.AddError(nameof(followUp.FollowUpDate), "Follow-up date cannot be earlier than today.");
            }
        }

        if (string.IsNullOrWhiteSpace(followUp.AssignedTo))
        {
            result.AddError(nameof(followUp.AssignedTo), "Assigned user is required.");
        }
        else
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == followUp.AssignedTo && u.IsActive);
            if (!userExists)
            {
                result.AddError(nameof(followUp.AssignedTo), "Assigned user must be an active system user.");
            }
        }

        return result;
    }

    public async Task<BusinessRuleValidationResult> ValidateActivityAsync(Activity activity)
    {
        var result = new BusinessRuleValidationResult();

        if (string.IsNullOrWhiteSpace(activity.Subject))
        {
            result.AddError(nameof(activity.Subject), "Subject is required.");
        }

        if (activity.CustomerId == null && activity.LeadId == null && activity.OpportunityId == null)
        {
            result.AddError("Relations", "At least one related CRM record (Customer, Lead, or Opportunity) must be supplied.");
        }

        if (string.IsNullOrWhiteSpace(activity.AssignedTo))
        {
            result.AddError(nameof(activity.AssignedTo), "Assigned user is required.");
        }
        else
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == activity.AssignedTo && u.IsActive);
            if (!userExists)
            {
                result.AddError(nameof(activity.AssignedTo), "Assigned user must be an active system user.");
            }
        }

        return result;
    }
}
