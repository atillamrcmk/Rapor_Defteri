using System.Data;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RaporDefteri.Application;
using RaporDefteri.Domain;

namespace RaporDefteri.Infrastructure;

public sealed class RaporStore(AppDbContext db, IInstitutionClock clock) : IRaporStore
{
    public async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new UniqueConstraintException(pg.ConstraintName);
        }
    }

    public void Clear() => db.ChangeTracker.Clear();

    public Task<uint> TokenAsync<T>(T entity, CancellationToken ct) where T : class
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(db.Entry(entity).Property<uint>("xmin").CurrentValue);
    }

    public Task<Shift?> ActiveShiftAsync(CancellationToken ct) =>
        db.Shifts.Include(shift => shift.Duties).FirstOrDefaultAsync(shift => shift.Status == ShiftStatus.Open || shift.Status == ShiftStatus.ReadyForHandover, ct);

    public Task<Shift?> ShiftAsync(Guid id, CancellationToken ct) =>
        db.Shifts.Include(shift => shift.Duties).FirstOrDefaultAsync(shift => shift.Id == id, ct);

    public Task<ShiftGate> GateAsync(CancellationToken ct) => db.ShiftGates.SingleAsync(ct);

    public Task AddShiftAsync(Shift shift, CancellationToken ct) => db.Shifts.AddAsync(shift, ct).AsTask();

    public async Task<IReadOnlyList<ShiftSummary>> RecentShiftsAsync(CancellationToken ct)
    {
        var shifts = await db.Shifts.AsNoTracking().Include(shift => shift.Duties).OrderByDescending(shift => shift.StartsAt).Take(20).ToListAsync(ct);
        var names = await NamesAsync(shifts.SelectMany(shift => shift.Duties.Select(duty => duty.UserId)).ToList(), ct);
        return shifts.Select(shift => new ShiftSummary(shift.Id, shift.StartsAt, shift.EndsAt, shift.Status, Labels.Shift(shift.Status), shift.Duties.Select(duty => names.GetValueOrDefault(duty.UserId) ?? string.Empty).Where(name => name.Length > 0).ToList())).ToList();
    }

    public Task<Inmate?> InmateAsync(Guid id, CancellationToken ct) =>
        db.Inmates.Include(inmate => inmate.CustodyPeriods).Include(inmate => inmate.FileHistory).FirstOrDefaultAsync(inmate => inmate.Id == id, ct);

    public Task<Inmate?> InmateByFileAsync(string normalizedFileNumber, CancellationToken ct) =>
        db.Inmates.Include(inmate => inmate.CustodyPeriods).Include(inmate => inmate.FileHistory)
            .FirstOrDefaultAsync(inmate => inmate.NormalizedFileNumber == normalizedFileNumber || inmate.FileHistory.Any(history => history.NormalizedFileNumber == normalizedFileNumber), ct);

    public Task<bool> FileTakenAsync(string normalizedFileNumber, Guid? exceptInmateId, CancellationToken ct) =>
        db.Inmates.AnyAsync(inmate => inmate.Id != exceptInmateId && (inmate.NormalizedFileNumber == normalizedFileNumber || inmate.FileHistory.Any(history => history.NormalizedFileNumber == normalizedFileNumber)), ct);

    public Task AddInmateAsync(Inmate inmate, CancellationToken ct) => db.Inmates.AddAsync(inmate, ct).AsTask();

    public async Task<IReadOnlyList<InmateRow>> SearchInmatesAsync(string? query, int take, CancellationToken ct)
    {
        var items = db.Inmates.AsNoTracking().Include(inmate => inmate.CustodyPeriods).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var name = TurkishText.NormalizeName(query);
            var file = TurkishText.NormalizeFileNumber(query);
            var ward = TurkishText.NormalizeName(query);
            items = items.Where(inmate =>
                inmate.NormalizedFullName.Contains(name) ||
                inmate.NormalizedFileNumber.Contains(file) ||
                inmate.FileHistory.Any(history => history.NormalizedFileNumber.Contains(file)) ||
                inmate.CustodyPeriods.Any(period => period.NormalizedWard != null && period.NormalizedWard.Contains(ward)));
        }

        var rows = await items.OrderBy(inmate => inmate.NormalizedFullName).ThenBy(inmate => inmate.FileNumber).Take(take).ToListAsync(ct);
        return rows.Select(MapInmate).ToList();
    }

    public async Task<IReadOnlyList<HistoryRow>> HistoryAsync(Guid inmateId, CancellationToken ct)
    {
        var entryIds = await db.ReportEntryInmates.Where(link => link.InmateId == inmateId).Select(link => link.ReportEntryId).ToListAsync(ct);
        var entries = await db.ReportEntries.AsNoTracking().Where(entry => entryIds.Contains(entry.Id)).OrderBy(entry => entry.EventAt).ToListAsync(ct);
        var absences = await db.TemporaryAbsences.AsNoTracking().Where(absence => absence.InmateId == inmateId).ToListAsync(ct);
        return entries.Select(entry =>
        {
            var destination = absences.FirstOrDefault(absence => absence.DepartureReportEntryId == entry.Id || absence.ReturnReportEntryId == entry.Id)?.Destination ?? string.Empty;
            return new HistoryRow(entry.EventAt, Labels.Entry(entry.EntryType), destination, entry.ReportText, entry.Id);
        }).ToList();
    }

    public Task<bool> HasOpenAbsenceAsync(Guid inmateId, CancellationToken ct) =>
        db.TemporaryAbsences.AnyAsync(absence => absence.InmateId == inmateId && absence.Status == TemporaryAbsenceStatus.Open, ct);

    public async Task<IReadOnlyList<OpenPersonRow>> OpenPeopleAsync(CancellationToken ct)
    {
        var absences = await db.TemporaryAbsences.AsNoTracking().Where(absence => absence.Status == TemporaryAbsenceStatus.Open).OrderBy(absence => absence.DepartedAt).ToListAsync(ct);
        var inmateIds = absences.Select(absence => absence.InmateId).Distinct().ToList();
        var inmates = await db.Inmates.AsNoTracking().Include(inmate => inmate.CustodyPeriods).Where(inmate => inmateIds.Contains(inmate.Id)).ToDictionaryAsync(inmate => inmate.Id, ct);
        return absences.Select(absence =>
        {
            var inmate = inmates[absence.InmateId];
            var watch = ReturnWatchRules.Evaluate(absence.ExpectedReturnAt, clock.UtcNow);
            return new OpenPersonRow(absence.Id, inmate.Id, inmate.FileNumber, inmate.FullName, inmate.ActiveCustody()?.Ward, absence.Kind, Labels.Kind(absence.Kind), absence.Destination, absence.DepartedAt, absence.ExpectedReturnAt, ReturnWatchRules.Label(watch), absence.DepartureShiftId, absence.DepartureVehicleVisitId);
        }).ToList();
    }

    public async Task<IReadOnlyList<TemporaryAbsence>> AbsencesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.TemporaryAbsences.Where(absence => ids.Contains(absence.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> OpenIdsForDepartureAsync(Guid departureReportEntryId, CancellationToken ct) =>
        await db.TemporaryAbsences.Where(absence => absence.DepartureReportEntryId == departureReportEntryId && absence.Status == TemporaryAbsenceStatus.Open).Select(absence => absence.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<TemporaryAbsence>> AbsencesForDepartureAsync(Guid departureReportEntryId, CancellationToken ct) =>
        await db.TemporaryAbsences.Where(absence => absence.DepartureReportEntryId == departureReportEntryId).ToListAsync(ct);

    public async Task<IReadOnlyList<TemporaryAbsence>> AbsencesForReturnAsync(Guid returnReportEntryId, CancellationToken ct) =>
        await db.TemporaryAbsences.Where(absence => absence.ReturnReportEntryId == returnReportEntryId).ToListAsync(ct);

    public Task AddAbsenceAsync(TemporaryAbsence absence, CancellationToken ct) => db.TemporaryAbsences.AddAsync(absence, ct).AsTask();

    public Task<ReportEntry?> ByIdempotencyAsync(string key, CancellationToken ct) =>
        db.ReportEntries.AsNoTracking().FirstOrDefaultAsync(entry => entry.IdempotencyKey == key, ct);

    public async Task<string> NextNumberAsync(CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT nextval('rapor.report_entry_number_seq')";
        if (db.Database.CurrentTransaction is not null)
            command.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
        var value = (long)(await command.ExecuteScalarAsync(ct))!;
        return RecordNumbers.Format(value);
    }

    public Task AddEntryAsync(ReportEntry entry, CancellationToken ct) => db.ReportEntries.AddAsync(entry, ct).AsTask();

    public Task<ReportEntry?> EntryAsync(Guid id, CancellationToken ct) =>
        db.ReportEntries.Include(entry => entry.People).Include(entry => entry.Revisions).FirstOrDefaultAsync(entry => entry.Id == id, ct);

    public async Task<Page<LedgerRow>> LedgerAsync(LedgerQuery query, CancellationToken ct)
    {
        var items = db.ReportEntries.AsNoTracking().AsQueryable();
        if (query.From is not null)
            items = items.Where(entry => entry.EventAt >= query.From);
        if (query.To is not null)
            items = items.Where(entry => entry.EventAt < query.To);
        if (query.Type is not null)
            items = items.Where(entry => entry.EntryType == query.Type);
        if (query.ShiftId is not null)
            items = items.Where(entry => entry.ShiftId == query.ShiftId);
        if (query.Status is not null)
            items = items.Where(entry => entry.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Number))
        {
            var number = query.Number.Trim();
            items = items.Where(entry => entry.RecordNumber.Contains(number));
        }

        var total = await items.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var rows = await items.OrderByDescending(entry => entry.EventAt).ThenByDescending(entry => entry.RecordedAt)
            .Skip((page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new Page<LedgerRow>(rows.Select(MapEntry).ToList(), page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<ReportEntry>> EntriesForShiftAsync(Guid shiftId, CancellationToken ct) =>
        await db.ReportEntries.AsNoTracking().Where(entry => entry.ShiftId == shiftId).OrderBy(entry => entry.EventAt).ToListAsync(ct);

    public async Task<IReadOnlyList<ReportEntry>> EntriesBetweenAsync(DateTimeOffset start, DateTimeOffset end, ReportEntryType? type, CancellationToken ct)
    {
        var items = db.ReportEntries.AsNoTracking().Where(entry => entry.EventAt >= start && entry.EventAt < end);
        if (type is not null)
            items = items.Where(entry => entry.EntryType == type);
        return await items.OrderBy(entry => entry.EventAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LedgerRow>> RecentEntriesAsync(int take, CancellationToken ct) =>
        (await db.ReportEntries.AsNoTracking().OrderByDescending(entry => entry.RecordedAt).Take(take).ToListAsync(ct)).Select(MapEntry).ToList();

    public async Task<IReadOnlyList<LedgerRow>> FollowUpsAsync(CancellationToken ct)
    {
        var rows = await db.GeneralEventDetails.Where(detail => detail.FollowUp)
            .Join(db.ReportEntries.Where(entry => entry.Status != ReportEntryStatus.Cancelled), detail => detail.ReportEntryId, entry => entry.Id, (_, entry) => entry)
            .OrderBy(entry => entry.EventAt).ToListAsync(ct);
        return rows.Select(MapEntry).ToList();
    }

    public Task AddAdmissionAsync(AdmissionDetail detail, CancellationToken ct) => db.AdmissionDetails.AddAsync(detail, ct).AsTask();
    public Task AddReleaseAsync(ReleaseDetail detail, CancellationToken ct) => db.ReleaseDetails.AddAsync(detail, ct).AsTask();
    public Task AddTransferAsync(TransferDetail detail, CancellationToken ct) => db.TransferDetails.AddAsync(detail, ct).AsTask();
    public Task AddVisitorAsync(VisitorDetail detail, CancellationToken ct) => db.VisitorDetails.AddAsync(detail, ct).AsTask();
    public Task AddMaterialAsync(MaterialDetail detail, CancellationToken ct) => db.MaterialDetails.AddAsync(detail, ct).AsTask();
    public Task AddGeneralAsync(GeneralEventDetail detail, CancellationToken ct) => db.GeneralEventDetails.AddAsync(detail, ct).AsTask();
    public Task AddHandoverNoteAsync(HandoverNoteDetail detail, CancellationToken ct) => db.HandoverNoteDetails.AddAsync(detail, ct).AsTask();
    public Task<AdmissionDetail?> AdmissionAsync(Guid reportEntryId, CancellationToken ct) => db.AdmissionDetails.FirstOrDefaultAsync(detail => detail.ReportEntryId == reportEntryId, ct);
    public Task<ReleaseDetail?> ReleaseAsync(Guid reportEntryId, CancellationToken ct) => db.ReleaseDetails.FirstOrDefaultAsync(detail => detail.ReportEntryId == reportEntryId, ct);
    public Task<TransferDetail?> TransferAsync(Guid reportEntryId, CancellationToken ct) => db.TransferDetails.FirstOrDefaultAsync(detail => detail.ReportEntryId == reportEntryId, ct);
    public Task<VisitorDetail?> VisitorAsync(Guid reportEntryId, CancellationToken ct) => db.VisitorDetails.FirstOrDefaultAsync(detail => detail.ReportEntryId == reportEntryId, ct);
    public Task<MaterialDetail?> MaterialAsync(Guid reportEntryId, CancellationToken ct) => db.MaterialDetails.FirstOrDefaultAsync(detail => detail.ReportEntryId == reportEntryId, ct);
    public Task<GeneralEventDetail?> GeneralAsync(Guid reportEntryId, CancellationToken ct) => db.GeneralEventDetails.FirstOrDefaultAsync(detail => detail.ReportEntryId == reportEntryId, ct);
    public Task<HandoverNoteDetail?> HandoverNoteAsync(Guid reportEntryId, CancellationToken ct) => db.HandoverNoteDetails.FirstOrDefaultAsync(detail => detail.ReportEntryId == reportEntryId, ct);

    public Task AddSnapshotAsync(ReportSnapshot snapshot, CancellationToken ct) => db.ReportSnapshots.AddAsync(snapshot, ct).AsTask();

    public Task<ReportSnapshot?> FirstShiftSnapshotAsync(Guid shiftId, CancellationToken ct) =>
        db.ReportSnapshots.AsNoTracking().Where(snapshot => snapshot.Kind == SnapshotKind.ShiftClose && snapshot.ShiftId == shiftId).OrderBy(snapshot => snapshot.Version).FirstOrDefaultAsync(ct);

    public Task<ReportSnapshot?> LatestSnapshotAsync(SnapshotKind kind, Guid? shiftId, DateTimeOffset periodStart, CancellationToken ct) =>
        db.ReportSnapshots.AsNoTracking().Where(snapshot => snapshot.Kind == kind && snapshot.ShiftId == shiftId && snapshot.PeriodStart == periodStart).OrderByDescending(snapshot => snapshot.Version).FirstOrDefaultAsync(ct);

    public async Task<int> NextSnapshotVersionAsync(SnapshotKind kind, Guid? shiftId, DateTimeOffset periodStart, CancellationToken ct)
    {
        var max = await db.ReportSnapshots.Where(snapshot => snapshot.Kind == kind && snapshot.ShiftId == shiftId && snapshot.PeriodStart == periodStart).MaxAsync(snapshot => (int?)snapshot.Version, ct);
        return (max ?? 0) + 1;
    }

    public Task<VehicleCard?> CardByPlateAsync(string normalizedPlate, CancellationToken ct) =>
        db.VehicleCards.FirstOrDefaultAsync(card => card.NormalizedPlate == normalizedPlate, ct);

    public Task AddCardAsync(VehicleCard card, CancellationToken ct) => db.VehicleCards.AddAsync(card, ct).AsTask();

    public Task<VehicleVisit?> OpenVisitAsync(Guid cardId, CancellationToken ct) =>
        db.VehicleVisits.FirstOrDefaultAsync(visit => visit.VehicleCardId == cardId && visit.Status == VehicleVisitStatus.Open, ct);

    public Task<VehicleVisit?> VisitAsync(Guid id, CancellationToken ct) => db.VehicleVisits.FirstOrDefaultAsync(visit => visit.Id == id, ct);

    public Task AddVisitAsync(VehicleVisit visit, CancellationToken ct) => db.VehicleVisits.AddAsync(visit, ct).AsTask();

    public Task<VehicleType?> VehicleTypeAsync(Guid id, CancellationToken ct) => db.VehicleTypes.FirstOrDefaultAsync(type => type.Id == id, ct);

    public async Task<IReadOnlyList<VehicleType>> VehicleTypesAsync(bool activeOnly, CancellationToken ct)
    {
        var items = db.VehicleTypes.AsQueryable();
        if (activeOnly)
            items = items.Where(type => type.IsActive);
        return await items.OrderBy(type => type.SortOrder).ToListAsync(ct);
    }

    public Task AddVehicleTypeAsync(VehicleType type, CancellationToken ct) => db.VehicleTypes.AddAsync(type, ct).AsTask();

    public async Task<IReadOnlyList<VehicleVisitRow>> VisitsByPlateAsync(string normalizedPlate, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var items = from visit in db.VehicleVisits
                    join card in db.VehicleCards on visit.VehicleCardId equals card.Id
                    join type in db.VehicleTypes on visit.VehicleTypeId equals type.Id
                    join entry in db.ReportEntries on visit.StartReportEntryId equals entry.Id
                    where card.NormalizedPlate == normalizedPlate
                    select new { visit, card, type, entry };
        if (from is not null)
            items = items.Where(row => row.visit.StartedAt >= from);
        if (to is not null)
            items = items.Where(row => row.visit.StartedAt < to);
        var rows = await items.OrderByDescending(row => row.visit.StartedAt).ToListAsync(ct);
        return rows.Select(row => new VehicleVisitRow(row.visit.Id, row.card.PlateDisplay, row.type.Name, row.visit.Status == VehicleVisitStatus.Open ? "Açık" : row.visit.Status == VehicleVisitStatus.Completed ? "Tamamlandı" : "Bağımsız", Labels.Inference(row.visit.LocationInference), row.visit.StartedAt, row.visit.EndedAt, row.visit.Purpose, row.visit.DriverName, row.entry.RecordNumber)).ToList();
    }

    public async Task<IReadOnlyList<OpenVehicleRow>> OpenVisitsAsync(CancellationToken ct)
    {
        var rows = await (from visit in db.VehicleVisits
                          join card in db.VehicleCards on visit.VehicleCardId equals card.Id
                          join type in db.VehicleTypes on visit.VehicleTypeId equals type.Id
                          where visit.Status == VehicleVisitStatus.Open && visit.LocationInference != LocationInference.None
                          orderby visit.StartedAt
                          select new OpenVehicleRow(visit.Id, card.PlateDisplay, type.Name, Labels.Inference(visit.LocationInference), visit.StartedAt, visit.Purpose)).ToListAsync(ct);
        return rows;
    }

    public Task AddAuditAsync(AuditEvent audit, CancellationToken ct) => db.AuditEvents.AddAsync(audit, ct).AsTask();

    public async Task<IReadOnlyList<AuditRow>> AuditsAsync(int take, CancellationToken ct)
    {
        var rows = await db.AuditEvents.AsNoTracking().OrderByDescending(item => item.OccurredAt).Take(take).ToListAsync(ct);
        var names = await NamesAsync(rows.Where(item => item.UserId != null).Select(item => item.UserId!.Value).ToList(), ct);
        return rows.Select(item => new AuditRow(item.OccurredAt, item.UserId is null ? null : names.GetValueOrDefault(item.UserId.Value), item.Action, item.EntityName, item.EntityId, item.Details)).ToList();
    }

    public Task<BackupLog?> LatestBackupAsync(CancellationToken ct) =>
        db.BackupLogs.AsNoTracking().OrderByDescending(item => item.VerifiedAt).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<UserPick>> ActiveUsersAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(user => user.IsActive).OrderBy(user => user.DisplayName)
            .Select(user => new UserPick(user.Id, user.UserName ?? string.Empty, user.DisplayName)).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> UserNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var names = await NamesAsync(userIds.Where(id => id != Guid.Empty).ToList(), ct);
        return userIds.Select(id => names.GetValueOrDefault(id) ?? string.Empty).ToList();
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(user => distinct.Contains(user.Id)).Select(user => new { user.Id, user.DisplayName, user.UserName }).ToListAsync(ct);
        return users.ToDictionary(user => user.Id, user => string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName ?? string.Empty : user.DisplayName);
    }

    private static InmateRow MapInmate(Inmate inmate)
    {
        var custody = inmate.ActiveCustody();
        return new InmateRow(inmate.Id, inmate.FileNumber, inmate.FullName, custody?.Ward, custody is not null, custody is null ? "Kurumda değil" : "Kurumda");
    }

    private static LedgerRow MapEntry(ReportEntry entry) =>
        new(entry.Id, entry.RecordNumber, entry.EventAt, entry.RecordedAt, entry.EntryType, Labels.Entry(entry.EntryType), entry.Status, Labels.Status(entry.Status), entry.ReportText, entry.ShiftId);
}
