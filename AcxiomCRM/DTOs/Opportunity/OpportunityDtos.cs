using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.DTOs.Opportunity;

public class OpportunityDto
{
    public int OpportunityId { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public decimal Amount { get; set; }
    public string Stage { get; set; } = string.Empty;
    public int Probability { get; set; }
    public decimal WeightedAmount => Amount * Probability / 100m;
    public DateTime ExpectedCloseDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateOpportunityDto
{
    [Required(ErrorMessage = "Opportunity name is required.")]
    [MaxLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    [Required]
    public int CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Opportunity Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required]
    public string Stage { get; set; } = "Qualification";

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 20;

    [Required]
    public DateTime ExpectedCloseDate { get; set; }

    public string? OwnerId { get; set; }
    public string? Notes { get; set; }
}

public class UpdateOpportunityDto
{
    [Required(ErrorMessage = "Opportunity name is required.")]
    [MaxLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    [Required]
    public int CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Opportunity Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required]
    public string Stage { get; set; } = "Qualification";

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 20;

    [Required]
    public DateTime ExpectedCloseDate { get; set; }

    public string? OwnerId { get; set; }
    public string? Notes { get; set; }
}
