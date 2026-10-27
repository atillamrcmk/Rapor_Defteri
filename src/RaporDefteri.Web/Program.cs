using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using RaporDefteri.Application;
using RaporDefteri.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Services.AddRaporDefteri(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy(Policies.ViewRecords, policy => policy.RequireRole(AppRoles.Operator, AppRoles.ShiftSupervisor, AppRoles.Administrator, AppRoles.ReadOnly));
    options.AddPolicy(Policies.CreateRecords, policy => policy.RequireRole(AppRoles.Operator, AppRoles.ShiftSupervisor));
    options.AddPolicy(Policies.ManageShift, policy => policy.RequireRole(AppRoles.ShiftSupervisor));
    options.AddPolicy(Policies.CorrectRecords, policy => policy.RequireRole(AppRoles.Operator, AppRoles.ShiftSupervisor));
    options.AddPolicy(Policies.ManageUsers, policy => policy.RequireRole(AppRoles.Administrator));
    options.AddPolicy(Policies.ManageDefinitions, policy => policy.RequireRole(AppRoles.Administrator));
});
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
var turkish = new CultureInfo("tr-TR");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(turkish);
    options.SupportedCultures = [turkish];
    options.SupportedUICultures = [turkish];
});

var app = builder.Build();
if (args.Contains("--demo"))
{
    if (!app.Environment.IsDevelopment())
    {
        Console.Error.WriteLine("Demo veri yalnızca Development ortamında ve açık komutla yüklenir.");
        return;
    }

    await using var scope = app.Services.CreateAsyncScope();
    await RoleSeeder.EnsureAsync(scope.ServiceProvider, CancellationToken.None);
    Console.WriteLine(await scope.ServiceProvider.GetRequiredService<IDemoData>().SeedAsync(CancellationToken.None));
    return;
}

app.UseExceptionHandler("/hata");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRequestLocalization();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/hata") || context.Request.Path.StartsWithSegments("/erisim-yok"))
    {
        await next();
        return;
    }

    await RoleSeeder.EnsureAsync(context.RequestServices, context.RequestAborted);
    await next();
});
app.MapControllers();
app.Run();

public partial class Program;
