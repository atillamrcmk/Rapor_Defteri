using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RaporDefteri.Application;

namespace RaporDefteri.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRaporDefteri(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connection = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connection))
            connection = "Host=127.0.0.1;Port=5432;Database=rapor_defteri;Username=rapor_app;Password=missing";
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connection));
        services.AddIdentity<AppUser, AppRole>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var requireHttps = !environment.IsDevelopment() || configuration.GetValue("Security:RequireHttps", false);
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/giris";
            options.AccessDeniedPath = "/erisim-yok";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(configuration.GetValue("Security:SessionMinutes", 30));
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            options.Events.OnValidatePrincipal = async context =>
            {
                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                var user = await userManager.GetUserAsync(context.Principal!);
                if (user is null || !user.IsActive)
                    context.RejectPrincipal();
            };
        });

        var keys = configuration["DataProtection:KeyPath"] ?? "data-protection-keys";
        Directory.CreateDirectory(keys);
        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys)).SetApplicationName("RaporDefteri");

        var zone = InstitutionClock.Resolve(configuration["Institution:TimeZone"]);
        services.AddSingleton<IInstitutionClock>(new InstitutionClock(zone));
        services.AddSingleton<IInstitutionProfile>(new InstitutionProfile(
            configuration["Institution:Name"] ?? "Kurum adı yapılandırmadan girilir",
            configuration["Institution:UnitName"] ?? "Merkez Kontrol"));
        services.AddScoped<IRaporStore, RaporStore>();
        services.AddScoped<IUserAdmin, UserAdmin>();
        services.AddScoped<IDemoData, DemoData>();
        services.AddScoped<MovementService>();
        services.AddScoped<InmateService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<VehicleService>();
        services.AddScoped<RecordService>();
        services.AddScoped<LedgerService>();
        services.AddScoped<ReportService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}

public sealed class AppDbContextFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Migrator")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=127.0.0.1;Port=5433;Database=rapor_defteri;Username=rapor_migrator;Password=missing";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", "rapor")).Options;
        return new AppDbContext(options);
    }
}
