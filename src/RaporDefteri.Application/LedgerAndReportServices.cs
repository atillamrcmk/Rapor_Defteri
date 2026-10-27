using System.Text.Json;
using RaporDefteri.Domain;

namespace RaporDefteri.Application;

public sealed class LedgerService(IRaporStore store, IInstitutionClock clock)
{
    public Task<Page<LedgerRow>> SearchAsync(LedgerQuery query, CancellationToken ct) => store.LedgerAsync(query, ct);

    public async Task<EntryDetail?> DetailAsync(Guid id, Actor actor, CancellationToken ct)
    {
        var entry = await store.EntryAsync(id, ct);
        if (entry is null)
            return null;
        var shift = await store.ShiftAsync(entry.ShiftId, ct);
        var people = new List<InmateRow>();
        foreach (var link in entry.People)
        {
            var inmate = await store.InmateAsync(link.InmateId, ct);
            if (inmate is null)
                continue;
            var custody = inmate.ActiveCustody();
            people.Add(new InmateRow(inmate.Id, inmate.FileNumber, inmate.FullName, custody?.Ward, custody is not null, custody is null ? "Kurumda değil" : "Kurumda"));
        }

        var canCorrect = shift is not null && CorrectionRules.Validate(shift.Status, actor.IsSupervisor, actor.IsOperator, "gerekçe").Count == 0;
        return new EntryDetail(entry, Labels.Entry(entry.EntryType), Labels.Status(entry.Status), await store.TokenAsync(entry, ct), shift is null ? string.Empty : $"{clock.Format(shift.StartsAt)} – {clock.Format(shift.EndsAt)}", people, null, canCorrect, canCorrect && entry.Status != ReportEntryStatus.Cancelled);
    }

    public async Task<OperationResult<bool>> CorrectAsync(CorrectCommand command, Actor actor, CancellationToken ct)
    {
        var entry = await store.EntryAsync(command.EntryId, ct);
        if (entry is null)
            return OperationResult.Fail("Kayıt bulunamadı.");
        var shift = await store.ShiftAsync(entry.ShiftId, ct);
        if (shift is null)
            return OperationResult.Fail("Vardiya bulunamadı.");
        var token = await store.TokenAsync(entry, ct);
        if (ConcurrencyRules.IsStale(command.Token, token))
            return OperationResult<bool>.Conflict();
        var errors = CorrectionRules.Validate(shift.Status, actor.IsSupervisor, actor.IsOperator, command.Reason);
        if (errors.Count > 0)
            return OperationResult.Fail(errors.ToArray());
        if (TimeRules.IsTooFarInFuture(command.EventAt, clock.UtcNow))
            return OperationResult.Fail("Olay saati gelecekte olamaz.");

        var oldValues = $"Olay={entry.EventAt:O}; Not={entry.AdditionalNote}";
        var text = await RebuildAsync(entry, command, ct);
        var newValues = $"Olay={command.EventAt:O}; Not={command.AdditionalNote}";
        entry.Correct(text, command.AdditionalNote, command.EventAt, command.Reason, actor.UserId, clock.UtcNow, oldValues, newValues);
        await store.AddAuditAsync(new AuditEvent
        {
            Id = Guid.CreateVersion7(),
            OccurredAt = clock.UtcNow,
            UserId = actor.UserId,
            Action = "Kayıt düzeltildi",
            EntityName = "ReportEntry",
            EntityId = entry.Id.ToString(),
            Details = command.Reason.Trim()
        }, ct);
        try
        {
            await store.SaveAsync(ct);
            return OperationResult.Ok();
        }
        catch (ConcurrencyConflictException)
        {
            return OperationResult<bool>.Conflict();
        }
    }

