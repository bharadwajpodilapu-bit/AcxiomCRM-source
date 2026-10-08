using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.DTOs.FollowUp;

public class FollowUpDto
{
    public int FollowUpId { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? LeadId { get; set; }
    public string? LeadName { get; set; }
    public int? OpportunityId { get; set; }
    public DateTime FollowUpDate { get; set; }
    public string FollowUpType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string? AssignedUserName { get; set; }
    public bool IsOverdue { get; set; }
}

public class CreateFollowUpDto
{
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public int? OpportunityId { get; set; }

    [Required]
    public DateTime FollowUpDate { get; set; }

    [Required]
    public string FollowUpType { get; set; } = "Call";

    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    public string? Remarks { get; set; }

    public string? AssignedTo { get; set; }
}
