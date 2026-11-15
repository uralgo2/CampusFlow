using CampusFlow.Domain;
using CampusFlow.Tests.Testing;
using static CampusFlow.Tests.Testing.Assert;

namespace CampusFlow.Tests.Domain;

/// <summary>Справочники оборудования и корпусов: коды контракта и разбор значений.</summary>
public sealed class CatalogTests
{
    [Test]
    public void EquipmentCatalog_ParsesCodesCaseInsensitively()
    {
        True(EquipmentCatalog.TryParse("projector", out var projector), "projector должен разбираться");
        Equal(Equipment.Projector, projector);

        True(EquipmentCatalog.TryParse(" WhiteBoard ", out var whiteboard), "пробелы и регистр не важны");
        Equal(Equipment.Whiteboard, whiteboard);
    }

    [Test]
    public void EquipmentCatalog_RejectsUnknownCode()
    {
        False(EquipmentCatalog.TryParse("monitor", out _), "monitor не из справочника");
        False(EquipmentCatalog.TryParse("", out _), "пустая строка не разбирается");
        False(EquipmentCatalog.TryParse(null, out _), "null не разбирается");
    }

    [Test]
    public void EquipmentCatalog_AllCodesRoundTrip()
    {
        foreach (var code in EquipmentCatalog.Codes)
        {
            True(EquipmentCatalog.TryParse(code, out var equipment), $"код {code} должен разбираться");
            Equal(code, EquipmentCatalog.ToCode(equipment), $"код {code} должен возвращаться из ToCode");
        }

        SequenceEqual(
            new[] { "projector", "whiteboard", "computers", "audio", "video" },
            EquipmentCatalog.Codes);
    }

    [Test]
    public void Buildings_NormalizesLowercaseToUppercase()
    {
        True(Buildings.TryNormalize("b", out var building), "строчная b допустима");
        Equal("B", building);

        True(Buildings.TryNormalize(" c ", out building), "пробелы обрезаются");
        Equal("C", building);
    }

    [Test]
    public void Buildings_RejectsUnknownBuilding()
    {
        False(Buildings.TryNormalize("Z", out _), "Z не из списка");
        False(Buildings.TryNormalize("AA", out _), "AA не из списка");
        False(Buildings.TryNormalize(null, out _), "null не из списка");
    }
}
