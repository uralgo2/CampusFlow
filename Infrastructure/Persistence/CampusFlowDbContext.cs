using CampusFlow.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CampusFlow.Infrastructure.Persistence;

/// <summary>EF Core контекст SQLite: три сущности MVP — пользователи, аудитории, брони.</summary>
public sealed class CampusFlowDbContext : DbContext
{
    /// <summary>Оборудование хранится строкой кодов через запятую, чтобы не заводить отдельную таблицу.</summary>
    private static readonly ValueConverter<List<Equipment>, string> EquipmentConverter = new(
        equipment => string.Join(',', equipment.Select(EquipmentCatalog.ToCode)),
        text => string.IsNullOrEmpty(text)
            ? new List<Equipment>()
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(EquipmentCatalog.Parse).ToList());

    private static readonly ValueComparer<List<Equipment>> EquipmentComparer = new(
        (left, right) => left != null && right != null && left.SequenceEqual(right),
        value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        value => value.ToList());

    public CampusFlowDbContext(DbContextOptions<CampusFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("Users");
            user.HasKey(entity => entity.Id);
            user.Property(entity => entity.Login).IsRequired().HasMaxLength(64);
            user.HasIndex(entity => entity.Login).IsUnique();
            user.Property(entity => entity.DisplayName).IsRequired().HasMaxLength(200);
            user.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        });

        modelBuilder.Entity<Room>(room =>
        {
            room.ToTable("Rooms");
            room.HasKey(entity => entity.Id);
            room.Property(entity => entity.Name).IsRequired().HasMaxLength(32);
            room.HasIndex(entity => entity.Name).IsUnique();
            room.Property(entity => entity.Building).IsRequired().HasMaxLength(1);
            room.Property(entity => entity.Equipment).HasConversion(EquipmentConverter, EquipmentComparer).IsRequired();
        });

        modelBuilder.Entity<Booking>(booking =>
        {
            booking.ToTable("Bookings");
            booking.HasKey(entity => entity.Id);
            booking.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            booking.Property(entity => entity.Comment).HasMaxLength(500);
            booking.HasIndex(entity => new { entity.RoomId, entity.Date });
            booking.HasIndex(entity => new { entity.UserId, entity.Date });

            booking
                .HasOne(entity => entity.Room)
                .WithMany(room => room.Bookings)
                .HasForeignKey(entity => entity.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            booking
                .HasOne(entity => entity.User)
                .WithMany(user => user.Bookings)
                .HasForeignKey(entity => entity.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}