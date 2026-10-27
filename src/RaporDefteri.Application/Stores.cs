using RaporDefteri.Domain;

namespace RaporDefteri.Application;

public interface IInstitutionClock
{
    TimeZoneInfo Zone { get; }
    DateTimeOffset UtcNow { get; }
    DateTimeOffset FromLocal(DateTime unspecifiedLocal);
    DateTime ToLocal(DateTimeOffset value);
    string Format(DateTimeOffset value);
    TimeOnly LocalTime(DateTimeOffset value) => TimeOnly.FromDateTime(ToLocal(value));
}

public interface IInstitutionProfile
{
    string Name { get; }
    string UnitName { get; }
}

public interface IRaporStore
{
    Task SaveAsync(CancellationToken ct);
    void Clear();
    Task<uint> TokenAsync<T>(T entity, CancellationToken ct) where T : class;

    Task<Shift?> ActiveShiftAsync(CancellationToken ct);
    Task<Shift?> ShiftAsync(Guid id, CancellationToken ct);
    Task<ShiftGate> GateAsync(CancellationToken ct);
    Task AddShiftAsync(Shift shift, CancellationToken ct);
    Task<IReadOnlyList<ShiftSummary>> RecentShiftsAsync(CancellationToken ct);

    Task<Inmate?> InmateAsync(Guid id, CancellationToken ct);
    Task<Inmate?> InmateByFileAsync(string normalizedFileNumber, CancellationToken ct);
    Task<bool> FileTakenAsync(string normalizedFileNumber, Guid? exceptInmateId, CancellationToken ct);
    Task AddInmateAsync(Inmate inmate, CancellationToken ct);
    Task<IReadOnlyList<InmateRow>> SearchInmatesAsync(string? query, int take, CancellationToken ct);
    Task<IReadOnlyList<HistoryRow>> HistoryAsync(Guid inmateId, CancellationToken ct);

    Task<bool> HasOpenAbsenceAsync(Guid inmateId, CancellationToken ct);
    Task<IReadOnlyList<OpenPersonRow>> OpenPeopleAsync(CancellationToken ct);
    Task<IReadOnlyList<TemporaryAbsence>> AbsencesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<Guid>> OpenIdsForDepartureAsync(Guid departureReportEntryId, CancellationToken ct);
    Task<IReadOnlyList<TemporaryAbsence>> AbsencesForDepartureAsync(Guid departureReportEntryId, CancellationToken ct);
    Task<IReadOnlyList<TemporaryAbsence>> AbsencesForReturnAsync(Guid returnReportEntryId, CancellationToken ct);
    Task AddAbsenceAsync(TemporaryAbsence absence, CancellationToken ct);

    Task<ReportEntry?> ByIdempotencyAsync(string key, CancellationToken ct);
    Task<string> NextNumberAsync(CancellationToken ct);
    Task AddEntryAsync(ReportEntry entry, CancellationToken ct);
    Task<ReportEntry?> EntryAsync(Guid id, CancellationToken ct);
    Task<Page<LedgerRow>> LedgerAsync(LedgerQuery query, CancellationToken ct);
    Task<IReadOnlyList<ReportEntry>> EntriesForShiftAsync(Guid shiftId, CancellationToken ct);
    Task<IReadOnlyList<ReportEntry>> EntriesBetweenAsync(DateTimeOffset start, DateTimeOffset end, ReportEntryType? type, CancellationToken ct);
    Task<IReadOnlyList<LedgerRow>> RecentEntriesAsync(int take, CancellationToken ct);
    Task<IReadOnlyList<LedgerRow>> FollowUpsAsync(CancellationToken ct);

    Task AddAdmissionAsync(AdmissionDetail detail, CancellationToken ct);
    Task AddReleaseAsync(ReleaseDetail detail, CancellationToken ct);
    Task AddTransferAsync(TransferDetail detail, CancellationToken ct);
    Task AddVisitorAsync(VisitorDetail detail, CancellationToken ct);
    Task AddMaterialAsync(MaterialDetail detail, CancellationToken ct);
    Task AddGeneralAsync(GeneralEventDetail detail, CancellationToken ct);
    Task AddHandoverNoteAsync(HandoverNoteDetail detail, CancellationToken ct);
    Task<AdmissionDetail?> AdmissionAsync(Guid reportEntryId, CancellationToken ct);
    Task<ReleaseDetail?> ReleaseAsync(Guid reportEntryId, CancellationToken ct);
    Task<TransferDetail?> TransferAsync(Guid reportEntryId, CancellationToken ct);
    Task<VisitorDetail?> VisitorAsync(Guid reportEntryId, CancellationToken ct);
    Task<MaterialDetail?> MaterialAsync(Guid reportEntryId, CancellationToken ct);
    Task<GeneralEventDetail?> GeneralAsync(Guid reportEntryId, CancellationToken ct);
    Task<HandoverNoteDetail?> HandoverNoteAsync(Guid reportEntryId, CancellationToken ct);

    Task AddSnapshotAsync(ReportSnapshot snapshot, CancellationToken ct);
    Task<ReportSnapshot?> FirstShiftSnapshotAsync(Guid shiftId, CancellationToken ct);
    Task<ReportSnapshot?> LatestSnapshotAsync(SnapshotKind kind, Guid? shiftId, DateTimeOffset periodStart, CancellationToken ct);
    Task<int> NextSnapshotVersionAsync(SnapshotKind kind, Guid? shiftId, DateTimeOffset periodStart, CancellationToken ct);

    Task<VehicleCard?> CardByPlateAsync(string normalizedPlate, CancellationToken ct);
    Task AddCardAsync(VehicleCard card, CancellationToken ct);
    Task<VehicleVisit?> OpenVisitAsync(Guid cardId, CancellationToken ct);
    Task<VehicleVisit?> VisitAsync(Guid id, CancellationToken ct);
    Task AddVisitAsync(VehicleVisit visit, CancellationToken ct);
    Task<VehicleType?> VehicleTypeAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<VehicleType>> VehicleTypesAsync(bool activeOnly, CancellationToken ct);
    Task AddVehicleTypeAsync(VehicleType type, CancellationToken ct);
    Task<IReadOnlyList<VehicleVisitRow>> VisitsByPlateAsync(string normalizedPlate, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct);
    Task<IReadOnlyList<OpenVehicleRow>> OpenVisitsAsync(CancellationToken ct);

    Task AddAuditAsync(AuditEvent audit, CancellationToken ct);
    Task<IReadOnlyList<AuditRow>> AuditsAsync(int take, CancellationToken ct);
    Task<BackupLog?> LatestBackupAsync(CancellationToken ct);
    Task<IReadOnlyList<UserPick>> ActiveUsersAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> UserNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}

public interface IUserAdmin
{
    Task<bool> AnyUserAsync(CancellationToken ct);
    Task<IReadOnlyList<UserAccountRow>> ListAsync(CancellationToken ct);
    Task<OperationResult<Guid>> CreateAsync(string userName, string displayName, string password, IReadOnlyCollection<string> roles, CancellationToken ct);
    Task<OperationResult<bool>> UpdateAsync(Guid userId, string displayName, IReadOnlyCollection<string> roles, bool isActive, string? newPassword, CancellationToken ct);
}

public interface IDemoData
{
    Task<string> SeedAsync(CancellationToken ct);
}
