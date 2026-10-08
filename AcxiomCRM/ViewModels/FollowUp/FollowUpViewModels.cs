using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Common;

namespace AcxiomCRM.ViewModels.FollowUp;

public class FollowUpListViewModel
{
    public PagedResult<Models.FollowUp> FollowUps { get; set; } = new();
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? AssignedTo { get; set; }
    public bool? OverdueOnly { get; set; }
    public List<SelectListItem> UsersList { get; set; } = new();
}

public class FollowUpCreateEditViewModel
{
    public int FollowUpId { get; set; }

    [Display(Name = "Related Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Related Lead")]
    public int? LeadId { get; set; }

    [Display(Name = "Related Opportunity")]
    public int? OpportunityId { get; set; }

    [Required(ErrorMessage = "Follow-up date is required.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Follow-Up Date & Time")]
    public DateTime FollowUpDate { get; set; } = DateTime.UtcNow.AddDays(1);

    [Required]
    [Display(Name = "Follow-Up Type")]
    public string FollowUpType { get; set; } = "Call";

    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Remarks { get; set; }

    [Required]
    public string Status { get; set; } = "Planned";

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    public List<SelectListItem> CustomersList { get; set; } = new();
    public List<SelectListItem> LeadsList { get; set; } = new();
    public List<SelectListItem> OpportunitiesList { get; set; } = new();
    public List<SelectListItem> UsersList { get; set; } = new();
}

public class FollowUpRescheduleViewModel
{
    public int FollowUpId { get; set; }
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "New date is required.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "New Follow-Up Date & Time")]
    public DateTime NewDate { get; set; } = DateTime.UtcNow.AddDays(1);

    [MaxLength(500)]
    [Display(Name = "Reason / Remarks")]
    public string? Remarks { get; set; }
}

public class FollowUpStatusChangeViewModel
{
    public int FollowUpId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string TargetStatus { get; set; } = string.Empty; // Completed, Missed, Cancelled

    [MaxLength(500)]
    [Display(Name = "Remarks")]
    public string? Remarks { get; set; }
}
