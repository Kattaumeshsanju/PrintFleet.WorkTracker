using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<MonthlyWorkSheet> MonthlyWorkSheets => Set<MonthlyWorkSheet>();
    public DbSet<MonthlyTask> MonthlyTasks => Set<MonthlyTask>();
    public DbSet<DailyWorkEntry> DailyWorkEntries => Set<DailyWorkEntry>();
    public DbSet<DailySubmission> DailySubmissions => Set<DailySubmission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var department = modelBuilder.Entity<Department>();
        department.HasIndex(x => x.Name).IsUnique();
        department.Property(x => x.Name).HasMaxLength(120).IsRequired();

        var user = modelBuilder.Entity<User>();
        user.HasIndex(x => x.Email).IsUnique();
        user.HasIndex(x => x.DepartmentId);
        user.HasOne(x => x.Department).WithMany(x => x.Users).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        user.Property(x => x.Name).HasMaxLength(120).IsRequired();
        user.Property(x => x.Email).HasMaxLength(200).IsRequired();
        user.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
        user.Property(x => x.Role).HasMaxLength(30).IsRequired();
        user.Property(x => x.JobTitle).HasMaxLength(120);

        var project = modelBuilder.Entity<Project>();
        project.HasIndex(x => x.Code).IsUnique();
        project.HasIndex(x => x.DepartmentId);
        project.HasOne(x => x.Department).WithMany(x => x.Projects).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        project.Property(x => x.Name).HasMaxLength(160).IsRequired();
        project.Property(x => x.Code).HasMaxLength(20).IsRequired();
        project.Property(x => x.Description).HasMaxLength(2000);
        project.Property(x => x.Status).HasMaxLength(30).IsRequired();

        var sheet = modelBuilder.Entity<MonthlyWorkSheet>();
        sheet.HasIndex(x => new { x.UserId, x.Year, x.Month }).IsUnique();
        sheet.HasIndex(x => x.UserId);
        sheet.HasIndex(x => new { x.Year, x.Month });
        sheet.Property(x => x.Status).HasMaxLength(20).IsRequired();
        sheet.HasOne(x => x.User).WithMany(x => x.Sheets).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        var task = modelBuilder.Entity<MonthlyTask>();
        task.HasIndex(x => x.MonthlyWorkSheetId);
        task.HasIndex(x => x.ProjectId);
        task.HasIndex(x => x.Status);
        task.Property(x => x.Title).HasMaxLength(200).IsRequired();
        task.Property(x => x.Description).HasMaxLength(4000);
        task.Property(x => x.Priority).HasMaxLength(20).IsRequired();
        task.Property(x => x.Status).HasMaxLength(30).IsRequired();
        task.Property(x => x.Remarks).HasMaxLength(2000);
        task.Property(x => x.EstimatedHours).HasPrecision(8, 2);
        task.HasOne(x => x.Sheet).WithMany(x => x.Tasks).HasForeignKey(x => x.MonthlyWorkSheetId).OnDelete(DeleteBehavior.Cascade);
        task.HasOne(x => x.Project).WithMany(x => x.Tasks).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.SetNull);

        var entry = modelBuilder.Entity<DailyWorkEntry>();
        entry.HasIndex(x => x.UserId);
        entry.HasIndex(x => x.WorkDate);
        entry.HasIndex(x => new { x.UserId, x.WorkDate });
        entry.HasIndex(x => x.MonthlyTaskId);
        entry.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        entry.Property(x => x.Status).HasMaxLength(30).IsRequired();
        entry.Property(x => x.Remarks).HasMaxLength(2000);
        entry.HasOne(x => x.User).WithMany(x => x.DailyEntries).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        entry.HasOne(x => x.MonthlyTask).WithMany(x => x.DailyEntries).HasForeignKey(x => x.MonthlyTaskId).OnDelete(DeleteBehavior.Restrict);

        var submission = modelBuilder.Entity<DailySubmission>();
        submission.HasIndex(x => new { x.UserId, x.WorkDate }).IsUnique();
        submission.HasIndex(x => x.WorkDate);
        submission.Property(x => x.Status).HasMaxLength(20).IsRequired();
        submission.Property(x => x.DayType).HasMaxLength(20);
        submission.HasOne(x => x.User).WithMany(x => x.Submissions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        var audit = modelBuilder.Entity<AuditLog>();
        audit.HasIndex(x => x.CreatedAt);
        audit.HasIndex(x => new { x.EntityType, x.EntityId });
        audit.Property(x => x.Action).HasMaxLength(40).IsRequired();
        audit.Property(x => x.EntityType).HasMaxLength(60).IsRequired();
        audit.Property(x => x.UserName).HasMaxLength(120);
        audit.Property(x => x.Details).HasMaxLength(2000);
    }
}