    public async Task<OperationResult<bool>> CancelAsync(Guid entryId, uint token, string reason, Actor actor, CancellationToken ct)
    {
        var entry = await store.EntryAsync(entryId, ct);
        if (entry is null)
            return OperationResult.Fail("Kayıt bulunamadı.");
        var shift = await store.ShiftAsync(entry.ShiftId, ct);
        if (shift is null)
            return OperationResult.Fail("Vardiya bulunamadı.");
        if (ConcurrencyRules.IsStale(token, await store.TokenAsync(entry, ct)))
            return OperationResult<bool>.Conflict();
        var errors = CorrectionRules.Validate(shift.Status, actor.IsSupervisor, actor.IsOperator, reason);
        if (errors.Count > 0)
            return OperationResult.Fail(errors.ToArray());

        if (EntryKind.ExpectsReturn(entry.EntryType))
        {
            var absences = await store.AbsencesForDepartureAsync(entry.Id, ct);
            if (absences.Any(item => item.Status == TemporaryAbsenceStatus.Returned))
                return OperationResult.Fail("Dönüşü olan çıkış iptal edilemez.");
            foreach (var absence in absences.Where(item => item.Status == TemporaryAbsenceStatus.Open))
                absence.CancelOpen();
        }
        else if (entry.EntryType is ReportEntryType.HospitalReturn or ReportEntryType.CourtReturn or ReportEntryType.OtherReturn)
        {
            var absences = await store.AbsencesForReturnAsync(entry.Id, ct);
            foreach (var absence in absences)
            {
                if (await store.HasOpenAbsenceAsync(absence.InmateId, ct))
                    return OperationResult.Fail("Kişinin başka açık hareketi varken bu dönüş iptal edilemez.");
                absence.ReopenAfterReturnCancelled();
            }
        }
        else if (entry.EntryType == ReportEntryType.Release)
        {
            var detail = await store.ReleaseAsync(entry.Id, ct);
            var inmate = detail is null ? null : await store.InmateAsync(detail.InmateId, ct);
            if (inmate?.ActiveCustody() is not null)
                return OperationResult.Fail("Aktif kalış varken tahliye iptali kalışı yeniden açamaz.");
            inmate?.CustodyPeriods.SingleOrDefault(period => period.Id == detail!.CustodyPeriodId)?.Reopen();
        }
        else if (entry.EntryType == ReportEntryType.Transfer)
        {
            var detail = await store.TransferAsync(entry.Id, ct);
            var inmate = detail is null ? null : await store.InmateAsync(detail.InmateId, ct);
            if (inmate?.ActiveCustody() is not null)
                return OperationResult.Fail("Aktif kalış varken sevk iptali kalışı yeniden açamaz.");
            inmate?.CustodyPeriods.SingleOrDefault(period => period.Id == detail!.CustodyPeriodId)?.Reopen();
        }
        else if (entry.EntryType == ReportEntryType.Admission)
        {
            var detail = await store.AdmissionAsync(entry.Id, ct);
            var inmate = detail is null ? null : await store.InmateAsync(detail.InmateId, ct);
            var custody = inmate?.CustodyPeriods.SingleOrDefault(period => period.Id == detail!.CustodyPeriodId);
            if (custody is null || await store.HasOpenAbsenceAsync(inmate!.Id, ct) || custody.Status != CustodyStatus.Active)
                return OperationResult.Fail("Hareket görmüş veya aktif olmayan kabul iptal edilemez.");
            custody.CancelAdmission();
        }

        entry.Cancel(reason, actor.UserId, clock.UtcNow);
        await store.AddAuditAsync(new AuditEvent
        {
            Id = Guid.CreateVersion7(),
            OccurredAt = clock.UtcNow,
            UserId = actor.UserId,
            Action = "Kayıt iptal edildi",
            EntityName = "ReportEntry",
            EntityId = entry.Id.ToString(),
            Details = reason.Trim()
        }, ct);
        try
        {
            await store.SaveAsync(ct);
            return OperationResult.Ok();
        }
        catch (ConcurrencyConflictException)
        {
            return OperationResult<bool>.Conflict();
        }
    }

