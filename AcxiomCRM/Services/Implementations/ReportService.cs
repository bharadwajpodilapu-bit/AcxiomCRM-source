using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.Report;

namespace AcxiomCRM.Services.Implementations;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;
    private readonly IResourceAuthorizationService _authService;

    public ReportService(ApplicationDbContext db, IResourceAuthorizationService authService)
    {
        _db = db;
        _authService = authService;
    }

    public async Task<List<CustomerReportItem>> GetCustomerReportAsync(ClaimsPrincipal user, string? status = null)
    {
        var query = _authService.ScopeCustomers(_db.Customers.Include(c => c.Owner).AsNoTracking(), user);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        return await query
            .OrderByDescending(c => c.CreatedDate)
            .Select(c => new CustomerReportItem
            {
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Status = c.Status,
                OwnerName = c.Owner != null ? c.Owner.FullName : string.Empty,
                CreatedDate = c.CreatedDate
            })
            .ToListAsync();
    }

    public async Task<List<LeadReportItem>> GetLeadReportAsync(ClaimsPrincipal user, string? status = null, string? source = null)
    {
        var query = _authService.ScopeLeads(_db.Leads.Include(l => l.AssignedUser).Include(l => l.ConvertedCustomer).AsNoTracking(), user);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(source)) query = query.Where(l => l.Source == source);

        return await query
            .OrderByDescending(l => l.CreatedDate)
            .Select(l => new LeadReportItem
            {
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                Source = l.Source,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,
                AssignedToName = l.AssignedUser != null ? l.AssignedUser.FullName : "Unassigned",
                CreatedDate = l.CreatedDate,
                ConvertedCustomerName = l.ConvertedCustomer != null ? l.ConvertedCustomer.CustomerName : null
            })
            .ToListAsync();
    }

    public async Task<List<FollowUpReportItem>> GetFollowUpReportAsync(ClaimsPrincipal user, string? status = null, bool? overdueOnly = null)
    {
        var query = _authService.ScopeFollowUps(
            _db.FollowUps.Include(f => f.AssignedUser).Include(f => f.Customer).Include(f => f.Lead).AsNoTracking(),
            user);

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(f => f.Status == status);
        if (overdueOnly == true)
        {
            var today = DateTime.UtcNow.Date;
            query = query.Where(f => f.Status == "Planned" && f.FollowUpDate.Date < today);
        }

        return await query
            .OrderBy(f => f.FollowUpDate)
            .Select(f => new FollowUpReportItem
            {
                FollowUpId = f.FollowUpId,
                Subject = f.Subject,
                FollowUpType = f.FollowUpType,
                FollowUpDate = f.FollowUpDate,
                Status = f.Status,
                AssignedToName = f.AssignedUser != null ? f.AssignedUser.FullName : "Unassigned",
                RelatedTo = f.Customer != null ? f.Customer.CustomerName : (f.Lead != null ? f.Lead.LeadName : "Opportunity")
            })
            .ToListAsync();
    }

    public async Task<List<OpportunityReportItem>> GetOpportunityReportAsync(ClaimsPrincipal user, string? stage = null)
    {
        var query = _authService.ScopeOpportunities(
            _db.Opportunities.Include(o => o.Customer).Include(o => o.Owner).AsNoTracking(),
            user);

        if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(o => o.Stage == stage);

        return await query
            .OrderByDescending(o => o.CreatedDate)
            .Select(o => new OpportunityReportItem
            {
                OpportunityName = o.OpportunityName,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : string.Empty,
                Stage = o.Stage,
                Amount = o.Amount,
                Probability = o.Probability,
                ExpectedCloseDate = o.ExpectedCloseDate,
                OwnerName = o.Owner != null ? o.Owner.FullName : string.Empty,
                Status = o.Status
            })
            .ToListAsync();
    }

    public async Task<PipelineReportViewModel> GetPipelineReportAsync(ClaimsPrincipal user)
    {
        var query = _authService.ScopeOpportunities(
            _db.Opportunities.Include(o => o.Customer).Include(o => o.Owner).AsNoTracking(),
            user);

        var allOpps = await query.ToListAsync();

        var stageSummaries = allOpps
            .GroupBy(o => o.Stage)
            .Select(g => new PipelineStageSummary
            {
                Stage = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(x => x.Amount),
                WeightedAmount = g.Sum(x => x.WeightedAmount)
            })
            .OrderBy(s => s.Stage)
            .ToList();

        var ownerSummaries = allOpps
            .GroupBy(o => o.Owner != null ? o.Owner.FullName : "Unassigned")
            .Select(g => new PipelineOwnerSummary
            {
                OwnerName = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(x => x.Amount),
                WeightedAmount = g.Sum(x => x.WeightedAmount)
            })
            .OrderByDescending(s => s.TotalAmount)
            .ToList();

        return new PipelineReportViewModel
        {
            GrandTotalAmount = allOpps.Sum(o => o.Amount),
            GrandWeightedAmount = allOpps.Sum(o => o.WeightedAmount),
            StageSummaries = stageSummaries,
            OwnerSummaries = ownerSummaries,
            Opportunities = allOpps.Select(o => new OpportunityReportItem
            {
                OpportunityName = o.OpportunityName,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : string.Empty,
                Stage = o.Stage,
                Amount = o.Amount,
                Probability = o.Probability,
                ExpectedCloseDate = o.ExpectedCloseDate,
                OwnerName = o.Owner != null ? o.Owner.FullName : string.Empty,
                Status = o.Status
            }).ToList()
        };
    }

    public async Task<ConversionReportViewModel> GetConversionReportAsync(ClaimsPrincipal user)
    {
        var query = _authService.ScopeLeads(
            _db.Leads.Include(l => l.AssignedUser).Include(l => l.ConvertedCustomer).Include(l => l.ConvertedOpportunity).AsNoTracking(),
            user);

        var allLeads = await query.ToListAsync();
        var converted = allLeads.Where(l => l.Status == "Converted").ToList();

        return new ConversionReportViewModel
        {
            TotalLeads = allLeads.Count,
            ConvertedLeads = converted.Count,
            UnconvertedLeads = allLeads.Count - converted.Count,
            ConvertedPipelineTotal = converted.Sum(l => l.ConvertedOpportunity?.Amount ?? 0m),
            WonConvertedOpps = converted.Count(l => l.ConvertedOpportunity?.Stage == "Won"),
            ConvertedList = converted.Select(l => new LeadReportItem
            {
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                Source = l.Source,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,
                AssignedToName = l.AssignedUser != null ? l.AssignedUser.FullName : "Unassigned",
                CreatedDate = l.CreatedDate,
                ConvertedCustomerName = l.ConvertedCustomer != null ? l.ConvertedCustomer.CustomerName : null
            }).ToList()
        };
    }

    public async Task<List<UserActivityReportItem>> GetUserActivityReportAsync(ClaimsPrincipal user, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _authService.ScopeActivities(
            _db.Activities.Include(a => a.AssignedUser).AsNoTracking(),
            user);

        if (startDate.HasValue) query = query.Where(a => a.ActivityDate >= startDate.Value.ToUniversalTime());
        if (endDate.HasValue) query = query.Where(a => a.ActivityDate <= endDate.Value.ToUniversalTime());

        var list = await query.ToListAsync();

        return list
            .GroupBy(a => a.AssignedUser != null ? a.AssignedUser.FullName : "Unassigned")
            .Select(g => new UserActivityReportItem
            {
                UserName = g.Key,
                TotalActivities = g.Count(),
                Calls = g.Count(x => x.ActivityType == "Call"),
                Meetings = g.Count(x => x.ActivityType == "Meeting"),
                Emails = g.Count(x => x.ActivityType == "Email"),
                Tasks = g.Count(x => x.ActivityType == "Task")
            })
            .OrderByDescending(x => x.TotalActivities)
            .ToList();
    }
}
