using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class DataFiles
{
    // مسار مجلد Data الرئيسي
    public static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");

    public static readonly string InvoicesDirectory = Path.Combine(DataDirectory, "invoices");
    public static readonly string ReportsDirectory = Path.Combine(DataDirectory, "reports");

    // أسماء ملفات الـ JSON الثابتة
    public const string Customers = "customers.json";
    public const string Products = "products.json";
    public const string Coupons = "coupons.json";
    public const string IncomingOrders = "incoming_orders.json";
    public const string Orders = "orders.json";
    public const string Payments = "payments.json";
    public const string Emails = "emails.json";
    public const string EmailRetry = "email_retry.json";

    // دالة لإنشاء الفولدرات لو مش موجودة
    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(InvoicesDirectory);
        Directory.CreateDirectory(ReportsDirectory);
    }
}


public static class JsonConfig
{
    public static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
}