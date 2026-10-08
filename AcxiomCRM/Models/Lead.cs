using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class Lead
{
    [Key]
    public int LeadId { get; set; }

    [Required]
    [MaxLength(50)]
    public string LeadCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string LeadName { get; set; } = string.Empty;

    [MaxLength(256)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(150)]
    public string? CompanyName { get; set; }

    [Required]
    [MaxLength(50)]
    public string Source { get; set; } = "Website"; // Website, Referral, Cold Call, Campaign, Other

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "New"; // New, Contacted, Qualified, Unqualified, Converted, Lost

    [Required]
    [MaxLength(50)]
    public string Priority { get; set; } = "Medium"; // Low, Medium, High

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ExpectedValue { get; set; }

    public string? AssignedTo { get; set; }

    [ForeignKey(nameof(AssignedTo))]
    public virtual ApplicationUser? AssignedUser { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? ModifiedDate { get; set; }

    [MaxLength(100)]
    public string? ModifiedBy { get; set; }

    public int? ConvertedCustomerId { get; set; }

    [ForeignKey(nameof(ConvertedCustomerId))]
    public virtual Customer? ConvertedCustomer { get; set; }

    public int? ConvertedOpportunityId { get; set; }

    [ForeignKey(nameof(ConvertedOpportunityId))]
    public virtual Opportunity? ConvertedOpportunity { get; set; }

    // Navigation properties
    public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
