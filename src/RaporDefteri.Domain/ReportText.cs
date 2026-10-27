namespace RaporDefteri.Domain;

public readonly record struct PersonLine(string FileNumber, string FullName);

public static class ReportTextComposer
{
    public static string Departure(TimeOnly localTime, TemporaryMovementKind kind, IReadOnlyList<PersonLine> people, string destination, string reason, string? escort)
    {
        RequirePeople(people);
        if (string.IsNullOrWhiteSpace(destination))
            throw new DomainRuleException("Gidilen yer olmadan cümle üretilmez.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("Gerekçe olmadan cümle üretilmez.");

        var place = kind switch
        {
            TemporaryMovementKind.Hospital => HospitalPhrase(destination),
            TemporaryMovementKind.Court => CourtPhrase(destination),
            _ => $"{destination.Trim()} istikametine"
        };
        var escortText = string.IsNullOrWhiteSpace(escort) ? string.Empty : $" Refakat: {escort.Trim()}.";
        return $"Saat {TurkishClock.WithLocative(localTime)} {JoinPeople(people)}, {place} götürülmek üzere kurumdan çıkış yapmıştır.{escortText}";
    }

    public static string Return(TimeOnly localTime, TemporaryMovementKind kind, IReadOnlyList<PersonLine> people, string? reason)
    {
        RequirePeople(people);
        var after = kind switch
        {
            TemporaryMovementKind.Hospital => "hastane işlemleri",
            TemporaryMovementKind.Court => "adliye işlemleri",
            _ => string.IsNullOrWhiteSpace(reason) ? "kurum dışı işlemi" : reason.Trim()
        };
        return $"Saat {TurkishClock.WithLocative(localTime)} {JoinPeople(people)}, {after} sonrasında kuruma dönüş yapmıştır.";
    }

    public static string Vehicle(TimeOnly localTime, string plate, string typeName, PassageDirection direction, string? driver, string? organization)
    {
        if (string.IsNullOrWhiteSpace(plate) || string.IsNullOrWhiteSpace(typeName))
            throw new DomainRuleException("Plaka ve araç türü olmadan cümle üretilmez.");

        var action = direction == PassageDirection.Entry ? "kuruma giriş yapmıştır" : "kurumdan çıkış yapmıştır";
        var extra = new List<string>();
        if (!string.IsNullOrWhiteSpace(organization))
            extra.Add($"Kurum/firma: {organization.Trim()}.");
        if (!string.IsNullOrWhiteSpace(driver))
            extra.Add($"Sürücü: {driver.Trim()}.");
        var tail = extra.Count == 0 ? string.Empty : " " + string.Join(' ', extra);
        return $"Saat {TurkishClock.WithLocative(localTime)} {plate.Trim()} plakalı {typeName.Trim().ToLower(new System.Globalization.CultureInfo("tr-TR"))} {action}.{tail}";
    }

    public static string Visitor(TimeOnly localTime, string fullName, string? organization, string? purpose)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainRuleException("Gelen kişinin adı olmadan cümle üretilmez.");
        var org = string.IsNullOrWhiteSpace(organization) ? string.Empty : $" Birim/kurum: {organization.Trim()}.";
        var aim = string.IsNullOrWhiteSpace(purpose) ? string.Empty : $" Amaç: {purpose.Trim()}.";
        return $"Saat {TurkishClock.WithLocative(localTime)} {fullName.Trim()}, kuruma gelen görevli veya ziyaretçi olarak kaydedilmiştir.{org}{aim}";
    }

    public static string Material(TimeOnly localTime, string description, MaterialDirection direction, string? counterparty)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainRuleException("Malzeme tanımı olmadan cümle üretilmez.");
        var verb = direction == MaterialDirection.Received ? "teslim alınmıştır" : "teslim edilmiştir";
        var who = string.IsNullOrWhiteSpace(counterparty) ? string.Empty : $" Karşı taraf: {counterparty.Trim()}.";
        return $"Saat {TurkishClock.WithLocative(localTime)} {description.Trim()} {verb}.{who}";
    }

    public static string General(TimeOnly localTime, string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainRuleException("Olay başlığı olmadan cümle üretilmez.");
        var body = string.IsNullOrWhiteSpace(description) ? string.Empty : $" {description.Trim()}";
        return $"Saat {TurkishClock.WithLocative(localTime)} {title.Trim()}.{body}";
    }

    public static string HandoverNote(string note)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new DomainRuleException("Devir notu boş olamaz.");
        return $"Vardiya devir notu: {note.Trim()}";
    }

    public static string Admission(TimeOnly localTime, PersonLine person, string? ward)
    {
        var wardText = string.IsNullOrWhiteSpace(ward) ? string.Empty : $" Koğuş/oda: {ward.Trim()}.";
        return $"Saat {TurkishClock.WithLocative(localTime)} {One(person)} kuruma kabul edilmiştir.{wardText}";
    }

    public static string Release(TimeOnly localTime, PersonLine person, string? reason)
    {
        var reasonText = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" Gerekçe: {reason.Trim()}.";
        return $"Saat {TurkishClock.WithLocative(localTime)} {One(person)} tahliye edilmiştir.{reasonText}";
    }

    public static string Transfer(TimeOnly localTime, PersonLine person, string destination, string? reason)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new DomainRuleException("Sevk kurumu olmadan cümle üretilmez.");
        var reasonText = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" Gerekçe: {reason.Trim()}.";
        return $"Saat {TurkishClock.WithLocative(localTime)} {One(person)}, {destination.Trim()} adlı kuruma sevk edilmiştir.{reasonText}";
    }

    public static string JoinPeople(IReadOnlyList<PersonLine> people)
    {
        RequirePeople(people);
        var parts = people.Select(One).ToList();
        if (parts.Count == 1)
            return parts[0];
        if (parts.Count == 2)
            return $"{parts[0]} ve {parts[1]}";
        return string.Join(", ", parts.Take(parts.Count - 1)) + " ve " + parts[^1];
    }

    private static string One(PersonLine person)
    {
        if (string.IsNullOrWhiteSpace(person.FileNumber) || string.IsNullOrWhiteSpace(person.FullName))
            throw new DomainRuleException("Kişi sicili ve adı olmadan cümle üretilmez.");
        return $"{person.FileNumber.Trim()} numaralı {person.FullName.Trim()}";
    }

    private static void RequirePeople(IReadOnlyList<PersonLine> people)
    {
        if (people.Count == 0)
            throw new DomainRuleException("Kişi olmadan cümle üretilmez.");
    }

    private static string HospitalPhrase(string destination)
    {
        var core = StripEnding(destination, "hastanesi") ?? StripEnding(destination, "hastane") ?? destination.Trim();
        if (string.IsNullOrWhiteSpace(core))
            throw new DomainRuleException("Hastane adı olmadan cümle üretilmez.");
        return $"{core} hastanesine";
    }

    private static string CourtPhrase(string destination)
    {
        var core = StripEnding(destination, "adliyesi") ?? StripEnding(destination, "adliye") ?? destination.Trim();
        if (string.IsNullOrWhiteSpace(core))
            throw new DomainRuleException("Adliye adı olmadan cümle üretilmez.");
        return $"{core} adliyesine";
    }

    private static string? StripEnding(string value, string ending)
    {
        var trimmed = value.Trim();
        var normalized = TurkishText.NormalizeName(trimmed);
        if (!normalized.EndsWith(ending, StringComparison.Ordinal))
            return null;
        return trimmed[..^ending.Length].Trim();
    }
}
