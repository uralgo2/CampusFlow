using CampusFlow.Application.Errors;
using CampusFlow.Application.Validation;
using CampusFlow.Domain;
using CampusFlow.Tests.Testing;
using static CampusFlow.Tests.Testing.Assert;

namespace CampusFlow.Tests.Application;

/// <summary>Валидация аудитории при создании и полном обновлении.</summary>
public sealed class RoomRequestValidatorTests
{
    [Test]
    public void Validate_ValidRoom_NormalizesBuildingAndDeduplicatesEquipment()
    {
        var values = RoomRequestValidator.Validate(
            "  D-201  ",
            " d ",
            2,
            25,
            new[] { "projector", "Projector", "whiteboard" });

        Equal("D-201", values.Name);
        Equal("D", values.Building);
        Equal(2, values.Floor);
        Equal(25, values.Capacity);
        SequenceEqual(new[] { Equipment.Projector, Equipment.Whiteboard }, values.Equipment);
    }

    [Test]
    public void Validate_EmptyName_IsRejected()
    {
        var exception = ThrowsApiError(
            () => RoomRequestValidator.Validate("   ", "A", 1, 10, Array.Empty<string>()),
            400,
            ErrorCodes.Validation);

        Contains("name", Details(exception), "ошибка должна указывать на name");
    }

    [Test]
    public void Validate_TooLongName_IsRejected()
    {
        ThrowsApiError(
            () => RoomRequestValidator.Validate(new string('A', 33), "A", 1, 10, Array.Empty<string>()),
            400,
            ErrorCodes.Validation);
    }

    [Test]
    public void Validate_UnknownBuilding_IsRejected()
    {
        var exception = ThrowsApiError(
            () => RoomRequestValidator.Validate("X-1", "Z", 1, 10, Array.Empty<string>()),
            400,
            ErrorCodes.Validation);

        Contains("building", Details(exception), "ошибка должна указывать на building");
    }

    [Test]
    public void Validate_FloorAndCapacityLimits_AreChecked()
    {
        ThrowsApiError(() => RoomRequestValidator.Validate("X-1", "A", 0, 10, Array.Empty<string>()), 400,
            ErrorCodes.Validation);
        ThrowsApiError(() => RoomRequestValidator.Validate("X-1", "A", 1, 0, Array.Empty<string>()), 400,
            ErrorCodes.Validation);
        ThrowsApiError(() => RoomRequestValidator.Validate("X-1", "A", 1, -5, Array.Empty<string>()), 400,
            ErrorCodes.Validation);
    }

    [Test]
    public void Validate_UnknownEquipment_IsRejected()
    {
        var exception = ThrowsApiError(
            () => RoomRequestValidator.Validate("X-1", "A", 1, 10, new[] { "projector", "monitor" }),
            400,
            ErrorCodes.Validation);

        Contains("equipment", Details(exception), "ошибка должна указывать на equipment");
        Contains("monitor", exception.Message, "сообщение должно называть неизвестное оборудование");
    }

    [Test]
    public void Validate_EmptyEquipment_IsAllowed()
    {
        var values = RoomRequestValidator.Validate("X-1", "A", 1, 10, Array.Empty<string>());

        Empty(values.Equipment);
    }

    [Test]
    public void Validate_MissingEquipment_IsRejected()
    {
        ThrowsApiError(() => RoomRequestValidator.Validate("X-1", "A", 1, 10, null), 400, ErrorCodes.Validation);
    }

    private static string Details(ApiException exception)
    {
        var details = (IDictionary<string, string>)exception.Details!;
        return string.Join(",", details.Keys);
    }
}