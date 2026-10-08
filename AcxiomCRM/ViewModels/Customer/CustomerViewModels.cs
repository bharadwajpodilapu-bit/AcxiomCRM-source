using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Common;

namespace AcxiomCRM.ViewModels.Customer;

public class CustomerListViewModel
{
    public PagedResult<Models.Customer> Customers { get; set; } = new();
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? OwnerId { get; set; }
    public string? SortBy { get; set; } = "CreatedDate";
    public bool SortDesc { get; set; } = true;
    public List<SelectListItem> OwnersList { get; set; } = new();
}

public class CustomerCreateEditViewModel
{
    public int CustomerId { get; set; }

    [Display(Name = "Customer Code")]
    public string? CustomerCode { get; set; }

    [Required(ErrorMessage = "Customer Name is required.")]
    [MaxLength(150, ErrorMessage = "Customer Name cannot exceed 150 characters.")]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [MaxLength(30)]
    [RegularExpression(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$|^[0-9]{10}$", ErrorMessage = "Enter a valid phone number.")]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(150)]
    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    [MaxLength(250)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    [Required]
    public string Status { get; set; } = "Active";

    [Display(Name = "Account Owner")]
    public string? OwnerId { get; set; }

    public List<SelectListItem> OwnersList { get; set; } = new();
}

public class CustomerDetailsViewModel
{
    public Models.Customer Customer { get; set; } = null!;
    public List<Models.Opportunity> Opportunities { get; set; } = new();
    public List<Models.FollowUp> FollowUps { get; set; } = new();
    public List<Models.Activity> Activities { get; set; } = new();
    public List<Models.Lead> ConvertedFromLeads { get; set; } = new();
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}
