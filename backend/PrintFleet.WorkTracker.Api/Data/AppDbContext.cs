using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<DepartmentMonthlyPlan> DepartmentMonthlyPlans => Set<DepartmentMonthlyPlan>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<MonthlyWorkSheet> MonthlyWorkSheets => Set<MonthlyWorkSheet>();
    public DbSet<MonthlyTask> MonthlyTasks => Set<MonthlyTask>();
    public DbSet<DailyWorkEntry> DailyWorkEntries => Set<DailyWorkEntry>();
    public DbSet<DailySubmission> DailySubmissions => Set<DailySubmission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();
    public DbSet<TeamNotice> TeamNotices => Set<TeamNotice>();
    public DbSet<TeamNoticeRecipient> TeamNoticeRecipients => Set<TeamNoticeRecipient>();
    public DbSet<EmailDelivery> EmailDeliveries => Set<EmailDelivery>();
    public DbSet<PlanReminderSettings> PlanReminderSettings => Set<PlanReminderSettings>();
    public DbSet<PlanReminderRun> PlanReminderRuns => Set<PlanReminderRun>();
    public DbSet<PlanReminderDelivery> PlanReminderDeliveries => Set<PlanReminderDelivery>();
    public DbSet<WorkCategory> WorkCategories => Set<WorkCategory>();
    public DbSet<Initiative> Initiatives => Set<Initiative>();
    public DbSet<InitiativeCollaborator> InitiativeCollaborators => Set<InitiativeCollaborator>();
    public DbSet<InitiativeMilestone> InitiativeMilestones => Set<InitiativeMilestone>();
    public DbSet<ResearchRecord> ResearchRecords => Set<ResearchRecord>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<CeoInvitation> CeoInvitations => Set<CeoInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var department = modelBuilder.Entity<Department>();
        department.HasIndex(x => x.Name).IsUnique();
        department.Property(x => x.Name).HasMaxLength(120).IsRequired();
        department.Property(x => x.IsActive).HasDefaultValue(true);

        var user = modelBuilder.Entity<User>();
        user.HasIndex(x => x.Email).IsUnique();
        user.HasIndex(x => x.DepartmentId);
        user.HasOne(x => x.Department).WithMany(x => x.Users).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        user.Property(x => x.Name).HasMaxLength(120).IsRequired();
        user.Property(x => x.Email).HasMaxLength(200).IsRequired();
        user.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
        user.Property(x => x.Role).HasMaxLength(30).IsRequired();
        user.Property(x => x.JobTitle).HasMaxLength(120);

        var permission = modelBuilder.Entity<UserPermission>();
        permission.HasKey(x => new { x.UserId, x.Permission });
        permission.Property(x => x.Permission).HasMaxLength(40).IsRequired();
        permission.HasOne(x => x.User).WithMany(x => x.Permissions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        var ceoInvite = modelBuilder.Entity<CeoInvitation>();
        ceoInvite.HasIndex(x => x.TokenHash).IsUnique();
        ceoInvite.HasIndex(x => new { x.Email, x.Status });
        ceoInvite.Property(x => x.Name).HasMaxLength(120).IsRequired();
        ceoInvite.Property(x => x.Email).HasMaxLength(200).IsRequired();
        ceoInvite.Property(x => x.JobTitle).HasMaxLength(120);
        ceoInvite.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        ceoInvite.Property(x => x.Status).HasMaxLength(20).IsRequired();

        var project = modelBuilder.Entity<Project>();
        project.HasIndex(x => x.Code).IsUnique();
        project.HasIndex(x => x.DepartmentId);
        project.HasOne(x => x.Department).WithMany(x => x.Projects).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        project.Property(x => x.Name).HasMaxLength(160).IsRequired();
        project.Property(x => x.Code).HasMaxLength(20).IsRequired();
        project.Property(x => x.Description).HasMaxLength(2000);
        project.Property(x => x.Status).HasMaxLength(30).IsRequired();
        project.Property(x => x.Priority).HasMaxLength(20).IsRequired().HasDefaultValue(Priorities.Medium);
        project.HasIndex(x => x.OwnerId);
        project.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.SetNull);
        project.HasOne(x => x.WorkCategory).WithMany().HasForeignKey(x => x.WorkCategoryId).OnDelete(DeleteBehavior.Restrict);

        var category = modelBuilder.Entity<WorkCategory>();
        category.HasIndex(x => new { x.DepartmentId, x.NameKey }).HasFilter("\"IsActive\" = true").IsUnique();
        category.Property(x => x.Name).HasMaxLength(120).IsRequired();
        category.Property(x => x.NameKey).HasMaxLength(120).IsRequired();
        category.Property(x => x.Description).HasMaxLength(1000);
        category.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        var initiative = modelBuilder.Entity<Initiative>();
        initiative.HasIndex(x => x.Number).IsUnique();
        initiative.HasIndex(x => new { x.DepartmentId, x.Status });
        initiative.Property(x => x.Number).HasMaxLength(20).IsRequired();
        initiative.Property(x => x.Title).HasMaxLength(200).IsRequired();
        initiative.Property(x => x.Description).HasMaxLength(4000);
        initiative.Property(x => x.Status).HasMaxLength(30).IsRequired();
        initiative.Property(x => x.Priority).HasMaxLength(20).IsRequired();
        initiative.Property(x => x.Remarks).HasMaxLength(4000);
        initiative.Property(x => x.ResearchObjective).HasMaxLength(4000);
        initiative.Property(x => x.Hypothesis).HasMaxLength(4000);
        initiative.Property(x => x.ResearchStage).HasMaxLength(30);
        initiative.Property(x => x.SuccessCriteria).HasMaxLength(4000);
        initiative.Property(x => x.ExperimentPlan).HasMaxLength(4000);
        initiative.Property(x => x.Conclusion).HasMaxLength(4000);
        initiative.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        initiative.HasOne(x => x.WorkCategory).WithMany().HasForeignKey(x => x.WorkCategoryId).OnDelete(DeleteBehavior.Restrict);
        initiative.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        initiative.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);

        var collaborator = modelBuilder.Entity<InitiativeCollaborator>();
        collaborator.HasKey(x => new { x.InitiativeId, x.UserId });
        collaborator.HasOne(x => x.Initiative).WithMany(x => x.Collaborators).HasForeignKey(x => x.InitiativeId).OnDelete(DeleteBehavior.Cascade);
        collaborator.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        var milestone = modelBuilder.Entity<InitiativeMilestone>();
        milestone.Property(x => x.Title).HasMaxLength(200).IsRequired();
        milestone.Property(x => x.Description).HasMaxLength(2000);
        milestone.Property(x => x.Status).HasMaxLength(30).IsRequired();
        milestone.HasOne(x => x.Initiative).WithMany(x => x.Milestones).HasForeignKey(x => x.InitiativeId).OnDelete(DeleteBehavior.Cascade);
        milestone.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);

        var research = modelBuilder.Entity<ResearchRecord>();
        research.HasIndex(x => new { x.InitiativeId, x.RecordedOn });
        research.Property(x => x.Title).HasMaxLength(200).IsRequired();
        research.Property(x => x.Objective).HasMaxLength(4000);
        research.Property(x => x.Method).HasMaxLength(4000);
        research.Property(x => x.Findings).HasMaxLength(4000);
        research.Property(x => x.Outcome).HasMaxLength(4000);
        research.Property(x => x.EvidenceLinks).HasMaxLength(2000);
        research.Property(x => x.NextSteps).HasMaxLength(4000);
        research.HasOne(x => x.Initiative).WithMany(x => x.Records).HasForeignKey(x => x.InitiativeId).OnDelete(DeleteBehavior.Cascade);

        var departmentPlan = modelBuilder.Entity<DepartmentMonthlyPlan>();
        departmentPlan.HasIndex(x => new { x.DepartmentId, x.Year, x.Month }).IsUnique();
        departmentPlan.Property(x => x.Status).HasMaxLength(20).IsRequired();
        departmentPlan.Property(x => x.Summary).HasMaxLength(4000);
        departmentPlan.Property(x => x.Risks).HasMaxLength(4000);
        departmentPlan.Property(x => x.Comments).HasMaxLength(4000);
        departmentPlan.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        var sheet = modelBuilder.Entity<MonthlyWorkSheet>();
        sheet.HasIndex(x => new { x.UserId, x.Year, x.Month }).IsUnique();
        sheet.HasIndex(x => x.UserId);
        sheet.HasIndex(x => new { x.Year, x.Month });
        sheet.Property(x => x.Status).HasMaxLength(20).IsRequired();
        sheet.HasOne(x => x.User).WithMany(x => x.Sheets).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        sheet.HasIndex(x => x.DepartmentId);
        sheet.HasIndex(x => new { x.DepartmentId, x.Year, x.Month });
        sheet.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        sheet.HasIndex(x => x.DepartmentPlanId);
        sheet.HasOne(x => x.DepartmentPlan).WithMany(x => x.Sheets).HasForeignKey(x => x.DepartmentPlanId).OnDelete(DeleteBehavior.SetNull);

        var task = modelBuilder.Entity<MonthlyTask>();
        task.HasIndex(x => x.MonthlyWorkSheetId);
        task.HasIndex(x => x.ProjectId);
        task.HasIndex(x => x.WorkCategoryId);
        task.HasIndex(x => x.InitiativeId);
        task.HasIndex(x => x.Status);
        task.HasIndex(x => new { x.Status, x.PlannedEndDate });
        task.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        task.Property(x => x.Title).HasMaxLength(200).IsRequired();
        task.Property(x => x.Description).HasMaxLength(4000);
        task.Property(x => x.Priority).HasMaxLength(20).IsRequired();
        task.Property(x => x.Status).HasMaxLength(30).IsRequired();
        task.Property(x => x.Remarks).HasMaxLength(2000);
        task.Property(x => x.EstimatedHours).HasPrecision(8, 2);
        task.Property(x => x.ProgressPercent).HasDefaultValue(0);
        task.HasOne(x => x.Sheet).WithMany(x => x.Tasks).HasForeignKey(x => x.MonthlyWorkSheetId).OnDelete(DeleteBehavior.Cascade);
        task.HasOne(x => x.Project).WithMany(x => x.Tasks).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.SetNull);
        task.HasOne(x => x.WorkCategory).WithMany().HasForeignKey(x => x.WorkCategoryId).OnDelete(DeleteBehavior.Restrict);
        task.HasOne(x => x.Initiative).WithMany().HasForeignKey(x => x.InitiativeId).OnDelete(DeleteBehavior.Restrict);

        var entry = modelBuilder.Entity<DailyWorkEntry>();
        entry.HasIndex(x => x.UserId);
        entry.HasIndex(x => x.WorkDate);
        entry.HasIndex(x => new { x.UserId, x.WorkDate });
        entry.HasIndex(x => new { x.UserId, x.WorkDate, x.StartTime });
        entry.HasIndex(x => x.MonthlyTaskId);
        entry.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
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

        var invite = modelBuilder.Entity<TeamInvitation>();
        invite.HasIndex(x => x.TokenHash).IsUnique();
        invite.HasIndex(x => new { x.Email, x.Status });
        invite.Property(x => x.Name).HasMaxLength(120).IsRequired();
        invite.Property(x => x.Email).HasMaxLength(200).IsRequired();
        invite.Property(x => x.JobTitle).HasMaxLength(120);
        invite.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        invite.Property(x => x.Status).HasMaxLength(20).IsRequired();
        invite.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        var notice = modelBuilder.Entity<TeamNotice>();
        notice.HasIndex(x => new { x.DepartmentId, x.CreatedAt });
        notice.HasIndex(x => new { x.SenderId, x.CreatedAt });
        notice.Property(x => x.Title).HasMaxLength(160).IsRequired();
        notice.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        notice.Property(x => x.Category).HasMaxLength(30).IsRequired();
        notice.Property(x => x.Channel).HasMaxLength(20).IsRequired();
        notice.Property(x => x.LinkPath).HasMaxLength(200);
        notice.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        notice.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        notice.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);

        var recipient = modelBuilder.Entity<TeamNoticeRecipient>();
        recipient.HasIndex(x => new { x.UserId, x.ReadAt });
        recipient.HasIndex(x => new { x.NoticeId, x.UserId }).IsUnique();
        recipient.Property(x => x.Email).HasMaxLength(200).IsRequired();
        recipient.HasOne(x => x.Notice).WithMany(x => x.Recipients).HasForeignKey(x => x.NoticeId).OnDelete(DeleteBehavior.Cascade);
        recipient.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        var delivery = modelBuilder.Entity<EmailDelivery>();
        delivery.HasIndex(x => x.RecipientId).IsUnique();
        delivery.HasIndex(x => new { x.Status, x.NextAttemptAt });
        delivery.Property(x => x.Status).HasMaxLength(30).IsRequired();
        delivery.Property(x => x.ErrorType).HasMaxLength(80);
        delivery.HasOne(x => x.Recipient).WithOne(x => x.Delivery).HasForeignKey<EmailDelivery>(x => x.RecipientId).OnDelete(DeleteBehavior.Cascade);

        var audit = modelBuilder.Entity<AuditLog>();
        audit.HasIndex(x => x.CreatedAt);
        audit.HasIndex(x => new { x.EntityType, x.EntityId });
        audit.Property(x => x.Action).HasMaxLength(40).IsRequired();
        audit.Property(x => x.EntityType).HasMaxLength(60).IsRequired();
        audit.Property(x => x.UserName).HasMaxLength(120);
        audit.Property(x => x.Details).HasMaxLength(2000);

        var reminderSettings = modelBuilder.Entity<PlanReminderSettings>();
        reminderSettings.Property(x => x.Weekdays).HasMaxLength(32).IsRequired();

        var reminderRun = modelBuilder.Entity<PlanReminderRun>();
        reminderRun.HasIndex(x => x.ScheduledDate).IsUnique();
        reminderRun.Property(x => x.Status).HasMaxLength(20).IsRequired();
        reminderRun.Property(x => x.FailureDetail).HasMaxLength(200);

        var planDelivery = modelBuilder.Entity<PlanReminderDelivery>();
        planDelivery.HasIndex(x => x.IdempotencyKey).IsUnique();
        planDelivery.HasIndex(x => new { x.ScheduledDate, x.Status });
        planDelivery.Property(x => x.DepartmentName).HasMaxLength(120).IsRequired();
        planDelivery.Property(x => x.RecipientEmail).HasMaxLength(200).IsRequired();
        planDelivery.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        planDelivery.Property(x => x.MessageType).HasMaxLength(40).IsRequired();
        planDelivery.Property(x => x.Status).HasMaxLength(20).IsRequired();
        planDelivery.Property(x => x.FailureDetail).HasMaxLength(200);
        planDelivery.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
    }
}
