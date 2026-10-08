using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs.Auth;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api;

[Route("api/auth")]
public class AuthApiController : BaseApiController
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuthApiController> _logger;

    public AuthApiController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        ILogger<AuthApiController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto model)
    {
        if (!ModelState.IsValid)
        {
            return ApiBadRequest("Invalid request payload.");
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null || !user.IsActive)
        {
            await _auditService.LogAsync(
                null,
                "FailedLogin",
                "Auth",
                "User",
                model.Email,
                null,
                null,
                "Failed",
                $"Failed API login attempt for email {model.Email}: User not found or inactive.",
                ip);

            return ApiUnauthorized("Invalid email or password.");
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginDate = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var roles = await _userManager.GetRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? "SalesExecutive";

            await _auditService.LogAsync(
                user.Id,
                "Login",
                "Auth",
                "User",
                user.Id,
                null,
                null,
                "Success",
                $"API Login successful for {user.Email}",
                ip);

            return ApiSuccess(new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Role = primaryRole
            }, "Login successful.");
        }

        if (result.IsLockedOut)
        {
            await _auditService.LogAsync(
                user.Id,
                "Lockout",
                "Auth",
                "User",
                user.Id,
                null,
                null,
                "Failed",
                $"API Account locked out due to multiple failed login attempts for {user.Email}",
                ip);

            return StatusCode(StatusCodes.Status423Locked,
                new { success = false, message = "Account is locked due to repeated failed login attempts. Please try again later.", traceId = CurrentTraceId });
        }

        await _auditService.LogAsync(
            user.Id,
            "FailedLogin",
            "Auth",
            "User",
            user.Id,
            null,
            null,
            "Failed",
            $"API Failed login attempt (incorrect password) for {user.Email}",
            ip);

        return ApiUnauthorized("Invalid email or password.");
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        await _signInManager.SignOutAsync();

        await _auditService.LogAsync(
            userId,
            "Logout",
            "Auth",
            "User",
            userId,
            null,
            null,
            "Success",
            "API Logout successful",
            ip);

        return ApiSuccess(new { }, "Logged out successfully.");
    }
}
