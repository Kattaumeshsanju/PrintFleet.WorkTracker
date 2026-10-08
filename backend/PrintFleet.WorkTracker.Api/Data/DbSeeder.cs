using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Data;

public static class DbSeeder
{
    public const string DevPassword = "WorkTracker@2026";

    private static readonly Guid Lead = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid John = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid David = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid Rahul = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid Suresh = Guid.Parse("10000000-0000-0000-0000-000000000005");
    private static readonly Guid Anita = Guid.Parse("10000000-0000-0000-0000-000000000006");
    private static readonly Guid Meera = Guid.Parse("10000000-0000-0000-0000-000000000007");

    private static readonly Guid Hmi = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid Laser = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid Fleet = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly Guid Plc = Guid.Parse("20000000-0000-0000-0000-000000000004");
    private static readonly Guid Machine = Guid.Parse("20000000-0000-0000-0000-000000000005");
    private static readonly Guid Management = Guid.Parse("20000000-0000-0000-0000-000000000006");
    private static readonly Guid Support = Guid.Parse("20000000-0000-0000-0000-000000000007");

    public static async Task SeedAsync(AppDbContext db, bool allow, CancellationToken ct = default)
    {
        if (!allow || await db.Users.AnyAsync(ct)) return;

        var now = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var hash = BCrypt.Net.BCrypt.HashPassword(DevPassword);
        var sheets = new Dictionary<Guid, Guid>
        {
            [Lead] = SheetId(1),
            [John] = SheetId(2),
            [David] = SheetId(3),
            [Rahul] = SheetId(4),
            [Suresh] = SheetId(5),
            [Anita] = SheetId(6),
            [Meera] = SheetId(7)
        };

        db.Users.AddRange(
            User(Lead, "Team Lead", "lead@company.com", Roles.TeamLead, "Engineering Team Lead", hash, now),
            User(John, "John Mathew", "john@company.com", Roles.TeamMember, "Software Engineer", hash, now),
            User(David, "David Kumar", "david@company.com", Roles.TeamMember, "Software Engineer", hash, now),
            User(Rahul, "Rahul Sharma", "rahul@company.com", Roles.TeamMember, "Software Engineer", hash, now),
            User(Suresh, "Suresh Nair", "suresh@company.com", Roles.TeamMember, "Software Engineer", hash, now),
            User(Anita, "Anita Desai", "anita@company.com", Roles.TeamMember, "Software Engineer", hash, now),
            User(Meera, "Meera Iyer", "meera@company.com", Roles.TeamMember, "Software Engineer", hash, now));

        db.Projects.AddRange(
            Project(Hmi, "HMI", "HMI", "Machine HMI screens, settings and operator workflows.", now),
            Project(Laser, "Laser Scanner", "LSR", "Scanner preview, calibration and firmware handshake.", now),
            Project(Fleet, "PrintFleet", "PF", "Print fleet status, alarms and job queue.", now),
            Project(Plc, "PLC Integration", "PLC", "PLC tags, communication and diagnostics.", now),
            Project(Machine, "Machine Development", "MCH", "Motion, gas flow and machine sequences.", now),
            Project(Management, "Management", "MGT", "Planning, reviews and engineering coordination.", now),
            Project(Support, "Support", "SUP", "Customer issues and field support.", now));

        foreach (var pair in sheets)
        {
            db.MonthlyWorkSheets.Add(new MonthlyWorkSheet
            {
                Id = pair.Value,
                UserId = pair.Key,
                Year = 2026,
                Month = 10,
                Status = SheetStatuses.Open,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        var tasks = new List<MonthlyTask>();
        void Task(int n, Guid userId, Guid? projectId, string title, string? description, string priority, int start, int end, decimal hours, string status, string? remarks = null)
        {
            tasks.Add(new MonthlyTask
            {
                Id = TaskId(n),
                MonthlyWorkSheetId = sheets[userId],
                ProjectId = projectId,
                Title = title,
                Description = description,
                Priority = priority,
                PlannedStartDate = new DateOnly(2026, 10, start),
                PlannedEndDate = new DateOnly(2026, 10, end),
                EstimatedHours = hours,
                Status = status,
                Remarks = remarks,
                CreatedBy = userId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        Task(1, Lead, Hmi, "HMI Architecture", "Review the HMI module boundaries and settings flow.", Priorities.High, 1, 10, 40, TaskStatuses.InProgress);
        Task(2, Lead, Management, "Team Planning", "Plan October work and review daily progress with the team.", Priorities.High, 1, 31, 20, TaskStatuses.InProgress);
        Task(3, Lead, Hmi, "Code Review", "Review team changes on HMI and machine screens.", Priorities.Medium, 5, 20, 15, TaskStatuses.InProgress);
        Task(4, Lead, Plc, "PLC Integration Review", "Review tag mapping and communication error handling.", Priorities.High, 6, 18, 18, TaskStatuses.InProgress);
        Task(5, Lead, Support, "Customer Support", "Handle open customer questions on machine settings.", Priorities.Medium, 10, 15, 10, TaskStatuses.Planned);
        Task(6, Lead, Management, "October kickoff plan", "Publish the October engineering plan.", Priorities.Medium, 1, 2, 4, TaskStatuses.Completed, "Shared with the team on 2 Oct.");

        Task(7, John, Plc, "Implement PLC communication", "PLC tag mapping and connection lifecycle.", Priorities.High, 1, 7, 40, TaskStatuses.Completed, "Communication path is stable.");
        Task(8, John, Hmi, "Improve machine settings UI", "Settings screen layout and validation.", Priorities.Medium, 8, 15, 32, TaskStatuses.InProgress);
        Task(9, John, Hmi, "Testing and bug fixing", "Machine communication and settings regressions.", Priorities.High, 16, 25, 24, TaskStatuses.Planned);
        Task(10, John, Laser, "Laser scanner calibration support", "Support calibration values shown on the HMI.", Priorities.Medium, 6, 12, 16, TaskStatuses.InProgress);
        Task(11, John, Fleet, "PrintFleet status widget", "Compact fleet status on the main screen.", Priorities.Low, 20, 28, 12, TaskStatuses.Planned);

        Task(12, David, Laser, "Dual laser path planner", "Path planning for the dual laser head.", Priorities.High, 1, 14, 36, TaskStatuses.InProgress);
        Task(13, David, Hmi, "HMI alarm banners", "Operator alarm banners and acknowledgement.", Priorities.Medium, 5, 16, 20, TaskStatuses.InProgress);
        Task(14, David, Machine, "Machine homing sequence", "Homing order and interlocks.", Priorities.High, 8, 22, 24, TaskStatuses.Planned);
        Task(15, David, Fleet, "PrintFleet job queue", "Queue view for active print jobs.", Priorities.Medium, 15, 28, 16, TaskStatuses.Planned);
        Task(16, David, Hmi, "Legacy banner cleanup", "Remove the unused alarm banner.", Priorities.Low, 1, 3, 6, TaskStatuses.Completed);

        Task(17, Rahul, Hmi, "Settings screen layout", "Responsive layout for the settings pages.", Priorities.High, 1, 9, 28, TaskStatuses.InProgress);
        Task(18, Rahul, Fleet, "PrintFleet alarm panel", "Alarm list for fleet printers.", Priorities.High, 1, 6, 18, TaskStatuses.InProgress, "Still waiting on the alarm code list.");
        Task(19, Rahul, Laser, "Scanner preview pane", "Live preview for the scanner camera.", Priorities.Medium, 12, 24, 20, TaskStatuses.Planned);
        Task(20, Rahul, Hmi, "Bug triage", "Triage open HMI defects from the last build.", Priorities.Medium, 6, 18, 12, TaskStatuses.InProgress);
        Task(21, Rahul, Hmi, "Icon pass on settings", "Replace placeholder icons on settings.", Priorities.Low, 1, 2, 4, TaskStatuses.Completed);

        Task(22, Suresh, Machine, "Machine communication tests", "End-to-end tests for machine communication.", Priorities.High, 1, 15, 30, TaskStatuses.InProgress);
        Task(23, Suresh, Laser, "Scanner firmware handshake", "Complete the scanner firmware handshake.", Priorities.Critical, 3, 10, 16, TaskStatuses.Blocked, "Waiting for scanner vendor firmware.");
        Task(24, Suresh, Plc, "PLC tag verification", "Verify the released PLC tag list.", Priorities.High, 8, 20, 18, TaskStatuses.Planned);
        Task(25, Suresh, Hmi, "Regression checklist", "October regression checklist for HMI.", Priorities.Medium, 16, 30, 14, TaskStatuses.Planned);
        Task(26, Suresh, Machine, "Test jig setup", "Prepare the bench jig for communication tests.", Priorities.Medium, 1, 2, 6, TaskStatuses.Completed);

        Task(27, Anita, Hmi, "Build history filters", "Filter build history by date and status.", Priorities.Medium, 1, 12, 18, TaskStatuses.InProgress);
        Task(28, Anita, Machine, "Gas flow UI", "Gas flow setpoints and readback.", Priorities.High, 6, 17, 22, TaskStatuses.Blocked, "Mechanical drawing is not released.");
        Task(29, Anita, Support, "Support ticket review", "Review open field tickets for the HMI.", Priorities.Low, 1, 31, 10, TaskStatuses.InProgress);
        Task(30, Anita, Fleet, "PrintFleet labels", "Label text for fleet status states.", Priorities.Medium, 13, 27, 8, TaskStatuses.Planned);
        Task(31, Anita, Hmi, "Build history column picker", "Choose which build columns are visible.", Priorities.Low, 1, 3, 6, TaskStatuses.Completed);

        Task(32, Meera, Plc, "RTC status diagnostics", "Diagnostics for the RTC connection status.", Priorities.High, 1, 11, 24, TaskStatuses.InProgress);
        Task(33, Meera, Laser, "Multi-laser offset UI", "Offset entry for the multi-laser head.", Priorities.High, 7, 21, 22, TaskStatuses.InProgress);
        Task(34, Meera, Management, "Documentation pack", "Update the engineering notes for October.", Priorities.Low, 14, 29, 8, TaskStatuses.Planned);
        Task(35, Meera, Hmi, "Emission screen fixes", "Fix the emission screen layout issues.", Priorities.Medium, 2, 9, 12, TaskStatuses.InProgress);
        Task(36, Meera, Plc, "RTC reconnect notes", "Write the reconnect sequence notes.", Priorities.Low, 1, 2, 4, TaskStatuses.Completed);

        db.MonthlyTasks.AddRange(tasks);

        var entries = new List<DailyWorkEntry>();
        var submissions = new List<DailySubmission>();

        void Entry(Guid userId, DateOnly date, string start, string end, int taskNumber, string description, string status, string? remarks = null)
        {
            var startTime = TimeOnly.Parse(start);
            var endTime = TimeOnly.Parse(end);
            entries.Add(new DailyWorkEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MonthlyTaskId = TaskId(taskNumber),
                WorkDate = date,
                StartTime = startTime,
                EndTime = endTime,
                DurationMinutes = (int)(endTime - startTime).TotalMinutes,
                Description = description,
                Status = status,
                Remarks = remarks,
                CreatedBy = userId,
                CreatedAt = Stamp(date, startTime),
                UpdatedAt = Stamp(date, startTime)
            });
        }

        void Submit(Guid userId, DateOnly date)
        {
            submissions.Add(new DailySubmission
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                WorkDate = date,
                Status = SubmissionStatuses.Submitted,
                SubmittedAt = Stamp(date, new TimeOnly(17, 30)),
                SubmittedBy = userId
            });
        }

        var routine = new (Guid Id, int Morning, int Afternoon, string MorningName, string AfternoonName)[]
        {
            (Lead, 1, 2, "HMI architecture", "team planning"),
            (John, 7, 8, "PLC communication", "machine settings"),
            (David, 12, 13, "laser path planning", "alarm banners"),
            (Rahul, 17, 18, "settings layout", "PrintFleet alarms"),
            (Suresh, 22, 23, "machine communication tests", "scanner handshake"),
            (Anita, 27, 29, "build history filters", "support tickets"),
            (Meera, 32, 35, "RTC diagnostics", "emission screen")
        };

        foreach (var day in new[] { 1, 2, 5, 6, 7 })
        {
            var date = new DateOnly(2026, 10, day);
            foreach (var person in routine)
            {
                if (person.Id == Anita && day == 7)
                {
                    Entry(person.Id, date, "09:00", "12:00", person.Morning, "Short session on build history filters before a site call.", EntryStatuses.InProgress, "Left early for a customer call.");
                    Submit(person.Id, date);
                    continue;
                }

                Entry(person.Id, date, "09:00", "10:30", person.Morning, $"Worked on {person.MorningName}.", EntryStatuses.Completed);
                Entry(person.Id, date, "10:45", "12:30", person.Morning, $"Follow-up fixes for {person.MorningName}.", EntryStatuses.Completed);
                Entry(person.Id, date, "13:30", "15:30", person.Afternoon, $"Integration work on {person.AfternoonName}.", day == 7 ? EntryStatuses.InProgress : EntryStatuses.Completed);
                Entry(person.Id, date, "15:45", "17:15", person.Afternoon, $"Verification for {person.AfternoonName}.", EntryStatuses.InProgress);
                Submit(person.Id, date);
            }
        }

        var today = new DateOnly(2026, 10, 8);
        Entry(Lead, today, "09:00", "10:00", 1, "Reviewed HMI architecture", EntryStatuses.Completed);
        Entry(Lead, today, "10:00", "11:00", 2, "Discussed tasks with the team", EntryStatuses.Completed);
        Entry(Lead, today, "11:15", "13:00", 4, "Reviewed PLC integration", EntryStatuses.InProgress);
        Entry(Lead, today, "14:00", "15:30", 3, "Reviewed team code", EntryStatuses.Completed);
        Entry(Lead, today, "15:30", "17:00", 1, "Implemented UI improvements", EntryStatuses.InProgress);
        Submit(Lead, today);

        Entry(John, today, "09:00", "10:30", 7, "Worked on PLC tag mapping", EntryStatuses.Completed);
        Entry(John, today, "10:30", "11:00", 9, "Debugged communication issue", EntryStatuses.Completed);
        Entry(John, today, "11:15", "13:00", 8, "Developed settings screen", EntryStatuses.InProgress);
        Entry(John, today, "14:00", "16:30", 9, "Tested machine communication", EntryStatuses.InProgress);
        Submit(John, today);

        Entry(David, today, "09:00", "11:00", 12, "Continued dual laser path planning", EntryStatuses.InProgress);
        Entry(David, today, "11:00", "13:00", 13, "Adjusted HMI alarm banner layout", EntryStatuses.InProgress);
        Entry(David, today, "14:00", "16:30", 14, "Drafted the machine homing sequence", EntryStatuses.InProgress);
        Submit(David, today);

        Entry(Suresh, today, "09:00", "12:00", 22, "Ran machine communication tests", EntryStatuses.InProgress);
        Entry(Suresh, today, "13:00", "15:00", 23, "Tried the scanner firmware handshake", EntryStatuses.Blocked, "Vendor firmware still missing.");
        Entry(Suresh, today, "15:15", "17:00", 24, "Started PLC tag verification", EntryStatuses.InProgress);
        Submit(Suresh, today);

        Entry(Meera, today, "09:15", "11:30", 32, "Checked RTC status diagnostics", EntryStatuses.InProgress);
        Entry(Meera, today, "11:30", "13:00", 35, "Fixed emission screen spacing", EntryStatuses.Completed);
        Entry(Meera, today, "14:00", "17:00", 33, "Built the multi-laser offset form", EntryStatuses.InProgress);
        Submit(Meera, today);

        db.DailyWorkEntries.AddRange(entries);
        db.DailySubmissions.AddRange(submissions);
        db.AuditLogs.Add(new AuditLog
        {
            Action = "Seeded",
            EntityType = "Database",
            Details = "Loaded the October 2026 sample team, projects, monthly plans and daily entries.",
            CreatedAt = now,
            UserName = "System"
        });
        await db.SaveChangesAsync(ct);
    }

    private static Guid SheetId(int n) => Guid.Parse($"40000000-0000-0000-0000-{n:D12}");
    private static Guid TaskId(int n) => Guid.Parse($"30000000-0000-0000-0000-{n:D12}");

    private static DateTime Stamp(DateOnly date, TimeOnly time)
        => DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Utc);

    private static User User(Guid id, string name, string email, string role, string title, string hash, DateTime created) => new()
    {
        Id = id,
        Name = name,
        Email = email,
        PasswordHash = hash,
        Role = role,
        JobTitle = title,
        IsActive = true,
        CreatedAt = created
    };

    private static Project Project(Guid id, string name, string code, string description, DateTime created) => new()
    {
        Id = id,
        Name = name,
        Code = code,
        Description = description,
        StartDate = new DateOnly(2026, 1, 6),
        EndDate = new DateOnly(2026, 12, 31),
        Status = ProjectStatuses.Active,
        CreatedAt = created,
        UpdatedAt = created
    };
}
