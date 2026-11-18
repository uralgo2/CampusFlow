namespace CampusFlow.Tests.Testing;

/// <summary>Помечает метод как тест. Аналог xunit-атрибута <c>[Fact]</c>.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute
{
    public TestAttribute()
    {
    }

    public TestAttribute(string displayName)
    {
        DisplayName = displayName;
    }

    /// <summary>Необязательное имя для вывода в консоли.</summary>
    public string? DisplayName { get; }
}