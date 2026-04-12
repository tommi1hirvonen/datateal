using System.Text.Json;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DuckHouse.Orchestrator.Infrastructure.Data;

public class OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<Dictionary<string, string>?, string?> DictJsonConverter = new(
        v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null));

    private static readonly ValueConverter<List<Guid>?, string?> GuidListJsonConverter = new(
        v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => v == null ? null : JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null));

    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobParameter> JobParameters => Set<JobParameter>();
    public DbSet<JobTask> JobTasks => Set<JobTask>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    public DbSet<JobSchedule> JobSchedules => Set<JobSchedule>();
    public DbSet<NodePoolConfig> NodePoolConfigs => Set<NodePoolConfig>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();
    public DbSet<TaskRun> TaskRuns => Set<TaskRun>();
    public DbSet<TaskRunCellOutput> TaskRunCellOutputs => Set<TaskRunCellOutput>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasMany(e => e.Parameters).WithOne(p => p.Job).HasForeignKey(p => p.JobId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Tasks).WithOne(t => t.Job).HasForeignKey(t => t.JobId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Schedules).WithOne(s => s.Job).HasForeignKey(s => s.JobId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobParameter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
        });

        // TPH for task types
        modelBuilder.Entity<JobTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.HasDiscriminator<string>("TaskType")
                .HasValue<NotebookTask>("Notebook")
                .HasValue<SqlQueryTask>("SqlQuery")
                .HasValue<SubJobTask>("SubJob");
            entity.Property("TaskType").HasMaxLength(32).IsRequired();
            entity.HasMany(e => e.Dependencies).WithOne(d => d.Task).HasForeignKey(d => d.TaskId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotebookTask>(entity =>
        {
            entity.Property(e => e.Parameters).HasColumnType("jsonb").HasConversion(DictJsonConverter);
        });

        modelBuilder.Entity<SqlQueryTask>(entity =>
        {
            entity.Property(e => e.Parameters).HasColumnType("jsonb").HasConversion(DictJsonConverter);
        });

        modelBuilder.Entity<SubJobTask>(entity =>
        {
            entity.Property(e => e.Parameters).HasColumnType("jsonb").HasConversion(DictJsonConverter);
        });

        modelBuilder.Entity<TaskDependency>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Condition).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(e => e.DependsOnTask).WithMany().HasForeignKey(e => e.DependsOnTaskId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobSchedule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CronExpression).HasMaxLength(128).IsRequired();
            entity.Property(e => e.TimeZone).HasMaxLength(64);
            entity.Property(e => e.Parameters).HasColumnType("jsonb").HasConversion(DictJsonConverter);
        });

        modelBuilder.Entity<NodePoolConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.VmSize).HasMaxLength(64).IsRequired();
            entity.Property(e => e.WheelPackageIds).HasColumnType("jsonb").HasConversion(GuidListJsonConverter);
        });

        modelBuilder.Entity<JobRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Trigger).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.JobName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ParametersJson).HasColumnType("jsonb");
            entity.Ignore(e => e.Parameters);
            entity.HasOne(e => e.Job).WithMany().HasForeignKey(e => e.JobId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ParentRun).WithMany().HasForeignKey(e => e.ParentRunId).OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.TaskRuns).WithOne(tr => tr.JobRun).HasForeignKey(tr => tr.JobRunId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.JobId);
        });

        modelBuilder.Entity<TaskRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.TaskName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.TaskType).HasMaxLength(32).IsRequired();
            entity.HasOne(e => e.Task).WithMany().HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.CellOutputs).WithOne(c => c.TaskRun).HasForeignKey(c => c.TaskRunId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskRunCellOutput>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CellType).HasMaxLength(32).IsRequired();
            entity.Property(e => e.CellRole).HasMaxLength(32);
            entity.Property(e => e.Language).HasMaxLength(32);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.OutputsJson).HasColumnType("jsonb");
            entity.Property(e => e.ErrorJson).HasColumnType("jsonb");
            entity.HasIndex(e => new { e.TaskRunId, e.CellIndex });
        });
    }
}
