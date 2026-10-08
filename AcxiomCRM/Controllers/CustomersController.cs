using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.Customer;

namespace AcxiomCRM.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly ICustomerService _customerService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IResourceAuthorizationService _authService;

    public CustomersController(
        ICustomerService customerService,
        UserManager<ApplicationUser> userManager,
        IResourceAuthorizationService authService)
    {
        _customerService = customerService;
        _userManager = userManager;
        _authService = authService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string? ownerId,
        string? sortBy = "CreatedDate",
        bool sortDesc = true,
        int page = 1)
    {
        var result = await _customerService.GetCustomersAsync(
            User, search, status, ownerId, sortBy, sortDesc, page, pageSize: 10);

        var model = new CustomerListViewModel
        {
            Customers = result,
            Search = search,
            Status = status,
            OwnerId = ownerId,
            SortBy = sortBy,
            SortDesc = sortDesc,
            OwnersList = await GetOwnersSelectListAsync(ownerId)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var customer = await _customerService.GetCustomerByIdAsync(id, User, includeRelated: true);
        if (customer == null)
        {
            if (await _customerService.CustomerExistsAsync(id))
            {
                return Forbid();
            }
            return NotFound();
        }

        var model = new CustomerDetailsViewModel
        {
            Customer = customer,
            Opportunities = customer.Opportunities.OrderByDescending(o => o.CreatedDate).ToList(),
            FollowUps = customer.FollowUps.OrderByDescending(f => f.FollowUpDate).ToList(),
            Activities = customer.Activities.OrderByDescending(a => a.ActivityDate).ToList(),
            ConvertedFromLeads = customer.ConvertedFromLeads.ToList(),
            CanEdit = _authService.CanAccessCustomer(User, customer),
            CanDelete = _authService.IsAdmin(User) || _authService.CanAccessCustomer(User, customer)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CustomerCreateEditViewModel
        {
            OwnersList = await GetOwnersSelectListAsync(User.FindFirstValue(ClaimTypes.NameIdentifier))
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerCreateEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.OwnersList = await GetOwnersSelectListAsync(model.OwnerId);
            return View(model);
        }

        var customer = new Customer
        {
            CustomerName = model.CustomerName,
            Email = model.Email,
            Phone = model.Phone,
            CompanyName = model.CompanyName,
            Address = model.Address,
            City = model.City,
            State = model.State,
            Status = model.Status,
            OwnerId = model.OwnerId ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty
        };

        var result = await _customerService.CreateCustomerAsync(customer, User);
        if (!result.Success)
        {
            foreach (var (prop, errors) in result.Errors)
            {
                foreach (var err in errors) ModelState.AddModelError(prop, err);
            }
            model.OwnersList = await GetOwnersSelectListAsync(model.OwnerId);
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = result.Data!.CustomerId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _customerService.GetCustomerByIdAsync(id, User);
        if (customer == null)
        {
            if (await _customerService.CustomerExistsAsync(id))
            {
                return Forbid();
            }
            return NotFound();
        }

        var model = new CustomerCreateEditViewModel
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Status = customer.Status,
            OwnerId = customer.OwnerId,
            OwnersList = await GetOwnersSelectListAsync(customer.OwnerId)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerCreateEditViewModel model)
    {
        if (id != model.CustomerId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            model.OwnersList = await GetOwnersSelectListAsync(model.OwnerId);
            return View(model);
        }

        var customer = new Customer
        {
            CustomerId = model.CustomerId,
            CustomerName = model.CustomerName,
            Email = model.Email,
            Phone = model.Phone,
            CompanyName = model.CompanyName,
            Address = model.Address,
            City = model.City,
            State = model.State,
            Status = model.Status,
            OwnerId = model.OwnerId ?? string.Empty
        };

        var result = await _customerService.UpdateCustomerAsync(customer, User);
        if (result.IsUnauthorized)
        {
            return Forbid();
        }
        if (result.IsNotFound)
        {
            return NotFound();
        }

        if (!result.Success)
        {
            foreach (var (prop, errors) in result.Errors)
            {
                foreach (var err in errors) ModelState.AddModelError(prop, err);
            }
            model.OwnersList = await GetOwnersSelectListAsync(model.OwnerId);
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = model.CustomerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var result = await _customerService.DeactivateCustomerAsync(id, User);
        if (result.IsUnauthorized)
        {
            return Forbid();
        }
        if (result.IsNotFound)
        {
            return NotFound();
        }

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
        }
        else
        {
            TempData["SuccessMessage"] = result.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _customerService.DeleteCustomerAsync(id, User);
        if (result.IsUnauthorized)
        {
            return Forbid();
        }
        if (result.IsNotFound)
        {
            return NotFound();
        }

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
        }
        else
        {
            TempData["SuccessMessage"] = result.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> GetOwnersSelectListAsync(string? selectedId)
    {
        var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        return users.Select(u => new SelectListItem
        {
            Value = u.Id,
            Text = $"{u.FullName} ({u.Email})",
            Selected = u.Id == selectedId
        }).ToList();
    }
}
