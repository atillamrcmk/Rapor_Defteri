namespace RaporDefteri.Domain;

public sealed class ReportEntry
{
    private ReportEntry()
    {
    }

    public Guid Id { get; private set; }
    public string RecordNumber { get; private set; } = string.Empty;
    public DateTimeOffset EventAt { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public ReportEntryType EntryType { get; private set; }
    public Guid ShiftId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string ReportText { get; private set; } = string.Empty;
    public string OriginalReportText { get; private set; } = string.Empty;
    public string? AdditionalNote { get; private set; }
    public ReportEntryStatus Status { get; private set; }
    public string? LateEntryReason { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public List<ReportEntryInmate> People { get; private set; } = [];
    public List<ReportRevision> Revisions { get; private set; } = [];

    public static ReportEntry Create(
        string recordNumber,
        DateTimeOffset eventAt,
        DateTimeOffset recordedAt,
        ReportEntryType entryType,
        Guid shiftId,
        Guid createdBy,
        string reportText,
        string? additionalNote,
        string? lateEntryReason,
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(reportText))
            throw new DomainRuleException("Rapor metni boş olamaz.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainRuleException("İstek anahtarı zorunludur.");

        return new ReportEntry
        {
            Id = Guid.CreateVersion7(),
            RecordNumber = recordNumber,
            EventAt = eventAt,
            RecordedAt = recordedAt,
            EntryType = entryType,
            ShiftId = shiftId,
            CreatedByUserId = createdBy,
            ReportText = reportText.Trim(),
            OriginalReportText = reportText.Trim(),
            AdditionalNote = Clean(additionalNote),
            Status = ReportEntryStatus.Valid,
            LateEntryReason = Clean(lateEntryReason),
            IdempotencyKey = idempotencyKey.Trim()
        };
    }

    public ReportRevision Correct(string newReportText, string? newAdditionalNote, DateTimeOffset newEventAt, string reason, Guid userId, DateTimeOffset now, string oldValues, string newValues)
    {
        if (Status == ReportEntryStatus.Cancelled)
            throw new DomainRuleException("İptal edilmiş kayıt düzeltilemez.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("Düzeltme gerekçesi zorunludur.");
        if (string.IsNullOrWhiteSpace(newReportText))
            throw new DomainRuleException("Rapor metni boş olamaz.");

        var revision = new ReportRevision
        {
            Id = Guid.CreateVersion7(),
            ReportEntryId = Id,
            RevisedAt = now,
            RevisedByUserId = userId,
            Reason = reason.Trim(),
            OldValues = oldValues,
            NewValues = newValues,
            OldReportText = ReportText,
            NewReportText = newReportText.Trim()
        };
        Revisions.Add(revision);
        ReportText = newReportText.Trim();
        AdditionalNote = Clean(newAdditionalNote);
        EventAt = newEventAt;
        Status = ReportEntryStatus.Corrected;
        return revision;
    }

    public void Cancel(string reason, Guid userId, DateTimeOffset now)
    {
        if (Status == ReportEntryStatus.Cancelled)
            throw new DomainRuleException("Kayıt zaten iptal edilmiş.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("İptal gerekçesi zorunludur.");

        Status = ReportEntryStatus.Cancelled;
        CancellationReason = reason.Trim();
        CancelledByUserId = userId;
        CancelledAt = now;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ReportEntryInmate
{
    public Guid ReportEntryId { get; set; }
    public Guid InmateId { get; set; }
}

public sealed class ReportRevision
{
    public Guid Id { get; set; }
    public Guid ReportEntryId { get; set; }
    public DateTimeOffset RevisedAt { get; set; }
    public Guid RevisedByUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string OldValues { get; set; } = string.Empty;
    public string NewValues { get; set; } = string.Empty;
    public string OldReportText { get; set; } = string.Empty;
    public string NewReportText { get; set; } = string.Empty;
}

public sealed class ReportSnapshot
{
    public Guid Id { get; set; }
    public SnapshotKind Kind { get; set; }
    public Guid? ShiftId { get; set; }
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BodyJson { get; set; } = string.Empty;
}

public sealed class AdmissionDetail
{
    public Guid ReportEntryId { get; set; }
    public Guid InmateId { get; set; }
    public Guid CustodyPeriodId { get; set; }
    public string? Ward { get; set; }
}

public sealed class ReleaseDetail
{
    public Guid ReportEntryId { get; set; }
    public Guid InmateId { get; set; }
    public Guid CustodyPeriodId { get; set; }
    public string? Reason { get; set; }
}

public sealed class TransferDetail
{
    public Guid ReportEntryId { get; set; }
    public Guid InmateId { get; set; }
    public Guid CustodyPeriodId { get; set; }
    public string DestinationInstitution { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class VisitorDetail
{
    public Guid ReportEntryId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Organization { get; set; }
    public string? Purpose { get; set; }
}

public sealed class MaterialDetail
{
    public Guid ReportEntryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public MaterialDirection Direction { get; set; }
    public string? Counterparty { get; set; }
}

public sealed class GeneralEventDetail
{
    public Guid ReportEntryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool FollowUp { get; set; }
}

public sealed class HandoverNoteDetail
{
    public Guid ReportEntryId { get; set; }
    public string Note { get; set; } = string.Empty;
}
