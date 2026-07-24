using ManagementSPD.Models;
using Microsoft.EntityFrameworkCore;

namespace ManagementSPD.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<License> Licenses { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<LoanTransaction> LoanTransactions { get; set; }
        public DbSet<LoanApproval> LoanApprovals { get; set; }

        public DbSet<AuditLog> AuditLogs { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<User>().Property(u => u.Password).HasColumnName("Password");

            modelBuilder.Entity<LoanTransaction>()
                .HasOne(t => t.Employee)
                .WithMany(u => u.LoansRequested)
                .HasForeignKey(t => t.EmployeeID)
                .OnDelete(DeleteBehavior.Restrict); 

            modelBuilder.Entity<LoanTransaction>()
                .HasOne(t => t.Staff)
                .WithMany(u => u.LoansManaged)
                .HasForeignKey(t => t.StaffID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LoanApproval>()
                .HasOne(a => a.Staff)
                .WithMany(u => u.ApprovalsHistory)
                .HasForeignKey(a => a.StaffID)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}