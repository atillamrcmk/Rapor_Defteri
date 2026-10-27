using RaporDefteri.Domain;

namespace RaporDefteri.Application;

public sealed record SnapshotLine(string RecordNumber, DateTimeOffset EventAt, DateTimeOffset RecordedAt, string Text, string? Note, string Status);

public sealed class VehicleService(IRaporStore store, IInstitutionClock clock)
{
    public async Task<OperationResult<string>> PreviewAsync(VehicleCommand command, CancellationToken ct)
    {
        var plan = await PlanAsync(command, ct);
        return plan.Succeeded ? OperationResult<string>.Ok(plan.Value!.Text) : OperationResult<string>.Fail(plan.Errors);
    }

    public async Task<OperationResult<Guid>> RecordAsync(VehicleCommand command, Actor actor, CancellationToken ct)
    {
        var replay = await store.ByIdempotencyAsync(command.IdempotencyKey, ct);
        if (replay is not null)
            return OperationResult<Guid>.Ok(replay.Id);
        var plan = await PlanAsync(command, ct);
        if (!plan.Succeeded || plan.Value is null)
            return OperationResult<Guid>.Fail(plan.Errors);

        var shift = (await store.ActiveShiftAsync(ct))!;
        var now = clock.UtcNow;
        var entry = ReportEntry.Create(await store.NextNumberAsync(ct), command.EventAt, now, ReportEntryType.VehiclePassage, shift.Id, actor.UserId, plan.Value.Text, command.AdditionalNote, command.LateReason, command.IdempotencyKey);
        var card = plan.Value.Card;
        if (card is null)
        {
            card = VehicleCard.Create(command.Plate, command.Location, command.Organization, null);
            await store.AddCardAsync(card, ct);
        }
        else
        {
            card.ChangeLocation(command.Location);
            card.ChangeOrganization(command.Organization);
        }

        VehicleVisit visit;
        if (plan.Value.Closing is not null)
        {
            visit = plan.Value.Closing;
            visit.Close(command.EventAt, command.Direction, entry.Id);
        }
        else
        {
            var status = plan.Value.Plan.Kind == VehiclePlanKind.Standalone ? VehicleVisitStatus.Standalone : VehicleVisitStatus.Open;
            visit = VehicleVisit.Begin(card.Id, command.VehicleTypeId, command.Location, status, plan.Value.Plan.Inference, command.Direction, command.EventAt, command.Purpose, command.Driver, command.Organization, entry.Id);
            await store.AddVisitAsync(visit, ct);
        }

        if (command.LinkAbsenceIds.Count > 0)
        {
            var absences = await store.AbsencesAsync(command.LinkAbsenceIds, ct);
            foreach (var absence in absences)
                absence.LinkVehicle(visit.Id, command.Direction == PassageDirection.Entry && card.BaseLocation == VehicleBaseLocation.Institution);
        }

        await store.AddEntryAsync(entry, ct);
        return await Persist.SaveNewAsync(store, entry.Id, command.IdempotencyKey, ct);
    }

    private async Task<OperationResult<VehicleDraft>> PlanAsync(VehicleCommand command, CancellationToken ct)
    {
        var shift = await Persist.RequireWritableShiftAsync(store, ct);
        if (shift is null)
            return OperationResult<VehicleDraft>.Fail("Açık vardiya yok. Kayıt yazılmadı.");
        var timeErrors = TimeRules.ValidateEventTime(command.EventAt, clock.UtcNow, shift.StartsAt, command.LateReason);
        if (timeErrors.Count > 0)
            return OperationResult<VehicleDraft>.Fail(timeErrors);
        if (string.IsNullOrWhiteSpace(command.Purpose))
            return OperationResult<VehicleDraft>.Fail("Geçiş amacı zorunludur.");
        var type = await store.VehicleTypeAsync(command.VehicleTypeId, ct);
        if (type is null || !type.IsActive)
            return OperationResult<VehicleDraft>.Fail("Araç türü bulunamadı.");

        var card = await store.CardByPlateAsync(TurkishText.NormalizePlate(command.Plate), ct);
        var open = card is null ? null : await store.OpenVisitAsync(card.Id, ct);
        var plan = VehicleRules.Plan(command.Location, command.Direction, open is not null, open?.LocationInference ?? LocationInference.None);
        if (plan.Kind == VehiclePlanKind.Reject)
            return OperationResult<VehicleDraft>.Fail(plan.Error ?? "Araç seferi kaydedilemedi.");

        var text = ReportTextComposer.Vehicle(clock.LocalTime(command.EventAt), command.Plate.Trim(), type.Name, command.Direction, command.Driver, command.Organization);
        return OperationResult<VehicleDraft>.Ok(new VehicleDraft(text, plan, card, plan.Kind == VehiclePlanKind.CloseVisit ? open : null));
    }