    private async Task<string> RebuildAsync(ReportEntry entry, CorrectCommand command, CancellationToken ct)
    {
        var time = clock.LocalTime(command.EventAt);
        var people = new List<PersonLine>();
        foreach (var link in entry.People.DistinctBy(item => item.InmateId))
        {
            var inmate = await store.InmateAsync(link.InmateId, ct);
            if (inmate is not null)
                people.Add(new PersonLine(inmate.FileNumber, inmate.FullName));
        }

        if (entry.EntryType is ReportEntryType.HospitalDeparture or ReportEntryType.CourtDeparture or ReportEntryType.OtherDeparture)
        {
            var kind = entry.EntryType switch
            {
                ReportEntryType.HospitalDeparture => TemporaryMovementKind.Hospital,
                ReportEntryType.CourtDeparture => TemporaryMovementKind.Court,
                _ => TemporaryMovementKind.Other
            };
            var absences = await store.AbsencesForDepartureAsync(entry.Id, ct);
            var destination = string.IsNullOrWhiteSpace(command.Primary) ? absences.FirstOrDefault()?.Destination ?? string.Empty : command.Primary.Trim();
            var reason = string.IsNullOrWhiteSpace(command.Secondary) ? absences.FirstOrDefault()?.Reason ?? string.Empty : command.Secondary.Trim();
            foreach (var absence in absences)
                absence.CorrectFacts(destination, reason);

            return ReportTextComposer.Departure(time, kind, people, destination, reason, absences.FirstOrDefault()?.EscortNote);
        }

        if (entry.EntryType is ReportEntryType.HospitalReturn or ReportEntryType.CourtReturn or ReportEntryType.OtherReturn)
        {
            var kind = entry.EntryType switch
            {
                ReportEntryType.HospitalReturn => TemporaryMovementKind.Hospital,
                ReportEntryType.CourtReturn => TemporaryMovementKind.Court,
                _ => TemporaryMovementKind.Other
            };
            return ReportTextComposer.Return(time, kind, people, command.Secondary);
        }

        if (entry.EntryType == ReportEntryType.GeneralEvent)
        {
            var general = await store.GeneralAsync(entry.Id, ct);
            if (general is not null && !string.IsNullOrWhiteSpace(command.Primary))
                general.Title = command.Primary.Trim();
            return ReportTextComposer.General(time, general?.Title ?? command.Primary ?? "Olay", general?.Description);
        }

        return entry.ReportText;
    }
}

public sealed class ReportService(IRaporStore store, IInstitutionClock clock, IInstitutionProfile institution)
{
    public async Task<PrintDocument> DailyAsync(DateOnly day, bool locked, CancellationToken ct)
    {
        var bounds = ReportPeriod.DayBounds(day, clock.Zone);
        if (locked)
        {
            var snapshot = await store.LatestSnapshotAsync(SnapshotKind.DailyLock, null, bounds.Start, ct);
            if (snapshot is not null)
                return FromSnapshot(snapshot, $"Günlük rapor kilit sürümü {snapshot.Version}");
        }

        var entries = await store.EntriesBetweenAsync(bounds.Start, bounds.End, null, ct);
        return Build("Günlük merkez kontrol raporu", day.ToString("dd.MM.yyyy"), null, entries, null, null, null);
    }

    public async Task<OperationResult<bool>> LockDailyAsync(DateOnly day, Actor actor, CancellationToken ct)
    {
        var bounds = ReportPeriod.DayBounds(day, clock.Zone);
        var entries = await store.EntriesBetweenAsync(bounds.Start, bounds.End, null, ct);
        var lines = entries.OrderBy(entry => entry.EventAt).Select(ToLine).ToList();
        var version = await store.NextSnapshotVersionAsync(SnapshotKind.DailyLock, null, bounds.Start, ct);
        await store.AddSnapshotAsync(new ReportSnapshot
        {
            Id = Guid.CreateVersion7(),
            Kind = SnapshotKind.DailyLock,
            PeriodStart = bounds.Start,
            PeriodEnd = bounds.End,
            Version = version,
            CreatedAt = clock.UtcNow,
            CreatedByUserId = actor.UserId,
            Title = "Günlük rapor kilidi",
            BodyJson = JsonSerializer.Serialize(lines)
        }, ct);
        await store.SaveAsync(ct);
        return OperationResult.Ok();
    }

    public async Task<PrintDocument?> ShiftAsync(Guid shiftId, bool closingVersion, CancellationToken ct)
    {
        var shift = await store.ShiftAsync(shiftId, ct);
        if (shift is null)
            return null;
        var names = await store.UserNamesAsync([shift.PreparedByUserId ?? Guid.Empty, shift.AcceptedByUserId ?? Guid.Empty], ct);
        var period = $"{clock.Format(shift.StartsAt)} – {clock.Format(shift.EndsAt)}";
        if (closingVersion && shift.Status == ShiftStatus.Closed)
        {
            var snapshot = await store.FirstShiftSnapshotAsync(shiftId, ct);
            if (snapshot is not null)
            {
                var document = FromSnapshot(snapshot, $"Kapanış sürümü {snapshot.Version}");
                return document with { Period = period, Handover = shift.HandoverNote, HandedOverBy = names.FirstOrDefault(), AcceptedBy = names.Skip(1).FirstOrDefault() };
            }
        }

        var entries = await store.EntriesForShiftAsync(shiftId, ct);
        return Build("Vardiya raporu", period, closingVersion ? "Güncel görünüm" : null, entries, shift.HandoverNote, names.FirstOrDefault(), names.Skip(1).FirstOrDefault());
    }

