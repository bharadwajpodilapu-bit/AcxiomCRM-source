using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.Activity;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private readonly IActivityService _activityService;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IResourceAuthorizationService _authService;

    public ActivitiesController(
        IActivityService activityService,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IResourceAuthorizationService authService)
    {
        _activityService = activityService;
        _db = db;
        _userManager = userManager;
        _authService = authService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? type,
        string? status,
        string? assignedTo,
        int page = 1)
    {
        var result = await _activityService.GetActivitiesAsync(
            User, search, type, status, assignedTo, null, null, null, page, pageSize: 10);

        var model = new ActivityListViewModel
        {
            Activities = result,
            Search = search,
            Type = type,
            Status = status,
            AssignedTo = assignedTo,
            UsersList = await GetUsersSelectListAsync(assignedTo)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var activity = await _activityService.GetActivityByIdAsync(id, User);
        if (activity == null)
        {
            if (await _activityService.ActivityExistsAsync(id))
            {
                return Forbid();
            }
            return NotFound();
        }

        return View(activity);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null, int? opportunityId = null)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var model = new ActivityCreateEditViewModel
        {
            CustomerId = customerId,
            LeadId = leadId,
            OpportunityId = opportunityId,
            AssignedTo = currentUserId,
            ActivityDate = DateTime.UtcNow
        };

        await PopulateDropDowns(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActivityCreateEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropDowns(model);
            return View(model);
        }

        var activity = new Activity
        {
            ActivityType = model.ActivityType,
            Subject = model.Subject,
            Description = model.Description,
            ActivityDate = model.ActivityDate,
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            OpportunityId = model.OpportunityId,
            Status = model.Status,
            AssignedTo = model.AssignedTo ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty
        };

        var result = await _activityService.CreateActivityAsync(activity, User);
        if (!result.Success)
        {
            foreach (var (prop, errors) in result.Errors)
            {
                foreach (var err in errors) ModelState.AddModelError(prop, err);
            }
            await PopulateDropDowns(model);
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var activity = await _activityService.GetActivityByIdAsync(id, User);
        if (activity == null)
        {
            if (await _activityService.ActivityExistsAsync(id))
            {
                return Forbid();
            }
            return NotFound();
        }

        var model = new ActivityCreateEditViewModel
        {
            ActivityId = activity.ActivityId,
            ActivityType = activity.ActivityType,
            Subject = activity.Subject,
            Description = activity.Description,
            ActivityDate = activity.ActivityDate,
            CustomerId = activity.CustomerId,
            LeadId = activity.LeadId,
            OpportunityId = activity.OpportunityId,
            Status = activity.Status,
            AssignedTo = activity.AssignedTo
        };

        await PopulateDropDowns(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ActivityCreateEditViewModel model)
    {
        if (id != model.ActivityId) return BadRequest();

        if (!ModelState.IsValid)
        {
            await PopulateDropDowns(model);
            return View(model);
        }

        var activity = new Activity
        {
            ActivityId = model.ActivityId,
            ActivityType = model.ActivityType,
            Subject = model.Subject,
            Description = model.Description,
            ActivityDate = model.ActivityDate,
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            OpportunityId = model.OpportunityId,
            Status = model.Status,
            AssignedTo = model.AssignedTo ?? string.Empty
        };

        var result = await _activityService.UpdateActivityAsync(activity, User);
        if (result.IsUnauthorized) return Forbid();
        if (result.IsNotFound) return NotFound();

        if (!result.Success)
        {
            foreach (var (prop, errors) in result.Errors)
            {
                foreach (var err in errors) ModelState.AddModelError(prop, err);
            }
            await PopulateDropDowns(model);
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var result = await _activityService.CompleteActivityAsync(id, User);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _activityService.DeleteActivityAsync(id, User);
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

    private async Task PopulateDropDowns(ActivityCreateEditViewModel model)
    {
        var customers = await _authService.ScopeCustomers(_db.Customers.AsNoTracking(), User).OrderBy(c => c.CustomerName).ToListAsync();
        var leads = await _authService.ScopeLeads(_db.Leads.AsNoTracking(), User).OrderBy(l => l.LeadName).ToListAsync();
        var opps = await _authService.ScopeOpportunities(_db.Opportunities.AsNoTracking(), User).OrderBy(o => o.OpportunityName).ToListAsync();

        model.CustomersList = customers.Select(c => new SelectListItem { Value = c.CustomerId.ToString(), Text = $"{c.CustomerName} ({c.CustomerCode})", Selected = c.CustomerId == model.CustomerId }).ToList();
        model.CustomersList.Insert(0, new SelectListItem { Value = "", Text = "-- None --" });

        model.LeadsList = leads.Select(l => new SelectListItem { Value = l.LeadId.ToString(), Text = $"{l.LeadName} ({l.LeadCode})", Selected = l.LeadId == model.LeadId }).ToList();
        model.LeadsList.Insert(0, new SelectListItem { Value = "", Text = "-- None --" });

        model.OpportunitiesList = opps.Select(o => new SelectListItem { Value = o.OpportunityId.ToString(), Text = $"{o.OpportunityName}", Selected = o.OpportunityId == model.OpportunityId }).ToList();
        model.OpportunitiesList.Insert(0, new SelectListItem { Value = "", Text = "-- None --" });

        model.UsersList = await GetUsersSelectListAsync(model.AssignedTo);
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
