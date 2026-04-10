namespace backend.Authorization;

public static class AppRoles
{
    public const string User = "User";
    public const string Admin = "Admin";

    public static bool IsValid(string role)
    {
        return role == User || role == Admin;
    }
}