    public async Task<PrintDocument> InmateAsync(Guid inmateId, CancellationToken ct)
    {
        var inmate = await store.InmateAsync(inmateId, ct);
        var history = inmate is null ? [] : await store.HistoryAsync(inmateId, ct);
        var rows = history.Select(row => new PrintRow("-", clock.Format(row.EventAt), string.Empty, row.Text, null, row.Kind)).ToList();
        return new PrintDocument(institution.Name, "Kişi hareket dökümü", inmate is null ? string.Empty : $"{inmate.FileNumber} {inmate.FullName}", null, rows, null, null, null, clock.UtcNow);
    }

    public async Task<PrintDocument> VehicleAsync(string plate, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        DateTimeOffset? start = from is null ? null : ReportPeriod.DayBounds(from.Value, clock.Zone).Start;
        DateTimeOffset? end = to is null ? null : ReportPeriod.DayBounds(to.Value, clock.Zone).End;
        var visits = await store.VisitsByPlateAsync(TurkishText.NormalizePlate(plate), start, end, ct);
        var rows = visits.Select(visit => new PrintRow(visit.RecordNumber, clock.Format(visit.StartedAt), visit.EndedAt is null ? string.Empty : clock.Format(visit.EndedAt.Value), $"{visit.Plate} {visit.TypeName} {visit.Purpose}", visit.Driver, visit.StatusText)).ToList();
        return new PrintDocument(institution.Name, "Araç geçiş dökümü", plate, null, rows, null, null, null, clock.UtcNow);
    }

    public async Task<PrintDocument> RangeAsync(DateOnly from, DateOnly to, ReportEntryType? type, CancellationToken ct)
    {
        var start = ReportPeriod.DayBounds(from, clock.Zone).Start;
        var end = ReportPeriod.DayBounds(to, clock.Zone).End;
        var entries = await store.EntriesBetweenAsync(start, end, type, ct);
        var title = type is null ? "Kayıt listesi" : Labels.Entry(type.Value);
        return Build(title, $"{from:dd.MM.yyyy} – {to:dd.MM.yyyy}", null, entries, null, null, null);
    }

    private PrintDocument Build(string title, string period, string? version, IReadOnlyList<ReportEntry> entries, string? handover, string? handed, string? accepted)
    {
        var rows = entries.OrderBy(entry => entry.EventAt).Select(entry => new PrintRow(entry.RecordNumber, clock.Format(entry.EventAt), clock.Format(entry.RecordedAt), entry.ReportText, entry.AdditionalNote, Labels.Status(entry.Status))).ToList();
        return new PrintDocument(institution.Name, title, period, version, rows, handover, handed, accepted, clock.UtcNow);
    }

    private PrintDocument FromSnapshot(ReportSnapshot snapshot, string version)
    {
        var lines = JsonSerializer.Deserialize<List<SnapshotLine>>(snapshot.BodyJson) ?? [];
        var rows = lines.Select(line => new PrintRow(line.RecordNumber, clock.Format(line.EventAt), clock.Format(line.RecordedAt), line.Text, line.Note, line.Status)).ToList();
        return new PrintDocument(institution.Name, snapshot.Title, $"{clock.Format(snapshot.PeriodStart)} – {clock.Format(snapshot.PeriodEnd)}", version, rows, null, null, null, snapshot.CreatedAt);
    }

    private static SnapshotLine ToLine(ReportEntry entry) =>
        new(entry.RecordNumber, entry.EventAt, entry.RecordedAt, entry.ReportText, entry.AdditionalNote, Labels.Status(entry.Status));
}

public sealed class DashboardService(IRaporStore store)
{
    public async Task<DashboardModel> LoadAsync(CancellationToken ct)
    {
        var shift = await store.ActiveShiftAsync(ct);
        ShiftSummary? summary = null;
        if (shift is not null)
        {
            var names = await store.UserNamesAsync(shift.Duties.Select(duty => duty.UserId).ToList(), ct);
            summary = new ShiftSummary(shift.Id, shift.StartsAt, shift.EndsAt, shift.Status, Labels.Shift(shift.Status), names);
        }

        return new DashboardModel(summary, await store.RecentEntriesAsync(8, ct), await store.OpenPeopleAsync(ct), await store.OpenVisitsAsync(ct), await store.FollowUpsAsync(ct), await store.LatestBackupAsync(ct));
    }
}
