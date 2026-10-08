using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.Lead;

namespace AcxiomCRM.Controllers;

[Authorize]
public class LeadsController : Controller
{
    private readonly ILeadService _leadService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IResourceAuthorizationService _authService;

    public LeadsController(
        ILeadService leadService,
        UserManager<ApplicationUser> userManager,
        IResourceAuthorizationService authService)
    {
        _leadService = leadService;
        _userManager = userManager;
        _authService = authService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string? priority,
        string? assignedTo,
        string? sortBy = "CreatedDate",
        bool sortDesc = true,
        int page = 1)
    {
        var result = await _leadService.GetLeadsAsync(
            User, search, status, priority, assignedTo, sortBy, sortDesc, page, pageSize: 10);

        var model = new LeadListViewModel
        {
            Leads = result,
            Search = search,
            Status = status,
            Priority = priority,
            AssignedTo = assignedTo,
            SortBy = sortBy,
            SortDesc = sortDesc,
            UsersList = await GetUsersSelectListAsync(assignedTo)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var lead = await _leadService.GetLeadByIdAsync(id, User, includeRelated: true);
        if (lead == null)
        {
            if (await _leadService.LeadExistsAsync(id))
            {
                return Forbid();
            }
            return NotFound();
        }

        var model = new LeadDetailsViewModel
        {
            Lead = lead,
            FollowUps = lead.FollowUps.OrderByDescending(f => f.FollowUpDate).ToList(),
            Activities = lead.Activities.OrderByDescending(a => a.ActivityDate).ToList(),
            CanEdit = _authService.CanAccessLead(User, lead),
            CanConvert = lead.Status != "Converted" && lead.Status != "Lost" && _authService.CanAccessLead(User, lead),
            CanDelete = _authService.IsAdmin(User) || _authService.CanAccessLead(User, lead)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new LeadCreateEditViewModel
        {
            AssignedTo = User.FindFirstValue(ClaimTypes.NameIdentifier),
            UsersList = await GetUsersSelectListAsync(User.FindFirstValue(ClaimTypes.NameIdentifier))
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeadCreateEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.UsersList = await GetUsersSelectListAsync(model.AssignedTo);
            return View(model);
        }

        var lead = new Lead
        {
            LeadName = model.LeadName,
            Email = model.Email,
            Phone = model.Phone,
            CompanyName = model.CompanyName,
            Source = model.Source,
            Status = model.Status,
            Priority = model.Priority,
            ExpectedValue = model.ExpectedValue,
            AssignedTo = model.AssignedTo ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        };

        var result = await _leadService.CreateLeadAsync(lead, User);
        if (!result.Success)
        {
            foreach (var (prop, errors) in result.Errors)
            {
                foreach (var err in errors) ModelState.AddModelError(prop, err);
            }
            model.UsersList = await GetUsersSelectListAsync(model.AssignedTo);
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = result.Data!.LeadId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var lead = await _leadService.GetLeadByIdAsync(id, User);
        if (lead == null)
        {
            if (await _leadService.LeadExistsAsync(id))
            {
                return Forbid();
            }
            return NotFound();
        }

        var model = new LeadCreateEditViewModel
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Source = lead.Source,
            Status = lead.Status,
            Priority = lead.Priority,
            ExpectedValue = lead.ExpectedValue,
            AssignedTo = lead.AssignedTo,
            UsersList = await GetUsersSelectListAsync(lead.AssignedTo)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LeadCreateEditViewModel model)
    {
        if (id != model.LeadId) return BadRequest();

        if (!ModelState.IsValid)
        {
            model.UsersList = await GetUsersSelectListAsync(model.AssignedTo);
            return View(model);
        }

        var lead = new Lead
        {
            LeadId = model.LeadId,
            LeadName = model.LeadName,
            Email = model.Email,
            Phone = model.Phone,
            CompanyName = model.CompanyName,
            Source = model.Source,
            Status = model.Status,
            Priority = model.Priority,
            ExpectedValue = model.ExpectedValue,
            AssignedTo = model.AssignedTo
        };

        var result = await _leadService.UpdateLeadAsync(lead, User);
        if (result.IsUnauthorized) return Forbid();
        if (result.IsNotFound) return NotFound();

        if (!result.Success)
        {
            foreach (var (prop, errors) in result.Errors)
            {
                foreach (var err in errors) ModelState.AddModelError(prop, err);
            }
            model.UsersList = await GetUsersSelectListAsync(model.AssignedTo);
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = model.LeadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, string newStatus)
    {
        var result = await _leadService.ChangeStatusAsync(id, newStatus, User);
        if (result.IsUnauthorized) return Forbid();
        if (result.IsNotFound) return NotFound();

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
        }
        else
        {
            TempData["SuccessMessage"] = result.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Convert(int id)
    {
        var lead = await _leadService.GetLeadByIdAsync(id, User);
        if (lead == null)
        {
            if (await _leadService.LeadExistsAsync(id))
            {
                return Forbid();
            }
            return NotFound();
        }

        if (lead.Status == "Converted" || lead.Status == "Lost")
        {
            TempData["ErrorMessage"] = $"Cannot convert a {lead.Status} lead.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new LeadConvertViewModel
        {
            LeadId = lead.LeadId,
            LeadName = lead.LeadName,
            CompanyName = lead.CompanyName,
            Email = lead.Email,
            Phone = lead.Phone,
            OpportunityAmount = lead.ExpectedValue ?? 1000m,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(LeadConvertViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _leadService.ConvertLeadAsync(
            model.LeadId,
            model.CreateOpportunity,
            model.OpportunityAmount,
            model.ExpectedCloseDate,
            User);

        if (result.IsUnauthorized) return Forbid();
        if (result.IsNotFound) return NotFound();

        if (!result.Success)
        {
            foreach (var (prop, errors) in result.Errors)
            {
                foreach (var err in errors) ModelState.AddModelError(prop, err);
            }
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction("Details", "Customers", new { id = result.Data!.CustomerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _leadService.DeleteLeadAsync(id, User);
        if (result.IsUnauthorized) return Forbid();
        if (result.IsNotFound) return NotFound();

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

    private async Task<List<SelectListItem>> GetUsersSelectListAsync(string? selectedId)
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
