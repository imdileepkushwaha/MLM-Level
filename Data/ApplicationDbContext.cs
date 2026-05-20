using Microsoft.EntityFrameworkCore;
using MLM_Level.Models;

namespace MLM_Level.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<CommissionTran> CommissionTrans { get; set; } = null!;
        public DbSet<ActivationRequest> ActivationRequests { get; set; } = null!;
        public DbSet<WithdrawalRequest> WithdrawalRequests { get; set; } = null!;
        public DbSet<SupportTicket> SupportTickets { get; set; } = null!;
        public DbSet<KycDetail> KycDetails { get; set; } = null!;
        public DbSet<Announcement> Announcements { get; set; } = null!;
        public DbSet<Package> Packages { get; set; } = null!;
        public DbSet<UserPackage> UserPackages { get; set; } = null!;
        public DbSet<MlmSetting> MlmSettings { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure self-referencing relationship for Sponsor/Referrals
            modelBuilder.Entity<User>()
                .HasOne(u => u.Sponsor)
                .WithMany(s => s.Referrals)
                .HasForeignKey(u => u.SponsorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure self-referencing relationship for Parent/PlacementDownlines
            modelBuilder.Entity<User>()
                .HasOne(u => u.Parent)
                .WithMany(p => p.PlacementDownlines)
                .HasForeignKey(u => u.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure self-referencing relationship for DefaultPlacement
            modelBuilder.Entity<User>()
                .HasOne(u => u.DefaultPlacement)
                .WithMany()
                .HasForeignKey(u => u.DefaultPlacementId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure SupportTicket relationship
            modelBuilder.Entity<SupportTicket>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure CommissionTran relationships
            modelBuilder.Entity<CommissionTran>()
                .HasOne(c => c.User)
                .WithMany(u => u.CommissionsReceived)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CommissionTran>()
                .HasOne(c => c.FromUser)
                .WithMany(u => u.CommissionsTriggered)
                .HasForeignKey(c => c.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure ActivationRequest relationships
            modelBuilder.Entity<ActivationRequest>()
                .HasOne(a => a.User)
                .WithMany(u => u.ActivationRequests)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure WithdrawalRequest relationships
            modelBuilder.Entity<WithdrawalRequest>()
                .HasOne(w => w.User)
                .WithMany(u => u.WithdrawalRequests)
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure indexes and unique constraints
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.ReferralCode)
                .IsUnique();
        }
    }
}
