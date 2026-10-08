using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return RedirectToAction("Login", "Account");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(string? traceId)
    {
        ViewData["TraceId"] = traceId ?? Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
