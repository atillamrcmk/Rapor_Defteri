using System.Globalization;
using System.Text;

namespace RaporDefteri.Domain;

public static class TurkishText
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts).ToLower(Turkish);
    }

    public static string NormalizeFileNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().Replace(" ", string.Empty).ToUpper(Turkish);
    }

    public static string NormalizePlate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToUpper(Turkish))
        {
            if (char.IsWhiteSpace(ch) || ch is '-' or '.')
                continue;
            builder.Append(ch);
        }

        return builder.ToString();
    }
}

public static class TurkishClock
{
    public static string WithLocative(TimeOnly time)
    {
        var word = time.Minute == 0 ? NumberWord(time.Hour) : NumberWord(time.Minute);
        return $"{time.Hour:00}.{time.Minute:00}'{LocativeSuffix(word)}";
    }

    public static string LocativeSuffix(string word)
    {
        var lastVowel = 'e';
        foreach (var ch in word)
        {
            if ("aeıioöuü".Contains(ch))
                lastVowel = ch;
        }

        var back = lastVowel is 'a' or 'ı' or 'o' or 'u';
        var vowel = back ? 'a' : 'e';
        var last = word[^1];
        var voiceless = "çfhkpsşt".Contains(last);
        return $"{(voiceless ? 't' : 'd')}{vowel}";
    }

    public static string NumberWord(int number)
    {
        if (number is < 0 or > 59)
            throw new ArgumentOutOfRangeException(nameof(number));

        string[] units = ["sıfır", "bir", "iki", "üç", "dört", "beş", "altı", "yedi", "sekiz", "dokuz"];
        string[] tens = ["", "on", "yirmi", "otuz", "kırk", "elli"];
        if (number < 10)
            return units[number];

        var ten = tens[number / 10];
        var unit = number % 10;
        return unit == 0 ? ten : $"{ten} {units[unit]}";
    }
}

public static class RecordNumbers
{
    public static string Format(long sequence) => $"MK-{sequence:D6}";
}

public static class EntryKind
{
    public static bool ExpectsReturn(ReportEntryType type) => type is
        ReportEntryType.HospitalDeparture or
        ReportEntryType.CourtDeparture or
        ReportEntryType.OtherDeparture;

    public static ReportEntryType DepartureType(TemporaryMovementKind kind) => kind switch
    {
        TemporaryMovementKind.Hospital => ReportEntryType.HospitalDeparture,
        TemporaryMovementKind.Court => ReportEntryType.CourtDeparture,
        _ => ReportEntryType.OtherDeparture
    };

    public static ReportEntryType ReturnType(TemporaryMovementKind kind) => kind switch
    {
        TemporaryMovementKind.Hospital => ReportEntryType.HospitalReturn,
        TemporaryMovementKind.Court => ReportEntryType.CourtReturn,
        _ => ReportEntryType.OtherReturn
    };
}
