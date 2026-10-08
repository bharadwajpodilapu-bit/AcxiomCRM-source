using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Common;

namespace AcxiomCRM.ViewModels.Opportunity;

public class OpportunityListViewModel
{
    public PagedResult<Models.Opportunity> Opportunities { get; set; } = new();
    public string? Search { get; set; }
    public string? Stage { get; set; }
    public string? Status { get; set; }
    public string? OwnerId { get; set; }
    public string? SortBy { get; set; } = "CreatedDate";
    public bool SortDesc { get; set; } = true;
    public List<SelectListItem> OwnersList { get; set; } = new();
}

public class OpportunityCreateEditViewModel
{
    public int OpportunityId { get; set; }

    [Required(ErrorMessage = "Opportunity name is required.")]
    [MaxLength(150)]
    [Display(Name = "Opportunity Name")]
    public string OpportunityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer is required.")]
    [Display(Name = "Customer")]
    public int CustomerId { get; set; }

    [Display(Name = "Associated Lead (Optional)")]
    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Opportunity Amount is required.")]
    [Range(0.01, 1000000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
    [Display(Name = "Amount ($)")]
    public decimal Amount { get; set; }

    [Required]
    public string Stage { get; set; } = "Qualification";

    [Required]
    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    [Display(Name = "Probability (%)")]
    public int Probability { get; set; } = 20;

    [Required(ErrorMessage = "Expected Close Date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Expected Close Date")]
    public DateTime ExpectedCloseDate { get; set; } = DateTime.UtcNow.AddDays(30);

    [Display(Name = "Owner")]
    public string? OwnerId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public List<SelectListItem> CustomersList { get; set; } = new();
    public List<SelectListItem> LeadsList { get; set; } = new();
    public List<SelectListItem> OwnersList { get; set; } = new();
}

public class OpportunityDetailsViewModel
{
    public Models.Opportunity Opportunity { get; set; } = null!;
    public List<Models.FollowUp> FollowUps { get; set; } = new();
    public List<Models.Activity> Activities { get; set; } = new();
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}
