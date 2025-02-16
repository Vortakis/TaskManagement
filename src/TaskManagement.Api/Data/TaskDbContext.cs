using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskManagement.Api.Common.Configuration.Settings.Sections;
using TaskManagement.Api.Models;

namespace TaskManagement.Api.Data
{
    public class TaskDbContext : DbContext
    {
        private readonly IConfiguration _configuration;
        private readonly ConcurrentProcessingSection _concurrencySettings;

        public TaskDbContext(
            DbContextOptions<TaskDbContext> options,
            IConfiguration configuration,
            IOptions<ConcurrentProcessingSection> concurrentOptions)  : base(options) 
        {
            _configuration = configuration;
            _concurrencySettings = concurrentOptions.Value;
        }

        public DbSet<TaskModel> Tasks { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
      //      if (!optionsBuilder.IsConfigured)
        //    {
                var connection = new SqliteConnection(_configuration.GetConnectionString("DefaultConnection"));
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    // Maker sure that max batch size is configured.
                    command.CommandText = $"PRAGMA max_variable_number = {_concurrencySettings.BatchSize + 1};";
                    command.ExecuteNonQuery();
                }
                optionsBuilder.UseSqlite(connection);
       //     }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TaskModel>()
                .Property(task => task.UpdatedAt)
                .IsConcurrencyToken();

            modelBuilder.Entity<TaskModel>()
                .HasIndex(t => new { t.Id, t.UpdatedAt, t.Status, t.DueDateTimeUtc })
                .HasDatabaseName("IDX_Task_Id_UpdatedAt_Status_DueDateTimeUtc");         
        }

        public override int SaveChanges()
        {
            UpdateVersionDates();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateVersionDates();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateVersionDates()
        {
            foreach (var entry in ChangeTracker.Entries<TaskModel>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH.mm.ss.fff");
                    entry.Entity.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH.mm.ss.fff");
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH.mm.ss.fff");
                }
            }
        }
    }
}
