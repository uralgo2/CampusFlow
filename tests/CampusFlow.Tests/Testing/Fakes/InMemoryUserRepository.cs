using CampusFlow.Application.Abstractions;
using CampusFlow.Domain;

namespace CampusFlow.Tests.Testing.Fakes;

/// <summary>Репозиторий пользователей в памяти.</summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly FakeDatabase _db;

    public InMemoryUserRepository(FakeDatabase db) => _db = db;

    public Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken) =>
        Task.FromResult(_db.Users.FirstOrDefault(user =>
            string.Equals(user.Login, login.Trim(), StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(_db.Users.FirstOrDefault(user => user.Id == id));
}
