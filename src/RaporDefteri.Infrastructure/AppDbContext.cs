using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RaporDefteri.Domain;

namespace RaporDefteri.Infrastructure;

public sealed class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AppRole : IdentityRole<Guid>;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<Inmate> Inmates => Set<Inmate>();
    public DbSet<InmateFileHistory> FileHistories => Set<InmateFileHistory>();
    public DbSet<CustodyPeriod> CustodyPeriods => Set<CustodyPeriod>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftDuty> ShiftDuties => Set<ShiftDuty>();
    public DbSet<ShiftGate> ShiftGates => Set<ShiftGate>();
    public DbSet<ReportEntry> ReportEntries => Set<ReportEntry>();
    public DbSet<ReportEntryInmate> ReportEntryInmates => Set<ReportEntryInmate>();
    public DbSet<ReportRevision> ReportRevisions => Set<ReportRevision>();
    public DbSet<ReportSnapshot> ReportSnapshots => Set<ReportSnapshot>();
    public DbSet<TemporaryAbsence> TemporaryAbsences => Set<TemporaryAbsence>();
    public DbSet<AdmissionDetail> AdmissionDetails => Set<AdmissionDetail>();
    public DbSet<ReleaseDetail> ReleaseDetails => Set<ReleaseDetail>();
    public DbSet<TransferDetail> TransferDetails => Set<TransferDetail>();
    public DbSet<VisitorDetail> VisitorDetails => Set<VisitorDetail>();
    public DbSet<MaterialDetail> MaterialDetails => Set<MaterialDetail>();
    public DbSet<GeneralEventDetail> GeneralEventDetails => Set<GeneralEventDetail>();
    public DbSet<HandoverNoteDetail> HandoverNoteDetails => Set<HandoverNoteDetail>();
    public DbSet<VehicleType> VehicleTypes => Set<VehicleType>();
    public DbSet<VehicleCard> VehicleCards => Set<VehicleCard>();
    public DbSet<VehicleVisit> VehicleVisits => Set<VehicleVisit>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<BackupLog> BackupLogs => Set<BackupLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("rapor");
        modelBuilder.HasSequence<long>("report_entry_number_seq", "rapor").StartsAt(1).IncrementsBy(1);

        modelBuilder.Entity<ShiftDuty>().HasKey(item => new { item.ShiftId, item.UserId });
        modelBuilder.Entity<ReportEntryInmate>().HasKey(item => new { item.ReportEntryId, item.InmateId });
        modelBuilder.Entity<AdmissionDetail>().HasKey(item => item.ReportEntryId);
        modelBuilder.Entity<ReleaseDetail>().HasKey(item => item.ReportEntryId);
        modelBuilder.Entity<TransferDetail>().HasKey(item => item.ReportEntryId);
        modelBuilder.Entity<VisitorDetail>().HasKey(item => item.ReportEntryId);
        modelBuilder.Entity<MaterialDetail>().HasKey(item => item.ReportEntryId);
        modelBuilder.Entity<GeneralEventDetail>().HasKey(item => item.ReportEntryId);
        modelBuilder.Entity<HandoverNoteDetail>().HasKey(item => item.ReportEntryId);
        modelBuilder.Entity<ShiftGate>().HasKey(item => item.Id);

        modelBuilder.Entity<Inmate>().HasMany(item => item.CustodyPeriods).WithOne().HasForeignKey(item => item.InmateId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Inmate>().HasMany(item => item.FileHistory).WithOne().HasForeignKey(item => item.InmateId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Shift>().HasMany(item => item.Duties).WithOne().HasForeignKey(item => item.ShiftId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReportEntry>().HasMany(item => item.People).WithOne().HasForeignKey(item => item.ReportEntryId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReportEntry>().HasMany(item => item.Revisions).WithOne().HasForeignKey(item => item.ReportEntryId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TemporaryAbsence>().HasOne<Inmate>().WithMany().HasForeignKey(item => item.InmateId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Inmate>().HasIndex(item => item.NormalizedFileNumber).IsUnique();
        modelBuilder.Entity<InmateFileHistory>().HasIndex(item => item.NormalizedFileNumber);
        modelBuilder.Entity<ReportEntry>().HasIndex(item => item.RecordNumber).IsUnique();
        modelBuilder.Entity<ReportEntry>().HasIndex(item => item.IdempotencyKey).IsUnique().HasDatabaseName("ux_report_entry_idempotency");
        modelBuilder.Entity<ReportEntry>().HasIndex(item => item.EventAt);
        modelBuilder.Entity<ReportEntry>().HasIndex(item => item.ShiftId);
        modelBuilder.Entity<TemporaryAbsence>().HasIndex(item => item.InmateId).IsUnique().HasFilter("status = 1").HasDatabaseName("ux_open_absence");
        modelBuilder.Entity<CustodyPeriod>().HasIndex(item => item.InmateId).IsUnique().HasFilter("status = 1").HasDatabaseName("ux_active_custody");
        modelBuilder.Entity<VehicleCard>().HasIndex(item => item.NormalizedPlate).IsUnique();
        modelBuilder.Entity<VehicleVisit>().HasIndex(item => item.VehicleCardId);
        modelBuilder.Entity<ReportSnapshot>().HasIndex(item => new { item.Kind, item.ShiftId, item.PeriodStart, item.Version }).IsUnique();

        Xmin<ReportEntry>(modelBuilder);
        Xmin<TemporaryAbsence>(modelBuilder);
        Xmin<Shift>(modelBuilder);
        Xmin<ShiftGate>(modelBuilder);
        Xmin<Inmate>(modelBuilder);
        Xmin<CustodyPeriod>(modelBuilder);
        Xmin<VehicleVisit>(modelBuilder);
        Xmin<VehicleCard>(modelBuilder);

        foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(entity => entity.ClrType?.Namespace == "RaporDefteri.Domain"))
        {
            entity.SetTableName(Snake(entity.ClrType.Name));
            foreach (var property in entity.GetProperties())
            {
                if (property.Name == "xmin")
                    continue;
                property.SetColumnName(Snake(property.Name));
            }
        }

        modelBuilder.Entity<ShiftGate>().HasData(new ShiftGate { Id = 1 });
        modelBuilder.Entity<VehicleType>().HasData(
            Type(VehicleTypeCatalog.Food, "Yemek aracı", 1),
            Type(VehicleTypeCatalog.Ring, "Ring/sevk aracı", 2),
            Type(VehicleTypeCatalog.Ambulance, "Ambulans", 3),
            Type(VehicleTypeCatalog.Supply, "Malzeme/tedarik aracı", 4),
            Type(VehicleTypeCatalog.Maintenance, "Bakım/teknik servis aracı", 5),
            Type(VehicleTypeCatalog.Official, "Resmî araç", 6),
            Type(VehicleTypeCatalog.Other, "Diğer", 7));

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(type => type.GetProperties()))
        {
            if (property.ClrType == typeof(DateTimeOffset))
                property.SetValueConverter(UtcTimestamp.Required);
            else if (property.ClrType == typeof(DateTimeOffset?))
                property.SetValueConverter(UtcTimestamp.Optional);
        }
    }

    private static class UtcTimestamp
    {
        public static readonly ValueConverter<DateTimeOffset, DateTimeOffset> Required = new(
            value => value.ToUniversalTime(),
            value => value.ToUniversalTime());

        public static readonly ValueConverter<DateTimeOffset?, DateTimeOffset?> Optional = new(
            value => value.HasValue ? value.Value.ToUniversalTime() : value,
            value => value.HasValue ? value.Value.ToUniversalTime() : value);
    }

    private static void Xmin<T>(ModelBuilder modelBuilder) where T : class =>
        modelBuilder.Entity<T>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

    private static VehicleType Type(Guid id, string name, int order) => new() { Id = id, Name = name, IsActive = true, SortOrder = order };

    private static string Snake(string name)
    {
        var chars = new List<char>(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current) && i > 0)
                chars.Add('_');
            chars.Add(char.ToLowerInvariant(current));
        }

        return new string(chars.ToArray());
    }
}

public static class VehicleTypeCatalog
{
    public static readonly Guid Food = Guid.Parse("a1111111-1111-4111-8111-111111111101");
    public static readonly Guid Ring = Guid.Parse("a1111111-1111-4111-8111-111111111102");
    public static readonly Guid Ambulance = Guid.Parse("a1111111-1111-4111-8111-111111111103");
    public static readonly Guid Supply = Guid.Parse("a1111111-1111-4111-8111-111111111104");
    public static readonly Guid Maintenance = Guid.Parse("a1111111-1111-4111-8111-111111111105");
    public static readonly Guid Official = Guid.Parse("a1111111-1111-4111-8111-111111111106");
    public static readonly Guid Other = Guid.Parse("a1111111-1111-4111-8111-111111111107");
}
