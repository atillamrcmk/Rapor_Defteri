using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaporDefteri.Application;
using RaporDefteri.Domain;

namespace RaporDefteri.Web;

[Authorize(Policy = Policies.ViewRecords)]
public sealed class HomeController(DashboardService dashboard, IInstitutionClock clock) : AppController
{
    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Clock"] = clock;
        return View(await dashboard.LoadAsync(ct));
    }
}

[Authorize(Policy = Policies.ViewRecords)]
[Route("vardiya")]
public sealed class VardiyaController(ShiftService shifts, IRaporStore store, IInstitutionClock clock) : AppController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Clock"] = clock;
        ViewData["Active"] = await shifts.SummaryAsync(null, ct);
        return View(await shifts.RecentAsync(ct));
    }

    [Authorize(Policy = Policies.ManageShift)]
    [HttpGet("ac")]
    public async Task<IActionResult> Ac(CancellationToken ct)
    {
        ViewData["Users"] = await store.ActiveUsersAsync(ct);
        ViewData["Clock"] = clock;
        return View(new OpenShiftInput());
    }

    [Authorize(Policy = Policies.ManageShift)]
    [HttpPost("ac")]
    public async Task<IActionResult> Ac(OpenShiftInput input, CancellationToken ct)
    {
        ViewData["Users"] = await store.ActiveUsersAsync(ct);
        ViewData["Clock"] = clock;
        if (!FormTime.TryParse(input.StartsAt, clock, out var start) || !FormTime.TryParse(input.EndsAt, clock, out var end))
        {
            ModelState.AddModelError(string.Empty, "Başlangıç ve bitiş saatini girin.");
            return View(input);
        }

        var result = await shifts.OpenAsync(new OpenShiftCommand(start, end, input.DutyUserIds), CurrentActor, ct);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View(input);
        }

        return Redirect("/vardiya");
    }

    [HttpGet("devir")]
    public async Task<IActionResult> Devir(CancellationToken ct)
    {
        ViewData["Clock"] = clock;
        return View(await shifts.HandoverAsync(ct));
    }

    [Authorize(Policy = Policies.ManageShift)]
    [HttpPost("hazirla")]
    public async Task<IActionResult> Hazirla(string? note, CancellationToken ct)
    {
        var result = await shifts.PrepareAsync(note, CurrentActor, ct);
        if (!result.Succeeded)
            TempData["Error"] = string.Join(" ", result.Errors);
        return Redirect("/vardiya/devir");
    }

    [Authorize(Policy = Policies.ManageShift)]
    [HttpPost("devral")]
    public async Task<IActionResult> Devral(string? closeNote, CancellationToken ct)
    {
        var result = await shifts.AcceptAsync(closeNote, CurrentActor, ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors);
            return Redirect("/vardiya/devir");
        }

        return Redirect($"/raporlar/vardiya/{result.Value}?kapanis=true");
    }
}

public sealed class OpenShiftInput
{
    public string StartsAt { get; set; } = string.Empty;
    public string EndsAt { get; set; } = string.Empty;
    public List<Guid> DutyUserIds { get; set; } = [];
}

[Authorize(Policy = Policies.ViewRecords)]
[Route("kisiler")]
public sealed class KisilerController(InmateService inmates, IRaporStore store, IInstitutionClock clock) : AppController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        ViewData["Query"] = q;
        return View(await inmates.SearchAsync(q, 50, ct));
    }

    [HttpGet("oner")]
    public async Task<IActionResult> Oner(string? q, CancellationToken ct) =>
        Json((await inmates.SearchAsync(q, 15, ct)).Select(row => new { row.Id, row.FileNumber, row.FullName, row.Ward, row.Presence }));

    [Authorize(Policy = Policies.CreateRecords)]
    [HttpGet("yeni")]
    public IActionResult Yeni() => View(new AdmissionInput { EventAt = FormTime.Input(clock), IdempotencyKey = Guid.NewGuid().ToString("N") });

    [Authorize(Policy = Policies.CreateRecords)]
    [HttpPost("yeni")]
    public async Task<IActionResult> Yeni(AdmissionInput input, string? step, CancellationToken ct)
    {
        if (!FormTime.TryParse(input.EventAt, clock, out var when))
        {
            ModelState.AddModelError(string.Empty, "Olay saati zorunludur.");
            return View(input);
        }

        var command = new AdmissionCommand(when, input.LateReason, input.AdditionalNote, input.FileNumber, input.FirstName, input.LastName, input.Ward, input.ExistingInmateId, input.IdempotencyKey);
        if (step == "kaydet")
        {
            var saved = await inmates.AdmitAsync(command, CurrentActor, ct);
            if (!saved.Succeeded)
            {
                AddErrors(saved);
                return View(input);
            }

            return Redirect($"/defter/{saved.Value}");
        }

        var preview = await inmates.PreviewAdmissionAsync(command, ct);
        if (!preview.Succeeded)
            AddErrors(preview);
        input.PreviewText = preview.Value;
        return View(input);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detay(Guid id, DateOnly? from, DateOnly? to, string? kind, string? destination, CancellationToken ct)
    {
        var inmate = await store.InmateAsync(id, ct);
        if (inmate is null)
            return NotFound();
        ViewData["Clock"] = clock;
        ViewData["History"] = await inmates.HistoryAsync(id, from, to, kind, destination, ct);
        ViewData["From"] = from?.ToString("yyyy-MM-dd");
        ViewData["To"] = to?.ToString("yyyy-MM-dd");
        ViewData["Kind"] = kind;
        ViewData["Destination"] = destination;
        return View(inmate);
    }

    [Authorize(Policy = Policies.CreateRecords)]
    [HttpPost("{id:guid}/tahliye")]
    public async Task<IActionResult> Tahliye(Guid id, string eventAt, string? reason, string? lateReason, string? additionalNote, string idempotencyKey, CancellationToken ct)
    {
        if (!FormTime.TryParse(eventAt, clock, out var when))
        {
            TempData["Error"] = "Olay saati zorunludur. Tahliye yazılmadı.";
            return Redirect($"/kisiler/{id}");
        }

        var result = await inmates.ReleaseAsync(new ReleaseCommand(when, lateReason, additionalNote, id, reason, idempotencyKey), CurrentActor, ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors);
            return Redirect($"/kisiler/{id}");
        }

        return Redirect($"/defter/{result.Value}");
    }

    [Authorize(Policy = Policies.CreateRecords)]
    [HttpPost("{id:guid}/sevk")]
    public async Task<IActionResult> Sevk(Guid id, string eventAt, string destination, string? reason, string? lateReason, string? additionalNote, string idempotencyKey, CancellationToken ct)
    {
        if (!FormTime.TryParse(eventAt, clock, out var when))
        {
            TempData["Error"] = "Olay saati zorunludur. Sevk yazılmadı.";
            return Redirect($"/kisiler/{id}");
        }

        var result = await inmates.TransferAsync(new TransferCommand(when, lateReason, additionalNote, id, destination, reason, idempotencyKey), CurrentActor, ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors);
            return Redirect($"/kisiler/{id}");
        }

        return Redirect($"/defter/{result.Value}");
    }
}

public sealed class AdmissionInput
{
    public string EventAt { get; set; } = string.Empty;
    public string? LateReason { get; set; }
    public string? AdditionalNote { get; set; }
    public string FileNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public Guid? ExistingInmateId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? PreviewText { get; set; }
}
