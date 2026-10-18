using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class RaceDayContext : DbContext
{
    public RaceDayContext(DbContextOptions<RaceDayContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Models.Route> Routes => Set<Models.Route>();
    public DbSet<EventCategory> EventCategories => Set<EventCategory>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        // Roles
        m.Entity<Role>(e =>
        {
            e.HasKey(r => r.RoleId);
            e.HasIndex(r => r.RoleName).IsUnique();
            e.Property(r => r.RoleName).HasMaxLength(20).IsRequired();
            e.ToTable(t => t.HasCheckConstraint("CK_Roles_RoleName", "RoleName IN ('Organiser','Participant')"));
        });

        // Users
        m.Entity<User>(e =>
        {
            e.HasKey(u => u.UserId);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(100).IsRequired();
            e.Property(u => u.FirstName).HasMaxLength(50).IsRequired();
            e.Property(u => u.LastName).HasMaxLength(50).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(u => u.PhoneNumber).HasMaxLength(20);
            e.Property(u => u.IsActive).HasDefaultValue(true);
            e.Property(u => u.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Users_Email", "Email LIKE '%_@_%.__%'");
            });
        });

        // Events
        m.Entity<Event>(e =>
        {
            e.HasKey(ev => ev.EventId);
            e.HasIndex(ev => new { ev.EventName, ev.EventDate, ev.Location }).IsUnique();
            e.Property(ev => ev.EventName).HasMaxLength(100).IsRequired();
            e.Property(ev => ev.Description).HasMaxLength(500).IsRequired();
            e.Property(ev => ev.Location).HasMaxLength(100).IsRequired();
            e.Property(ev => ev.DistanceKm).HasPrecision(6, 2);
            e.Property(ev => ev.EventType).HasMaxLength(10).IsRequired();
            e.HasOne(ev => ev.Organiser).WithMany(u => u.OrganisedEvents).HasForeignKey(ev => ev.OrganiserId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Events_DistanceKm", "DistanceKm > 0");
                t.HasCheckConstraint("CK_Events_EventType", "EventType IN ('Run','Walk','Cycle')");
            });
        });

        // Routes - 1:1
        m.Entity<Models.Route>(e =>
        {
            e.HasKey(r => r.RouteId);
            e.HasIndex(r => r.EventId).IsUnique();
            e.Property(r => r.StartPoint).HasMaxLength(100).IsRequired();
            e.Property(r => r.EndPoint).HasMaxLength(100).IsRequired();
            e.HasOne(r => r.Event).WithOne(ev => ev.Route).HasForeignKey<Models.Route>(r => r.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        // EventCategories
        m.Entity<EventCategory>(e =>
        {
            e.HasKey(c => c.CategoryId);
            e.HasIndex(c => new { c.EventId, c.CategoryName }).IsUnique();
            e.Property(c => c.CategoryName).HasMaxLength(50).IsRequired();
            e.HasOne(c => c.Event).WithMany(ev => ev.Categories).HasForeignKey(c => c.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        // Enrolments
        m.Entity<Enrolment>(e =>
        {
            e.HasKey(en => en.EnrolmentId);
            e.HasIndex(en => new { en.EventId, en.UserId }).IsUnique();
            e.Property(en => en.Status).HasMaxLength(20).HasDefaultValue("Confirmed");
            e.HasOne(en => en.Event).WithMany(ev => ev.Enrolments).HasForeignKey(en => en.EventId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(en => en.User).WithMany(u => u.Enrolments).HasForeignKey(en => en.UserId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(en => en.Category).WithMany(c => c.Enrolments).HasForeignKey(en => en.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        // Results
        m.Entity<Result>(e =>
        {
            e.HasKey(r => r.ResultId);
            e.HasIndex(r => r.EnrolmentId).IsUnique();
            e.HasIndex(r => new { r.EventId, r.FinishingPosition }).IsUnique();
            e.HasOne(r => r.Enrolment).WithOne(en => en.Result).HasForeignKey<Result>(r => r.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Event).WithMany(ev => ev.Results).HasForeignKey(r => r.EventId).OnDelete(DeleteBehavior.NoAction);
        });

        // Sessions
        m.Entity<Session>(e =>
        {
            e.HasKey(s => s.SessionId);
            e.HasIndex(s => s.SessionToken).IsUnique();
            e.Property(s => s.SessionToken).HasMaxLength(64).IsRequired();
            e.HasOne(s => s.User).WithMany(u => u.Sessions).HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Seed Roles (always)
        m.Entity<Role>().HasData(
            new Role { RoleId = 1, RoleName = "Organiser" },
            new Role { RoleId = 2, RoleName = "Participant" }
        );
    }
}
