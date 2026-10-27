using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaporDefteri.Application;
using RaporDefteri.Domain;

namespace RaporDefteri.Web;

[Authorize(Policy = Policies.ViewRecords)]
[Route("hareket")]
public sealed class HareketController(MovementService movements, IRaporStore store, IInstitutionClock clock) : AppController
{
    [HttpGet("cikis")]
    [Authorize(Policy = Policies.CreateRecords)]
    public async Task<IActionResult> Cikis(Guid? kisi, CancellationToken ct)
    {
        var input = new DepartInput { EventAt = FormTime.Input(clock), IdempotencyKey = Guid.NewGuid().ToString("N"), Kind = TemporaryMovementKind.Hospital };
        if (kisi is Guid id)
            input.InmateIds.Add(id);
        await FillAsync(input, ct);
        return View(input);
    }

    [HttpPost("cikis")]
    [Authorize(Policy = Policies.CreateRecords)]
    public async Task<IActionResult> Cikis(DepartInput input, string? step, CancellationToken ct)
    {
        await FillAsync(input, ct);
        if (!FormTime.TryParse(input.EventAt, clock, out var when))
        {
            ModelState.AddModelError(string.Empty, "Olay saati zorunludur.");
            return View(input);
        }

        DateTimeOffset? expected = null;
        if (!string.IsNullOrWhiteSpace(input.ExpectedReturn))
        {
            if (!FormTime.TryParse(input.ExpectedReturn, clock, out var parsed))
            {
                ModelState.AddModelError(string.Empty, "Beklenen dönüş saati okunamadı.");
                return View(input);
            }

            expected = parsed;
        }

        var command = new DepartCommand(when, input.Kind, input.Destination, input.Reason, input.Escort, expected, input.LateReason, input.AdditionalNote, input.InmateIds, input.VehicleVisitId, input.IdempotencyKey);
        if (step == "kaydet")
        {
            var saved = await movements.DepartAsync(command, CurrentActor, ct);
            if (!saved.Succeeded)
            {
                AddErrors(saved);
                return View(input);
            }

            return Redirect($"/defter/{saved.Value}");
        }

        var preview = await movements.PreviewDepartureAsync(command, ct);
        if (!preview.Succeeded)
            AddErrors(preview);
        input.PreviewText = preview.Value;
        return View(input);
    }

    [HttpGet("acik")]
    public async Task<IActionResult> Acik(CancellationToken ct)
    {
        ViewData["Clock"] = clock;
        return View(await store.OpenPeopleAsync(ct));
    }

    [HttpGet("donus")]
    [Authorize(Policy = Policies.CreateRecords)]
    public async Task<IActionResult> Donus(CancellationToken ct)
    {
        var input = new ReturnInput { EventAt = FormTime.Input(clock), IdempotencyKey = Guid.NewGuid().ToString("N") };
        ViewData["Open"] = await store.OpenPeopleAsync(ct);
        ViewData["Clock"] = clock;
        return View(input);
    }

    [HttpPost("donus")]
    [Authorize(Policy = Policies.CreateRecords)]
    public async Task<IActionResult> Donus(ReturnInput input, string? step, CancellationToken ct)
    {
        ViewData["Open"] = await store.OpenPeopleAsync(ct);
        ViewData["Clock"] = clock;
        if (!FormTime.TryParse(input.EventAt, clock, out var when))
        {
            ModelState.AddModelError(string.Empty, "Dönüş saati zorunludur.");
            return View(input);
        }

        var command = new ReturnCommand(when, input.LateReason, input.AdditionalNote, input.AbsenceIds, input.VehicleVisitId, input.IdempotencyKey);
        if (step == "kaydet")
        {
            var saved = await movements.ReturnAsync(command, CurrentActor, ct);
            if (!saved.Succeeded)
            {
                AddErrors(saved);
                return View(input);
            }

            return Redirect($"/defter/{saved.Value}");
        }

        var preview = await movements.PreviewReturnAsync(command, ct);
        if (!preview.Succeeded)
            AddErrors(preview);
        input.PreviewText = preview.Value;
        return View(input);
    }

    private async Task FillAsync(DepartInput input, CancellationToken ct)
    {
        var selected = new List<InmateRow>();
        foreach (var id in input.InmateIds.Distinct())
        {
            var inmate = await store.InmateAsync(id, ct);
            if (inmate is not null)
                selected.Add(new InmateRow(inmate.Id, inmate.FileNumber, inmate.FullName, inmate.ActiveCustody()?.Ward, inmate.ActiveCustody() is not null, inmate.ActiveCustody() is null ? "Kurumda değil" : "Kurumda"));
        }

        input.Selected = selected;
        ViewData["Clock"] = clock;
    }
}

