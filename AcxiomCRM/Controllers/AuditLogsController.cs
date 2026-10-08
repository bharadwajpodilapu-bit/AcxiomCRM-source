using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.Audit;
using AcxiomCRM.ViewModels.Common;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class AuditLogsController : Controller
{
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuditLogsController(IAuditService auditService, UserManager<ApplicationUser> userManager)
    {
        _auditService = auditService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? userId,
        string? module,
        string? actionName,
        string? entityName,
        DateTime? startDate,
        DateTime? endDate,
        string? result,
        int page = 1)
    {
        const int pageSize = 20;
        var totalCount = await _auditService.GetAuditLogsCountAsync(
            userId, module, actionName, entityName, startDate, endDate, result);

        var items = await _auditService.GetAuditLogsAsync(
            userId, module, actionName, entityName, startDate, endDate, result, page, pageSize);

        var paged = new PagedResult<AuditLog>
        {
            Items = items,
            PageIndex = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
        var userSelectList = users.Select(u => new SelectListItem
        {
            Value = u.Id,
            Text = $"{u.FullName} ({u.Email})",
            Selected = u.Id == userId
        }).ToList();

        var model = new AuditLogListViewModel
        {
            Logs = paged,
            UserId = userId,
            Module = module,
            Action = actionName,
            EntityName = entityName,
            StartDate = startDate,
            EndDate = endDate,
            Result = result,
            UsersList = userSelectList
        };

        return View(model);
    }
}
