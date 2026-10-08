using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs.Customer;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers.Api;

[Route("api/customers")]
[Authorize]
public class CustomersApiController : BaseApiController
{
    private readonly ICustomerService _customerService;

    public CustomersApiController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? ownerId,
        [FromQuery] string? sortBy = "CreatedDate",
        [FromQuery] bool sortDesc = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _customerService.GetCustomersAsync(
            User, search, status, ownerId, sortBy, sortDesc, page, pageSize);

        var dtoList = result.Items.Select(MapToDto).ToList();

        return ApiSuccess(new
        {
            items = dtoList,
            pageIndex = result.PageIndex,
            pageSize = result.PageSize,
            totalCount = result.TotalCount,
            totalPages = result.TotalPages,
            hasPreviousPage = result.HasPreviousPage,
            hasNextPage = result.HasNextPage
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCustomer(int id)
    {
        var customer = await _customerService.GetCustomerByIdAsync(id, User, includeRelated: false);
        if (customer == null)
        {
            if (await _customerService.CustomerExistsAsync(id))
            {
                return ApiForbidden("You do not have permission to view this customer.");
            }
            return ApiNotFound($"Customer with ID {id} not found.");
        }

        return ApiSuccess(MapToDto(customer));
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ApiBadRequest("Validation failed.", GetModelErrors());
        }

        var customer = new Customer
        {
            CustomerName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            OwnerId = dto.OwnerId ?? string.Empty
        };

        var result = await _customerService.CreateCustomerAsync(customer, User);
        if (!result.Success)
        {
            return ApiValidationError(result.Errors, result.Message);
        }

        return ApiCreated($"/api/customers/{result.Data!.CustomerId}", MapToDto(result.Data), result.Message);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ApiBadRequest("Validation failed.", GetModelErrors());
        }

        if (!await _customerService.CustomerExistsAsync(id))
        {
            return ApiNotFound($"Customer with ID {id} not found.");
        }

        var customer = new Customer
        {
            CustomerId = id,
            CustomerName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Status = dto.Status,
            OwnerId = dto.OwnerId ?? string.Empty
        };

        var result = await _customerService.UpdateCustomerAsync(customer, User);
        if (result.IsUnauthorized)
        {
            return ApiForbidden(result.Message);
        }
        if (result.IsNotFound)
        {
            return ApiNotFound(result.Message);
        }
        if (!result.Success)
        {
            return ApiValidationError(result.Errors, result.Message);
        }

        return ApiSuccess(MapToDto(result.Data!), result.Message);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var result = await _customerService.DeleteCustomerAsync(id, User);
        if (result.IsUnauthorized)
        {
            return ApiForbidden(result.Message);
        }
        if (result.IsNotFound)
        {
            return ApiNotFound(result.Message);
        }
        if (!result.Success)
        {
            return ApiBadRequest(result.Message);
        }

        return ApiSuccess(new { id }, result.Message);
    }

    private static CustomerDto MapToDto(Customer c) => new()
    {
        CustomerId = c.CustomerId,
        CustomerCode = c.CustomerCode,
        CustomerName = c.CustomerName,
        Email = c.Email,
        Phone = c.Phone,
        CompanyName = c.CompanyName,
        Address = c.Address,
        City = c.City,
        State = c.State,
        Status = c.Status,
        OwnerId = c.OwnerId,
        OwnerName = c.Owner?.FullName,
        CreatedDate = c.CreatedDate,
        IsActive = c.IsActive
    };

    private Dictionary<string, List<string>> GetModelErrors()
    {
        var dict = new Dictionary<string, List<string>>();
        foreach (var (key, val) in ModelState)
        {
            if (val.Errors.Any())
            {
                dict[key] = val.Errors.Select(e => e.ErrorMessage).ToList();
            }
        }
        return dict;
    }
}
