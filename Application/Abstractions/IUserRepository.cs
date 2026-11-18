using CampusFlow.Domain;

namespace CampusFlow.Application.Abstractions;

/// <summary>Хранилище пользователей.</summary>
public interface IUserRepository
{
    /// <summary>Найти пользователя по логину без учёта регистра.</summary>
    Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken);

    /// <summary>Найти пользователя по идентификатору.</summary>
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken);
}