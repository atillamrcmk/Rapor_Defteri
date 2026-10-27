namespace RaporDefteri.Domain;

public sealed class Shift
{
    private Shift()
    {
    }

    public Guid Id { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public ShiftStatus Status { get; private set; }
    public Guid OpenedByUserId { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public string? HandoverNote { get; private set; }
    public Guid? PreparedByUserId { get; private set; }
    public DateTimeOffset? PreparedAt { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public string? CloseNote { get; private set; }
    public List<ShiftDuty> Duties { get; private set; } = [];

    public static Shift Open(DateTimeOffset startsAt, DateTimeOffset endsAt, Guid openedBy, DateTimeOffset openedAt, IReadOnlyCollection<Guid> dutyUserIds)
    {
        var errors = ShiftRules.ValidateNew(startsAt, endsAt, anotherActive: false);
        if (errors.Count > 0)
            throw new DomainRuleException(errors[0]);
        if (dutyUserIds.Count == 0)
            throw new DomainRuleException("Vardiyada en az bir görevli seçilmelidir.");

        var shift = new Shift
        {
            Id = Guid.CreateVersion7(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = ShiftStatus.Open,
            OpenedByUserId = openedBy,
            OpenedAt = openedAt
        };
        foreach (var userId in dutyUserIds.Distinct())
            shift.Duties.Add(new ShiftDuty { ShiftId = shift.Id, UserId = userId, AssignedAt = openedAt });
        return shift;
    }

    public void MarkReady(Guid userId, DateTimeOffset now, string? note)
    {
        if (Status != ShiftStatus.Open)
            throw new DomainRuleException("Yalnızca açık vardiya devre hazırlanabilir.");
        Status = ShiftStatus.ReadyForHandover;
        PreparedByUserId = userId;
        PreparedAt = now;
        HandoverNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void AcceptAndClose(Guid userId, DateTimeOffset serverNow, string? closeNote)
    {
        var errors = ShiftRules.ValidateAccept(Status);
        if (errors.Count > 0)
            throw new DomainRuleException(errors[0]);

        AcceptedByUserId = userId;
        AcceptedAt = serverNow;
        ClosedByUserId = userId;
        ClosedAt = serverNow;
        CloseNote = string.IsNullOrWhiteSpace(closeNote) ? null : closeNote.Trim();
        Status = ShiftStatus.Closed;
    }
}

public sealed class ShiftDuty
{
    public Guid ShiftId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
}

public sealed class ShiftGate
{
    public int Id { get; set; } = 1;
    public Guid? ActiveShiftId { get; set; }
}
