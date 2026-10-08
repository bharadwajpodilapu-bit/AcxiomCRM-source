using System.Security.Claims;
using AcxiomCRM.ViewModels.Report;

namespace AcxiomCRM.Services.Interfaces;

public interface IReportService
{
    Task<List<CustomerReportItem>> GetCustomerReportAsync(ClaimsPrincipal user, string? status = null);
    Task<List<LeadReportItem>> GetLeadReportAsync(ClaimsPrincipal user, string? status = null, string? source = null);
    Task<List<FollowUpReportItem>> GetFollowUpReportAsync(ClaimsPrincipal user, string? status = null, bool? overdueOnly = null);
    Task<List<OpportunityReportItem>> GetOpportunityReportAsync(ClaimsPrincipal user, string? stage = null);
    Task<PipelineReportViewModel> GetPipelineReportAsync(ClaimsPrincipal user);
    Task<ConversionReportViewModel> GetConversionReportAsync(ClaimsPrincipal user);
    Task<List<UserActivityReportItem>> GetUserActivityReportAsync(ClaimsPrincipal user, DateTime? startDate = null, DateTime? endDate = null);
}
