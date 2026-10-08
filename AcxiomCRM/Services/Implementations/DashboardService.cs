using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.Dashboard;

namespace AcxiomCRM.Services.Implementations;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly IResourceAuthorizationService _authService;

    public DashboardService(ApplicationDbContext db, IResourceAuthorizationService authService)
    {
        _db = db;
        _authService = authService;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(
        ClaimsPrincipal user,
        string filterPeriod = "ThisMonth",
        DateTime? customStart = null,
        DateTime? customEnd = null)
    {
        var vm = new DashboardViewModel
        {
            FilterPeriod = filterPeriod,
            CustomStartDate = customStart,
            CustomEndDate = customEnd
        };

        if (_authService.IsAdmin(user)) vm.Role = AppRoles.Admin;
        else if (_authService.IsManager(user)) vm.Role = AppRoles.Manager;
        else vm.Role = AppRoles.SalesExecutive;

        // Base scoped queries
        var customersQuery = _authService.ScopeCustomers(_db.Customers.AsNoTracking(), user);
        var leadsQuery = _authService.ScopeLeads(_db.Leads.AsNoTracking(), user);
        var oppsQuery = _authService.ScopeOpportunities(_db.Opportunities.AsNoTracking(), user);
        var followUpsQuery = _authService.ScopeFollowUps(_db.FollowUps.AsNoTracking(), user);

        // Date range calculation for period filtering if applied
        var now = DateTime.UtcNow;
        DateTime? filterStart = null;
        DateTime? filterEnd = null;

        switch (filterPeriod?.ToLower())
        {
            case "today":
                filterStart = now.Date;
                filterEnd = now.Date.AddDays(1).AddTicks(-1);
                break;
            case "thisweek":
                int diff = (7 + (int)now.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                filterStart = now.Date.AddDays(-1 * diff);
                filterEnd = filterStart.Value.AddDays(7).AddTicks(-1);
                break;
            case "thismonth":
                filterStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                filterEnd = filterStart.Value.AddMonths(1).AddTicks(-1);
                break;
            case "custom":
                if (customStart.HasValue) filterStart = customStart.Value.ToUniversalTime();
                if (customEnd.HasValue) filterEnd = customEnd.Value.ToUniversalTime().Date.AddDays(1).AddTicks(-1);
                break;
        }

        // Summary Counts
        vm.TotalCustomers = await customersQuery.CountAsync();
        vm.TotalLeads = await leadsQuery.CountAsync();

        // Open leads exclude closed, lost, and converted leads
        vm.OpenLeads = await leadsQuery
            .Where(l => l.Status != "Converted" && l.Status != "Lost")
            .CountAsync();

        vm.TotalOpportunities = await oppsQuery.CountAsync();
        vm.OpenOpportunities = await oppsQuery
            .Where(o => o.Status == "Open" && o.Stage != "Won" && o.Stage != "Lost")
            .CountAsync();

        vm.WonOpportunities = await oppsQuery
            .Where(o => o.Status == "Won" || o.Stage == "Won")
            .CountAsync();

        vm.LostOpportunities = await oppsQuery
            .Where(o => o.Status == "Lost" || o.Stage == "Lost")
            .CountAsync();

        // Pipeline Value (sum of open opportunities)
        var openOpps = await oppsQuery
            .Where(o => o.Status == "Open" && o.Stage != "Won" && o.Stage != "Lost")
            .Select(o => new { o.Amount, o.Probability })
            .ToListAsync();

        vm.TotalPipelineValue = openOpps.Sum(o => o.Amount);
        vm.WeightedPipelineValue = openOpps.Sum(o => o.Amount * o.Probability / 100m);

        // Follow-ups
        var todayDate = now.Date;
        vm.PendingFollowUps = await followUpsQuery
            .Where(f => f.Status == "Planned")
            .CountAsync();

        vm.OverdueFollowUps = await followUpsQuery
            .Where(f => f.Status == "Planned" && f.FollowUpDate.Date < todayDate)
            .CountAsync();

        // Admin metrics
        if (_authService.IsAdmin(user))
        {
            vm.TotalUsers = await _db.Users.CountAsync();
            vm.ActiveUsers = await _db.Users.Where(u => u.IsActive).CountAsync();
            vm.LockedUsers = await _db.Users.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow).CountAsync();
            vm.SecurityEventsCount = await _db.AuditLogs.Where(a => a.Module == "Auth" || a.Module == "Security").CountAsync();
        }

        // Chart 1: Lead Status
        var leadStatuses = new[] { "New", "Contacted", "Qualified", "Unqualified", "Lost", "Converted" };
        var leadGroupings = await leadsQuery
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        vm.LeadStatusLabels = leadStatuses.ToList();
        vm.LeadStatusCounts = leadStatuses
            .Select(st => leadGroupings.FirstOrDefault(g => g.Status == st)?.Count ?? 0)
            .ToList();

        // Chart 2: Opportunity Stages
        var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
        var stageGroupings = await oppsQuery
            .GroupBy(o => o.Stage)
            .Select(g => new { Stage = g.Key, Amount = g.Sum(x => x.Amount), Count = g.Count() })
            .ToListAsync();

        vm.OpportunityStageLabels = stages.ToList();
        vm.OpportunityStageAmounts = stages
            .Select(s => stageGroupings.FirstOrDefault(g => g.Stage == s)?.Amount ?? 0m)
            .ToList();
        vm.OpportunityStageCounts = stages
            .Select(s => stageGroupings.FirstOrDefault(g => g.Stage == s)?.Count ?? 0)
            .ToList();

        // Chart 3: Monthly Sales (last 6 months)
        var monthsList = new List<DateTime>();
        for (int i = 5; i >= 0; i--)
        {
            var d = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
            monthsList.Add(d);
        }

        var sixMonthsAgo = monthsList.First();
        var oppsLast6Months = await oppsQuery
            .Where(o => o.CreatedDate >= sixMonthsAgo)
            .Select(o => new { o.CreatedDate, o.Amount, o.Status, o.Stage })
            .ToListAsync();

        foreach (var m in monthsList)
        {
            var label = m.ToString("MMM yyyy", CultureInfo.InvariantCulture);
            vm.MonthlyLabels.Add(label);

            var nextM = m.AddMonths(1);
            var monthOpps = oppsLast6Months
                .Where(o => o.CreatedDate >= m && o.CreatedDate < nextM);

            var wonAmt = monthOpps.Where(o => o.Status == "Won" || o.Stage == "Won").Sum(o => o.Amount);
            var pipelineAmt = monthOpps.Where(o => o.Status == "Open" && o.Stage != "Lost").Sum(o => o.Amount);

            vm.MonthlyWonAmounts.Add(wonAmt);
            vm.MonthlyPipelineAmounts.Add(pipelineAmt);
        }

        // Recent preview items
        vm.UpcomingFollowUps = await followUpsQuery
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Where(f => f.Status == "Planned")
            .OrderBy(f => f.FollowUpDate)
            .Take(5)
            .Select(f => new RecentFollowUpItem
            {
                FollowUpId = f.FollowUpId,
                Subject = f.Subject,
                FollowUpType = f.FollowUpType,
                FollowUpDate = f.FollowUpDate,
                Status = f.Status,
                RelatedTo = f.Customer != null ? f.Customer.CustomerName : (f.Lead != null ? f.Lead.LeadName : "Opportunity")
            })
            .ToListAsync();

        vm.TopOpportunities = await oppsQuery
            .Include(o => o.Customer)
            .Where(o => o.Status == "Open")
            .OrderByDescending(o => o.Amount)
            .Take(5)
            .Select(o => new RecentOpportunityItem
            {
                OpportunityId = o.OpportunityId,
                OpportunityName = o.OpportunityName,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : string.Empty,
                Amount = o.Amount,
                Stage = o.Stage,
                ExpectedCloseDate = o.ExpectedCloseDate
            })
            .ToListAsync();

        return vm;
    }
}
