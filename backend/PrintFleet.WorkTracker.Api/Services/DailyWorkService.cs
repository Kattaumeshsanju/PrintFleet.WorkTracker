using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IDailyWorkService
{
    Task<PagedResult<DailyEntryDto>> ListAsync(DailyWorkQuery query, CancellationToken ct);
    Task<DayViewDto> GetDayAsync(DateOnly date, Guid? userId, CancellationToken ct);
    Task<DailyEntryDto> CreateAsync(SaveDailyEntryRequest request, CancellationToken ct);
    Task<DailyEntryDto> UpdateAsync(Guid id, SaveDailyEntryRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<DayViewDto> SubmitAsync(DateOnly date, CancellationToken ct);
    Task<DayViewDto> SetDayMarkAsync(MarkDayRequest request, CancellationToken ct);
    Task<DayViewDto> ReopenAsync(ReopenDayRequest request, CancellationToken ct);
    Task<CalendarDto> GetCalendarAsync(int year, int month, Guid? userId, CancellationToken ct);
    Task<MonthTimelineDto> GetMonthTimelineAsync(int year, int month, Guid? userId, CancellationToken ct);
}

public class DailyWorkService : IDailyWorkService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;
    private readonly IDepartmentAccess _access;

    public DailyWorkService(AppDbContext db, ICurrentUser current, IAuditService audit, IDepartmentAccess access)
    {
        _db = db;
        _current = current;
        _audit = audit;
        _access = access;
    }

    public async Task<PagedResult<DailyEntryDto>> ListAsync(DailyWorkQuery query, CancellationToken ct)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 50 : query.PageSize;
        var target = ResolveOptionalUser(query.UserId);

        var entries = BaseQuery();
        if (target is Guid userId) entries = entries.Where(x => x.UserId == userId);
        if (query.WorkDate is DateOnly day) entries = entries.Where(x => x.WorkDate == day);
        if (query.From is DateOnly from) entries = entries.Where(x => x.WorkDate >= from);
        if (query.To is DateOnly to) entries = entries.Where(x => x.WorkDate <= to);
        if (query.WorkDate is null && query.From is null && query.To is null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var start = new DateOnly(today.Year, today.Month, 1);
            entries = entries.Where(x => x.WorkDate >= start && x.WorkDate < start.AddMonths(1));
        }
        if (query.ProjectId is Guid projectId) entries = entries.Where(x => x.MonthlyTask.ProjectId == projectId);
        if (query.TaskId is Guid taskId) entries = entries.Where(x => x.MonthlyTaskId == taskId);
        if (!string.IsNullOrWhiteSpace(query.Status)) entries = entries.Where(x => x.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            entries = entries.Where(x =>
                x.Description.ToLower().Contains(term) ||
                x.MonthlyTask.Title.ToLower().Contains(term) ||
                x.User.Name.ToLower().Contains(term) ||
                (x.MonthlyTask.Project != null && x.MonthlyTask.Project.Name.ToLower().Contains(term)));
        }

        var total = await entries.CountAsync(ct);
        var items = await entries.OrderByDescending(x => x.WorkDate).ThenBy(x => x.StartTime)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<DailyEntryDto>
        {
            Items = items.Select(MapEntry).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<DayViewDto> GetDayAsync(DateOnly date, Guid? userId, CancellationToken ct)
    {
        var target = await ResolveUserAsync(userId, ct);
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == target, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);

        var entries = await BaseQuery().Where(x => x.UserId == target && x.WorkDate == date)
            .OrderBy(x => x.StartTime).ToListAsync(ct);
        var submission = await _db.DailySubmissions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == target && x.WorkDate == date, ct);
        var locked = await IsMonthLocked(target, date, ct);
        var submitted = submission?.Status == SubmissionStatuses.Submitted;

        return new DayViewDto
        {
            Date = date,
            UserId = user.Id,
            UserName = user.Name,
            JobTitle = user.JobTitle,
            TotalMinutes = entries.Sum(x => x.DurationMinutes),
            SubmissionStatus = submission?.Status ?? SubmissionStatuses.Pending,
            SubmittedAt = submission?.SubmittedAt,
            MonthLocked = locked,
            CanEdit = target == _current.UserId && !locked && !submitted,
            DayType = submission?.DayType,
            Entries = entries.Select(MapEntry).ToList()
        };
    }

    public async Task<DailyEntryDto> CreateAsync(SaveDailyEntryRequest request, CancellationToken ct)
    {
        var userId = _current.UserId;
        await EnsureEditable(userId, request.WorkDate, ct);
        var task = await LoadOwnedTask(request.MonthlyTaskId, userId, request.WorkDate, ct);
        var (start, end, minutes) = ReadTimes(request);
        await EnsureNoOverlap(userId, request.WorkDate, start, end, null, request.AllowOverlap, ct);

        var now = DateTime.UtcNow;
        var entry = new DailyWorkEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MonthlyTaskId = task.Id,
            WorkDate = request.WorkDate,
            StartTime = start,
            EndTime = end,
            DurationMinutes = minutes,
            Description = request.Description.Trim(),
            Status = request.Status,
            Remarks = WorkMetrics.Clean(request.Remarks),
            CreatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.DailyWorkEntries.Add(entry);
        _audit.Stage(AuditActions.Created, "DailyWorkEntry", entry.Id, $"{request.WorkDate:yyyy-MM-dd} {WorkMetrics.FormatTime(start)}-{WorkMetrics.FormatTime(end)} {entry.Description}");
        await _db.SaveChangesAsync(ct);
        return await LoadDto(entry.Id, ct);
    }

    public async Task<DailyEntryDto> UpdateAsync(Guid id, SaveDailyEntryRequest request, CancellationToken ct)
    {
        var entry = await _db.DailyWorkEntries.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Work entry was not found.", StatusCodes.Status404NotFound);
        if (entry.UserId != _current.UserId)
            throw new AppException("You can only edit your own work entries.", StatusCodes.Status403Forbidden);

        await EnsureEditable(entry.UserId, entry.WorkDate, ct);
        if (request.WorkDate != entry.WorkDate)
            await EnsureEditable(entry.UserId, request.WorkDate, ct);

        var task = await LoadOwnedTask(request.MonthlyTaskId, entry.UserId, request.WorkDate, ct);
        var (start, end, minutes) = ReadTimes(request);
        await EnsureNoOverlap(entry.UserId, request.WorkDate, start, end, entry.Id, request.AllowOverlap, ct);

        entry.MonthlyTaskId = task.Id;
        entry.WorkDate = request.WorkDate;
        entry.StartTime = start;
        entry.EndTime = end;
        entry.DurationMinutes = minutes;
        entry.Description = request.Description.Trim();
        entry.Status = request.Status;
        entry.Remarks = WorkMetrics.Clean(request.Remarks);
        entry.UpdatedBy = _current.UserId;
        entry.UpdatedAt = DateTime.UtcNow;
        _audit.Stage(AuditActions.Updated, "DailyWorkEntry", entry.Id, $"{entry.WorkDate:yyyy-MM-dd} {entry.Description}");
        await _db.SaveChangesAsync(ct);
        return await LoadDto(entry.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entry = await _db.DailyWorkEntries.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Work entry was not found.", StatusCodes.Status404NotFound);
        if (entry.UserId != _current.UserId)
            throw new AppException("You can only delete your own work entries.", StatusCodes.Status403Forbidden);

        await EnsureEditable(entry.UserId, entry.WorkDate, ct);
        _db.DailyWorkEntries.Remove(entry);
        _audit.Stage(AuditActions.Deleted, "DailyWorkEntry", entry.Id, $"{entry.WorkDate:yyyy-MM-dd} {entry.Description}");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<DayViewDto> SubmitAsync(DateOnly date, CancellationToken ct)
    {
        var userId = _current.UserId;
        await EnsureMonthOpen(userId, date, ct);
        var count = await _db.DailyWorkEntries.CountAsync(x => x.UserId == userId && x.WorkDate == date, ct);
        if (count == 0) throw new AppException("Add at least one work entry before submitting.");

        var submission = await _db.DailySubmissions.FirstOrDefaultAsync(x => x.UserId == userId && x.WorkDate == date, ct);
        if (submission?.Status == SubmissionStatuses.Submitted)
            throw new AppException("This daily update is already submitted.");

        if (submission is null)
        {
            submission = new DailySubmission { Id = Guid.NewGuid(), UserId = userId, WorkDate = date };
            _db.DailySubmissions.Add(submission);
        }

        submission.Status = SubmissionStatuses.Submitted;
        submission.SubmittedAt = DateTime.UtcNow;
        submission.SubmittedBy = userId;
        _audit.Stage(AuditActions.Submitted, "DailySubmission", submission.Id, $"Submitted {date:yyyy-MM-dd}");
        await _db.SaveChangesAsync(ct);
        return await GetDayAsync(date, userId, ct);
    }

    public async Task<DayViewDto> SetDayMarkAsync(MarkDayRequest request, CancellationToken ct)
    {
        var userId = _current.UserId;
        await EnsureMonthOpen(userId, request.WorkDate, ct);
        var submission = await _db.DailySubmissions.FirstOrDefaultAsync(x => x.UserId == userId && x.WorkDate == request.WorkDate, ct);
        if (submission?.Status == SubmissionStatuses.Submitted)
            throw new AppException("This day is submitted. The Team Lead must reopen it before the mark can change.");

        var dayType = string.IsNullOrWhiteSpace(request.DayType) ? null : request.DayType;
        if (dayType is null)
        {
            if (submission is not null)
            {
                if (submission.SubmittedAt is null)
                    _db.DailySubmissions.Remove(submission);
                else
                    submission.DayType = null;
                _audit.Stage(AuditActions.Updated, "DailySubmission", submission.Id, $"Cleared the mark on {request.WorkDate:yyyy-MM-dd}");
            }
        }
        else
        {
            if (submission is null)
            {
                submission = new DailySubmission
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    WorkDate = request.WorkDate,
                    Status = SubmissionStatuses.Pending
                };
                _db.DailySubmissions.Add(submission);
            }

            submission.DayType = dayType;
            _audit.Stage(AuditActions.Updated, "DailySubmission", submission.Id, $"Marked {request.WorkDate:yyyy-MM-dd} as {dayType}");
        }

        await _db.SaveChangesAsync(ct);
        return await GetDayAsync(request.WorkDate, userId, ct);
    }

    public async Task<DayViewDto> ReopenAsync(ReopenDayRequest request, CancellationToken ct)
    {
        _current.EnsureLead();
        var submission = await _db.DailySubmissions.FirstOrDefaultAsync(x => x.UserId == request.UserId && x.WorkDate == request.WorkDate, ct);
        if (submission is null || submission.Status != SubmissionStatuses.Submitted)
            throw new AppException("That day is not submitted.");

        await EnsureMonthOpen(request.UserId, request.WorkDate, ct);
        submission.Status = SubmissionStatuses.Pending;
        submission.ReopenedAt = DateTime.UtcNow;
        submission.ReopenedBy = _current.UserId;
        _audit.Stage(AuditActions.Reopened, "DailySubmission", submission.Id, $"Reopened {request.WorkDate:yyyy-MM-dd}");
        await _db.SaveChangesAsync(ct);
        return await GetDayAsync(request.WorkDate, request.UserId, ct);
    }

    public async Task<CalendarDto> GetCalendarAsync(int year, int month, Guid? userId, CancellationToken ct)
    {
        if (month is < 1 or > 12) throw new AppException("Month must be between 1 and 12.");
        var target = await ResolveUserAsync(userId, ct);
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == target, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);

        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var totals = await _db.DailyWorkEntries.AsNoTracking()
            .Where(x => x.UserId == target && x.WorkDate >= start && x.WorkDate < end)
            .GroupBy(x => x.WorkDate)
            .Select(g => new { Date = g.Key, Minutes = g.Sum(x => x.DurationMinutes), Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => (x.Minutes, x.Count), ct);
        var submitted = await _db.DailySubmissions.AsNoTracking()
            .Where(x => x.UserId == target && x.WorkDate >= start && x.WorkDate < end && x.Status == SubmissionStatuses.Submitted)
            .Select(x => x.WorkDate)
            .ToListAsync(ct);
        var submittedSet = submitted.ToHashSet();
        var marks = await _db.DailySubmissions.AsNoTracking()
            .Where(x => x.UserId == target && x.WorkDate >= start && x.WorkDate < end && x.DayType != null)
            .ToDictionaryAsync(x => x.WorkDate, x => x.DayType!, ct);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var days = new List<CalendarDayDto>();
        for (var date = start; date < end; date = date.AddDays(1))
        {
            totals.TryGetValue(date, out var info);
            var weekend = WorkMetrics.IsWeekend(date);
            var isSubmitted = submittedSet.Contains(date);
            marks.TryGetValue(date, out var dayType);
            var marked = dayType is not null;
            days.Add(new CalendarDayDto
            {
                Date = date,
                Minutes = info.Minutes,
                EntryCount = info.Count,
                Submitted = isSubmitted,
                IsWeekend = weekend,
                Missing = !weekend && date <= today && !isSubmitted && !marked,
                LowHours = !weekend && !marked && date <= today && info.Minutes > 0 && info.Minutes < 360,
                DayType = dayType
            });
        }

        return new CalendarDto { Year = year, Month = month, UserId = user.Id, UserName = user.Name, Days = days };
    }

    public async Task<MonthTimelineDto> GetMonthTimelineAsync(int year, int month, Guid? userId, CancellationToken ct)
    {
        if (month is < 1 or > 12) throw new AppException("Month must be between 1 and 12.");
        var target = await ResolveUserAsync(userId, ct);
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == target, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);

        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var entries = await BaseQuery()
            .Where(x => x.UserId == target && x.WorkDate >= start && x.WorkDate < end)
            .OrderBy(x => x.WorkDate).ThenBy(x => x.StartTime)
            .ToListAsync(ct);
        var submissions = await _db.DailySubmissions.AsNoTracking()
            .Where(x => x.UserId == target && x.WorkDate >= start && x.WorkDate < end)
            .ToListAsync(ct);
        var byDate = entries.GroupBy(x => x.WorkDate).ToDictionary(g => g.Key, g => g.ToList());
        var bySubmission = submissions.ToDictionary(x => x.WorkDate);

        var days = new List<DayViewDto>();
        for (var date = start; date < end; date = date.AddDays(1))
        {
            byDate.TryGetValue(date, out var dayEntries);
            dayEntries ??= [];
            bySubmission.TryGetValue(date, out var submission);
            days.Add(new DayViewDto
            {
                Date = date,
                UserId = user.Id,
                UserName = user.Name,
                JobTitle = user.JobTitle,
                TotalMinutes = dayEntries.Sum(x => x.DurationMinutes),
                SubmissionStatus = submission?.Status ?? SubmissionStatuses.Pending,
                SubmittedAt = submission?.SubmittedAt,
                DayType = submission?.DayType,
                Entries = dayEntries.Select(MapEntry).ToList()
            });
        }

        return new MonthTimelineDto
        {
            Year = year,
            Month = month,
            UserId = user.Id,
            UserName = user.Name,
            JobTitle = user.JobTitle,
            TotalMinutes = entries.Sum(x => x.DurationMinutes),
            Days = days
        };
    }

    private IQueryable<DailyWorkEntry> BaseQuery()
    {
        var query = _db.DailyWorkEntries.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.MonthlyTask).ThenInclude(x => x.Project)
            .AsQueryable();
        if (!_current.IsTeamLead)
            query = query.Where(x => x.UserId == _current.UserId);
        return query;
    }

    private async Task<Guid> ResolveUserAsync(Guid? requested, CancellationToken ct)
    {
        if (requested is null || requested == Guid.Empty || requested == _current.UserId)
            return _current.UserId;
        await _access.EnsureCanManageUserAsync(requested.Value, ct);
        return requested.Value;
    }

    private Guid? ResolveOptionalUser(Guid? requested)
    {
        if (!_current.IsTeamLead) return _current.UserId;
        return requested is null || requested == Guid.Empty ? null : requested;
    }

    private async Task EnsureEditable(Guid userId, DateOnly date, CancellationToken ct)
    {
        await EnsureMonthOpen(userId, date, ct);
        var submitted = await _db.DailySubmissions.AnyAsync(
            x => x.UserId == userId && x.WorkDate == date && x.Status == SubmissionStatuses.Submitted, ct);
        if (submitted)
            throw new AppException("This daily update is submitted. The Team Lead must reopen it before changes.");
    }

    private async Task EnsureMonthOpen(Guid userId, DateOnly date, CancellationToken ct)
    {
        if (await IsMonthLocked(userId, date, ct))
            throw new AppException("This month is locked. The Team Lead must unlock it before changes can be made.");
    }

    private Task<bool> IsMonthLocked(Guid userId, DateOnly date, CancellationToken ct)
        => _db.MonthlyWorkSheets.AnyAsync(x => x.UserId == userId && x.Year == date.Year && x.Month == date.Month && x.Status == SheetStatuses.Locked, ct);

    private async Task<MonthlyTask> LoadOwnedTask(Guid taskId, Guid userId, DateOnly workDate, CancellationToken ct)
    {
        var task = await _db.MonthlyTasks.Include(x => x.Sheet).FirstOrDefaultAsync(x => x.Id == taskId, ct)
            ?? throw new AppException("Monthly task was not found.", StatusCodes.Status404NotFound);
        if (task.Sheet.UserId != userId)
            throw new AppException("That monthly task belongs to another employee.", StatusCodes.Status403Forbidden);
        if (task.Sheet.Year != workDate.Year || task.Sheet.Month != workDate.Month)
            throw new AppException("The monthly task is not part of this month's work sheet.");
        return task;
    }

    private async Task EnsureNoOverlap(Guid userId, DateOnly date, TimeOnly start, TimeOnly end, Guid? ignoreId, bool allowOverlap, CancellationToken ct)
    {
        if (allowOverlap) return;
        var existing = await _db.DailyWorkEntries.AsNoTracking()
            .Where(x => x.UserId == userId && x.WorkDate == date && x.Id != ignoreId)
            .Select(x => new { x.StartTime, x.EndTime })
            .ToListAsync(ct);
        if (existing.Any(x => WorkMetrics.Overlaps(start, end, x.StartTime, x.EndTime)))
            throw new AppException("Time entries overlap. Please correct the timeline.");
    }

    private static (TimeOnly Start, TimeOnly End, int Minutes) ReadTimes(SaveDailyEntryRequest request)
    {
        var start = WorkMetrics.ParseTime(request.StartTime);
        var end = WorkMetrics.ParseTime(request.EndTime);
        var minutes = WorkMetrics.DurationMinutes(start, end);
        return (start, end, minutes);
    }

    private async Task<DailyEntryDto> LoadDto(Guid id, CancellationToken ct)
    {
        var entry = await _db.DailyWorkEntries.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.MonthlyTask).ThenInclude(x => x.Project)
            .FirstAsync(x => x.Id == id, ct);
        return MapEntry(entry);
    }

    public static DailyEntryDto MapEntry(DailyWorkEntry entry) => new()
    {
        Id = entry.Id,
        UserId = entry.UserId,
        UserName = entry.User.Name,
        MonthlyTaskId = entry.MonthlyTaskId,
        MonthlyTaskTitle = entry.MonthlyTask.Title,
        ProjectId = entry.MonthlyTask.ProjectId,
        ProjectName = entry.MonthlyTask.Project?.Name,
        WorkDate = entry.WorkDate,
        StartTime = WorkMetrics.FormatTime(entry.StartTime),
        EndTime = WorkMetrics.FormatTime(entry.EndTime),
        DurationMinutes = entry.DurationMinutes,
        Description = entry.Description,
        Status = entry.Status,
        Remarks = entry.Remarks
    };
}
