using System.Collections;
using CampusFlow.Application.Errors;

namespace CampusFlow.Tests.Testing;

/// <summary>Ошибка проверки: тест провален.</summary>
public sealed class AssertionException : Exception
{
    public AssertionException(string message)
        : base(message)
    {
    }
}

/// <summary>Проверки в стиле xunit: <c>Assert.Equal</c>, <c>Assert.True</c>, <c>Assert.Throws</c>.</summary>
public static class Assert
{
    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertionException(
                $"{Prefix(message)}ожидалось <{Format(expected)}>, получено <{Format(actual)}>");
    }

    public static void NotEqual<T>(T unexpected, T actual, string? message = null)
    {
        if (EqualityComparer<T>.Default.Equals(unexpected, actual))
            throw new AssertionException($"{Prefix(message)}значение не должно быть <{Format(actual)}>");
    }

    public static void True(bool condition, string message)
    {
        if (!condition) throw new AssertionException(message);
    }

    public static void False(bool condition, string message)
    {
        if (condition) throw new AssertionException(message);
    }

    public static void Null(object? value, string? message = null)
    {
        if (value is not null)
            throw new AssertionException($"{Prefix(message)}ожидался null, получено <{Format(value)}>");
    }

    public static void NotNull(object? value, string? message = null)
    {
        if (value is null) throw new AssertionException($"{Prefix(message)}значение не должно быть null");
    }

    public static void Empty(IEnumerable collection, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var items = collection.Cast<object?>().ToList();
        if (items.Count > 0)
            throw new AssertionException($"{Prefix(message)}ожидалась пустая коллекция, элементов: {items.Count}");
    }

    public static void Count(int expected, IEnumerable collection, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var actual = collection.Cast<object?>().Count();
        if (actual != expected)
            throw new AssertionException($"{Prefix(message)}ожидалось элементов {expected}, получено {actual}");
    }

    public static void Contains(string expectedSubstring, string? actual, string? message = null)
    {
        if (actual is null || !actual.Contains(expectedSubstring, StringComparison.Ordinal))
            throw new AssertionException(
                $"{Prefix(message)}строка не содержит <{expectedSubstring}>. Получено: <{Format(actual)}>");
    }

    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string? message = null)
    {
        var expectedList = expected.ToList();
        var actualList = actual.ToList();

        if (!expectedList.SequenceEqual(actualList))
            throw new AssertionException(
                $"{Prefix(message)}ожидалось [{string.Join(", ", expectedList)}], получено [{string.Join(", ", actualList)}]");
    }

    public static TException Throws<TException>(Action action, string? message = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new AssertionException(
                $"{Prefix(message)}ожидалось исключение {typeof(TException).Name}, получено {exception.GetType().Name}: {exception.Message}");
        }

        throw new AssertionException(
            $"{Prefix(message)}ожидалось исключение {typeof(TException).Name}, но его не было");
    }

    /// <summary>Проверяет, что операция бросает <see cref="ApiException" /> с нужным HTTP-статусом и кодом.</summary>
    public static ApiException ThrowsApiError(Action action, int statusCode, string code)
    {
        var exception = Throws<ApiException>(action);

        Equal(statusCode, exception.StatusCode, $"HTTP-статус ошибки {code}");
        Equal(code, exception.Code, "код ошибки");

        return exception;
    }

    /// <summary>Асинхронный вариант <see cref="Throws{TException}" />.</summary>
    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, string? message = null)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            await action();
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new AssertionException(
                $"{Prefix(message)}ожидалось исключение {typeof(TException).Name}, получено {exception.GetType().Name}: {exception.Message}");
        }

        throw new AssertionException(
            $"{Prefix(message)}ожидалось исключение {typeof(TException).Name}, но его не было");
    }

    /// <summary>Асинхронный вариант <see cref="ThrowsApiError" />.</summary>
    public static async Task<ApiException> ThrowsApiErrorAsync(Func<Task> action, int statusCode, string code)
    {
        var exception = await ThrowsAsync<ApiException>(action);

        Equal(statusCode, exception.StatusCode, $"HTTP-статус ошибки {code}");
        Equal(code, exception.Code, "код ошибки");

        return exception;
    }

    /// <summary>Проверяет поле объекта <c>details</c> (у 409-ошибок это анонимный объект).</summary>
    public static void Detail(object? details, string propertyName, object? expected)
    {
        NotNull(details, "у ошибки должны быть details");

        var property = details!.GetType().GetProperty(propertyName)
                       ?? throw new AssertionException($"в details нет поля {propertyName}");

        var actual = property.GetValue(details);
        Equal(Convert.ToString(expected), Convert.ToString(actual), $"details.{propertyName}");
    }

    private static string Prefix(string? message)
    {
        return string.IsNullOrWhiteSpace(message) ? string.Empty : message + ": ";
    }

    private static string Format(object? value)
    {
        return value switch
        {
            null => "null",
            string text => text,
            IEnumerable enumerable => string.Join(", ", enumerable.Cast<object?>().Select(Format)),
            _ => value.ToString() ?? "null"
        };
    }
}