using RaporDefteri.Domain;

namespace RaporDefteri.Application;

public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record ShiftSummary(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt, ShiftStatus Status, string StatusText, IReadOnlyList<string> DutyNames);
public sealed record InmateRow(Guid Id, string FileNumber, string FullName, string? Ward, bool Active, string Presence);
public sealed record HistoryRow(DateTimeOffset EventAt, string Kind, string Destination, string Text, Guid? ReportEntryId);
public sealed record OpenPersonRow(Guid AbsenceId, Guid InmateId, string FileNumber, string FullName, string? Ward, TemporaryMovementKind Kind, string KindText, string Destination, DateTimeOffset DepartedAt, DateTimeOffset? ExpectedReturnAt, string WatchText, Guid DepartureShiftId, Guid? VehicleVisitId);
public sealed record LedgerRow(Guid Id, string RecordNumber, DateTimeOffset EventAt, DateTimeOffset RecordedAt, ReportEntryType Type, string TypeText, ReportEntryStatus Status, string StatusText, string Text, Guid ShiftId);
public sealed record LedgerQuery(DateTimeOffset? From, DateTimeOffset? To, ReportEntryType? Type, Guid? ShiftId, ReportEntryStatus? Status, string? Number, int Page, int PageSize);
public sealed record OpenVehicleRow(Guid VisitId, string Plate, string TypeName, string InferenceText, DateTimeOffset StartedAt, string Purpose);
public sealed record VehicleVisitRow(Guid VisitId, string Plate, string TypeName, string StatusText, string InferenceText, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string Purpose, string? Driver, string RecordNumber);
public sealed record UserPick(Guid Id, string UserName, string DisplayName);
public sealed record UserAccountRow(Guid Id, string UserName, string DisplayName, bool IsActive, IReadOnlyList<string> Roles);
public sealed record AuditRow(DateTimeOffset At, string? UserName, string Action, string EntityName, string EntityId, string Details);
public sealed record DashboardModel(ShiftSummary? Shift, IReadOnlyList<LedgerRow> Recent, IReadOnlyList<OpenPersonRow> OpenPeople, IReadOnlyList<OpenVehicleRow> OpenVehicles, IReadOnlyList<LedgerRow> FollowUps, BackupLog? Backup);
public sealed record HandoverModel(ShiftSummary Shift, IReadOnlyList<OpenPersonRow> OpenPeople, IReadOnlyList<OpenVehicleRow> OpenVehicles, IReadOnlyList<LedgerRow> FollowUps, IReadOnlyList<LedgerRow> Notes);
public sealed record EntryDetail(
    ReportEntry Entry,
    string TypeText,
    string StatusText,
    uint Token,
    string ShiftText,
    IReadOnlyList<InmateRow> People,
    string? VehicleText,
    bool CanCorrect,
    bool CanCancel);
public sealed record PrintDocument(string Institution, string Title, string Period, string? Version, IReadOnlyList<PrintRow> Rows, string? Handover, string? HandedOverBy, string? AcceptedBy, DateTimeOffset GeneratedAt);
public sealed record PrintRow(string RecordNumber, string EventTime, string RecordedTime, string Text, string? Note, string Status);

