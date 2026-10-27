using RaporDefteri.Domain;

namespace RaporDefteri.Application;

public sealed class InmateService(IRaporStore store, IInstitutionClock clock)
{
    public Task<IReadOnlyList<InmateRow>> SearchAsync(string? query, int take, CancellationToken ct) =>
        store.SearchInmatesAsync(query, take, ct);

    public async Task<IReadOnlyList<HistoryRow>> HistoryAsync(Guid inmateId, DateOnly? from, DateOnly? to, string? kind, string? destination, CancellationToken ct)
    {
        var rows = await store.HistoryAsync(inmateId, ct);
        var kindKey = TurkishText.NormalizeName(kind);
        var destinationKey = TurkishText.NormalizeName(destination);
        return rows.Where(row =>
        {
            var day = DateOnly.FromDateTime(clock.ToLocal(row.EventAt));
            if (from is not null && day < from.Value)
                return false;
            if (to is not null && day > to.Value)
                return false;
            if (kindKey.Length > 0 && !TurkishText.NormalizeName(row.Kind).Contains(kindKey, StringComparison.Ordinal))
                return false;
            if (destinationKey.Length > 0 && !TurkishText.NormalizeName(row.Destination).Contains(destinationKey, StringComparison.Ordinal))
                return false;
            return true;
        }).OrderBy(row => row.EventAt).ToList();
    }

    public async Task<OperationResult<string>> PreviewAdmissionAsync(AdmissionCommand command, CancellationToken ct)
    {
        var shift = await Persist.RequireWritableShiftAsync(store, ct);
        if (shift is null)
            return OperationResult<string>.Fail("Açık vardiya yok. Kayıt yazılmadı.");
        var person = await ResolvePersonAsync(command, ct);
        if (person.Error is not null)
            return OperationResult<string>.Fail(person.Error);
        var timeErrors = TimeRules.ValidateEventTime(command.EventAt, clock.UtcNow, shift.StartsAt, command.LateReason);
        if (timeErrors.Count > 0)
            return OperationResult<string>.Fail(timeErrors);
        var text = ReportTextComposer.Admission(clock.LocalTime(command.EventAt), new PersonLine(person.FileNumber!, person.FullName!), command.Ward);
        return OperationResult<string>.Ok(text);
    }

    public async Task<OperationResult<Guid>> AdmitAsync(AdmissionCommand command, Actor actor, CancellationToken ct)
    {
        var replay = await store.ByIdempotencyAsync(command.IdempotencyKey, ct);
        if (replay is not null)
            return OperationResult<Guid>.Ok(replay.Id);
        var preview = await PreviewAdmissionAsync(command, ct);
        if (!preview.Succeeded || preview.Value is null)
            return OperationResult<Guid>.Fail(preview.Errors);

        var shift = (await store.ActiveShiftAsync(ct))!;
        var now = clock.UtcNow;
        var entry = ReportEntry.Create(await store.NextNumberAsync(ct), command.EventAt, now, ReportEntryType.Admission, shift.Id, actor.UserId, preview.Value, command.AdditionalNote, command.LateReason, command.IdempotencyKey);
        Inmate inmate;
        if (command.ExistingInmateId is Guid existingId)
        {
            inmate = (await store.InmateAsync(existingId, ct))!;
            if (!string.Equals(TurkishText.NormalizeFileNumber(command.FileNumber), inmate.NormalizedFileNumber, StringComparison.Ordinal))
                inmate.ChangeFileNumber(command.FileNumber, now);
        }
        else
        {
            inmate = Inmate.Create(command.FileNumber, command.FirstName, command.LastName, actor.UserId, now);
        }

        var custody = CustodyPeriod.Open(inmate.Id, command.Ward, command.EventAt, entry.Id);
        inmate.CustodyPeriods.Add(custody);
        if (command.ExistingInmateId is null)
            await store.AddInmateAsync(inmate, ct);
        entry.People.Add(new ReportEntryInmate { ReportEntryId = entry.Id, InmateId = inmate.Id });
        await store.AddEntryAsync(entry, ct);
        await store.AddAdmissionAsync(new AdmissionDetail { ReportEntryId = entry.Id, InmateId = inmate.Id, CustodyPeriodId = custody.Id, Ward = custody.Ward }, ct);
        return await Persist.SaveNewAsync(store, entry.Id, command.IdempotencyKey, ct);
    }

    public async Task<OperationResult<Guid>> ReleaseAsync(ReleaseCommand command, Actor actor, CancellationToken ct) =>
        await CloseStayAsync(command.InmateId, command.EventAt, command.LateReason, command.AdditionalNote, command.Reason, command.IdempotencyKey, null, actor, ct);

