namespace AcxiomCRM.ViewModels.Report;

public class CustomerReportItem
{
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

public class LeadReportItem
{
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal? ExpectedValue { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ConvertedCustomerName { get; set; }
}

public class FollowUpReportItem
{
    public int FollowUpId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string FollowUpType { get; set; } = string.Empty;
    public DateTime FollowUpDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AssignedToName { get; set; } = string.Empty;
    public string RelatedTo { get; set; } = string.Empty;
    public bool IsOverdue => Status == "Planned" && FollowUpDate.Date < DateTime.UtcNow.Date;
}

public class OpportunityReportItem
{
    public string OpportunityName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Probability { get; set; }
    public decimal WeightedAmount => Amount * Probability / 100m;
    public DateTime ExpectedCloseDate { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class PipelineStageSummary
{
    public string Stage { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal WeightedAmount { get; set; }
}

public class PipelineOwnerSummary
{
    public string OwnerName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal WeightedAmount { get; set; }
}

public class PipelineReportViewModel
{
    public decimal GrandTotalAmount { get; set; }
    public decimal GrandWeightedAmount { get; set; }
    public List<PipelineStageSummary> StageSummaries { get; set; } = new();
    public List<PipelineOwnerSummary> OwnerSummaries { get; set; } = new();
    public List<OpportunityReportItem> Opportunities { get; set; } = new();
}

public class ConversionReportViewModel
{
    public int TotalLeads { get; set; }
    public int ConvertedLeads { get; set; }
    public int UnconvertedLeads { get; set; }
    public decimal ConversionRate => TotalLeads > 0 ? Math.Round((decimal)ConvertedLeads / TotalLeads * 100, 2) : 0;
    public decimal ConvertedPipelineTotal { get; set; }
    public int WonConvertedOpps { get; set; }
    public List<LeadReportItem> ConvertedList { get; set; } = new();
}

public class UserActivityReportItem
{
    public string UserName { get; set; } = string.Empty;
    public int TotalActivities { get; set; }
    public int Calls { get; set; }
    public int Meetings { get; set; }
    public int Emails { get; set; }
    public int Tasks { get; set; }
}
