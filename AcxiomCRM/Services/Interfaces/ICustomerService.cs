using System.Security.Claims;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Common;

namespace AcxiomCRM.Services.Interfaces;

public interface ICustomerService
{
    Task<PagedResult<Customer>> GetCustomersAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? status = null,
        string? ownerId = null,
        string? sortBy = "CreatedDate",
        bool sortDesc = true,
        int pageIndex = 1,
        int pageSize = 10);

    Task<Customer?> GetCustomerByIdAsync(int id, ClaimsPrincipal user, bool includeRelated = false);
    Task<bool> CustomerExistsAsync(int id);
    Task<ServiceResult<Customer>> CreateCustomerAsync(Customer customer, ClaimsPrincipal user);
    Task<ServiceResult<Customer>> UpdateCustomerAsync(Customer customer, ClaimsPrincipal user);
    Task<ServiceResult> DeactivateCustomerAsync(int id, ClaimsPrincipal user);
    Task<ServiceResult> DeleteCustomerAsync(int id, ClaimsPrincipal user);
}
