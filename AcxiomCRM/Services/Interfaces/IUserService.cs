using System.Security.Claims;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.User;

namespace AcxiomCRM.Services.Interfaces;

public interface IUserService
{
    Task<List<UserListViewModel>> GetAllUsersAsync();
    Task<ApplicationUser?> GetUserByIdAsync(string id);
    Task<string> GetUserRoleAsync(ApplicationUser user);
    Task<ServiceResult<ApplicationUser>> CreateUserAsync(CreateUserViewModel model, ClaimsPrincipal adminUser);
    Task<ServiceResult<ApplicationUser>> UpdateUserAsync(EditUserViewModel model, ClaimsPrincipal adminUser);
    Task<ServiceResult> ToggleLockoutAsync(string id, ClaimsPrincipal adminUser);
    Task<ServiceResult> ResetPasswordAsync(ResetUserPasswordViewModel model, ClaimsPrincipal adminUser);
    Task<ServiceResult> ToggleStatusAsync(string id, ClaimsPrincipal adminUser);
}
