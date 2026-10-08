using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AuditService> _logger;

    public AuditService(ApplicationDbContext db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(
        string? userId,
        string action,
        string module,
        string entityName,
        string? recordId = null,
        string? oldValue = null,
        string? newValue = null,
        string result = "Success",
        string? details = null,
        string? ipAddress = null)
    {
        try
        {
            var audit = new AuditLog
            {
                UserId = userId,
                Action = action,
                Module = module,
                EntityName = entityName,
                RecordId = recordId,
                OldValue = Sanitize(oldValue),
                NewValue = Sanitize(newValue),
                Result = result,
                Details = Sanitize(details),
                IpAddress = ipAddress,
                CreatedDate = DateTime.UtcNow
            };

            _db.AuditLogs.Add(audit);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record audit log for Action: {Action}, Module: {Module}", action, module);
        }
    }

    private static string? Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        // Strip sensitive substrings if present (safety defense)
        if (text.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("secret", StringComparison.OrdinalIgnoreCase))
        {
            return "[REDACTED]";
        }
        return text.Length > 1000 ? text[..1000] : text;
    }

    public async Task<List<AuditLog>> GetAuditLogsAsync(
        string? userId = null,
        string? module = null,
        string? action = null,
        string? entityName = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? result = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = FilterAuditLogs(userId, module, action, entityName, startDate, endDate, result);
        return await query
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetAuditLogsCountAsync(
        string? userId = null,
        string? module = null,
        string? action = null,
        string? entityName = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? result = null)
    {
        var query = FilterAuditLogs(userId, module, action, entityName, startDate, endDate, result);
        return await query.CountAsync();
    }

    private IQueryable<AuditLog> FilterAuditLogs(
        string? userId,
        string? module,
        string? action,
        string? entityName,
        DateTime? startDate,
        DateTime? endDate,
        string? result)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(a => a.UserId == userId);

        if (!string.IsNullOrWhiteSpace(module))
            query = query.Where(a => a.Module == module);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (!string.IsNullOrWhiteSpace(entityName))
            query = query.Where(a => a.EntityName == entityName);

        if (!string.IsNullOrWhiteSpace(result))
            query = query.Where(a => a.Result == result);

        if (startDate.HasValue)
            query = query.Where(a => a.CreatedDate >= startDate.Value.ToUniversalTime());

        if (endDate.HasValue)
            query = query.Where(a => a.CreatedDate <= endDate.Value.ToUniversalTime());

        return query;
    }
}
