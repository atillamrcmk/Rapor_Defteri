using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RaporDefteri.Domain;
using RaporDefteri.Infrastructure;

namespace RaporDefteri.Tests;

public class AcceptanceTests
{
    private static readonly TimeZoneInfo Turkey = TimeZoneInfo.CreateCustomTimeZone("TR-test", TimeSpan.FromHours(3), "TR", "TR");

    [Fact]
    public void Same_name_stays_two_people()
    {
        var hits = PersonSearch.Present(
        [
            new PersonHit(Guid.NewGuid(), "KURMACA-001", "Kurmaca Ali Deneme", "A-1"),
            new PersonHit(Guid.NewGuid(), "KURMACA-002", "Kurmaca Ali Deneme", "A-2")
        ]);
        Assert.Equal(2, hits.Count);
        Assert.NotEqual(hits[0].FileNumber, hits[1].FileNumber);
        Assert.Equal("ışık", TurkishText.NormalizeName("IŞIK"));
        Assert.Equal("ısık", TurkishText.NormalizeName("ISIK"));
        Assert.NotEqual(TurkishText.NormalizeName("IŞIK"), TurkishText.NormalizeName("ISIK"));
    }

    [Fact]
    public void Two_of_three_returns_leave_one_open()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var departed = new DateTimeOffset(2026, 10, 27, 9, 15, 0, TimeSpan.FromHours(3));
        var selected = new[] { State(first, "101"), State(second, "102") };
        var plan = ReturnPlanner.Plan(
            departed.AddHours(3),
            departed.AddHours(3).AddMinutes(1),
            departed.AddHours(-1),
            Guid.NewGuid(),
            null,
            Turkey,
            selected,
            [first, second, third]);
        Assert.True(plan.Succeeded);
        Assert.Equal([third], plan.Value!.StillOpenAbsenceIds);
        Assert.DoesNotContain(first, plan.Value.StillOpenAbsenceIds);
    }

    [Fact]
    public void Return_can_belong_to_a_later_shift()
    {
        var morning = Guid.NewGuid();
        var night = Guid.NewGuid();
        var absence = State(Guid.NewGuid(), "101") with { DepartureShiftId = night };
        var plan = ReturnPlanner.Plan(
            new DateTimeOffset(2026, 10, 28, 7, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 10, 28, 7, 1, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.FromHours(3)),
            morning,
            null,
            Turkey,
            [absence],
            [absence.AbsenceId]);
        Assert.Equal(morning, plan.Value!.ReturnShiftId);
        Assert.NotEqual(night, plan.Value.ReturnShiftId);
        Assert.Equal(absence.DepartureReportEntryId, absence.DepartureReportEntryId);
    }

    [Fact]
    public void Overnight_shift_is_not_split_but_daily_report_is()
    {
        var start = new DateTimeOffset(2026, 10, 27, 20, 0, 0, TimeSpan.FromHours(3));
        var end = new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.FromHours(3));
        var evening = new DateTimeOffset(2026, 10, 27, 23, 30, 0, TimeSpan.FromHours(3));
        var afterMidnight = new DateTimeOffset(2026, 10, 28, 1, 15, 0, TimeSpan.FromHours(3));
        Assert.True(ReportPeriod.InShift(evening, start, end));
        Assert.True(ReportPeriod.InShift(afterMidnight, start, end));
        Assert.True(ReportPeriod.InDailyReport(evening, new DateOnly(2026, 10, 27), Turkey));
        Assert.False(ReportPeriod.InDailyReport(afterMidnight, new DateOnly(2026, 10, 27), Turkey));
        Assert.True(ReportPeriod.InDailyReport(afterMidnight, new DateOnly(2026, 10, 28), Turkey));
    }

    [Fact]
    public void Second_open_departure_is_rejected()
    {
        var result = DeparturePlanner.Plan(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(-1), null, Turkey, TemporaryMovementKind.Hospital, "Şehir", "muayene", null, null,
            [new PersonDepartureState(Guid.NewGuid(), Guid.NewGuid(), "101", "Ali Deneme", true, true)]);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("açık"));
    }

    [Fact]
    public void Return_before_departure_and_second_return_are_rejected()
    {
        var departed = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(3));
        Assert.NotEmpty(MovementRules.ValidateReturn(TemporaryAbsenceStatus.Open, departed, departed.AddMinutes(-1)));
        Assert.NotEmpty(MovementRules.ValidateReturn(TemporaryAbsenceStatus.Returned, departed, departed.AddHours(1)));
    }

    [Fact]
    public void Replay_does_not_create_another_record()
    {
        Assert.True(ReplayGuard.IsReplay(Guid.NewGuid()));
        Assert.False(ReplayGuard.IsReplay(null));
    }

    [Fact]
    public void Vehicle_plan_does_not_close_people_and_allows_another_visit_after_close()
    {
        var open = VehicleRules.Plan(VehicleBaseLocation.External, PassageDirection.Entry, false, LocationInference.None);
        var blocked = VehicleRules.Plan(VehicleBaseLocation.External, PassageDirection.Entry, true, LocationInference.AwaitingExit);
        var close = VehicleRules.Plan(VehicleBaseLocation.External, PassageDirection.Exit, true, LocationInference.AwaitingExit);
        var again = VehicleRules.Plan(VehicleBaseLocation.External, PassageDirection.Entry, false, LocationInference.None);
        var unknown = VehicleRules.Plan(VehicleBaseLocation.Unspecified, PassageDirection.Entry, false, LocationInference.None);
        Assert.Equal(VehiclePlanKind.OpenVisit, open.Kind);
        Assert.Equal(VehiclePlanKind.Reject, blocked.Kind);
        Assert.Equal(VehiclePlanKind.CloseVisit, close.Kind);
        Assert.Equal(VehiclePlanKind.OpenVisit, again.Kind);
        Assert.Equal(VehiclePlanKind.Standalone, unknown.Kind);
        Assert.Equal(LocationInference.None, unknown.Inference);
        Assert.False(EntryKind.ExpectsReturn(ReportEntryType.Release));
        Assert.False(EntryKind.ExpectsReturn(ReportEntryType.Transfer));
    }

    [Fact]
    public void Linking_a_vehicle_does_not_return_the_person()
    {
        var absence = TemporaryAbsence.Open(Guid.NewGuid(), Guid.NewGuid(), TemporaryMovementKind.Hospital, "Şehir", "muayene", DateTimeOffset.UtcNow, null, Guid.NewGuid(), Guid.NewGuid(), null, null);
        absence.LinkVehicle(Guid.NewGuid(), asReturn: false);
        Assert.Equal(TemporaryAbsenceStatus.Open, absence.Status);
        Assert.Null(absence.ReturnedAt);
    }

    [Fact]
    public void Closed_shift_cannot_be_changed_without_reason_or_authority()
    {
        Assert.NotEmpty(CorrectionRules.Validate(ShiftStatus.Closed, false, true, "saat yanlış"));
        Assert.NotEmpty(CorrectionRules.Validate(ShiftStatus.Closed, true, false, " "));
        Assert.Empty(CorrectionRules.Validate(ShiftStatus.Closed, true, false, "Olay saati yanlış yazılmıştı"));
    }

    [Fact]
    public void Correction_keeps_the_original_text()
    {
        var entry = ReportEntry.Create("MK-000001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ReportEntryType.GeneralEvent, Guid.NewGuid(), Guid.NewGuid(), "ilk metin", null, null, "anahtar");
        var revision = entry.Correct("yeni metin", "not", entry.EventAt, "yazım düzeltmesi", Guid.NewGuid(), DateTimeOffset.UtcNow, "eski", "yeni");
        Assert.Equal("ilk metin", entry.OriginalReportText);
        Assert.Equal("ilk metin", revision.OldReportText);
        Assert.Equal("yeni metin", entry.ReportText);
        Assert.Equal(ReportEntryStatus.Corrected, entry.Status);
    }

    [Fact]
    public void Stale_token_is_detected()
    {
        Assert.True(ConcurrencyRules.IsStale(1, 2));
        Assert.False(ConcurrencyRules.IsStale(5, 5));
    }

    [Theory]
    [InlineData(9, 15, "09.15'te")]
    [InlineData(12, 40, "12.40'ta")]
    [InlineData(11, 20, "11.20'de")]
    public void Turkish_time_suffix_matches_the_examples(int hour, int minute, string expected)
    {
        Assert.Equal(expected, TurkishClock.WithLocative(new TimeOnly(hour, minute)));
    }

    [Fact]
    public void Report_text_does_not_invent_missing_optional_facts()
    {
        var text = ReportTextComposer.Vehicle(new TimeOnly(11, 20), "34 ABC 123", "Yemek aracı", PassageDirection.Entry, null, null);
        Assert.Equal("Saat 11.20'de 34 ABC 123 plakalı yemek aracı kuruma giriş yapmıştır.", text);
        Assert.DoesNotContain("Sürücü", text);
        Assert.Throws<DomainRuleException>(() => ReportTextComposer.Departure(new TimeOnly(9, 15), TemporaryMovementKind.Hospital, [], "Şehir", "muayene", null));
    }

    [Fact]
    public void Unscheduled_return_is_not_called_late()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal("Dönüş saati belirlenmedi", ReturnWatchRules.Label(ReturnWatchRules.Evaluate(null, now)));
        Assert.Equal("Beklenen saat geçti", ReturnWatchRules.Label(ReturnWatchRules.Evaluate(now.AddMinutes(-5), now)));
        Assert.DoesNotContain("Gecikti", ReturnWatchRules.Label(ReturnWatchRules.Evaluate(null, now)));
    }

    [Fact]
    public void Record_number_is_not_produced_by_formatting_a_max_plus_one_query()
    {
        Assert.Equal("MK-000123", RecordNumbers.Format(123));
        Assert.Equal("MK-1000000", RecordNumbers.Format(1_000_000));
    }

    [Fact]
    public void Model_uses_sequence_and_concurrency_token()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=127.0.0.1;Database=rapor_defteri").Options;
        using var db = new AppDbContext(options);
        Assert.Contains(db.Model.GetSequences(), sequence => sequence.Name == "report_entry_number_seq");
        var entry = db.Model.FindEntityType(typeof(ReportEntry));
        Assert.Contains(entry!.GetProperties(), property => property.IsConcurrencyToken);
    }

    [Fact]
    public void Connectivity_errors_are_not_treated_as_success()
    {
        Assert.True(DatabaseExceptionClassifier.IsConnectivity(new NpgsqlException("connection refused")));
        Assert.True(DatabaseExceptionClassifier.IsConnectivityState("08006"));
        Assert.False(DatabaseExceptionClassifier.IsConnectivityState("23505"));
    }

    [Fact]
    public void Interface_files_do_not_load_remote_assets()
    {
        var root = FindWebRoot();
        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cshtml") || path.EndsWith(".css") || path.EndsWith(".js"));
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("http://", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("https://", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cdn.", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("googleapis", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindWebRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "RaporDefteri.Web");
            if (Directory.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Web projesi bulunamadı.");
    }

    private static OpenAbsenceState State(Guid id, string file) =>
        new(id, Guid.NewGuid(), file, "Kurmaca Kişi", TemporaryMovementKind.Hospital, "Şehir", new DateTimeOffset(2026, 10, 27, 9, 15, 0, TimeSpan.FromHours(3)), Guid.NewGuid(), Guid.NewGuid(), "muayene", TemporaryAbsenceStatus.Open);
}

public class AnonymousAccessTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AnonymousAccessTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Protected_url_without_login_is_not_executed()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/vardiya/ac");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/giris", response.Headers.Location?.OriginalString);
    }
}