    public Task<IReadOnlyList<VehicleType>> TypesAsync(bool activeOnly, CancellationToken ct) =>
        store.VehicleTypesAsync(activeOnly, ct);

    public async Task<OperationResult<bool>> SaveTypeAsync(Guid? id, string name, bool active, int sort, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult<bool>.Fail("Araç türü adı zorunludur.");
        if (id is Guid existing)
        {
            var type = await store.VehicleTypeAsync(existing, ct);
            if (type is null)
                return OperationResult<bool>.Fail("Araç türü bulunamadı.");
            type.Name = name.Trim();
            type.IsActive = active;
            type.SortOrder = sort;
        }
        else
        {
            await store.AddVehicleTypeAsync(new VehicleType { Id = Guid.CreateVersion7(), Name = name.Trim(), IsActive = active, SortOrder = sort }, ct);
        }

        await store.SaveAsync(ct);
        return OperationResult<bool>.Ok(true);
    }

    private sealed record VehicleDraft(string Text, VehiclePlan Plan, VehicleCard? Card, VehicleVisit? Closing);
}

public sealed class RecordService(IRaporStore store, IInstitutionClock clock)
{
    public async Task<OperationResult<string>> PreviewAsync(SimpleRecordCommand command, CancellationToken ct)
    {
        var shift = await Persist.RequireWritableShiftAsync(store, ct);
        if (shift is null)
            return OperationResult<string>.Fail("Açık vardiya yok. Kayıt yazılmadı.");
        var timeErrors = TimeRules.ValidateEventTime(command.EventAt, clock.UtcNow, shift.StartsAt, command.LateReason);
        if (timeErrors.Count > 0)
            return OperationResult<string>.Fail(timeErrors);
        try
        {
            return OperationResult<string>.Ok(Compose(command));
        }
        catch (DomainRuleException ex)
        {
            return OperationResult<string>.Fail(ex.Message);
        }
    }

    public async Task<OperationResult<Guid>> SaveAsync(SimpleRecordCommand command, Actor actor, CancellationToken ct)
    {
        var replay = await store.ByIdempotencyAsync(command.IdempotencyKey, ct);
        if (replay is not null)
            return OperationResult<Guid>.Ok(replay.Id);
        var preview = await PreviewAsync(command, ct);
        if (!preview.Succeeded || preview.Value is null)
            return OperationResult<Guid>.Fail(preview.Errors);
        var shift = (await store.ActiveShiftAsync(ct))!;
        var entry = ReportEntry.Create(await store.NextNumberAsync(ct), command.EventAt, clock.UtcNow, command.Type, shift.Id, actor.UserId, preview.Value, command.AdditionalNote, command.LateReason, command.IdempotencyKey);
        await store.AddEntryAsync(entry, ct);
        switch (command.Type)
        {
            case ReportEntryType.Visitor:
                await store.AddVisitorAsync(new VisitorDetail { ReportEntryId = entry.Id, FullName = command.Title.Trim(), Organization = command.Organization, Purpose = command.Body }, ct);
                break;
            case ReportEntryType.MaterialDelivery:
                await store.AddMaterialAsync(new MaterialDetail { ReportEntryId = entry.Id, Description = command.Title.Trim(), Direction = command.MaterialDirection ?? MaterialDirection.Received, Counterparty = command.Counterparty }, ct);
                break;
            case ReportEntryType.GeneralEvent:
                await store.AddGeneralAsync(new GeneralEventDetail { ReportEntryId = entry.Id, Title = command.Title.Trim(), Description = command.Body, FollowUp = command.FollowUp }, ct);
                break;
            case ReportEntryType.HandoverNote:
                await store.AddHandoverNoteAsync(new HandoverNoteDetail { ReportEntryId = entry.Id, Note = command.Title.Trim() }, ct);
                break;
            default:
                return OperationResult<Guid>.Fail("Bu tür bu formdan kaydedilemez.");
        }

        return await Persist.SaveNewAsync(store, entry.Id, command.IdempotencyKey, ct);
    }

    private string Compose(SimpleRecordCommand command)
    {
        var time = clock.LocalTime(command.EventAt);
        return command.Type switch
        {
            ReportEntryType.Visitor => ReportTextComposer.Visitor(time, command.Title, command.Organization, command.Body),
            ReportEntryType.MaterialDelivery => ReportTextComposer.Material(time, command.Title, command.MaterialDirection ?? MaterialDirection.Received, command.Counterparty),
            ReportEntryType.GeneralEvent => ReportTextComposer.General(time, command.Title, command.Body),
            ReportEntryType.HandoverNote => ReportTextComposer.HandoverNote(command.Title),
            _ => throw new DomainRuleException("Bu tür bu formdan kaydedilemez.")
        };
    }
}
