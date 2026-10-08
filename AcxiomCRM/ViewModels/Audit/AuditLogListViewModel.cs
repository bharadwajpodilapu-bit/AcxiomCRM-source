using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Common;

namespace AcxiomCRM.ViewModels.Audit;

public class AuditLogListViewModel
{
    public PagedResult<AuditLog> Logs { get; set; } = new();
    public string? UserId { get; set; }
    public string? Module { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Result { get; set; }
    public List<SelectListItem> UsersList { get; set; } = new();
}
