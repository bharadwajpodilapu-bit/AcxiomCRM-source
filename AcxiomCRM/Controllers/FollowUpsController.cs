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
using AcxiomCRM.ViewModels.FollowUp;

namespace AcxiomCRM.Controllers;

[Authorize]
public class FollowUpsController : Controller
{
    private readonly IFollowUpService _followUpService;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IResourceAuthorizationService _authService;

    public FollowUpsController(
        IFollowUpService followUpService,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IResourceAuthorizationService authService)
    {
        _followUpService = followUpService;
        _db = db;
        _userManager = userManager;
        _authService = authService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string? type,
        string? assignedTo,
        bool? overdueOnly,
        int page = 1)
    {
        var result = await _followUpService.GetFollowUpsAsync(
            User, search, status, type, assignedTo, overdueOnly, null, null, null, page, pageSize: 10);

        var model = new FollowUpListViewModel
        {
            FollowUps = result,
            Search = search,
            Status = status,
            Type = type,
            AssignedTo = assignedTo,
            OverdueOnly = overdueOnly,
            UsersList = await GetUsersSelectListAsync(assignedTo)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var followUp = await _followUpService.GetFollowUpByIdAsync(id, User);
        if (followUp == null)
        {
            if (await _followUpService.FollowUpExistsAsync(id))
            {
                return Forbid();
            }
            return NotFound();
        }

        return View(followUp);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null, int? opportunityId = null)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var model = new FollowUpCreateEditViewModel
        {
            CustomerId = customerId,
            LeadId = leadId,
            OpportunityId = opportunityId,
            AssignedTo = currentUserId,
            FollowUpDate = DateTime.UtcNow.AddDays(1)
        };

        await PopulateDropDowns(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUpCreateEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropDowns(model);
            return View(model);
        }

        var followUp = new FollowUp
        {
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            OpportunityId = model.OpportunityId,
            FollowUpDate = model.FollowUpDate,
            FollowUpType = model.FollowUpType,
            Subject = model.Subject,
            Remarks = model.Remarks,
            AssignedTo = model.AssignedTo ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty
        };

        var result = await _followUpService.CreateFollowUpAsync(followUp, User);
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
        var followUp = await _followUpService.GetFollowUpByIdAsync(id, User);
        if (followUp == null)
        {
            if (await _followUpService.FollowUpExistsAsync(id))
            {
                return Forbid();
            }
            return NotFound();
        }

        var model = new FollowUpCreateEditViewModel
        {
            FollowUpId = followUp.FollowUpId,
            CustomerId = followUp.CustomerId,
            LeadId = followUp.LeadId,
            OpportunityId = followUp.OpportunityId,
            FollowUpDate = followUp.FollowUpDate,
            FollowUpType = followUp.FollowUpType,
            Subject = followUp.Subject,
            Remarks = followUp.Remarks,
            Status = followUp.Status,
            AssignedTo = followUp.AssignedTo
        };

        await PopulateDropDowns(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FollowUpCreateEditViewModel model)
    {
        if (id != model.FollowUpId) return BadRequest();

        if (!ModelState.IsValid)
        {
            await PopulateDropDowns(model);
            return View(model);
        }

        var followUp = new FollowUp
        {
            FollowUpId = model.FollowUpId,
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            OpportunityId = model.OpportunityId,
            FollowUpDate = model.FollowUpDate,
            FollowUpType = model.FollowUpType,
            Subject = model.Subject,
            Remarks = model.Remarks,
            Status = model.Status,
            AssignedTo = model.AssignedTo ?? string.Empty
        };

        var result = await _followUpService.UpdateFollowUpAsync(followUp, User);
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
    public async Task<IActionResult> ChangeStatus(int id, string newStatus, string? remarks)
    {
        var result = await _followUpService.ChangeStatusAsync(id, newStatus, remarks, User);
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
    public async Task<IActionResult> Reschedule(int id, DateTime newDate, string? remarks)
    {
        var result = await _followUpService.RescheduleAsync(id, newDate, remarks, User);
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
        var result = await _followUpService.DeleteFollowUpAsync(id, User);
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

    private async Task PopulateDropDowns(FollowUpCreateEditViewModel model)
    {
        var customers = await _authService.ScopeCustomers(_db.Customers.AsNoTracking(), User).OrderBy(c => c.CustomerName).ToListAsync();
        var leads = await _authService.ScopeLeads(_db.Leads.AsNoTracking(), User).OrderBy(l => l.LeadName).ToListAsync();
        var opps = await _authService.ScopeOpportunities(_db.Opportunities.AsNoTracking(), User).OrderBy(o => o.OpportunityName).ToListAsync();

        model.CustomersList = customers.Select(c => new SelectListItem { Value = c.CustomerId.ToString(), Text = $"{c.CustomerName} ({c.CustomerCode})", Selected = c.CustomerId == model.CustomerId }).ToList();
        model.CustomersList.Insert(0, new SelectListItem { Value = "", Text = "-- None --" });

        model.LeadsList = leads.Select(l => new SelectListItem { Value = l.LeadId.ToString(), Text = $"{l.LeadName} ({l.LeadCode})", Selected = l.LeadId == model.LeadId }).ToList();
        model.LeadsList.Insert(0, new SelectListItem { Value = "", Text = "-- None --" });

        model.OpportunitiesList = opps.Select(o => new SelectListItem { Value = o.OpportunityId.ToString(), Text = $"{o.OpportunityName} (${o.Amount:N0})", Selected = o.OpportunityId == model.OpportunityId }).ToList();
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
