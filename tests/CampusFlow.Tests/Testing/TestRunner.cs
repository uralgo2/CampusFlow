using System.Diagnostics;
using System.Reflection;

namespace CampusFlow.Tests.Testing;

/// <summary>
/// Мини-раннер юнит-тестов: находит все методы с <see cref="TestAttribute"/> и выполняет их.
/// Используется вместо xunit, потому что пакеты xunit в этом окружении недоступны без сети;
/// структура тестов (атрибут + Assert) повторяет xunit, поэтому перейти на него не составит труда.
/// </summary>
public static class TestRunner
{
    /// <summary>Запуск всех тестов. Возвращает код выхода: 0 — всё прошло, 1 — есть падения.</summary>
    public static int Run(string[] args)
    {
        var filter = ParseFilter(args);
        var tests = DiscoverTests(filter);

        if (tests.Count == 0)
        {
            Console.WriteLine("Тесты не найдены.");
            return 1;
        }

        Console.WriteLine($"Запуск тестов: {tests.Count}{(filter is null ? string.Empty : $" (фильтр: {filter})")}");
        Console.WriteLine();

        var passed = 0;
        var failures = new List<string>();
        var stopwatch = Stopwatch.StartNew();
        string? currentClass = null;

        foreach (var test in tests)
        {
            if (test.Type.Name != currentClass)
            {
                currentClass = test.Type.Name;
                Console.WriteLine($"  {currentClass}");
            }

            var displayName = test.Method.GetCustomAttribute<TestAttribute>()?.DisplayName ?? test.Method.Name;
            var testStopwatch = Stopwatch.StartNew();

            try
            {
                var instance = Activator.CreateInstance(test.Type)
                               ?? throw new InvalidOperationException(
                                   $"Не удалось создать экземпляр {test.Type.Name}: нужен конструктор без параметров.");

                var result = test.Method.Invoke(instance, null);
                if (result is Task task)
                {
                    task.GetAwaiter().GetResult();
                }

                passed++;
                Console.WriteLine($"    PASS  {displayName} ({testStopwatch.ElapsedMilliseconds} мс)");
            }
            catch (TargetInvocationException exception)
            {
                var inner = exception.InnerException ?? exception;
                failures.Add($"{test.Type.Name}.{displayName}: {inner.GetType().Name}: {inner.Message}");
                Console.WriteLine($"    FAIL  {displayName}");
                Console.WriteLine($"          {inner.GetType().Name}: {inner.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Итого: {tests.Count}, прошло {passed}, упало {failures.Count}, время {stopwatch.ElapsedMilliseconds} мс");

        if (failures.Count == 0)
        {
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("Проваленные тесты:");
        foreach (var failure in failures)
        {
            Console.WriteLine($"  - {failure}");
        }

        return 1;
    }

    private static string? ParseFilter(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument.StartsWith("--filter=", StringComparison.OrdinalIgnoreCase))
            {
                return argument["--filter=".Length..];
            }

            if (string.Equals(argument, "--filter", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static List<TestCase> DiscoverTests(string? filter)
    {
        var tests = new List<TestCase>();

        foreach (var type in typeof(TestRunner).Assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
            {
                continue;
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttribute<TestAttribute>() is null)
                {
                    continue;
                }

                if (method.GetParameters().Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Тестовый метод {type.Name}.{method.Name} не должен принимать параметры.");
                }

                var fullName = $"{type.Name}.{method.Name}";
                if (filter is not null && !fullName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                tests.Add(new TestCase(type, method));
            }
        }

        return tests
            .OrderBy(test => test.Type.Name, StringComparer.Ordinal)
            .ThenBy(test => test.Method.Name, StringComparer.Ordinal)
            .ToList();
    }

    private sealed record TestCase(Type Type, MethodInfo Method);
}
