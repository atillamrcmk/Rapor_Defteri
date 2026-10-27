namespace RaporDefteri.Domain;

public sealed class TemporaryAbsence
{
    private TemporaryAbsence()
    {
    }

    public Guid Id { get; private set; }
    public Guid InmateId { get; private set; }
    public Guid CustodyPeriodId { get; private set; }
    public TemporaryMovementKind Kind { get; private set; }
    public string Destination { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset DepartedAt { get; private set; }
    public DateTimeOffset? ExpectedReturnAt { get; private set; }
    public DateTimeOffset? ReturnedAt { get; private set; }
    public Guid DepartureReportEntryId { get; private set; }
    public Guid? ReturnReportEntryId { get; private set; }
    public Guid? DepartureVehicleVisitId { get; private set; }
    public Guid? ReturnVehicleVisitId { get; private set; }
    public string? EscortNote { get; private set; }
    public TemporaryAbsenceStatus Status { get; private set; }
    public Guid DepartureShiftId { get; private set; }
    public Guid? ReturnShiftId { get; private set; }

    public static TemporaryAbsence Open(
        Guid inmateId,
        Guid custodyPeriodId,
        TemporaryMovementKind kind,
        string destination,
        string reason,
        DateTimeOffset departedAt,
        DateTimeOffset? expectedReturnAt,
        Guid departureReportEntryId,
        Guid departureShiftId,
        Guid? vehicleVisitId,
        string? escortNote)
    {
        return new TemporaryAbsence
        {
            Id = Guid.CreateVersion7(),
            InmateId = inmateId,
            CustodyPeriodId = custodyPeriodId,
            Kind = kind,
            Destination = destination.Trim(),
            Reason = reason.Trim(),
            DepartedAt = departedAt,
            ExpectedReturnAt = expectedReturnAt,
            DepartureReportEntryId = departureReportEntryId,
            DepartureShiftId = departureShiftId,
            DepartureVehicleVisitId = vehicleVisitId,
            EscortNote = string.IsNullOrWhiteSpace(escortNote) ? null : escortNote.Trim(),
            Status = TemporaryAbsenceStatus.Open
        };
    }

    public void MarkReturned(DateTimeOffset returnedAt, Guid returnReportEntryId, Guid returnShiftId, Guid? vehicleVisitId)
    {
        var errors = MovementRules.ValidateReturn(Status, DepartedAt, returnedAt);
        if (errors.Count > 0)
            throw new DomainRuleException(errors[0]);

        Status = TemporaryAbsenceStatus.Returned;
        ReturnedAt = returnedAt;
        ReturnReportEntryId = returnReportEntryId;
        ReturnShiftId = returnShiftId;
        ReturnVehicleVisitId = vehicleVisitId;
    }

    public void CancelOpen()
    {
        if (Status != TemporaryAbsenceStatus.Open)
            throw new DomainRuleException("Dönüş kaydı olan çıkış bu işlemle iptal edilemez.");
        Status = TemporaryAbsenceStatus.Cancelled;
    }

    public void CorrectFacts(string destination, string reason)
    {
        if (Status == TemporaryAbsenceStatus.Cancelled)
            throw new DomainRuleException("İptal edilmiş hareket düzeltilemez.");
        if (string.IsNullOrWhiteSpace(destination) || string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("Gidilen yer ve gerekçe zorunludur.");
        Destination = destination.Trim();
        Reason = reason.Trim();
    }

    public void LinkVehicle(Guid visitId, bool asReturn)
    {
        if (asReturn)
            ReturnVehicleVisitId = visitId;
        else
            DepartureVehicleVisitId = visitId;
    }

    public void ReopenAfterReturnCancelled()
    {
        if (Status != TemporaryAbsenceStatus.Returned)
            throw new DomainRuleException("Yalnızca dönüşü kaydedilmiş hareket yeniden açılabilir.");
        Status = TemporaryAbsenceStatus.Open;
        ReturnedAt = null;
        ReturnReportEntryId = null;
        ReturnShiftId = null;
        ReturnVehicleVisitId = null;
    }
}
