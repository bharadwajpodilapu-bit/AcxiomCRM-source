using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public interface IAuditService
{
    Task LogAsync(
        string? userId,
        string action,
        string module,
        string entityName,
        string? recordId = null,
        string? oldValue = null,
        string? newValue = null,
        string result = "Success",
        string? details = null,
        string? ipAddress = null);

    Task<List<AuditLog>> GetAuditLogsAsync(
        string? userId = null,
        string? module = null,
        string? action = null,
        string? entityName = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? result = null,
        int page = 1,
        int pageSize = 20);

    Task<int> GetAuditLogsCountAsync(
        string? userId = null,
        string? module = null,
        string? action = null,
        string? entityName = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? result = null);
}
