using CampusFlow.Application.Errors;
using CampusFlow.Application.Parsing;
using CampusFlow.Application.Validation;
using CampusFlow.Domain;
using CampusFlow.Tests.Testing;
using static CampusFlow.Tests.Testing.Assert;

namespace CampusFlow.Tests.Application;

/// <summary>Правила даты и интервала брони: 30 минут, 09:00–18:00, не в прошлом.</summary>
public sealed class SlotParserTests
{
    [Test]
    public void Parse_ValidSlot_ReturnsValues()
    {
        var (date, start, end) = SlotParser.Parse(Fixtures.Today, Fixtures.Tomorrow(), "14:00", "15:30");

        Equal(Fixtures.Today.AddDays(1), date);
        Equal(new TimeOnly(14, 0), start);
        Equal(new TimeOnly(15, 30), end);
    }

    [Test]
    public void Parse_TodayIsAllowed()
    {
        var (date, _, _) = SlotParser.Parse(Fixtures.Today, Fixtures.Iso(Fixtures.Today), "09:00", "10:00");

        Equal(Fixtures.Today, date);
    }

    [Test]
    public void Parse_EndNotAfterStart_IsValidationError()
    {
        var exception = ThrowsApiError(
            () => SlotParser.Parse(Fixtures.Today, Fixtures.Tomorrow(), "15:00", "15:00"),
            400,
            ErrorCodes.Validation);

        Contains("endTime", Details(exception), "ошибка должна указывать на endTime");
    }

    [Test]
    public void Parse_TimesOutsideGrid_AreRejected()
    {
        ThrowsApiError(() => SlotParser.Parse(Fixtures.Today, Fixtures.Tomorrow(), "14:10", "15:10"), 400, ErrorCodes.Validation);
        ThrowsApiError(() => SlotParser.Parse(Fixtures.Today, Fixtures.Tomorrow(), "14:00", "15:10"), 400, ErrorCodes.Validation);
    }

    [Test]
    public void Parse_TimesOutsideWorkingDay_AreRejected()
    {
        ThrowsApiError(() => SlotParser.Parse(Fixtures.Today, Fixtures.Tomorrow(), "08:30", "09:30"), 400, ErrorCodes.Validation);
        ThrowsApiError(() => SlotParser.Parse(Fixtures.Today, Fixtures.Tomorrow(), "17:30", "18:30"), 400, ErrorCodes.Validation);
    }

    [Test]
    public void Parse_PastDate_IsRejected()
    {
        ThrowsApiError(
            () => SlotParser.Parse(Fixtures.Today, Fixtures.Iso(Fixtures.Today.AddDays(-1)), "10:00", "11:00"),
            400,
            ErrorCodes.Validation);
    }

    [Test]
    public void Parse_MissingFields_AreReportedTogether()
    {
        var exception = ThrowsApiError(
            () => SlotParser.Parse(Fixtures.Today, null, null, null),
            400,
            ErrorCodes.Validation);

        var details = Details(exception);
        Contains("date", details, "нет ошибки по date");
        Contains("startTime", details, "нет ошибки по startTime");
        Contains("endTime", details, "нет ошибки по endTime");
    }

    [Test]
    public void Parse_BadFormats_AreRejected()
    {
        ThrowsApiError(() => SlotParser.Parse(Fixtures.Today, "15.11.2026", "10:00", "11:00"), 400, ErrorCodes.Validation);
        ThrowsApiError(() => SlotParser.Parse(Fixtures.Today, Fixtures.Tomorrow(), "10:00:00", "11:00"), 400, ErrorCodes.Validation);
        ThrowsApiError(() => SlotParser.Parse(Fixtures.Today, Fixtures.Tomorrow(), "десять", "11:00"), 400, ErrorCodes.Validation);
    }

    [Test]
    public void Parse_AcceptsSingleDigitHour()
    {
        var (_, start, _) = SlotParser.Parse(Fixtures.Today, Fixtures.Tomorrow(), "9:00", "10:00");

        Equal(new TimeOnly(9, 0), start);
    }

    [Test]
    public void ParseDate_RequiresDate()
    {
        ThrowsApiError(() => SlotParser.ParseDate(Fixtures.Today, null), 400, ErrorCodes.Validation);
        ThrowsApiError(() => SlotParser.ParseDate(Fixtures.Today, ""), 400, ErrorCodes.Validation);
        ThrowsApiError(() => SlotParser.ParseDate(Fixtures.Today, "2026-13-40"), 400, ErrorCodes.Validation);
        ThrowsApiError(() => SlotParser.ParseDate(Fixtures.Today, Fixtures.Iso(Fixtures.Today.AddDays(-1))), 400, ErrorCodes.Validation);
    }

    [Test]
    public void ParseDate_ReturnsParsedDate()
    {
        Equal(Fixtures.Today.AddDays(2), SlotParser.ParseDate(Fixtures.Today, Fixtures.Iso(Fixtures.Today.AddDays(2))));
    }

    private static string Details(ApiException exception)
    {
        NotNull(exception.Details, "у ошибки валидации должны быть details");
        var details = (IDictionary<string, string>)exception.Details!;
        return string.Join(",", details.Keys);
    }
}
