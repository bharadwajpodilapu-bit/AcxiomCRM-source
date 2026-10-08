using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string period = "ThisMonth",
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var model = await _dashboardService.GetDashboardAsync(User, period, startDate, endDate);
        return View(model);
    }
}
