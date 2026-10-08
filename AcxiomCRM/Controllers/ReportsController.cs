using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Authorization;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;
    private readonly IResourceAuthorizationService _authService;

    public ReportsController(IReportService reportService, IResourceAuthorizationService authService)
    {
        _reportService = reportService;
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Customers(string? status, bool export = false)
    {
        var data = await _reportService.GetCustomerReportAsync(User, status);
        if (export)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Code,Customer Name,Email,Phone,Company,Status,Owner,Created Date");
            foreach (var item in data)
            {
                csv.AppendLine($"\"{item.CustomerCode}\",\"{Escape(item.CustomerName)}\",\"{Escape(item.Email)}\",\"{Escape(item.Phone)}\",\"{Escape(item.CompanyName)}\",\"{item.Status}\",\"{Escape(item.OwnerName)}\",\"{item.CreatedDate:yyyy-MM-dd}\"");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"CustomerReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        ViewBag.Status = status;
        return View(data);
    }

    [HttpGet]
    public async Task<IActionResult> Leads(string? status, string? source, bool export = false)
    {
        var data = await _reportService.GetLeadReportAsync(User, status, source);
        if (export)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Code,Lead Name,Source,Status,Priority,Expected Value,Assigned To,Created Date,Converted Customer");
            foreach (var item in data)
            {
                csv.AppendLine($"\"{item.LeadCode}\",\"{Escape(item.LeadName)}\",\"{item.Source}\",\"{item.Status}\",\"{item.Priority}\",\"{item.ExpectedValue}\",\"{Escape(item.AssignedToName)}\",\"{item.CreatedDate:yyyy-MM-dd}\",\"{Escape(item.ConvertedCustomerName)}\"");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"LeadReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        ViewBag.Status = status;
        ViewBag.Source = source;
        return View(data);
    }

    [HttpGet]
    public async Task<IActionResult> FollowUps(string? status, bool? overdueOnly, bool export = false)
    {
        var data = await _reportService.GetFollowUpReportAsync(User, status, overdueOnly);
        if (export)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Subject,Type,Date,Status,Assigned To,Related To,Is Overdue");
            foreach (var item in data)
            {
                csv.AppendLine($"\"{Escape(item.Subject)}\",\"{item.FollowUpType}\",\"{item.FollowUpDate:yyyy-MM-dd HH:mm}\",\"{item.Status}\",\"{Escape(item.AssignedToName)}\",\"{Escape(item.RelatedTo)}\",\"{item.IsOverdue}\"");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"FollowUpReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        ViewBag.Status = status;
        ViewBag.OverdueOnly = overdueOnly;
        return View(data);
    }

    [HttpGet]
    public async Task<IActionResult> Opportunities(string? stage, bool export = false)
    {
        var data = await _reportService.GetOpportunityReportAsync(User, stage);
        if (export)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Opportunity,Customer,Stage,Amount,Probability,Weighted Value,Expected Close Date,Owner,Status");
            foreach (var item in data)
            {
                csv.AppendLine($"\"{Escape(item.OpportunityName)}\",\"{Escape(item.CustomerName)}\",\"{item.Stage}\",\"{item.Amount}\",\"{item.Probability}%\",\"{item.WeightedAmount}\",\"{item.ExpectedCloseDate:yyyy-MM-dd}\",\"{Escape(item.OwnerName)}\",\"{item.Status}\"");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"OpportunityReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        ViewBag.Stage = stage;
        return View(data);
    }

    [HttpGet]
    public async Task<IActionResult> Pipeline(bool export = false)
    {
        var vm = await _reportService.GetPipelineReportAsync(User);
        if (export)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Opportunity,Customer,Stage,Amount,Probability,Weighted Value,Expected Close Date,Owner,Status");
            foreach (var item in vm.Opportunities)
            {
                csv.AppendLine($"\"{Escape(item.OpportunityName)}\",\"{Escape(item.CustomerName)}\",\"{item.Stage}\",\"{item.Amount}\",\"{item.Probability}%\",\"{item.WeightedAmount}\",\"{item.ExpectedCloseDate:yyyy-MM-dd}\",\"{Escape(item.OwnerName)}\",\"{item.Status}\"");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"PipelineReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Conversion(bool export = false)
    {
        var vm = await _reportService.GetConversionReportAsync(User);
        if (export)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Lead Code,Lead Name,Source,Status,Priority,Expected Value,Assigned To,Created Date,Converted Customer");
            foreach (var item in vm.ConvertedList)
            {
                csv.AppendLine($"\"{item.LeadCode}\",\"{Escape(item.LeadName)}\",\"{item.Source}\",\"{item.Status}\",\"{item.Priority}\",\"{item.ExpectedValue}\",\"{Escape(item.AssignedToName)}\",\"{item.CreatedDate:yyyy-MM-dd}\",\"{Escape(item.ConvertedCustomerName)}\"");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"LeadConversionReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        return View(vm);
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> UserActivity(DateTime? startDate, DateTime? endDate, bool export = false)
    {
        var data = await _reportService.GetUserActivityReportAsync(User, startDate, endDate);
        if (export)
        {
            var csv = new StringBuilder();
            csv.AppendLine("User,Total Activities,Calls,Meetings,Emails,Tasks");
            foreach (var item in data)
            {
                csv.AppendLine($"\"{Escape(item.UserName)}\",\"{item.TotalActivities}\",\"{item.Calls}\",\"{item.Meetings}\",\"{item.Emails}\",\"{item.Tasks}\"");
            }
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"UserActivityReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;
        return View(data);
    }

    private static string Escape(string? val) => (val ?? string.Empty).Replace("\"", "\"\"");
}
