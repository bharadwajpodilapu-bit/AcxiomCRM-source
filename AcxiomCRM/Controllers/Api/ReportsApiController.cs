using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs.Report;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers.Api;

[Route("api/reports")]
[Authorize]
public class ReportsApiController : BaseApiController
{
    private readonly IReportService _reportService;

    public ReportsApiController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("pipeline")]
    public async Task<IActionResult> GetPipelineReport()
    {
        var vm = await _reportService.GetPipelineReportAsync(User);

        var dto = new PipelineReportDto
        {
            GrandTotalAmount = vm.GrandTotalAmount,
            GrandWeightedAmount = vm.GrandWeightedAmount,
            Stages = vm.StageSummaries.Select(s => new StageSummaryDto
            {
                Stage = s.Stage,
                Count = s.Count,
                TotalAmount = s.TotalAmount,
                WeightedAmount = s.WeightedAmount
            }).ToList(),
            Owners = vm.OwnerSummaries.Select(o => new OwnerSummaryDto
            {
                OwnerName = o.OwnerName,
                Count = o.Count,
                TotalAmount = o.TotalAmount,
                WeightedAmount = o.WeightedAmount
            }).ToList()
        };

        return ApiSuccess(dto, "Pipeline report retrieved successfully.");
    }
}
