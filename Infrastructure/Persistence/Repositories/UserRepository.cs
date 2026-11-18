using CampusFlow.Application.Abstractions;
using CampusFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace CampusFlow.Infrastructure.Persistence.Repositories;

/// <inheritdoc />
public sealed class UserRepository : IUserRepository
{
    private readonly CampusFlowDbContext _db;

    public UserRepository(CampusFlowDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken)
    {
        var normalized = login.Trim().ToLowerInvariant();
        return _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Login.ToLower() == normalized, cancellationToken);
    }

    /// <inheritdoc />
    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return _db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }
}