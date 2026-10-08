namespace AcxiomCRM.ViewModels.Dashboard;

public class DashboardViewModel
{
    public string Role { get; set; } = string.Empty;
    public string FilterPeriod { get; set; } = "ThisMonth"; // Today, ThisWeek, ThisMonth, Custom
    public DateTime? CustomStartDate { get; set; }
    public DateTime? CustomEndDate { get; set; }

    // Metrics cards
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public int PendingFollowUps { get; set; }
    public int OverdueFollowUps { get; set; }

    // Admin extras
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int LockedUsers { get; set; }
    public int SecurityEventsCount { get; set; }

    // Chart.js data
    // 1. Lead Status
    public List<string> LeadStatusLabels { get; set; } = new() { "New", "Contacted", "Qualified", "Unqualified", "Lost", "Converted" };
    public List<int> LeadStatusCounts { get; set; } = new();

    // 2. Opportunity Pipeline Stages
    public List<string> OpportunityStageLabels { get; set; } = new() { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
    public List<decimal> OpportunityStageAmounts { get; set; } = new();
    public List<int> OpportunityStageCounts { get; set; } = new();

    // 3. Monthly Sales / Outcomes
    public List<string> MonthlyLabels { get; set; } = new();
    public List<decimal> MonthlyWonAmounts { get; set; } = new();
    public List<decimal> MonthlyPipelineAmounts { get; set; } = new();

    // Recent items lists for table previews
    public List<RecentFollowUpItem> UpcomingFollowUps { get; set; } = new();
    public List<RecentOpportunityItem> TopOpportunities { get; set; } = new();
}

public class RecentFollowUpItem
{
    public int FollowUpId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string FollowUpType { get; set; } = string.Empty;
    public DateTime FollowUpDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RelatedTo { get; set; } = string.Empty;
    public bool IsOverdue => Status == "Planned" && FollowUpDate.Date < DateTime.UtcNow.Date;
}

public class RecentOpportunityItem
{
    public int OpportunityId { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Stage { get; set; } = string.Empty;
    public DateTime ExpectedCloseDate { get; set; }
}
