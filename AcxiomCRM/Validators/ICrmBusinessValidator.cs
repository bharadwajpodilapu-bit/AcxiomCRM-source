using AcxiomCRM.Models;

namespace AcxiomCRM.Validators;

public interface ICrmBusinessValidator
{
    Task<BusinessRuleValidationResult> ValidateCustomerAsync(Customer customer, int? existingCustomerId = null);
    Task<BusinessRuleValidationResult> ValidateLeadAsync(Lead lead, int? existingLeadId = null);
    BusinessRuleValidationResult ValidateLeadStatusTransition(string currentStatus, string newStatus);
    Task<BusinessRuleValidationResult> ValidateOpportunityAsync(Opportunity opportunity, int? existingOpportunityId = null);
    Task<BusinessRuleValidationResult> ValidateFollowUpAsync(FollowUp followUp, bool isNew = false);
    Task<BusinessRuleValidationResult> ValidateActivityAsync(Activity activity);
}
