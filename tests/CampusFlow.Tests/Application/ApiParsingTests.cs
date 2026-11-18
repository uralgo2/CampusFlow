using CampusFlow.Application.Parsing;
using CampusFlow.Domain;
using CampusFlow.Tests.Testing;
using static CampusFlow.Tests.Testing.Assert;

namespace CampusFlow.Tests.Application;

/// <summary>Форматы контракта: дата YYYY-MM-DD, время HH:MM.</summary>
public sealed class ApiParsingTests
{
    [Test]
    public void FormatDate_UsesContractFormat()
    {
        Equal("2026-11-14", ApiParsing.FormatDate(new DateOnly(2026, 11, 14)));
        Equal("2026-01-05", ApiParsing.FormatDate(new DateOnly(2026, 1, 5)));
    }

    [Test]
    public void FormatTime_UsesTwoDigitHours()
    {
        Equal("09:00", ApiParsing.FormatTime(new TimeOnly(9, 0)));
        Equal("18:30", ApiParsing.FormatTime(new TimeOnly(18, 30)));
    }

    [Test]
    public void TryParseDate_AcceptsOnlyContractFormat()
    {
        True(ApiParsing.TryParseDate("2026-11-14", out var date), "корректная дата");
        Equal(new DateOnly(2026, 11, 14), date);

        False(ApiParsing.TryParseDate("14.11.2026", out _), "точки не допускаются");
        False(ApiParsing.TryParseDate("2026-13-01", out _), "несуществующий месяц");
        False(ApiParsing.TryParseDate("", out _), "пустая строка");
    }

    [Test]
    public void TryParseTime_AcceptsSingleDigitHourButNotSeconds()
    {
        True(ApiParsing.TryParseTime("9:00", out var time), "9:00 допустимо");
        Equal(new TimeOnly(9, 0), time);

        True(ApiParsing.TryParseTime(" 14:30 ", out time), "пробелы обрезаются");
        Equal(new TimeOnly(14, 30), time);

        False(ApiParsing.TryParseTime("14:00:00", out _), "секунды не допускаются");
        False(ApiParsing.TryParseTime("25:00", out _), "такого часа нет");
    }

    [Test]
    public void NormalizeOptional_TrimsAndConvertsEmptyToNull()
    {
        Null(ApiParsing.NormalizeOptional(null));
        Null(ApiParsing.NormalizeOptional("   "));
        Equal("текст", ApiParsing.NormalizeOptional("  текст  "));
    }

    [Test]
    public void ParseStatusOrNull_HandlesKnownAndUnknownValues()
    {
        Equal(BookingStatus.Active, ApiParsing.ParseStatusOrNull("active"));
        Equal(BookingStatus.Cancelled, ApiParsing.ParseStatusOrNull("CANCELLED"));
        Null(ApiParsing.ParseStatusOrNull("deleted"));
        Null(ApiParsing.ParseStatusOrNull(null));
    }
}