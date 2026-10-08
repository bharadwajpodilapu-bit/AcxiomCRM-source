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
using AcxiomCRM.ViewModels.Opportunity;

namespace AcxiomCRM.Controllers;

[Authorize]
public class OpportunitiesController : Controller
{
    private readonly IOpportunityService _opportunityService;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IResourceAuthorizationService _authService;

    public OpportunitiesController(
        IOpportunityService opportunityService,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IResourceAuthorizationService authService)
    {
        _opportunityService = opportunityService;
        _db = db;
        _userManager = userManager;
        _authService = authService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? stage,
        string? status,
        string? ownerId,
        int? customerId,
        string? sortBy = "CreatedDate",
        bool sortDesc = true,
        int page = 1)
    {
        var result = await _opportunityService.GetOpportunitiesAsync(
            User, search, stage, status, ownerId, customerId, sortBy, sortDesc, page, pageSize: 10);

        var model = new OpportunityListViewModel
        {
            Opportunities = result,
            Search = search,
            Stage = stage,
            Status = status,
            OwnerId = ownerId,
            SortBy = sortBy,
            SortDesc = sortDesc,
            OwnersList = await GetOwnersSelectListAsync(ownerId)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Pipeline()
    {
        var result = await _opportunityService.GetOpportunitiesAsync(
            User, null, null, null, null, null, "Amount", true, 1, 100);

        return View(result.Items);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var opp = await _opportunityService.GetOpportunityByIdAsync(id, User, includeRelated: true);
        if (opp == null)
        {
            if (await _opportunityService.OpportunityExistsAsync(id))
            {
                return Forbid();
            }
            return NotFound();
        }

        var model = new OpportunityDetailsViewModel
        {
            Opportunity = opp,
            FollowUps = opp.FollowUps.OrderByDescending(f => f.FollowUpDate).ToList(),
            Activities = opp.Activities.OrderByDescending(a => a.ActivityDate).ToList(),
            CanEdit = _authService.CanAccessOpportunity(User, opp),
            CanDelete = _authService.IsAdmin(User) || _authService.CanAccessOpportunity(User, opp)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var model = new OpportunityCreateEditViewModel
        {
            CustomerId = customerId ?? 0,
            LeadId = leadId,
            OwnerId = currentUserId,
            CustomersList = await GetCustomersSelectListAsync(customerId),
            LeadsList = await GetLeadsSelectListAsync(leadId),
            OwnersList = await GetOwnersSelectListAsync(currentUserId)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OpportunityCreateEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropDowns(model);
            return View(model);
        }

        var opp = new Opportunity
        {
            OpportunityName = model.OpportunityName,
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            Amount = model.Amount,
            Stage = model.Stage,
            Probability = model.Probability,
            ExpectedCloseDate = model.ExpectedCloseDate,
            OwnerId = model.OwnerId ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            Notes = model.Notes
        };

        var result = await _opportunityService.CreateOpportunityAsync(opp, User);
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
        return RedirectToAction(nameof(Details), new { id = result.Data!.OpportunityId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var opp = await _opportunityService.GetOpportunityByIdAsync(id, User);
        if (opp == null)
        {
            if (await _opportunityService.OpportunityExistsAsync(id))
            {
                return Forbid();
            }
            return NotFound();
        }

        var model = new OpportunityCreateEditViewModel
        {
            OpportunityId = opp.OpportunityId,
            OpportunityName = opp.OpportunityName,
            CustomerId = opp.CustomerId,
            LeadId = opp.LeadId,
            Amount = opp.Amount,
            Stage = opp.Stage,
            Probability = opp.Probability,
            ExpectedCloseDate = opp.ExpectedCloseDate,
            OwnerId = opp.OwnerId,
            Notes = opp.Notes
        };

        await PopulateDropDowns(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OpportunityCreateEditViewModel model)
    {
        if (id != model.OpportunityId) return BadRequest();

        if (!ModelState.IsValid)
        {
            await PopulateDropDowns(model);
            return View(model);
        }

        var opp = new Opportunity
        {
            OpportunityId = model.OpportunityId,
            OpportunityName = model.OpportunityName,
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            Amount = model.Amount,
            Stage = model.Stage,
            Probability = model.Probability,
            ExpectedCloseDate = model.ExpectedCloseDate,
            OwnerId = model.OwnerId ?? string.Empty,
            Notes = model.Notes
        };

        var result = await _opportunityService.UpdateOpportunityAsync(opp, User);
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
        return RedirectToAction(nameof(Details), new { id = model.OpportunityId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStage(int id, string newStage)
    {
        var result = await _opportunityService.ChangeStageAsync(id, newStage, User);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _opportunityService.DeleteOpportunityAsync(id, User);
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

    private async Task PopulateDropDowns(OpportunityCreateEditViewModel model)
    {
        model.CustomersList = await GetCustomersSelectListAsync(model.CustomerId);
        model.LeadsList = await GetLeadsSelectListAsync(model.LeadId);
        model.OwnersList = await GetOwnersSelectListAsync(model.OwnerId);
    }

    private async Task<List<SelectListItem>> GetCustomersSelectListAsync(int? selectedId)
    {
        var customers = await _authService.ScopeCustomers(_db.Customers.AsNoTracking(), User)
            .Where(c => c.IsActive)
            .OrderBy(c => c.CustomerName)
            .ToListAsync();

        return customers.Select(c => new SelectListItem
        {
            Value = c.CustomerId.ToString(),
            Text = $"{c.CustomerName} ({c.CustomerCode})",
            Selected = c.CustomerId == selectedId
        }).ToList();
    }

    private async Task<List<SelectListItem>> GetLeadsSelectListAsync(int? selectedId)
    {
        var leads = await _authService.ScopeLeads(_db.Leads.AsNoTracking(), User)
            .OrderBy(l => l.LeadName)
            .ToListAsync();

        var list = leads.Select(l => new SelectListItem
        {
            Value = l.LeadId.ToString(),
            Text = $"{l.LeadName} ({l.LeadCode})",
            Selected = l.LeadId == selectedId
        }).ToList();

        list.Insert(0, new SelectListItem { Value = "", Text = "-- None (Optional) --" });
        return list;
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