    public async Task<OperationResult<Guid>> TransferAsync(TransferCommand command, Actor actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Destination))
            return OperationResult<Guid>.Fail("Sevk kurumu zorunludur.");
        return await CloseStayAsync(command.InmateId, command.EventAt, command.LateReason, command.AdditionalNote, command.Reason, command.IdempotencyKey, command.Destination, actor, ct);
    }

    private async Task<OperationResult<Guid>> CloseStayAsync(Guid inmateId, DateTimeOffset eventAt, string? lateReason, string? note, string? reason, string idempotencyKey, string? transferDestination, Actor actor, CancellationToken ct)
    {
        var replay = await store.ByIdempotencyAsync(idempotencyKey, ct);
        if (replay is not null)
            return OperationResult<Guid>.Ok(replay.Id);
        var shift = await Persist.RequireWritableShiftAsync(store, ct);
        if (shift is null)
            return OperationResult<Guid>.Fail("Açık vardiya yok. Kayıt yazılmadı.");
        var inmate = await store.InmateAsync(inmateId, ct);
        if (inmate is null)
            return OperationResult<Guid>.Fail("Hükümlü/tutuklu bulunamadı.");
        var custody = inmate.ActiveCustody();
        var errors = MovementRules.ValidatePermanentExit(custody is not null, await store.HasOpenAbsenceAsync(inmateId, ct));
        errors = errors.Concat(TimeRules.ValidateEventTime(eventAt, clock.UtcNow, shift.StartsAt, lateReason)).ToList();
        if (errors.Count > 0)
            return OperationResult<Guid>.Fail(errors);

        var person = new PersonLine(inmate.FileNumber, inmate.FullName);
        var local = clock.LocalTime(eventAt);
        var isTransfer = transferDestination is not null;
        var text = isTransfer
            ? ReportTextComposer.Transfer(local, person, transferDestination!, reason)
            : ReportTextComposer.Release(local, person, reason);
        var entry = ReportEntry.Create(await store.NextNumberAsync(ct), eventAt, clock.UtcNow, isTransfer ? ReportEntryType.Transfer : ReportEntryType.Release, shift.Id, actor.UserId, text, note, lateReason, idempotencyKey);
        entry.People.Add(new ReportEntryInmate { ReportEntryId = entry.Id, InmateId = inmate.Id });
        custody!.Close(isTransfer ? CustodyStatus.Transferred : CustodyStatus.Released, eventAt, reason, entry.Id);
        await store.AddEntryAsync(entry, ct);
        if (isTransfer)
            await store.AddTransferAsync(new TransferDetail { ReportEntryId = entry.Id, InmateId = inmate.Id, CustodyPeriodId = custody.Id, DestinationInstitution = transferDestination!.Trim(), Reason = reason }, ct);
        else
            await store.AddReleaseAsync(new ReleaseDetail { ReportEntryId = entry.Id, InmateId = inmate.Id, CustodyPeriodId = custody.Id, Reason = reason }, ct);
        return await Persist.SaveNewAsync(store, entry.Id, idempotencyKey, ct);
    }

    private async Task<(string? FileNumber, string? FullName, string? Error)> ResolvePersonAsync(AdmissionCommand command, CancellationToken ct)
    {
        if (command.ExistingInmateId is Guid id)
        {
            var inmate = await store.InmateAsync(id, ct);
            if (inmate is null)
                return (null, null, "Kişi bulunamadı. Yeni kart açılmadı.");
            if (inmate.ActiveCustody() is not null)
                return (null, null, "Bu kişinin aktif kalışı var. İkinci kabul kaydı yazılmadı.");
            if (string.IsNullOrWhiteSpace(command.FileNumber))
                return (null, null, "Sicil veya dosya numarası zorunludur.");
            return (command.FileNumber.Trim(), inmate.FullName, null);
        }

        if (string.IsNullOrWhiteSpace(command.FirstName) || string.IsNullOrWhiteSpace(command.LastName))
            return (null, null, "Ad ve soyad zorunludur.");
        var normalized = TurkishText.NormalizeFileNumber(command.FileNumber);
        if (await store.FileTakenAsync(normalized, null, ct))
            return (null, null, "Bu sicil numarası kayıtlı. Aynı kişi yeniden kabul edilecekse mevcut kart seçilmelidir. Yeni kart açılmadı.");
        return (command.FileNumber.Trim(), $"{command.FirstName.Trim()} {command.LastName.Trim()}", null);
    }
}

public sealed class ShiftService(IRaporStore store, IInstitutionClock clock)
{
    public async Task<OperationResult<Guid>> OpenAsync(OpenShiftCommand command, Actor actor, CancellationToken ct)
    {
        var gate = await store.GateAsync(ct);
        var errors = ShiftRules.ValidateNew(command.StartsAt, command.EndsAt, gate.ActiveShiftId is not null);
        if (errors.Count > 0)
            return OperationResult<Guid>.Fail(errors);
        var duties = command.DutyUserIds.Append(actor.UserId).Distinct().ToList();
        var shift = Shift.Open(command.StartsAt, command.EndsAt, actor.UserId, clock.UtcNow, duties);
        gate.ActiveShiftId = shift.Id;
        await store.AddShiftAsync(shift, ct);
        await store.AddAuditAsync(Audit(actor, clock, "Vardiya açıldı", "Shift", shift.Id, $"{shift.StartsAt:O} - {shift.EndsAt:O}"), ct);
        try
        {
            await store.SaveAsync(ct);
            return OperationResult<Guid>.Ok(shift.Id);
        }
        catch (ConcurrencyConflictException)
        {
            return OperationResult<Guid>.Fail("Vardiya aynı anda başka bir kullanıcı tarafından açıldı. Bu işlem yazılmadı.");
        }
    }

