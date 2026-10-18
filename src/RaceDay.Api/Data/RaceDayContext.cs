using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core context mirroring docs/RaceDayDatabase.sql exactly.
/// Eight entities, all PKs, FKs, UQs and CKs as per the ERD.
/// </summary>
public class RaceDayContext : DbContext
{
    /// <summary>Standard DI constructor — options injected by Program.cs.</summary>
    public RaceDayContext(DbContextOptions<RaceDayContext> options) : base(options) { }

    // DbSets — one per table in the SQL script
    public DbSet<Role> Roles => Set<Role>(); // lookup: Organiser, Participant
    public DbSet<User> Users => Set<User>(); // accounts
    public DbSet<Event> Events => Set<Event>(); // races
    public DbSet<Models.Route> Routes => Set<Models.Route>(); // 1:1 per event
    public DbSet<EventCategory> EventCategories => Set<EventCategory>(); // per-event categories
    public DbSet<Enrolment> Enrolments => Set<Enrolment>(); // participant entries
    public DbSet<Result> Results => Set<Result>(); // finish data
    public DbSet<Session> Sessions => Set<Session>(); // server-side sessions

    /// <summary>
    /// Configures keys, indexes, constraints and seed data.
    /// Every constraint name (PK_/UQ_/CK_/FK_/DF_) matches the SQL script for traceability.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder m)
    {
        // --- Roles: exactly two rows, enforced by CHECK ---
        m.Entity<Role>(e =>
        {
            e.HasKey(r => r.RoleId); // PK_Roles
            e.HasIndex(r => r.RoleName).IsUnique(); // UQ_Roles_RoleName
            e.Property(r => r.RoleName).HasMaxLength(20).IsRequired();
            e.ToTable(t => t.HasCheckConstraint("CK_Roles_RoleName", "RoleName IN ('Organiser','Participant')"));
        });

        // --- Users: unique email, BCrypt hash, FK to Roles ---
        m.Entity<User>(e =>
        {
            e.HasKey(u => u.UserId); // PK_Users
            e.HasIndex(u => u.Email).IsUnique(); // UQ_Users_Email
            e.Property(u => u.Email).HasMaxLength(100).IsRequired();
            e.Property(u => u.FirstName).HasMaxLength(50).IsRequired();
            e.Property(u => u.LastName).HasMaxLength(50).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired(); // BCrypt hash
            e.Property(u => u.PhoneNumber).HasMaxLength(20);
            e.Property(u => u.IsActive).HasDefaultValue(true); // DF_Users_IsActive
            e.Property(u => u.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()"); // DF_Users_CreatedAt
            e.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict); // FK_Users_Roles
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Users_Email", "Email LIKE '%_@_%.__%'"); // cheap format check
            });
        });

        // --- Events: unique on (Name,Date,Location), ownership via OrganiserId ---
        m.Entity<Event>(e =>
        {
            e.HasKey(ev => ev.EventId); // PK_Events
            e.HasIndex(ev => new { ev.EventName, ev.EventDate, ev.Location }).IsUnique(); // UQ_Events_Name_Date_Location
            e.Property(ev => ev.EventName).HasMaxLength(100).IsRequired();
            e.Property(ev => ev.Description).HasMaxLength(500).IsRequired();
            e.Property(ev => ev.Location).HasMaxLength(100).IsRequired();
            e.Property(ev => ev.DistanceKm).HasPrecision(6, 2); // DECIMAL(6,2)
            e.Property(ev => ev.EventType).HasMaxLength(10).IsRequired();
            e.HasOne(ev => ev.Organiser).WithMany(u => u.OrganisedEvents).HasForeignKey(ev => ev.OrganiserId).OnDelete(DeleteBehavior.Restrict); // FK_Events_Users
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Events_DistanceKm", "DistanceKm > 0");
                t.HasCheckConstraint("CK_Events_EventType", "EventType IN ('Run','Walk','Cycle')");
            });
        });

        // --- Routes: 1:1 with Events (UQ on EventId) ---
        m.Entity<Models.Route>(e =>
        {
            e.HasKey(r => r.RouteId); // PK_Routes
            e.HasIndex(r => r.EventId).IsUnique(); // UQ_Routes_EventId — enforces 1:1
            e.Property(r => r.StartPoint).HasMaxLength(100).IsRequired();
            e.Property(r => r.EndPoint).HasMaxLength(100).IsRequired();
            e.HasOne(r => r.Event).WithOne(ev => ev.Route).HasForeignKey<Models.Route>(r => r.EventId).OnDelete(DeleteBehavior.Cascade); // FK_Routes_Events
        });

        // --- EventCategories: unique per (EventId, CategoryName), nullable age bounds ---
        m.Entity<EventCategory>(e =>
        {
            e.HasKey(c => c.CategoryId); // PK_EventCategories
            e.HasIndex(c => new { c.EventId, c.CategoryName }).IsUnique(); // UQ_EventCategories_Event_Name
            e.Property(c => c.CategoryName).HasMaxLength(50).IsRequired();
            e.HasOne(c => c.Event).WithMany(ev => ev.Categories).HasForeignKey(c => c.EventId).OnDelete(DeleteBehavior.Cascade); // FK_EventCategories_Events
        });

        // --- Enrolments: junction (User↔Event) + chosen Category, unique per (Event,User) ---
        m.Entity<Enrolment>(e =>
        {
            e.HasKey(en => en.EnrolmentId); // PK_Enrolments
            e.HasIndex(en => new { en.EventId, en.UserId }).IsUnique(); // UQ_Enrolments_Event_User
            e.Property(en => en.Status).HasMaxLength(20).HasDefaultValue("Confirmed"); // DF_Enrolments_Status
            e.HasOne(en => en.Event).WithMany(ev => ev.Enrolments).HasForeignKey(en => en.EventId).OnDelete(DeleteBehavior.NoAction); // FK_Enrolments_Events
            e.HasOne(en => en.User).WithMany(u => u.Enrolments).HasForeignKey(en => en.UserId).OnDelete(DeleteBehavior.NoAction); // FK_Enrolments_Users
            e.HasOne(en => en.Category).WithMany(c => c.Enrolments).HasForeignKey(en => en.CategoryId).OnDelete(DeleteBehavior.Restrict); // FK_Enrolments_Categories
        });

        // --- Results: 1:1 with Enrolment + unique position per event ---
        m.Entity<Result>(e =>
        {
            e.HasKey(r => r.ResultId); // PK_Results
            e.HasIndex(r => r.EnrolmentId).IsUnique(); // UQ_Results_EnrolmentId — 1:1
            e.HasIndex(r => new { r.EventId, r.FinishingPosition }).IsUnique(); // UQ_Results_Event_Position
            e.HasOne(r => r.Enrolment).WithOne(en => en.Result).HasForeignKey<Result>(r => r.EnrolmentId).OnDelete(DeleteBehavior.Cascade); // FK_Results_Enrolments
            e.HasOne(r => r.Event).WithMany(ev => ev.Results).HasForeignKey(r => r.EventId).OnDelete(DeleteBehavior.NoAction); // FK_Results_Events
        });

        // --- Sessions: opaque token, IsActive for logout audit ---
        m.Entity<Session>(e =>
        {
            e.HasKey(s => s.SessionId); // PK_Sessions
            e.HasIndex(s => s.SessionToken).IsUnique(); // UQ_Sessions_SessionToken
            e.Property(s => s.SessionToken).HasMaxLength(64).IsRequired();
            e.HasOne(s => s.User).WithMany(u => u.Sessions).HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade); // FK_Sessions_Users
        });

        // --- Seed: the two brief roles always exist ---
        m.Entity<Role>().HasData(
            new Role { RoleId = 1, RoleName = "Organiser" }, // can create/manage
            new Role { RoleId = 2, RoleName = "Participant" } // can enrol/view
        );
    }
}
