using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaporDefteri.Application;
using RaporDefteri.Domain;

namespace RaporDefteri.Web;

[Authorize(Policy = Policies.ViewRecords)]
[Route("araclar")]
public sealed class AraclarController(VehicleService vehicles, IRaporStore store, IInstitutionClock clock) : AppController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? plaka, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        ViewData["Clock"] = clock;
        ViewData["Plate"] = plaka;
        ViewData["Open"] = await store.OpenVisitsAsync(ct);
        if (string.IsNullOrWhiteSpace(plaka))
            return View(Array.Empty<VehicleVisitRow>());
        DateTimeOffset? start = from is null ? null : ReportPeriod.DayBounds(from.Value, clock.Zone).Start;
        DateTimeOffset? end = to is null ? null : ReportPeriod.DayBounds(to.Value, clock.Zone).End;
        return View(await store.VisitsByPlateAsync(TurkishText.NormalizePlate(plaka), start, end, ct));
    }

    [Authorize(Policy = Policies.CreateRecords)]
    [HttpGet("gecis")]
    public async Task<IActionResult> Gecis(CancellationToken ct)
    {
        ViewData["Types"] = await vehicles.TypesAsync(true, ct);
        return View(new VehicleInput { EventAt = FormTime.Input(clock), IdempotencyKey = Guid.NewGuid().ToString("N") });
    }

    [Authorize(Policy = Policies.CreateRecords)]
    [HttpPost("gecis")]
    public async Task<IActionResult> Gecis(VehicleInput input, string? step, CancellationToken ct)
    {
        ViewData["Types"] = await vehicles.TypesAsync(true, ct);
        if (!FormTime.TryParse(input.EventAt, clock, out var when))
        {
            ModelState.AddModelError(string.Empty, "Olay saati zorunludur.");
            return View(input);
        }

        var command = new VehicleCommand(when, input.LateReason, input.AdditionalNote, input.Plate, input.Location, input.VehicleTypeId, input.Direction, input.Purpose, input.Driver, input.Organization, input.LinkAbsenceIds, input.IdempotencyKey);
        if (step == "kaydet")
        {
            var saved = await vehicles.RecordAsync(command, CurrentActor, ct);
            if (!saved.Succeeded)
            {
                AddErrors(saved);
                return View(input);
            }

            return Redirect($"/defter/{saved.Value}");
        }

        var preview = await vehicles.PreviewAsync(command, ct);
        if (!preview.Succeeded)
            AddErrors(preview);
        input.PreviewText = preview.Value;
        return View(input);
    }
}

public sealed class VehicleInput
{
    public string EventAt { get; set; } = string.Empty;
    public string? LateReason { get; set; }
    public string? AdditionalNote { get; set; }
    public string Plate { get; set; } = string.Empty;
    public VehicleBaseLocation Location { get; set; }
    public Guid VehicleTypeId { get; set; }
    public PassageDirection Direction { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string? Driver { get; set; }
    public string? Organization { get; set; }
    public List<Guid> LinkAbsenceIds { get; set; } = [];
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? PreviewText { get; set; }
}

[Authorize(Policy = Policies.CreateRecords)]
[Route("kayit")]
public sealed class KayitController(RecordService records, IInstitutionClock clock) : AppController
{
    [HttpGet("{type}")]
    public IActionResult Yeni(string type)
    {
        if (!TryMap(type, out var entryType))
            return NotFound();
        return View(new SimpleInput { Type = entryType, EventAt = FormTime.Input(clock), IdempotencyKey = Guid.NewGuid().ToString("N") });
    }

    [HttpPost("{type}")]
    public async Task<IActionResult> Yeni(string type, SimpleInput input, string? step, CancellationToken ct)
    {
        if (!TryMap(type, out var entryType))
            return NotFound();
        input.Type = entryType;
        if (!FormTime.TryParse(input.EventAt, clock, out var when))
        {
            ModelState.AddModelError(string.Empty, "Olay saati zorunludur.");
            return View(input);
        }

        var command = new SimpleRecordCommand(when, input.LateReason, input.AdditionalNote, entryType, input.Title, input.Body, input.Organization, input.Counterparty, input.MaterialDirection, input.FollowUp, input.IdempotencyKey);
        if (step == "kaydet")
        {
            var saved = await records.SaveAsync(command, CurrentActor, ct);
            if (!saved.Succeeded)
            {
                AddErrors(saved);
                return View(input);
            }

            return Redirect($"/defter/{saved.Value}");
        }

        var preview = await records.PreviewAsync(command, ct);
        if (!preview.Succeeded)
            AddErrors(preview);
        input.PreviewText = preview.Value;
        return View(input);
    }

    private static bool TryMap(string type, out ReportEntryType entryType)
    {
        entryType = type switch
        {
            "ziyaretci" => ReportEntryType.Visitor,
            "malzeme" => ReportEntryType.MaterialDelivery,
            "olay" => ReportEntryType.GeneralEvent,
            "not" => ReportEntryType.HandoverNote,
            _ => 0
        };
        return entryType != 0;
    }
}

public sealed class SimpleInput
{
    public ReportEntryType Type { get; set; }
    public string EventAt { get; set; } = string.Empty;
    public string? LateReason { get; set; }
    public string? AdditionalNote { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? Organization { get; set; }
    public string? Counterparty { get; set; }
    public MaterialDirection MaterialDirection { get; set; } = MaterialDirection.Received;
    public bool FollowUp { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? PreviewText { get; set; }
}

[Authorize(Policy = Policies.ViewRecords)]
[Route("raporlar")]
public sealed class RaporlarController(ReportService reports, ShiftService shifts, IInstitutionClock clock) : AppController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Shifts"] = await shifts.RecentAsync(ct);
        return View();
    }

