using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.DTOs.Lead;

public class LeadDto
{
    public int LeadId { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal? ExpectedValue { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedUserName { get; set; }
    public DateTime CreatedDate { get; set; }
    public int? ConvertedCustomerId { get; set; }
    public int? ConvertedOpportunityId { get; set; }
}

public class CreateLeadDto
{
    [Required(ErrorMessage = "Lead name is required.")]
    [MaxLength(150)]
    public string LeadName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? CompanyName { get; set; }

    public string Source { get; set; } = "Website";

    public string Priority { get; set; } = "Medium";

    public decimal? ExpectedValue { get; set; }

    public string? AssignedTo { get; set; }
}

public class UpdateLeadDto
{
    [Required(ErrorMessage = "Lead name is required.")]
    [MaxLength(150)]
    public string LeadName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? CompanyName { get; set; }

    public string Source { get; set; } = "Website";

    public string Status { get; set; } = "New";

    public string Priority { get; set; } = "Medium";

    public decimal? ExpectedValue { get; set; }

    public string? AssignedTo { get; set; }
}
