using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;

namespace BachataFeedback.Api.Data;

public class ApplicationDbContext : IdentityDbContext<User>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Event> Events { get; set; }
    public DbSet<EventParticipant> EventParticipants { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<UserPhoto> UserPhotos { get; set; }
    public DbSet<UserSettings> UserSettings { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<EventReview> EventReviews { get; set; }
    public DbSet<ModerationJob> ModerationJobs { get; set; }
    public DbSet<EventPhoto> EventPhotos { get; set; }

    // Telegram multi-chat / occurrence system
    public DbSet<DanceGroup> DanceGroups { get; set; }
    public DbSet<TelegramChat> TelegramChats { get; set; }
    public DbSet<Occurrence> Occurrences { get; set; }
    public DbSet<OccurrencePublication> OccurrencePublications { get; set; }
    public DbSet<Attendance> Attendances { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // User-UserSettings relationship (one-to-one)
        builder.Entity<UserSettings>()
            .HasOne(us => us.User)
            .WithOne(u => u.Settings)
            .HasForeignKey<UserSettings>(us => us.UserId);

        // Event-User relationship (creator)
        builder.Entity<Event>()
            .HasOne(e => e.Creator)
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // EventParticipant relationships
        builder.Entity<EventParticipant>()
            .HasOne(ep => ep.User)
            .WithMany(u => u.EventParticipations)
            .HasForeignKey(ep => ep.UserId);

        builder.Entity<EventParticipant>()
            .HasOne(ep => ep.Event)
            .WithMany(e => e.Participants)
            .HasForeignKey(ep => ep.EventId);

        // Review relationships
        builder.Entity<Review>()
            .HasOne(r => r.Reviewer)
            .WithMany(u => u.ReviewsGiven)
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Review>()
            .HasOne(r => r.Reviewee)
            .WithMany(u => u.ReviewsReceived)
            .HasForeignKey(r => r.RevieweeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Review>()
            .HasOne(r => r.Event)
            .WithMany(e => e.Reviews)
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.SetNull);

        // EventReview relationships
        builder.Entity<EventReview>()
            .HasOne(er => er.Event)
            .WithMany(e => e.EventReviews)
            .HasForeignKey(er => er.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<EventReview>()
            .HasOne(er => er.Reviewer)
            .WithMany()
            .HasForeignKey(er => er.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        // UserPhoto relationships
        builder.Entity<UserPhoto>()
            .HasOne(up => up.User)
            .WithMany(u => u.Photos)
            .HasForeignKey(up => up.UserId);

        // Report relationships
        builder.Entity<Report>()
            .HasOne(r => r.Reporter)
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        // ModerationJob index
        builder.Entity<ModerationJob>()
            .HasIndex(m => new { m.TargetType, m.TargetId })
            .IsUnique(false);

        // Unique constraints
        builder.Entity<EventParticipant>()
            .HasIndex(ep => new { ep.UserId, ep.EventId })
            .IsUnique();

        // Уникальный индекс TelegramId (sparse — только для не-NULL значений через фильтр)
        builder.Entity<User>()
            .HasIndex(u => u.TelegramId)
            .IsUnique()
            .HasFilter("[TelegramId] IS NOT NULL")
            .HasDatabaseName("IX_Users_TelegramId_Unique");

        // Предотвращаем дублирование: один пользователь — один отзыв на конкретное событие
        // (NULL EventId — разрешено несколько, но ограничено по времени через rate-limit в сервисе)
        builder.Entity<EventReview>()
            .HasIndex(er => new { er.ReviewerId, er.EventId })
            .IsUnique()
            .HasDatabaseName("IX_EventReviews_ReviewerId_EventId_Unique");

        builder.Entity<EventPhoto>()
            .HasOne(p => p.Event)
            .WithMany(e => e.Photos)
            .HasForeignKey(p => p.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<EventPhoto>()
            .HasOne(p => p.Uploader)
            .WithMany()
            .HasForeignKey(p => p.UploaderId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---- Performance indexes ----

        // Reviews: часто фильтруем по кому адресован отзыв, сортируем по дате
        builder.Entity<Review>()
            .HasIndex(r => new { r.RevieweeId, r.CreatedAt })
            .HasDatabaseName("IX_Reviews_RevieweeId_CreatedAt");

        // Мои оставленные (по автору) + дата
        builder.Entity<Review>()
            .HasIndex(r => new { r.ReviewerId, r.CreatedAt })
            .HasDatabaseName("IX_Reviews_ReviewerId_CreatedAt");

        // EventReviews: отбор по событию + дата
        builder.Entity<EventReview>()
            .HasIndex(er => new { er.EventId, er.CreatedAt })
            .HasDatabaseName("IX_EventReviews_EventId_CreatedAt");

        // Reports: модерация часто ищет все жалобы по цели
        builder.Entity<Report>()
            .HasIndex(r => new { r.TargetType, r.TargetId })
            .HasDatabaseName("IX_Reports_TargetType_TargetId");

        // Фото пользователей: выборка по владельцу
        builder.Entity<UserPhoto>()
            .HasIndex(up => up.UserId)
            .HasDatabaseName("IX_UserPhotos_UserId");

        // Фото событий: выборка по событию
        builder.Entity<EventPhoto>()
            .HasIndex(ep => ep.EventId)
            .HasDatabaseName("IX_EventPhotos_EventId");

        // ---- Telegram / Occurrence system ----

        // TelegramChat PK — ChatId (long)
        builder.Entity<TelegramChat>()
            .HasKey(tc => tc.ChatId);

        builder.Entity<TelegramChat>()
            .HasOne(tc => tc.DanceGroup)
            .WithMany(g => g.Chats)
            .HasForeignKey(tc => tc.DanceGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        // Occurrence → DanceGroup
        builder.Entity<Occurrence>()
            .HasOne(o => o.DanceGroup)
            .WithMany(g => g.Occurrences)
            .HasForeignKey(o => o.DanceGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        // OccurrencePublication → Occurrence
        builder.Entity<OccurrencePublication>()
            .HasOne(op => op.Occurrence)
            .WithMany(o => o.Publications)
            .HasForeignKey(op => op.OccurrenceId)
            .OnDelete(DeleteBehavior.Cascade);

        // OccurrencePublication → TelegramChat
        builder.Entity<OccurrencePublication>()
            .HasOne(op => op.TelegramChat)
            .WithMany(tc => tc.Publications)
            .HasForeignKey(op => op.TelegramChatId)
            .OnDelete(DeleteBehavior.Cascade);

        // Attendance → Occurrence
        builder.Entity<Attendance>()
            .HasOne(a => a.Occurrence)
            .WithMany(o => o.Attendances)
            .HasForeignKey(a => a.OccurrenceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Attendance → User (nullable)
        builder.Entity<Attendance>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Уникальность: один пользователь платформы — одна запись на занятие
        builder.Entity<Attendance>()
            .HasIndex(a => new { a.OccurrenceId, a.UserId })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL")
            .HasDatabaseName("IX_Attendance_OccurrenceId_UserId_Unique");

        // Уникальность: один Telegram-пользователь — одна запись на занятие
        builder.Entity<Attendance>()
            .HasIndex(a => new { a.OccurrenceId, a.TelegramUserId })
            .IsUnique()
            .HasFilter("[TelegramUserId] IS NOT NULL")
            .HasDatabaseName("IX_Attendance_OccurrenceId_TelegramUserId_Unique");

        // Только один canonical_poll (IsVotingSource=true) на Occurrence
        builder.Entity<OccurrencePublication>()
            .HasIndex(op => new { op.OccurrenceId, op.IsVotingSource })
            .HasDatabaseName("IX_OccurrencePublication_OccurrenceId_IsVotingSource");

        // Быстрый поиск publication по TelegramPollId (для poll_answer events)
        builder.Entity<OccurrencePublication>()
            .HasIndex(op => op.TelegramPollId)
            .HasDatabaseName("IX_OccurrencePublication_TelegramPollId");

        // Occurrence: сортировка и фильтрация по времени и статусу
        builder.Entity<Occurrence>()
            .HasIndex(o => new { o.StartsAt, o.Status })
            .HasDatabaseName("IX_Occurrence_StartsAt_Status");
    }
}
