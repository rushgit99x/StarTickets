using Microsoft.EntityFrameworkCore;
using StarTickets.Models;

namespace StarTickets.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).HasColumnName("UserId");
                entity.Property(e => e.Email).HasColumnName("Email").HasMaxLength(191).IsRequired();
                entity.Property(e => e.PasswordHash).HasColumnName("PasswordHash").HasMaxLength(255).IsRequired();
                entity.Property(e => e.FirstName).HasColumnName("FirstName").HasMaxLength(100).IsRequired();
                entity.Property(e => e.LastName).HasColumnName("LastName").HasMaxLength(100).IsRequired();
                entity.Property(e => e.PhoneNumber).HasColumnName("PhoneNumber").HasMaxLength(20);
                entity.Property(e => e.DateOfBirth).HasColumnName("DateOfBirth");
                entity.Property(e => e.Role).HasColumnName("Role").IsRequired();
                entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
                entity.Property(e => e.EmailConfirmed).HasColumnName("EmailConfirmed").HasDefaultValue(false);
                entity.Property(e => e.LoyaltyPoints).HasColumnName("LoyaltyPoints").HasDefaultValue(0);
                entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
                entity.Property(e => e.ResetToken).HasColumnName("ResetToken").HasMaxLength(255);
                entity.Property(e => e.ResetTokenExpiry).HasColumnName("ResetTokenExpiry");

                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Email).HasDatabaseName("idx_users_email");
                entity.HasIndex(e => e.Role).HasDatabaseName("idx_users_role");

                // Configure relationship with UserRole
                entity.HasOne(u => u.UserRole)
                      .WithMany(r => r.Users)
                      .HasForeignKey(u => u.Role)
                      .HasPrincipalKey(r => r.RoleId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure UserRole entity
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("userroles");
                entity.HasKey(e => e.RoleId);
                entity.Property(e => e.RoleId).HasColumnName("RoleId");
                entity.Property(e => e.RoleName).HasColumnName("RoleName").HasMaxLength(50).IsRequired();
                entity.Property(e => e.Description).HasColumnName("Description");
                entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(e => e.RoleName).IsUnique();
            });

            // Seed data for UserRoles (matching your database)
            modelBuilder.Entity<UserRole>().HasData(
                new UserRole { RoleId = 1, RoleName = "Admin", Description = "System Administrator with full access" },
                new UserRole { RoleId = 2, RoleName = "EventOrganizer", Description = "Event Organizer who can create and manage events" },
                new UserRole { RoleId = 3, RoleName = "Customer", Description = "Regular customer who can browse and book tickets" }
            );
        }
    }
}