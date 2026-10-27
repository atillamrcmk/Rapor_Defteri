namespace RaporDefteri.Domain;

public sealed class VehicleType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class VehicleCard
{
    private VehicleCard()
    {
    }

    public Guid Id { get; private set; }
    public string PlateDisplay { get; private set; } = string.Empty;
    public string NormalizedPlate { get; private set; } = string.Empty;
    public VehicleBaseLocation BaseLocation { get; private set; }
    public string? Organization { get; private set; }
    public string? Notes { get; private set; }

    public static VehicleCard Create(string plate, VehicleBaseLocation location, string? organization, string? notes)
    {
        var normalized = TurkishText.NormalizePlate(plate);
        if (normalized.Length < 4)
            throw new DomainRuleException("Plaka okunabilir değil.");

        return new VehicleCard
        {
            Id = Guid.CreateVersion7(),
            PlateDisplay = plate.Trim(),
            NormalizedPlate = normalized,
            BaseLocation = location,
            Organization = string.IsNullOrWhiteSpace(organization) ? null : organization.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }

    public void ChangeLocation(VehicleBaseLocation location) => BaseLocation = location;

    public void ChangeOrganization(string? organization) =>
        Organization = string.IsNullOrWhiteSpace(organization) ? null : organization.Trim();
}

public sealed class VehicleVisit
{
    private VehicleVisit()
    {
    }

    public Guid Id { get; private set; }
    public Guid VehicleCardId { get; private set; }
    public Guid VehicleTypeId { get; private set; }
    public VehicleBaseLocation BaseLocationSnapshot { get; private set; }
    public VehicleVisitStatus Status { get; private set; }
    public LocationInference LocationInference { get; private set; }
    public PassageDirection StartDirection { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public PassageDirection? EndDirection { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string? DriverName { get; private set; }
    public string? OrganizationSnapshot { get; private set; }
    public Guid StartReportEntryId { get; private set; }
    public Guid? EndReportEntryId { get; private set; }

    public static VehicleVisit Begin(
        Guid cardId,
        Guid typeId,
        VehicleBaseLocation location,
        VehicleVisitStatus status,
        LocationInference inference,
        PassageDirection direction,
        DateTimeOffset startedAt,
        string purpose,
        string? driverName,
        string? organization,
        Guid startReportEntryId)
    {
        return new VehicleVisit
        {
            Id = Guid.CreateVersion7(),
            VehicleCardId = cardId,
            VehicleTypeId = typeId,
            BaseLocationSnapshot = location,
            Status = status,
            LocationInference = inference,
            StartDirection = direction,
            StartedAt = startedAt,
            Purpose = purpose.Trim(),
            DriverName = string.IsNullOrWhiteSpace(driverName) ? null : driverName.Trim(),
            OrganizationSnapshot = string.IsNullOrWhiteSpace(organization) ? null : organization.Trim(),
            StartReportEntryId = startReportEntryId
        };
    }

    public void Close(DateTimeOffset endedAt, PassageDirection direction, Guid endReportEntryId)
    {
        if (Status != VehicleVisitStatus.Open)
            throw new DomainRuleException("Kapanacak açık sefer yok.");
        if (endedAt < StartedAt)
            throw new DomainRuleException("Araç dönüşü veya çıkışı, sefer başlangıcından önce olamaz.");

        Status = VehicleVisitStatus.Completed;
        LocationInference = LocationInference.None;
        EndedAt = endedAt;
        EndDirection = direction;
        EndReportEntryId = endReportEntryId;
    }
}

public sealed class AuditEvent
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public sealed class BackupLog
{
    public Guid Id { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string PrimaryPath { get; set; } = string.Empty;
    public string SecondaryPath { get; set; } = string.Empty;
    public string CheckCommand { get; set; } = string.Empty;
}
