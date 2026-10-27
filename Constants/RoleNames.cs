namespace DulceAtardecer.Constants;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Cashier = "Cashier";
    public const string Kitchen = "Kitchen";

    public static readonly IReadOnlyList<string> All = [Admin, Cashier, Kitchen];
}
