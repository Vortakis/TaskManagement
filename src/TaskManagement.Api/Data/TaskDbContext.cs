using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Models;

namespace TaskManagement.Api.Data
{
    public class TaskDbContext : DbContext
    {
        public TaskDbContext(DbContextOptions<TaskDbContext> options) : base(options) { }

        public DbSet<TaskModel> Tasks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TaskModel>()
                .Property(task => task.UpdatedAt)
                .IsConcurrencyToken();

            modelBuilder.Entity<TaskModel>()
                .HasIndex(task => task.DueDateTimeUtc)
                .HasDatabaseName("IX_Task_DueDateTime");

            modelBuilder.Entity<TaskModel>()
               .HasIndex(task => task.Priority)
               .HasDatabaseName("IX_Task_Priority");

            modelBuilder.Entity<TaskModel>()
               .HasIndex(task => task.Status)
               .HasDatabaseName("IX_Task_Status");
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            foreach (var entry in ChangeTracker.Entries<TaskModel>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
