using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RaporDefteri.Application;
using RaporDefteri.Infrastructure;

namespace RaporDefteri.Web;

public abstract class AppController : Controller
{
    protected Actor CurrentActor => new(
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value),
        User.Identity?.Name ?? string.Empty,
        User.IsInRole(AppRoles.Operator),
        User.IsInRole(AppRoles.ShiftSupervisor),
        User.IsInRole(AppRoles.Administrator));

    protected void AddErrors<T>(OperationResult<T> result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error);
    }
}

public static class FormTime
{
    public static bool TryParse(string? text, IInstitutionClock clock, out DateTimeOffset value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (!DateTime.TryParseExact(text.Trim(), "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return false;
        value = clock.FromLocal(local);
        return true;
    }

    public static string Input(IInstitutionClock clock, DateTimeOffset? value = null) =>
        clock.ToLocal(value ?? clock.UtcNow).ToString("yyyy-MM-ddTHH:mm");
}

public sealed class GirisInput
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class KurulumInput
{
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

[AllowAnonymous]
[Route("giris")]
public sealed class GirisController(SignInManager<AppUser> signIn, UserManager<AppUser> users) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpPost("")]
    public async Task<IActionResult> Index(GirisInput input)
    {
        var user = await users.FindByNameAsync(input.UserName);
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Giriş yapılamadı.");
            return View(input);
        }

        var result = await signIn.PasswordSignInAsync(user, input.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
            return Redirect("/");
        ModelState.AddModelError(string.Empty, result.IsLockedOut ? "Hesap geçici olarak kilitlendi." : "Giriş yapılamadı.");
        return View(input);
    }
}

[Authorize]
[Route("cikis")]
public sealed class CikisController(SignInManager<AppUser> signIn) : Controller
{
    [HttpPost("")]
    public async Task<IActionResult> Index()
    {
        await signIn.SignOutAsync();
        return Redirect("/giris");
    }
}

[AllowAnonymous]
[Route("kurulum")]
public sealed class KurulumController(IUserAdmin admin) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (await admin.AnyUserAsync(ct))
            return NotFound();
        return View(new KurulumInput());
    }

    [HttpPost("")]
    public async Task<IActionResult> Index(KurulumInput input, CancellationToken ct)
    {
        if (await admin.AnyUserAsync(ct))
            return NotFound();
        var result = await admin.CreateAsync(input.UserName, input.DisplayName, input.Password, [AppRoles.Administrator], ct);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);
            return View(input);
        }

        return Redirect("/giris");
    }
}

[AllowAnonymous]
[Route("erisim-yok")]
public sealed class ErisimController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}

[AllowAnonymous]
[Route("hata")]
public sealed class HataController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        var error = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        if (DatabaseExceptionClassifier.IsConnectivity(error))
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("Veritabani");
        }

        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("Genel");
    }
}
