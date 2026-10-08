namespace AcxiomCRM.DTOs.Report;

public class PipelineReportDto
{
    public decimal GrandTotalAmount { get; set; }
    public decimal GrandWeightedAmount { get; set; }
    public List<StageSummaryDto> Stages { get; set; } = new();
    public List<OwnerSummaryDto> Owners { get; set; } = new();
}

public class StageSummaryDto
{
    public string Stage { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal WeightedAmount { get; set; }
}

public class OwnerSummaryDto
{
    public string OwnerName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal WeightedAmount { get; set; }
}
