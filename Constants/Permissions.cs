namespace DulceAtardecer.Constants;

public static class Permissions
{
    public const string SalesCreate = $"{RoleNames.Admin},{RoleNames.Cashier}";
    public const string SalesCharge = $"{RoleNames.Admin},{RoleNames.Cashier}";
    public const string SalesCancelUnpaid = $"{RoleNames.Admin},{RoleNames.Cashier}";
    public const string SalesCancelPaid = RoleNames.Admin;
    public const string OrdersViewPending = $"{RoleNames.Admin},{RoleNames.Cashier},{RoleNames.Kitchen}";
    public const string ReportsView = RoleNames.Admin;
}
