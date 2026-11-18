using CampusFlow.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CampusFlow.Infrastructure.Persistence;

/// <summary>Подготовка базы при старте: создание схемы и загрузка seed-данных.</summary>
public static class DbInitializer
{
    /// <summary>
    ///     Создаёт схему SQLite (если её нет) и заполняет её тестовыми данными, когда таблица пользователей пуста.
    ///     Параметр конфигурации <c>Seed:RecreateOnStartup=true</c> пересоздаёт базу на каждом старте — так даты
    ///     сидов всегда актуальны на день демонстрации.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusFlowDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        if (configuration.GetValue("Seed:RecreateOnStartup", false))
        {
            logger.LogInformation("Seed:RecreateOnStartup=true — база данных пересоздаётся");
            await db.Database.EnsureDeletedAsync(cancellationToken);
        }

        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (await db.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation(
                "База данных готова: аудиторий {Rooms}, пользователей {Users}, броней {Bookings}",
                await db.Rooms.CountAsync(cancellationToken),
                await db.Users.CountAsync(cancellationToken),
                await db.Bookings.CountAsync(cancellationToken));
            return;
        }

        await DbSeeder.SeedAsync(db, clock.Today, cancellationToken);

        logger.LogInformation(
            "Загружены seed-данные: аудиторий {Rooms}, пользователей {Users}, броней {Bookings} (даты относительно {Today})",
            await db.Rooms.CountAsync(cancellationToken),
            await db.Users.CountAsync(cancellationToken),
            await db.Bookings.CountAsync(cancellationToken),
            clock.Today);
    }
}