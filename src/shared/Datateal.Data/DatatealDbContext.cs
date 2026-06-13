using System.Text.Json;
using Datateal.Core.Users;
using Datateal.Core.Catalogs;
using Datateal.Core.Environment;
using Datateal.Core.Nodes;
using Datateal.Core.Orchestration;
using Datateal.Core.RuntimePackages;
using Datateal.Core.Workspace;
using Datateal.Core.Workspaces;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Enums;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Datateal.Data;

public class DatatealDbContext(DbContextOptions<DatatealDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    private static readonly ValueConverter<Dictionary<string, string>?, string?> DictJsonConverter = new(
        v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null));

    private static readonly ValueConverter<List<Guid>?, string?> GuidListJsonConverter = new(
        v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => v == null ? null : JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null));

    private static readonly ValueConverter<List<string>?, string?> StringListJsonConverter = new(
        v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null));

    // ── Workspaces (tenancy) ──────────────────────────────────────────────
    public DbSet<Datateal.Core.Workspaces.Workspace> Workspaces => Set<Datateal.Core.Workspaces.Workspace>();
    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();

    // ── Workspace items ───────────────────────────────────────────────────
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<WorkspaceItem> WorkspaceItems => Set<WorkspaceItem>();

    // ── Catalogs ─────────────────────────────────────────────────────────
    public DbSet<Catalog> Catalogs => Set<Catalog>();
    public DbSet<ManagedCatalog> ManagedCatalogs => Set<ManagedCatalog>();
    public DbSet<UnmanagedCatalog> UnmanagedCatalogs => Set<UnmanagedCatalog>();
    public DbSet<CatalogWorkspaceAccess> CatalogWorkspaceAccess => Set<CatalogWorkspaceAccess>();

    // ── Runtime packages ──────────────────────────────────────────────────
    public DbSet<WheelPackage> WheelPackages => Set<WheelPackage>();

    // ── Environment ───────────────────────────────────────────────────────
    public DbSet<EnvironmentVariable> EnvironmentVariables => Set<EnvironmentVariable>();
    public DbSet<Secret> Secrets => Set<Secret>();

    // ── Data Protection ───────────────────────────────────────────────────
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // ── Users ─────────────────────────────────────────────────────────────
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<UserCatalogAccess> UserCatalogAccess => Set<UserCatalogAccess>();

    // ── Orchestrator ──────────────────────────────────────────────────────
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobParameter> JobParameters => Set<JobParameter>();
    public DbSet<JobTask> JobTasks => Set<JobTask>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    public DbSet<JobSchedule> JobSchedules => Set<JobSchedule>();
    public DbSet<NodePoolConfig> NodePoolConfigs => Set<NodePoolConfig>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();
    public DbSet<TaskRun> TaskRuns => Set<TaskRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureWorkspaces(modelBuilder);
        ConfigureWorkspace(modelBuilder);
        ConfigureCatalogs(modelBuilder);
        ConfigureRuntimePackages(modelBuilder);
        ConfigureEnvironment(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureOrchestrator(modelBuilder);
    }

    private static void ConfigureWorkspaces(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Datateal.Core.Workspaces.Workspace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(1024);
            entity.HasIndex(e => e.IsDefault)
                .IsUnique()
                .HasFilter("\"IsDefault\"")
                .HasDatabaseName("IX_Workspaces_IsDefault");
        });

        modelBuilder.Entity<WorkspaceMembership>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Memberships)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.WorkspaceId, e.UserId }).IsUnique();
            entity.PrimitiveCollection(e => e.Roles).HasColumnType("jsonb");
        });
    }

    private static void ConfigureWorkspace(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Folder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Datateal.Core.Workspaces.Workspace>()
                .WithMany()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.WorkspaceId);
        });

        modelBuilder.Entity<WorkspaceItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.HasDiscriminator(e => e.ItemType)
                .HasValue<Notebook>(WorkspaceItemType.Notebook)
                .HasValue<Query>(WorkspaceItemType.Query);
            entity.Property(e => e.ItemType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.HasOne(e => e.Folder)
                .WithMany(e => e.Items)
                .HasForeignKey(e => e.FolderId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Datateal.Core.Workspaces.Workspace>()
                .WithMany()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.WorkspaceId, e.Title, e.FolderId })
                .IsUnique()
                .HasFilter("\"FolderId\" IS NOT NULL")
                .HasDatabaseName("IX_WorkspaceItems_Workspace_Title_FolderId");
            entity.HasIndex(e => new { e.WorkspaceId, e.Title })
                .IsUnique()
                .HasFilter("\"FolderId\" IS NULL")
                .HasDatabaseName("IX_WorkspaceItems_Workspace_Title_Root");

            entity.Property(e => e.CatalogNames).HasColumnType("jsonb").HasConversion(StringListJsonConverter);
        });

        modelBuilder.Entity<Query>(entity =>
        {
            entity.Property(e => e.LastResultStatus).HasMaxLength(16);
        });
    }

    private static void ConfigureCatalogs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Catalog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.AccessibleFromAllWorkspaces).HasDefaultValue(true);
            entity.HasDiscriminator(e => e.CatalogType)
                .HasValue<ManagedCatalog>(CatalogType.Managed)
                .HasValue<UnmanagedCatalog>(CatalogType.Unmanaged);
            entity.Property(e => e.CatalogType).HasConversion<string>().HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<CatalogWorkspaceAccess>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Catalog)
                .WithMany(c => c.WorkspaceAccessList)
                .HasForeignKey(e => e.CatalogId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Workspace)
                .WithMany()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.CatalogId, e.WorkspaceId }).IsUnique();
        });

        modelBuilder.Entity<UnmanagedCatalog>(entity =>
        {
            entity.Property(e => e.DataPath).HasMaxLength(1024);
            entity.Property(e => e.CatalogHost).HasMaxLength(256);
            entity.Property(e => e.CatalogDatabase).HasMaxLength(256);
            entity.Property(e => e.CatalogUser).HasMaxLength(256);
        });
    }

    private static void ConfigureRuntimePackages(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WheelPackage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Data).IsRequired();
            entity.HasOne<Datateal.Core.Workspaces.Workspace>()
                .WithMany()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.WorkspaceId, e.Name }).IsUnique();
        });
    }

    private static void ConfigureEnvironment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EnvironmentVariable>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1024);
            entity.HasOne<Datateal.Core.Workspaces.Workspace>()
                .WithMany()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.WorkspaceId, e.Key }).IsUnique();
        });

        modelBuilder.Entity<Secret>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).HasMaxLength(256).IsRequired();
            entity.Property(e => e.EncryptedValue).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1024);
            entity.HasOne<Datateal.Core.Workspaces.Workspace>()
                .WithMany()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.WorkspaceId, e.Key }).IsUnique();
        });
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.ExternalId).HasMaxLength(256);
            entity.HasIndex(e => e.ExternalId).IsUnique().HasFilter("\"ExternalId\" IS NOT NULL");
            entity.Property(e => e.DisplayName).HasMaxLength(256).IsRequired();
            entity.PrimitiveCollection(e => e.Roles).HasColumnType("jsonb");
        });

        modelBuilder.Entity<UserCatalogAccess>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User)
                .WithMany(u => u.CatalogAccessList)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Catalog)
                .WithMany()
                .HasForeignKey(e => e.CatalogId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.CatalogId }).IsUnique();
        });
    }

    private void ConfigureOrchestrator(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.HasOne<Datateal.Core.Workspaces.Workspace>()
                .WithMany()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.WorkspaceId, e.Name }).IsUnique();
            entity.HasMany(e => e.Parameters).WithOne(p => p.Job).HasForeignKey(p => p.JobId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Tasks).WithOne(t => t.Job).HasForeignKey(t => t.JobId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Schedules).WithOne(s => s.Job).HasForeignKey(s => s.JobId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobParameter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<JobTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => new { e.JobId, e.Name }).IsUnique();
            entity.HasDiscriminator(e => e.TaskType)
                .HasValue<NotebookTask>(TaskType.Notebook)
                .HasValue<SqlQueryTask>(TaskType.SqlQuery)
                .HasValue<SubJobTask>(TaskType.SubJob);
            entity.Property(e => e.TaskType).HasConversion<string>().HasMaxLength(32).IsRequired();
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
            entity.HasOne<Datateal.Core.Workspaces.Workspace>()
                .WithMany()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.WorkspaceId, e.Name }).IsUnique();
            entity.Property(e => e.VmSize).HasMaxLength(64).IsRequired();
            entity.Property(e => e.WheelPackageIds).HasColumnType("jsonb").HasConversion(GuidListJsonConverter);
            entity.Property(e => e.EnvironmentVariableIds).HasColumnType("jsonb").HasConversion(GuidListJsonConverter);
            entity.Property(e => e.SecretIds).HasColumnType("jsonb").HasConversion(GuidListJsonConverter);
            entity.HasDiscriminator(e => e.PoolType)
                .HasValue<InteractiveNodePoolConfig>(NodePoolType.Interactive)
                .HasValue<JobNodePoolConfig>(NodePoolType.Job);
            entity.Property(e => e.PoolType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
        });

        modelBuilder.Entity<JobNodePoolConfig>(entity =>
        {
            entity.Property(e => e.WarmNodes).HasDefaultValue(0);
        });

        modelBuilder.Entity<JobRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Trigger).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.JobName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ParametersJson).HasColumnType("jsonb");
            entity.Property(e => e.SnapshotJson).HasColumnType("jsonb");
            entity.Ignore(e => e.Parameters);
            entity.HasOne(e => e.Job).WithMany().HasForeignKey(e => e.JobId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ParentRun).WithMany().HasForeignKey(e => e.ParentRunId).OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.TaskRuns).WithOne(tr => tr.JobRun).HasForeignKey(tr => tr.JobRunId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.JobId);
            entity.HasIndex(e => e.WorkspaceId);
        });

        modelBuilder.Entity<TaskRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.TaskName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Parameters).HasColumnType("jsonb").HasConversion(DictJsonConverter);
            entity.HasDiscriminator(e => e.TaskType)
                .HasValue<NotebookTaskRun>(TaskType.Notebook)
                .HasValue<SqlQueryTaskRun>(TaskType.SqlQuery)
                .HasValue<SubJobTaskRun>(TaskType.SubJob);
            entity.Property(e => e.TaskType).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.HasOne(e => e.Task).WithMany().HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
