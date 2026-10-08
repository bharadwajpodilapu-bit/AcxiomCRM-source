using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels.User;

namespace AcxiomCRM.Services.Implementations;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditService auditService,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<List<UserListViewModel>> GetAllUsersAsync()
    {
        var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
        var list = new List<UserListViewModel>();

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var isLocked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow;
            list.Add(new UserListViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "None",
                IsActive = u.IsActive,
                IsLockedOut = isLocked,
                CreatedDate = u.CreatedDate,
                LastLoginDate = u.LastLoginDate
            });
        }

        return list;
    }

    public async Task<ApplicationUser?> GetUserByIdAsync(string id)
    {
        return await _userManager.FindByIdAsync(id);
    }

    public async Task<string> GetUserRoleAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return roles.FirstOrDefault() ?? AppRoles.SalesExecutive;
    }

    public async Task<ServiceResult<ApplicationUser>> CreateUserAsync(CreateUserViewModel model, ClaimsPrincipal adminUser)
    {
        var adminId = adminUser.FindFirstValue(ClaimTypes.NameIdentifier);

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing != null)
        {
            return ServiceResult<ApplicationUser>.Fail("A user with this email address already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            var errors = new Dictionary<string, List<string>>();
            foreach (var err in result.Errors)
            {
                if (!errors.ContainsKey("Password")) errors["Password"] = new List<string>();
                errors["Password"].Add(err.Description);
            }
            return ServiceResult<ApplicationUser>.Fail("Failed to create user.", errors);
        }

        if (await _roleManager.RoleExistsAsync(model.Role))
        {
            await _userManager.AddToRoleAsync(user, model.Role);
        }

        await _auditService.LogAsync(
            adminId,
            "Create",
            "User",
            nameof(ApplicationUser),
            user.Id,
            null,
            JsonSerializer.Serialize(new { user.Id, user.FullName, user.Email, model.Role }),
            "Success",
            $"Created user {user.FullName} with role {model.Role}");

        return ServiceResult<ApplicationUser>.Ok(user, "User created successfully.");
    }

    public async Task<ServiceResult<ApplicationUser>> UpdateUserAsync(EditUserViewModel model, ClaimsPrincipal adminUser)
    {
        var adminId = adminUser.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
        {
            return ServiceResult<ApplicationUser>.Fail("User not found.");
        }

        var oldRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "None";
        var oldSnapshot = JsonSerializer.Serialize(new { user.FullName, user.Email, user.IsActive, Role = oldRole });

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.IsActive = model.IsActive;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = new Dictionary<string, List<string>>();
            foreach (var err in updateResult.Errors)
            {
                if (!errors.ContainsKey("General")) errors["General"] = new List<string>();
                errors["General"].Add(err.Description);
            }
            return ServiceResult<ApplicationUser>.Fail("Failed to update user.", errors);
        }

        if (oldRole != model.Role && await _roleManager.RoleExistsAsync(model.Role))
        {
            if (oldRole != "None")
            {
                await _userManager.RemoveFromRoleAsync(user, oldRole);
            }
            await _userManager.AddToRoleAsync(user, model.Role);

            await _auditService.LogAsync(
                adminId,
                "RoleChange",
                "User",
                nameof(ApplicationUser),
                user.Id,
                oldRole,
                model.Role,
                "Success",
                $"Changed role of user {user.FullName} from {oldRole} to {model.Role}");
        }

        var newSnapshot = JsonSerializer.Serialize(new { user.FullName, user.Email, user.IsActive, Role = model.Role });

        await _auditService.LogAsync(
            adminId,
            "Update",
            "User",
            nameof(ApplicationUser),
            user.Id,
            oldSnapshot,
            newSnapshot,
            "Success",
            $"Updated user {user.FullName}");

        return ServiceResult<ApplicationUser>.Ok(user, "User updated successfully.");
    }

    public async Task<ServiceResult> ToggleLockoutAsync(string id, ClaimsPrincipal adminUser)
    {
        var adminId = adminUser.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return ServiceResult.Fail("User not found.");
        }

        var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
        if (isLocked)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            await _auditService.LogAsync(
                adminId,
                "Unlock",
                "Security",
                nameof(ApplicationUser),
                user.Id,
                "Locked",
                "Unlocked",
                "Success",
                $"Admin unlocked user {user.FullName}");

            return ServiceResult.Ok("User unlocked successfully.");
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));

            await _auditService.LogAsync(
                adminId,
                "Lockout",
                "Security",
                nameof(ApplicationUser),
                user.Id,
                "Unlocked",
                "Locked",
                "Success",
                $"Admin locked user {user.FullName}");

            return ServiceResult.Ok("User locked out successfully.");
        }
    }

    public async Task<ServiceResult> ResetPasswordAsync(ResetUserPasswordViewModel model, ClaimsPrincipal adminUser)
    {
        var adminId = adminUser.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
        {
            return ServiceResult.Fail("User not found.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            var errors = new Dictionary<string, List<string>>();
            foreach (var err in result.Errors)
            {
                if (!errors.ContainsKey("NewPassword")) errors["NewPassword"] = new List<string>();
                errors["NewPassword"].Add(err.Description);
            }
            return ServiceResult.Fail("Failed to reset password.", errors);
        }

        await _auditService.LogAsync(
            adminId,
            "PasswordReset",
            "Security",
            nameof(ApplicationUser),
            user.Id,
            null,
            null,
            "Success",
            $"Admin reset password for user {user.FullName}");

        return ServiceResult.Ok("Password reset successfully.");
    }

    public async Task<ServiceResult> ToggleStatusAsync(string id, ClaimsPrincipal adminUser)
    {
        var adminId = adminUser.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return ServiceResult.Fail("User not found.");
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);

        await _auditService.LogAsync(
            adminId,
            user.IsActive ? "Activate" : "Deactivate",
            "User",
            nameof(ApplicationUser),
            user.Id,
            (!user.IsActive).ToString(),
            user.IsActive.ToString(),
            "Success",
            $"User {user.FullName} was {(user.IsActive ? "activated" : "deactivated")}");

        return ServiceResult.Ok($"User {(user.IsActive ? "activated" : "deactivated")} successfully.");
    }
}
