using FlowOS.Api.Models;
using FlowOS.Api.Models.Audit;
using FlowOS.Api.Models.Enums;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Task = FlowOS.Api.Models.Task;

namespace FlowOS.Api.Data
{
    public class FlowOSContext : IdentityDbContext<ApplicationUser>
    {
        public FlowOSContext(DbContextOptions<FlowOSContext> options) : base(options)
        {
        }
        public DbSet<Task> Tasks { get; set; }
        public DbSet<DailyPlan> DailyPlans { get; set; }
        public DbSet<UserRefreshToken> UserRefreshTokens { get; set; }

        public DbSet<DailyPlanItem> DailyPlanItems { get; set; }
        public DbSet<NudgeFeedback> NudgeFeedback { get; set; }

        public DbSet<ToneHistory> ToneHistories => Set<ToneHistory>();

        public DbSet<AiPlanAudit> AiPlanAudits { get; set; }
        public DbSet<UserDeviceToken> UserDeviceTokens => Set<UserDeviceToken>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserDeviceToken>()
            .HasIndex(x => new { x.UserId, x.ExpoPushToken })
            .IsUnique();

            // ---- Enum <-> string converters
            var toneConverter = new EnumToStringConverter<PlanTone>();

            modelBuilder.Entity<ApplicationUser>(b =>
            {
                b.Property(u => u.PreferredTone).HasConversion(toneConverter);
                b.Property(u => u.CurrentTone).HasConversion(toneConverter);
            });

            modelBuilder.Entity<ToneHistory>(b =>
            {
                b.Property(t => t.SuggestedTone).HasConversion(toneConverter);
                b.Property(t => t.AppliedTone).HasConversion(toneConverter);

                b.HasIndex(t => new { t.UserId, t.Date }).IsUnique(); // one row/day/user
                b.Property(t => t.Notes).HasMaxLength(400);
            });

            //modelBuilder.Entity<Task>()
            //    .Property(t => t.DueDate)
            //    .HasConversion(
            //        v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
            //        v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
            //    );

            modelBuilder.Entity<Task>()
                .Property(t => t.DueDate)
                .HasConversion(
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
                );

            modelBuilder.Entity<Task>()
                .Property(t => t.NudgeAtUtc)
                .HasConversion(
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v,
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v
                );

            modelBuilder.Entity<Task>()
                .Property(t => t.NudgeSentAtUtc)
                .HasConversion(
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v,
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v
                );

            modelBuilder.Entity<DailyPlan>(entity =>
            {
                entity.HasMany(p => p.Items)
                      .WithOne(i => i.Plan)
                      .HasForeignKey(i => i.PlanId)
                      .OnDelete(DeleteBehavior.Cascade);

                // ✅ Ensure one plan per user per date
                entity.HasIndex(p => new { p.UserId, p.Date })
                      .IsUnique();
            });

            modelBuilder.Entity<DailyPlanItem>()
            .HasOne(i => i.Task)
            .WithMany()
            .HasForeignKey(i => i.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        }
    }
}
