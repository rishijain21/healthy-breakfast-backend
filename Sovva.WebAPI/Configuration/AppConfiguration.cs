namespace Sovva.WebAPI.Configuration;

public class SupabaseOptions
{
    public const string Section = "Supabase";
    public string Url { get; set; } = "";
    public string AnonKey { get; set; } = "";
    public string ServiceRoleKey { get; set; } = "";
    public string StorageUrl { get; set; } = "";
}

public class CorsOptions
{
    public const string Section = "Cors";
    public string[] AllowedOrigins { get; set; } = [];
    public string[] AllowedVercelSlugs { get; set; } = [];
}

public class HangfireOptions
{
    public const string Section = "HangfireDashboard";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class DatabaseOptions
{
    public const string Section = "Database";
    public int MaxPoolSize { get; set; } = 10;
    public int MinPoolSize { get; set; } = 0;
    public int ConnectionIdleLifetime { get; set; } = 60;
    public int Keepalive { get; set; } = 30;
    public int CommandTimeout { get; set; } = 30;
    public int MaxRetryCount { get; set; } = 3;
}