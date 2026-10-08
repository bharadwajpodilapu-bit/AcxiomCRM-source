using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Middleware;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.Services.Implementations;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.Validators;

var builder = WebApplication.CreateBuilder(args);

// Database configuration
var provider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
var connectionString = provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
    ? builder.Configuration.GetConnectionString("SqlServerConnection")
    : builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// ASP.NET Core Identity configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password policy
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;

    // Lockout settings
    var maxFailed = builder.Configuration.GetValue<int>("IdentitySettings:Lockout:MaxFailedAccessAttempts", 5);
    var lockoutMinutes = builder.Configuration.GetValue<int>("IdentitySettings:Lockout:DefaultLockoutTimeSpanMinutes", 15);

    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = maxFailed;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(lockoutMinutes);

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Application Cookie configuration
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Enforce HTTPS-only cookie transmission
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// Role-based Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppPolicies.CanManageUsers, policy => policy.RequireRole(AppRoles.Admin));
    options.AddPolicy(AppPolicies.CanViewAuditLogs, policy => policy.RequireRole(AppRoles.Admin));
    options.AddPolicy(AppPolicies.CanViewTeamReports, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Manager));
    options.AddPolicy(AppPolicies.CanManageCRM, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Manager, AppRoles.SalesExecutive));
});

// Dependency Injection - CRM Business Layer & Repositories/Services
builder.Services.AddScoped<ICrmBusinessValidator, CrmBusinessValidator>();
builder.Services.AddScoped<IResourceAuthorizationService, ResourceAuthorizationService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ILeadService, LeadService>();
builder.Services.AddScoped<IOpportunityService, OpportunityService>();
builder.Services.AddScoped<IFollowUpService, FollowUpService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddControllersWithViews(options =>
{
    // Belt-and-suspenders: validate CSRF token on all non-GET MVC requests globally.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var config = services.GetRequiredService<IConfiguration>();

        await DbInitializer.InitializeAsync(context, userManager, roleManager, config);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database migration and initialization.");
    }
}

// Middleware Pipeline
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

// For test project accessibility
public partial class Program { }