    public async Task<HandoverModel?> HandoverAsync(CancellationToken ct)
    {
        var shift = await store.ActiveShiftAsync(ct);
        if (shift is null)
            return null;
        var summary = await SummarizeAsync(shift, ct);
        var notes = (await store.EntriesForShiftAsync(shift.Id, ct))
            .Where(entry => entry.EntryType == ReportEntryType.HandoverNote && entry.Status != ReportEntryStatus.Cancelled)
            .Select(entry => new LedgerRow(entry.Id, entry.RecordNumber, entry.EventAt, entry.RecordedAt, entry.EntryType, Labels.Entry(entry.EntryType), entry.Status, Labels.Status(entry.Status), entry.ReportText, entry.ShiftId))
            .ToList();
        return new HandoverModel(summary, await store.OpenPeopleAsync(ct), await store.OpenVisitsAsync(ct), await store.FollowUpsAsync(ct), notes);
    }

    public async Task<OperationResult<bool>> PrepareAsync(string? note, Actor actor, CancellationToken ct)
    {
        var shift = await store.ActiveShiftAsync(ct);
        if (shift is null)
            return OperationResult.Fail("Açık vardiya yok.");
        shift.MarkReady(actor.UserId, clock.UtcNow, note);
        await store.AddAuditAsync(Audit(actor, clock, "Vardiya devre hazırlandı", "Shift", shift.Id, note ?? string.Empty), ct);
        await store.SaveAsync(ct);
        return OperationResult.Ok();
    }

    public async Task<OperationResult<Guid>> AcceptAsync(string? closeNote, Actor actor, CancellationToken ct)
    {
        var shift = await store.ActiveShiftAsync(ct);
        if (shift is null)
            return OperationResult<Guid>.Fail("Devralınacak vardiya yok.");
        var errors = ShiftRules.ValidateAccept(shift.Status);
        if (errors.Count > 0)
            return OperationResult<Guid>.Fail(errors);

        var now = clock.UtcNow;
        shift.AcceptAndClose(actor.UserId, now, closeNote);
        var gate = await store.GateAsync(ct);
        gate.ActiveShiftId = null;
        var entries = await store.EntriesForShiftAsync(shift.Id, ct);
        var lines = entries.OrderBy(entry => entry.EventAt).Select(entry => new SnapshotLine(entry.RecordNumber, entry.EventAt, entry.RecordedAt, entry.ReportText, entry.AdditionalNote, Labels.Status(entry.Status))).ToList();
        var version = await store.NextSnapshotVersionAsync(SnapshotKind.ShiftClose, shift.Id, shift.StartsAt, ct);
        await store.AddSnapshotAsync(new ReportSnapshot
        {
            Id = Guid.CreateVersion7(),
            Kind = SnapshotKind.ShiftClose,
            ShiftId = shift.Id,
            PeriodStart = shift.StartsAt,
            PeriodEnd = shift.EndsAt,
            Version = version,
            CreatedAt = now,
            CreatedByUserId = actor.UserId,
            Title = "Vardiya kapanış sürümü",
            BodyJson = System.Text.Json.JsonSerializer.Serialize(lines)
        }, ct);
        await store.AddAuditAsync(Audit(actor, clock, "Vardiya devralındı ve kapatıldı", "Shift", shift.Id, "Devralma elektronik imza değildir."), ct);
        await store.SaveAsync(ct);
        return OperationResult<Guid>.Ok(shift.Id);
    }

    public async Task<ShiftSummary?> SummaryAsync(Guid? id, CancellationToken ct)
    {
        var shift = id is null ? await store.ActiveShiftAsync(ct) : await store.ShiftAsync(id.Value, ct);
        return shift is null ? null : await SummarizeAsync(shift, ct);
    }

    public Task<IReadOnlyList<ShiftSummary>> RecentAsync(CancellationToken ct) => store.RecentShiftsAsync(ct);

    private async Task<ShiftSummary> SummarizeAsync(Shift shift, CancellationToken ct)
    {
        var names = await store.UserNamesAsync(shift.Duties.Select(duty => duty.UserId).ToList(), ct);
        return new ShiftSummary(shift.Id, shift.StartsAt, shift.EndsAt, shift.Status, Labels.Shift(shift.Status), names);
    }

    private static AuditEvent Audit(Actor actor, IInstitutionClock clock, string action, string entity, Guid id, string details) => new()
    {
        Id = Guid.CreateVersion7(),
        OccurredAt = clock.UtcNow,
        UserId = actor.UserId,
        Action = action,
        EntityName = entity,
        EntityId = id.ToString(),
        Details = details
    };
}

public static class OperationResult
{
    public static OperationResult<bool> Ok() => OperationResult<bool>.Ok(true);
    public static OperationResult<bool> Fail(params string[] errors) => OperationResult<bool>.Fail(errors);
}
