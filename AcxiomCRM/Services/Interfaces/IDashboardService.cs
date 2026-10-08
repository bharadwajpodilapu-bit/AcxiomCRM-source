using System.Security.Claims;
using AcxiomCRM.ViewModels.Dashboard;

namespace AcxiomCRM.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardAsync(
        ClaimsPrincipal user,
        string filterPeriod = "ThisMonth",
        DateTime? customStart = null,
        DateTime? customEnd = null);
}
