namespace RaporDefteri.Domain;

public sealed class Inmate
{
    private Inmate()
    {
    }

    public Guid Id { get; private set; }
    public string FileNumber { get; private set; } = string.Empty;
    public string NormalizedFileNumber { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string NormalizedFullName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public List<CustodyPeriod> CustodyPeriods { get; private set; } = [];
    public List<InmateFileHistory> FileHistory { get; private set; } = [];

    public string FullName => $"{FirstName} {LastName}".Trim();

    public CustodyPeriod? ActiveCustody() =>
        CustodyPeriods.SingleOrDefault(period => period.Status == CustodyStatus.Active);

    public static Inmate Create(string fileNumber, string firstName, string lastName, Guid createdBy, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(fileNumber))
            throw new DomainRuleException("Sicil veya dosya numarası zorunludur.");
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new DomainRuleException("Ad ve soyad zorunludur.");

        var inmate = new Inmate
        {
            Id = Guid.CreateVersion7(),
            FileNumber = fileNumber.Trim(),
            NormalizedFileNumber = TurkishText.NormalizeFileNumber(fileNumber),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            CreatedAt = now,
            CreatedByUserId = createdBy
        };
        inmate.NormalizedFullName = TurkishText.NormalizeName(inmate.FullName);
        inmate.FileHistory.Add(InmateFileHistory.Start(inmate.Id, inmate.FileNumber, inmate.NormalizedFileNumber, now));
        return inmate;
    }

    public void Rename(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new DomainRuleException("Ad ve soyad zorunludur.");
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        NormalizedFullName = TurkishText.NormalizeName(FullName);
    }

    public void ChangeFileNumber(string fileNumber, DateTimeOffset now)
    {
        var normalized = TurkishText.NormalizeFileNumber(fileNumber);
        if (string.IsNullOrEmpty(normalized))
            throw new DomainRuleException("Sicil veya dosya numarası zorunludur.");
        if (normalized == NormalizedFileNumber)
            return;

        var current = FileHistory.Single(item => item.ValidTo is null);
        current.Close(now);
        FileNumber = fileNumber.Trim();
        NormalizedFileNumber = normalized;
        FileHistory.Add(InmateFileHistory.Start(Id, FileNumber, normalized, now));
    }
}

public sealed class InmateFileHistory
{
    private InmateFileHistory()
    {
    }

    public Guid Id { get; private set; }
    public Guid InmateId { get; private set; }
    public string FileNumber { get; private set; } = string.Empty;
    public string NormalizedFileNumber { get; private set; } = string.Empty;
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }

    public static InmateFileHistory Start(Guid inmateId, string fileNumber, string normalized, DateTimeOffset from) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            InmateId = inmateId,
            FileNumber = fileNumber,
            NormalizedFileNumber = normalized,
            ValidFrom = from
        };

    public void Close(DateTimeOffset at) => ValidTo = at;
}

public sealed class CustodyPeriod
{
    private CustodyPeriod()
    {
    }

    public Guid Id { get; private set; }
    public Guid InmateId { get; private set; }
    public string? Ward { get; private set; }
    public string? NormalizedWard { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public CustodyStatus Status { get; private set; }
    public string? EndNote { get; private set; }
    public Guid? AdmissionReportEntryId { get; private set; }
    public Guid? EndReportEntryId { get; private set; }

    public static CustodyPeriod Open(Guid inmateId, string? ward, DateTimeOffset startedAt, Guid admissionReportEntryId)
    {
        var period = new CustodyPeriod
        {
            Id = Guid.CreateVersion7(),
            InmateId = inmateId,
            StartedAt = startedAt,
            Status = CustodyStatus.Active,
            AdmissionReportEntryId = admissionReportEntryId
        };
        period.SetWard(ward);
        return period;
    }

    public void SetWard(string? ward)
    {
        Ward = string.IsNullOrWhiteSpace(ward) ? null : ward.Trim();
        NormalizedWard = Ward is null ? null : TurkishText.NormalizeName(Ward);
    }

    public void Close(CustodyStatus endStatus, DateTimeOffset endedAt, string? note, Guid endReportEntryId)
    {
        if (Status != CustodyStatus.Active)
            throw new DomainRuleException("Aktif kalış yok.");
        if (endStatus is not (CustodyStatus.Released or CustodyStatus.Transferred))
            throw new DomainRuleException("Kalış tahliye veya sevk ile kapanır.");
        if (endedAt < StartedAt)
            throw new DomainRuleException("Kalış bitişi başlangıçtan önce olamaz.");

        Status = endStatus;
        EndedAt = endedAt;
        EndNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        EndReportEntryId = endReportEntryId;
    }

    public void CancelAdmission()
    {
        if (Status != CustodyStatus.Active)
            throw new DomainRuleException("Yalnızca aktif kalışın kabul kaydı iptal edilebilir.");
        Status = CustodyStatus.Cancelled;
        EndedAt = StartedAt;
        EndNote = "Kabul kaydı iptal edildi.";
    }

    public void Reopen()
    {
        if (Status is not (CustodyStatus.Released or CustodyStatus.Transferred))
            throw new DomainRuleException("Kalış yeniden açılamaz.");
        Status = CustodyStatus.Active;
        EndedAt = null;
        EndNote = null;
        EndReportEntryId = null;
    }
}
