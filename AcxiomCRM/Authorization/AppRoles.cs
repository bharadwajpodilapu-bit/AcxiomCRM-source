namespace AcxiomCRM.Authorization;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string SalesExecutive = "SalesExecutive";

    public static readonly IReadOnlyList<string> AllRoles = new[] { Admin, Manager, SalesExecutive };
}