public sealed record DepartCommand(DateTimeOffset EventAt, TemporaryMovementKind Kind, string Destination, string Reason, string? Escort, DateTimeOffset? ExpectedReturn, string? LateReason, string? AdditionalNote, IReadOnlyList<Guid> InmateIds, Guid? VehicleVisitId, string IdempotencyKey);
public sealed record ReturnCommand(DateTimeOffset EventAt, string? LateReason, string? AdditionalNote, IReadOnlyList<Guid> AbsenceIds, Guid? VehicleVisitId, string IdempotencyKey);
public sealed record AdmissionCommand(DateTimeOffset EventAt, string? LateReason, string? AdditionalNote, string FileNumber, string FirstName, string LastName, string? Ward, Guid? ExistingInmateId, string IdempotencyKey);
public sealed record ReleaseCommand(DateTimeOffset EventAt, string? LateReason, string? AdditionalNote, Guid InmateId, string? Reason, string IdempotencyKey);
public sealed record TransferCommand(DateTimeOffset EventAt, string? LateReason, string? AdditionalNote, Guid InmateId, string Destination, string? Reason, string IdempotencyKey);
public sealed record VehicleCommand(DateTimeOffset EventAt, string? LateReason, string? AdditionalNote, string Plate, VehicleBaseLocation Location, Guid VehicleTypeId, PassageDirection Direction, string Purpose, string? Driver, string? Organization, IReadOnlyList<Guid> LinkAbsenceIds, string IdempotencyKey);
public sealed record SimpleRecordCommand(DateTimeOffset EventAt, string? LateReason, string? AdditionalNote, ReportEntryType Type, string Title, string? Body, string? Organization, string? Counterparty, MaterialDirection? MaterialDirection, bool FollowUp, string IdempotencyKey);
public sealed record CorrectCommand(Guid EntryId, uint Token, DateTimeOffset EventAt, string? AdditionalNote, string? Primary, string? Secondary, string Reason);
public sealed record OpenShiftCommand(DateTimeOffset StartsAt, DateTimeOffset EndsAt, IReadOnlyList<Guid> DutyUserIds);

public static class Labels
{
    public static string Entry(ReportEntryType type) => type switch
    {
        ReportEntryType.HospitalDeparture => "Hastane çıkışı",
        ReportEntryType.HospitalReturn => "Hastane dönüşü",
        ReportEntryType.CourtDeparture => "Adliye çıkışı",
        ReportEntryType.CourtReturn => "Adliye dönüşü",
        ReportEntryType.OtherDeparture => "Kurum dışı çıkış",
        ReportEntryType.OtherReturn => "Kurum dışı dönüş",
        ReportEntryType.Admission => "Kuruma kabul",
        ReportEntryType.Release => "Tahliye",
        ReportEntryType.Transfer => "Kalıcı sevk",
        ReportEntryType.VehiclePassage => "Araç geçişi",
        ReportEntryType.Visitor => "Görevli/ziyaretçi",
        ReportEntryType.MaterialDelivery => "Malzeme teslimi",
        ReportEntryType.GeneralEvent => "Genel olay",
        ReportEntryType.HandoverNote => "Devir notu",
        _ => type.ToString()
    };

    public static string Status(ReportEntryStatus status) => status switch
    {
        ReportEntryStatus.Valid => "Geçerli",
        ReportEntryStatus.Corrected => "Düzeltilmiş",
        ReportEntryStatus.Cancelled => "İptal",
        _ => status.ToString()
    };

    public static string Custody(CustodyStatus status) => status switch
    {
        CustodyStatus.Active => "Kurumda",
        CustodyStatus.Released => "Tahliye",
        CustodyStatus.Transferred => "Sevk",
        CustodyStatus.Cancelled => "İptal",
        _ => status.ToString()
    };

    public static string Shift(ShiftStatus status) => status switch
    {
        ShiftStatus.Open => "Açık",
        ShiftStatus.ReadyForHandover => "Devre hazır",
        ShiftStatus.Closed => "Kapalı",
        _ => status.ToString()
    };

    public static string Kind(TemporaryMovementKind kind) => kind switch
    {
        TemporaryMovementKind.Hospital => "Hastane",
        TemporaryMovementKind.Court => "Adliye",
        _ => "Diğer kurum dışı"
    };

    public static string Direction(PassageDirection direction) =>
        direction == PassageDirection.Entry ? "Giriş" : "Çıkış";

    public static string Location(VehicleBaseLocation location) => location switch
    {
        VehicleBaseLocation.External => "Dışarıdan gelen",
        VehicleBaseLocation.Institution => "Kurumda bulunan",
        _ => "Konum bilinmiyor"
    };

    public static string Inference(LocationInference inference) => inference switch
    {
        LocationInference.AwaitingExit => "Çıkış bekleniyor",
        LocationInference.AwaitingReturn => "Dönüş bekleniyor",
        _ => "Konum çıkarımı yok"
    };
}
