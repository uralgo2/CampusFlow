using CampusFlow.Application.Errors;
using CampusFlow.Domain;

namespace CampusFlow.Application.Validation;

/// <summary>Подготовленные и проверенные поля аудитории.</summary>
/// <param name="Name">Название без лишних пробелов.</param>
/// <param name="Building">Корпус: A, B, C или D.</param>
/// <param name="Floor">Этаж.</param>
/// <param name="Capacity">Вместимость.</param>
/// <param name="Equipment">Оборудование без дублей.</param>
public sealed record RoomValues(string Name, string Building, int Floor, int Capacity, List<Equipment> Equipment);

/// <summary>Валидация тела запроса создания и обновления аудитории (правила одинаковые).</summary>
public static class RoomRequestValidator
{
    /// <summary>Проверяет обязательность полей, корпус, этаж, вместимость и оборудование.</summary>
    public static RoomValues Validate(string? name, string? building, int? floor, int? capacity, IReadOnlyList<string>? equipment)
    {
        var errors = new Dictionary<string, string>();

        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            errors["name"] = "Поле name обязательно и не может быть пустым";
        }
        else if (trimmedName.Length > 32)
        {
            errors["name"] = "Название не должно быть длиннее 32 символов";
        }

        var normalizedBuilding = string.Empty;
        if (string.IsNullOrWhiteSpace(building))
        {
            errors["building"] = "Поле building обязательно: A, B, C или D";
        }
        else if (!Buildings.TryNormalize(building, out normalizedBuilding))
        {
            errors["building"] = "Допустимые значения: A, B, C, D";
        }

        if (floor is null)
        {
            errors["floor"] = "Поле floor обязательно";
        }
        else if (floor < 1)
        {
            errors["floor"] = "Этаж должен быть не меньше 1";
        }

        if (capacity is null)
        {
            errors["capacity"] = "Поле capacity обязательно";
        }
        else if (capacity <= 0)
        {
            errors["capacity"] = "Вместимость должна быть больше 0";
        }

        var equipmentList = new List<Equipment>();
        if (equipment is null)
        {
            errors["equipment"] = "Поле equipment обязательно; допустим пустой массив";
        }
        else
        {
            var unknown = new List<string>();
            foreach (var code in equipment)
            {
                if (EquipmentCatalog.TryParse(code, out var parsed))
                {
                    if (!equipmentList.Contains(parsed))
                    {
                        equipmentList.Add(parsed);
                    }
                }
                else
                {
                    unknown.Add(code ?? "null");
                }
            }

            if (unknown.Count > 0)
            {
                errors["equipment"] =
                    $"Неизвестное оборудование: {string.Join(", ", unknown)}. Допустимые значения: {string.Join(", ", EquipmentCatalog.Codes)}";
            }
        }

        if (errors.Count > 0)
        {
            throw ApiException.Validation(errors);
        }

        return new RoomValues(trimmedName!, normalizedBuilding, floor!.Value, capacity!.Value, equipmentList);
    }
}