    [HttpGet("gunluk")]
    public async Task<IActionResult> Gunluk(DateOnly? day, bool kilit, CancellationToken ct)
    {
        var date = day ?? DateOnly.FromDateTime(clock.ToLocal(clock.UtcNow));
        return Print(await reports.DailyAsync(date, kilit, ct));
    }

    [Authorize(Policy = Policies.ManageShift)]
    [HttpPost("gunluk/kilitle")]
    public async Task<IActionResult> Kilitle(DateOnly day, CancellationToken ct)
    {
        var result = await reports.LockDailyAsync(day, CurrentActor, ct);
        if (!result.Succeeded)
            TempData["Error"] = string.Join(" ", result.Errors);
        return Redirect($"/raporlar/gunluk?day={day:yyyy-MM-dd}&kilit=true");
    }

    [HttpGet("vardiya/{id:guid}")]
    public async Task<IActionResult> Vardiya(Guid id, bool kapanis, CancellationToken ct)
    {
        var document = await reports.ShiftAsync(id, kapanis, ct);
        return document is null ? NotFound() : Print(document);
    }

    [HttpGet("kisi/{id:guid}")]
    public async Task<IActionResult> Kisi(Guid id, CancellationToken ct) => Print(await reports.InmateAsync(id, ct));

    [HttpGet("arac")]
    public async Task<IActionResult> Arac(string plaka, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        Print(await reports.VehicleAsync(plaka ?? string.Empty, from, to, ct));

    [HttpGet("aralik")]
    public async Task<IActionResult> Aralik(DateOnly from, DateOnly to, ReportEntryType? type, CancellationToken ct) =>
        Print(await reports.RangeAsync(from, to, type, ct));

    private IActionResult Print(PrintDocument document)
    {
        ViewData["Clock"] = clock;
        return View("Yazdir", document);
    }
}

[Authorize(Policy = Policies.ViewRecords)]
[Route("yonetim")]
public sealed class YonetimController(IUserAdmin admin, VehicleService vehicles, IRaporStore store, IInstitutionClock clock) : AppController
{
    [Authorize(Policy = Policies.ManageUsers)]
    [HttpGet("kullanicilar")]
    public async Task<IActionResult> Kullanicilar(CancellationToken ct) => View(await admin.ListAsync(ct));

    [Authorize(Policy = Policies.ManageUsers)]
    [HttpGet("kullanicilar/yeni")]
    public IActionResult YeniKullanici() => View("Kullanici", new UserInput());

    [Authorize(Policy = Policies.ManageUsers)]
    [HttpPost("kullanicilar/yeni")]
    public async Task<IActionResult> YeniKullanici(UserInput input, CancellationToken ct)
    {
        var result = await admin.CreateAsync(input.UserName, input.DisplayName, input.Password, input.Roles, ct);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View("Kullanici", input);
        }

        return Redirect("/yonetim/kullanicilar");
    }

    [Authorize(Policy = Policies.ManageUsers)]
    [HttpGet("kullanicilar/{id:guid}")]
    public async Task<IActionResult> Duzenle(Guid id, CancellationToken ct)
    {
        var user = (await admin.ListAsync(ct)).FirstOrDefault(item => item.Id == id);
        if (user is null)
            return NotFound();
        return View("Kullanici", new UserInput { Id = id, UserName = user.UserName, DisplayName = user.DisplayName, IsActive = user.IsActive, Roles = user.Roles.ToList() });
    }

    [Authorize(Policy = Policies.ManageUsers)]
    [HttpPost("kullanicilar/{id:guid}")]
    public async Task<IActionResult> Duzenle(Guid id, UserInput input, CancellationToken ct)
    {
        var result = await admin.UpdateAsync(id, input.DisplayName, input.Roles, input.IsActive, input.Password, ct);
        if (!result.Succeeded)
        {
            AddErrors(result);
            input.Id = id;
            return View("Kullanici", input);
        }

        return Redirect("/yonetim/kullanicilar");
    }

    [Authorize(Policy = Policies.ManageDefinitions)]
    [HttpGet("turler")]
    public async Task<IActionResult> Turler(CancellationToken ct) => View(await vehicles.TypesAsync(false, ct));

    [Authorize(Policy = Policies.ManageDefinitions)]
    [HttpPost("turler")]
    public async Task<IActionResult> Turler(Guid? id, string name, bool active, int sort, CancellationToken ct)
    {
        var result = await vehicles.SaveTypeAsync(id, name, active, sort, ct);
        if (!result.Succeeded)
            TempData["Error"] = string.Join(" ", result.Errors);
        return Redirect("/yonetim/turler");
    }

    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.ShiftSupervisor}")]
    [HttpGet("denetim")]
    public async Task<IActionResult> Denetim(CancellationToken ct)
    {
        ViewData["Clock"] = clock;
        return View(await store.AuditsAsync(200, ct));
    }
}

public sealed class UserInput
{
    public Guid? Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<string> Roles { get; set; } = [];
}
