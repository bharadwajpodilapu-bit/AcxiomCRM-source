using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Common;

namespace AcxiomCRM.ViewModels.Lead;

public class LeadListViewModel
{
    public PagedResult<Models.Lead> Leads { get; set; } = new();
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? AssignedTo { get; set; }
    public string? SortBy { get; set; } = "CreatedDate";
    public bool SortDesc { get; set; } = true;
    public List<SelectListItem> UsersList { get; set; } = new();
}

public class LeadCreateEditViewModel
{
    public int LeadId { get; set; }

    [Display(Name = "Lead Code")]
    public string? LeadCode { get; set; }

    [Required(ErrorMessage = "Lead name is required.")]
    [MaxLength(150)]
    [Display(Name = "Lead Name")]
    public string LeadName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }

    [RegularExpression(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$|^[0-9]{10}$", ErrorMessage = "Enter a valid phone number.")]
    public string? Phone { get; set; }

    [MaxLength(150)]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [Required]
    public string Source { get; set; } = "Website";

    [Required]
    public string Status { get; set; } = "New";

    [Required]
    public string Priority { get; set; } = "Medium";

    [Range(0, 100000000, ErrorMessage = "Expected value cannot be negative.")]
    [Display(Name = "Expected Value ($)")]
    public decimal? ExpectedValue { get; set; }

    [Display(Name = "Assign To")]
    public string? AssignedTo { get; set; }

    public List<SelectListItem> UsersList { get; set; } = new();
}

public class LeadDetailsViewModel
{
    public Models.Lead Lead { get; set; } = null!;
    public List<Models.FollowUp> FollowUps { get; set; } = new();
    public List<Models.Activity> Activities { get; set; } = new();
    public bool CanEdit { get; set; }
    public bool CanConvert { get; set; }
    public bool CanDelete { get; set; }
}

public class LeadConvertViewModel
{
    public int LeadId { get; set; }
    public string LeadName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    [Display(Name = "Also create an Opportunity for this customer?")]
    public bool CreateOpportunity { get; set; } = true;

    [Range(0.01, 100000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
    [Display(Name = "Opportunity Amount ($)")]
    public decimal? OpportunityAmount { get; set; }

    [Display(Name = "Expected Close Date")]
    public DateTime? ExpectedCloseDate { get; set; } = DateTime.UtcNow.AddDays(30);
}