public sealed class DepartInput
{
    public string EventAt { get; set; } = string.Empty;
    public TemporaryMovementKind Kind { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Escort { get; set; }
    public string? ExpectedReturn { get; set; }
    public string? LateReason { get; set; }
    public string? AdditionalNote { get; set; }
    public List<Guid> InmateIds { get; set; } = [];
    public Guid? VehicleVisitId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? PreviewText { get; set; }
    public List<InmateRow> Selected { get; set; } = [];
}

public sealed class ReturnInput
{
    public string EventAt { get; set; } = string.Empty;
    public string? LateReason { get; set; }
    public string? AdditionalNote { get; set; }
    public List<Guid> AbsenceIds { get; set; } = [];
    public Guid? VehicleVisitId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? PreviewText { get; set; }
}

[Authorize(Policy = Policies.ViewRecords)]
[Route("defter")]
public sealed class DefterController(LedgerService ledger, IInstitutionClock clock, ShiftService shifts) : AppController
{
    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, ReportEntryType? type, Guid? shiftId, ReportEntryStatus? status, string? number, int page = 1, CancellationToken ct = default)
    {
        DateTimeOffset? start = from is null ? null : ReportPeriod.DayBounds(from.Value, clock.Zone).Start;
        DateTimeOffset? end = to is null ? null : ReportPeriod.DayBounds(to.Value, clock.Zone).End;
        var result = await ledger.SearchAsync(new LedgerQuery(start, end, type, shiftId, status, number, page, 25), ct);
        ViewData["Clock"] = clock;
        ViewData["Shifts"] = await shifts.RecentAsync(ct);
        ViewData["From"] = from?.ToString("yyyy-MM-dd");
        ViewData["To"] = to?.ToString("yyyy-MM-dd");
        ViewData["Type"] = type;
        ViewData["ShiftId"] = shiftId;
        ViewData["Status"] = status;
        ViewData["Number"] = number;
        return View(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detay(Guid id, CancellationToken ct)
    {
        var detail = await ledger.DetailAsync(id, CurrentActor, ct);
        if (detail is null)
            return NotFound();
        ViewData["Clock"] = clock;
        return View(detail);
    }

    [Authorize(Policy = Policies.CorrectRecords)]
    [HttpGet("{id:guid}/duzelt")]
    public async Task<IActionResult> Duzelt(Guid id, CancellationToken ct)
    {
        var detail = await ledger.DetailAsync(id, CurrentActor, ct);
        if (detail is null)
            return NotFound();
        ViewData["Clock"] = clock;
        return View(new CorrectInput { EntryId = id, Token = detail.Token, EventAt = FormTime.Input(clock, detail.Entry.EventAt), AdditionalNote = detail.Entry.AdditionalNote });
    }

    [Authorize(Policy = Policies.CorrectRecords)]
    [HttpPost("{id:guid}/duzelt")]
    public async Task<IActionResult> Duzelt(Guid id, CorrectInput input, CancellationToken ct)
    {
        if (!FormTime.TryParse(input.EventAt, clock, out var when))
        {
            ModelState.AddModelError(string.Empty, "Olay saati zorunludur.");
            ViewData["Clock"] = clock;
            return View(input);
        }

        var result = await ledger.CorrectAsync(new CorrectCommand(id, input.Token, when, input.AdditionalNote, input.Primary, input.Secondary, input.Reason), CurrentActor, ct);
        if (!result.Succeeded)
        {
            AddErrors(result);
            ViewData["Clock"] = clock;
            return View(input);
        }

        return Redirect($"/defter/{id}");
    }

    [Authorize(Policy = Policies.CorrectRecords)]
    [HttpPost("{id:guid}/iptal")]
    public async Task<IActionResult> Iptal(Guid id, uint token, string reason, CancellationToken ct)
    {
        var result = await ledger.CancelAsync(id, token, reason, CurrentActor, ct);
        if (!result.Succeeded)
            TempData["Error"] = string.Join(" ", result.Errors);
        return Redirect($"/defter/{id}");
    }
}

public sealed class CorrectInput
{
    public Guid EntryId { get; set; }
    public uint Token { get; set; }
    public string EventAt { get; set; } = string.Empty;
    public string? AdditionalNote { get; set; }
    public string? Primary { get; set; }
    public string? Secondary { get; set; }
    public string Reason { get; set; } = string.Empty;
}
