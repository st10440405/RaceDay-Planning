using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class RaceDayContext(DbContextOptions<RaceDayContext> options) : DbContext(options)
{
    public DbSet<Organiser> Organisers => Set<Organiser>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<RaceEvent> Events => Set<RaceEvent>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<EventCategory> EventCategories => Set<EventCategory>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Organiser>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.Email).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsUnicode(false);
        });
        model.Entity<Participant>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.Email).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsUnicode(false);
            e.Property(x => x.Gender).HasMaxLength(20).IsUnicode(false);
        });
        model.Entity<RaceEvent>().HasKey(x => x.EventId);
        model.Entity<RaceEvent>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsUnicode(false);
            e.Property(x => x.EventDate).HasColumnType("date");
            e.Property(x => x.Location).HasMaxLength(200).IsUnicode(false);
            e.Property(x => x.Description).IsUnicode(false);
            e.Property(x => x.EventType).HasMaxLength(10).IsUnicode(false);
        });
        model.Entity<RaceEvent>().Property(x => x.DistanceKm).HasPrecision(6, 2);
        model.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.Description).IsUnicode(false);
        });
        model.Entity<Enrolment>(e =>
        {
            e.Property(x => x.Status).HasMaxLength(50).IsUnicode(false);
            e.Property(x => x.RegistrationDate).HasColumnType("datetime");
        });
        model.Entity<Organiser>().HasIndex(x => x.Email).IsUnique();
        model.Entity<Participant>().HasIndex(x => x.Email).IsUnique();
        model.Entity<EventCategory>().HasKey(x => new { x.EventId, x.CategoryId });
        model.Entity<EventCategory>().HasOne(x => x.Event).WithMany(x => x.EventCategories).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<EventCategory>().HasOne(x => x.Category).WithMany(x => x.EventCategories).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<RaceEvent>().HasOne(x => x.Organiser).WithMany(x => x.Events).HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Enrolment>().HasIndex(x => new { x.ParticipantId, x.EventId }).IsUnique();
        model.Entity<Enrolment>().HasOne(x => x.Participant).WithMany(x => x.Enrolments).HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Enrolment>().HasOne(x => x.Event).WithMany(x => x.Enrolments).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Enrolment>().HasOne(x => x.Category).WithMany(x => x.Enrolments).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
