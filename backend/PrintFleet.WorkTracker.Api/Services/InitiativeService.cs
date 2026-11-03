using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IInitiativeService
{
    Task<PagedResult<InitiativeDto>> ListAsync(string? search, Guid? departmentId, Guid? categoryId, string? status, string? priority, Guid? ownerId, string? stage, int? year, int? month, string? sort, int page, int pageSize, CancellationToken ct);
    Task<InitiativeDto> GetAsync(Guid id, CancellationToken ct);
    Task<InitiativeDto> CreateAsync(SaveInitiativeRequest request, CancellationToken ct);
    Task<InitiativeDto> UpdateAsync(Guid id, SaveInitiativeRequest request, CancellationToken ct);
    Task<MilestoneDto> AddMilestoneAsync(Guid id, SaveMilestoneRequest request, CancellationToken ct);
    Task<MilestoneDto> UpdateMilestoneAsync(Guid id, Guid milestoneId, SaveMilestoneRequest request, CancellationToken ct);
    Task<ResearchRecordDto> AddRecordAsync(Guid id, SaveResearchRecordRequest request, CancellationToken ct);
    Task<IReadOnlyList<InitiativeDto>> ExportRowsAsync(string? search, Guid? departmentId, Guid? categoryId, string? status, string? priority, Guid? ownerId, string? stage, int? year, int? month, CancellationToken ct);
    Task<ResearchPortfolioDto> PortfolioAsync(Guid? departmentId, Guid? categoryId, string? status, string? priority, Guid? ownerId, string? stage, int year, int month, CancellationToken ct);
    Task<(Guid? ProjectId, Guid? CategoryId)> LinkGoalAsync(Guid? initiativeId, Guid? projectId, Guid? categoryId, Guid? currentCategoryId, Guid departmentId, CancellationToken ct);
}

public class InitiativeService : IInitiativeService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;
    private readonly IWorkCategoryService _categories;

    public InitiativeService(AppDbContext db, ICurrentUser current, IAuditService audit, IWorkCategoryService categories)
    {
        _db = db;
        _current = current;
        _audit = audit;
        _categories = categories;
    }

    public async Task<PagedResult<InitiativeDto>> ListAsync(string? search, Guid? departmentId, Guid? categoryId, string? status, string? priority, Guid? ownerId, string? stage, int? year, int? month, string? sort, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = await FilteredAsync(search, departmentId, categoryId, status, priority, ownerId, stage, year, month, ct);
        query = sort switch
        {
            "title" => query.OrderBy(x => x.Title),
            "status" => query.OrderBy(x => x.Status).ThenBy(x => x.Number),
            "priority" => query.OrderBy(x => x.Priority).ThenBy(x => x.Number),
            "target" => query.OrderBy(x => x.TargetEnd).ThenBy(x => x.Number),
            _ => query.OrderBy(x => x.Number)
        };
        var total = await query.CountAsync(ct);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var me = await MeAsync(ct);
        return new PagedResult<InitiativeDto>
        {
            Items = rows.Select(x => Map(x, me, false)).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<InitiativeDto> GetAsync(Guid id, CancellationToken ct)
    {
        var row = await Detailed().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Initiative was not found.", StatusCodes.Status404NotFound);
        await EnsureCanViewAsync(row.DepartmentId, ct);
        return Map(row, await MeAsync(ct), true);
    }

    public async Task<InitiativeDto> CreateAsync(SaveInitiativeRequest request, CancellationToken ct)
    {
        var me = await RequireEditorAsync(request.DepartmentId, ct);
        var departmentId = me.Role == Roles.Admin ? request.DepartmentId!.Value : me.DepartmentId!.Value;
        Validate(request);
        var categoryId = await _categories.AssignAsync(request.WorkCategoryId, departmentId, null, ct);
        var projectId = await ProjectForAsync(request.ProjectId, departmentId, ct);
        var ownerId = await PersonForAsync(request.OwnerId, departmentId, ct);
        var collaborators = await PeopleForAsync(request.CollaboratorIds, departmentId, ownerId, ct);
        var numbers = await _db.Initiatives.Select(x => x.Number).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var initiative = new Initiative
        {
            Id = Guid.NewGuid(),
            DepartmentId = departmentId,
            WorkCategoryId = categoryId,
            ProjectId = projectId,
            Number = CategoryRules.NextNumber(numbers),
            Title = request.Title.Trim(),
            Description = WorkMetrics.Clean(request.Description),
            Status = request.Status,
            Priority = request.Priority,
            OwnerId = ownerId,
            TargetStart = request.TargetStart,
            TargetEnd = request.TargetEnd,
            Remarks = WorkMetrics.Clean(request.Remarks),
            ResearchObjective = WorkMetrics.Clean(request.ResearchObjective),
            Hypothesis = WorkMetrics.Clean(request.Hypothesis),
            ResearchStage = string.IsNullOrWhiteSpace(request.ResearchStage) ? null : request.ResearchStage,
            SuccessCriteria = WorkMetrics.Clean(request.SuccessCriteria),
            ExperimentPlan = WorkMetrics.Clean(request.ExperimentPlan),
            Conclusion = WorkMetrics.Clean(request.Conclusion),
            CreatedById = me.Id,
            CreatedAt = now,
            UpdatedAt = now,
            Collaborators = collaborators.Select(id => new InitiativeCollaborator { InitiativeId = Guid.Empty, UserId = id }).ToList()
        };
        foreach (var person in initiative.Collaborators) person.InitiativeId = initiative.Id;
        _db.Initiatives.Add(initiative);
        _audit.Stage(AuditActions.Created, "Initiative", initiative.Id, $"Created initiative {initiative.Number}");
        await _db.SaveChangesAsync(ct);
        return await GetAsync(initiative.Id, ct);
    }

    public async Task<InitiativeDto> UpdateAsync(Guid id, SaveInitiativeRequest request, CancellationToken ct)
    {
        var initiative = await _db.Initiatives.Include(x => x.Collaborators).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Initiative was not found.", StatusCodes.Status404NotFound);
        var me = await RequireEditorAsync(initiative.DepartmentId, ct);
        Validate(request);
        var previousStatus = initiative.Status;
        var previousStage = initiative.ResearchStage;
        initiative.WorkCategoryId = await _categories.AssignAsync(request.WorkCategoryId, initiative.DepartmentId, initiative.WorkCategoryId, ct);
        initiative.ProjectId = await ProjectForAsync(request.ProjectId, initiative.DepartmentId, ct);
        initiative.Title = request.Title.Trim();
        initiative.Description = WorkMetrics.Clean(request.Description);
        initiative.Status = request.Status;
        initiative.Priority = request.Priority;
        initiative.OwnerId = await PersonForAsync(request.OwnerId, initiative.DepartmentId, ct);
        initiative.TargetStart = request.TargetStart;
        initiative.TargetEnd = request.TargetEnd;
        initiative.Remarks = WorkMetrics.Clean(request.Remarks);
        initiative.ResearchObjective = WorkMetrics.Clean(request.ResearchObjective);
        initiative.Hypothesis = WorkMetrics.Clean(request.Hypothesis);
        initiative.ResearchStage = string.IsNullOrWhiteSpace(request.ResearchStage) ? null : request.ResearchStage;
        initiative.SuccessCriteria = WorkMetrics.Clean(request.SuccessCriteria);
        initiative.ExperimentPlan = WorkMetrics.Clean(request.ExperimentPlan);
        initiative.Conclusion = WorkMetrics.Clean(request.Conclusion);
        initiative.UpdatedAt = DateTime.UtcNow;
        var collaborators = await PeopleForAsync(request.CollaboratorIds, initiative.DepartmentId, initiative.OwnerId, ct);
        _db.InitiativeCollaborators.RemoveRange(initiative.Collaborators);
        foreach (var personId in collaborators)
            _db.InitiativeCollaborators.Add(new InitiativeCollaborator { InitiativeId = initiative.Id, UserId = personId });
        if (previousStatus != initiative.Status || previousStage != initiative.ResearchStage)
            _audit.Stage(AuditActions.Updated, "Initiative", initiative.Id, $"Updated {initiative.Number}: status {previousStatus} to {initiative.Status}, stage {previousStage ?? "none"} to {initiative.ResearchStage ?? "none"}");
        else
            _audit.Stage(AuditActions.Updated, "Initiative", initiative.Id, $"Updated {initiative.Number}");
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<MilestoneDto> AddMilestoneAsync(Guid id, SaveMilestoneRequest request, CancellationToken ct)
    {
        var initiative = await _db.Initiatives.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Initiative was not found.", StatusCodes.Status404NotFound);
        await RequireEditorAsync(initiative.DepartmentId, ct);
        if (!InitiativeStatuses.All.Contains(request.Status)) throw new AppException("Choose a milestone status.");
        var milestone = new InitiativeMilestone
        {
            Id = Guid.NewGuid(),
            InitiativeId = initiative.Id,
            Title = request.Title.Trim(),
            Description = WorkMetrics.Clean(request.Description),
            OwnerId = await PersonForAsync(request.OwnerId, initiative.DepartmentId, ct),
            TargetDate = request.TargetDate,
            Status = request.Status,
            CompletedOn = request.Status == InitiativeStatuses.Completed ? DateOnly.FromDateTime(DateTime.Today) : null,
            CreatedAt = DateTime.UtcNow
        };
        _db.InitiativeMilestones.Add(milestone);
        _audit.Stage(AuditActions.Created, "InitiativeMilestone", milestone.Id, $"Added milestone to {initiative.Number}");
        await _db.SaveChangesAsync(ct);
        milestone.Owner = milestone.OwnerId is Guid ownerId ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ownerId, ct) : null;
        return MapMilestone(milestone);
    }

    public async Task<MilestoneDto> UpdateMilestoneAsync(Guid id, Guid milestoneId, SaveMilestoneRequest request, CancellationToken ct)
    {
        var milestone = await _db.InitiativeMilestones.Include(x => x.Initiative).Include(x => x.Owner)
            .FirstOrDefaultAsync(x => x.Id == milestoneId && x.InitiativeId == id, ct)
            ?? throw new AppException("Milestone was not found.", StatusCodes.Status404NotFound);
        var me = await MeAsync(ct);
        var editor = CategoryRules.CanEditInitiative(me.Role, me.DepartmentId, milestone.Initiative.DepartmentId);
        if (!editor && milestone.OwnerId != me.Id)
            throw new AppException("You can update only a milestone assigned to you.", StatusCodes.Status403Forbidden);
        if (!InitiativeStatuses.All.Contains(request.Status)) throw new AppException("Choose a milestone status.");
        if (editor)
        {
            milestone.Title = request.Title.Trim();
            milestone.Description = WorkMetrics.Clean(request.Description);
            milestone.OwnerId = await PersonForAsync(request.OwnerId, milestone.Initiative.DepartmentId, ct);
            milestone.TargetDate = request.TargetDate;
        }
        milestone.Status = request.Status;
        milestone.CompletedOn = request.Status == InitiativeStatuses.Completed
            ? milestone.CompletedOn ?? DateOnly.FromDateTime(DateTime.Today)
            : null;
        _audit.Stage(AuditActions.Updated, "InitiativeMilestone", milestone.Id, $"Updated milestone on {milestone.Initiative.Number}");
        await _db.SaveChangesAsync(ct);
        return MapMilestone(milestone);
    }

    public async Task<ResearchRecordDto> AddRecordAsync(Guid id, SaveResearchRecordRequest request, CancellationToken ct)
    {
        var initiative = await _db.Initiatives.Include(x => x.Collaborators).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Initiative was not found.", StatusCodes.Status404NotFound);
        var me = await MeAsync(ct);
        var involved = initiative.OwnerId == me.Id || initiative.Collaborators.Any(x => x.UserId == me.Id);
        if (!CategoryRules.CanAddResearch(me.Role, me.DepartmentId, initiative.DepartmentId, involved))
            throw new AppException("You cannot add research records to this initiative.", StatusCodes.Status403Forbidden);
        if (string.IsNullOrWhiteSpace(request.Title)) throw new AppException("Enter a title for the research record.");
        var record = new ResearchRecord
        {
            Id = Guid.NewGuid(),
            InitiativeId = initiative.Id,
            Title = request.Title.Trim(),
            RecordedOn = request.RecordedOn == default ? DateOnly.FromDateTime(DateTime.Today) : request.RecordedOn,
            Objective = WorkMetrics.Clean(request.Objective),
            Method = WorkMetrics.Clean(request.Method),
            Findings = WorkMetrics.Clean(request.Findings),
            Outcome = WorkMetrics.Clean(request.Outcome),
            EvidenceLinks = WorkMetrics.Clean(request.EvidenceLinks),
            NextSteps = WorkMetrics.Clean(request.NextSteps),
            CreatedById = me.Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.ResearchRecords.Add(record);
        _audit.Stage(AuditActions.Created, "ResearchRecord", record.Id, $"Added a research record to {initiative.Number}");
        await _db.SaveChangesAsync(ct);
        return new ResearchRecordDto
        {
            Id = record.Id,
            Title = record.Title,
            RecordedOn = record.RecordedOn,
            Objective = record.Objective,
            Method = record.Method,
            Findings = record.Findings,
            Outcome = record.Outcome,
            EvidenceLinks = record.EvidenceLinks,
            NextSteps = record.NextSteps,
            CreatedByName = me.Name,
            CreatedAt = record.CreatedAt
        };
    }

    public async Task<IReadOnlyList<InitiativeDto>> ExportRowsAsync(string? search, Guid? departmentId, Guid? categoryId, string? status, string? priority, Guid? ownerId, string? stage, int? year, int? month, CancellationToken ct)
    {
        var query = await FilteredAsync(search, departmentId, categoryId, status, priority, ownerId, stage, year, month, ct);
        var rows = await query.OrderBy(x => x.Department.Name).ThenBy(x => x.Number).Take(2000).ToListAsync(ct);
        var me = await MeAsync(ct);
        return rows.Select(x => Map(x, me, false)).ToList();
    }

    public async Task<ResearchPortfolioDto> PortfolioAsync(Guid? departmentId, Guid? categoryId, string? status, string? priority, Guid? ownerId, string? stage, int year, int month, CancellationToken ct)
    {
        var me = await MeAsync(ct);
        if (me.Role is not (Roles.Admin or Roles.Ceo))
            throw new AppException("Only an admin or the CEO can open the research portfolio.", StatusCodes.Status403Forbidden);
        var query = await FilteredAsync(null, departmentId, categoryId, status, priority, ownerId, stage, null, null, ct);
        var rows = await query.Include(x => x.Milestones).Include(x => x.Records).ToListAsync(ct);
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var completed = rows.Where(x => x.Status == InitiativeStatuses.Completed && DateOnly.FromDateTime(x.UpdatedAt) >= start && DateOnly.FromDateTime(x.UpdatedAt) <= end).ToList();
        var milestones = rows.SelectMany(x => x.Milestones).ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var categories = rows.Where(x => x.WorkCategoryId is not null)
            .GroupBy(x => new { x.DepartmentId, Department = x.Department.Name, CategoryId = x.WorkCategoryId!.Value, Category = x.WorkCategory?.Name ?? "" })
            .Select(group =>
            {
                var counted = group.Count(x => x.Status != InitiativeStatuses.Cancelled);
                var done = group.Count(x => x.Status == InitiativeStatuses.Completed);
                return new CategoryProgressDto
                {
                    DepartmentId = group.Key.DepartmentId,
                    DepartmentName = group.Key.Department,
                    CategoryId = group.Key.CategoryId,
                    CategoryName = group.Key.Category,
                    Counted = counted,
                    Completed = done,
                    CompletionPercent = CategoryRules.Completion(done, counted)
                };
            })
            .OrderBy(x => x.DepartmentName).ThenBy(x => x.CategoryName)
            .ToList();
        return new ResearchPortfolioDto
        {
            ActiveInitiatives = rows.Count(x => x.Status is InitiativeStatuses.Planned or InitiativeStatuses.Ongoing),
            OnHold = rows.Count(x => x.Status == InitiativeStatuses.OnHold),
            CompletedInPeriod = completed.Count,
            MilestonesDue = milestones.Count(x => x.TargetDate >= start && x.TargetDate <= end && x.Status != InitiativeStatuses.Completed && x.Status != InitiativeStatuses.Cancelled),
            MilestonesOverdue = milestones.Count(x => x.TargetDate < today && x.Status != InitiativeStatuses.Completed && x.Status != InitiativeStatuses.Cancelled),
            ByStatus = Count(rows.Select(x => x.Status)),
            ByStage = Count(rows.Select(x => x.ResearchStage ?? "Not set")),
            ByPriority = Count(rows.Select(x => x.Priority)),
            Categories = categories,
            Deadlines = rows.Where(x => x.TargetEnd >= start && x.TargetEnd <= end && x.Status != InitiativeStatuses.Completed && x.Status != InitiativeStatuses.Cancelled)
                .OrderBy(x => x.TargetEnd).Take(30).Select(x => Map(x, me, false)).ToList(),
            Outcomes = rows.SelectMany(x => x.Records)
                .Where(x => x.RecordedOn >= start && x.RecordedOn <= end && !string.IsNullOrWhiteSpace(x.Outcome))
                .OrderByDescending(x => x.RecordedOn)
                .Take(30)
                .Select(MapRecord)
                .ToList()
        };
    }

    public async Task<(Guid? ProjectId, Guid? CategoryId)> LinkGoalAsync(Guid? initiativeId, Guid? projectId, Guid? categoryId, Guid? currentCategoryId, Guid departmentId, CancellationToken ct)
    {
        if (initiativeId is null || initiativeId == Guid.Empty)
            return (projectId, await _categories.AssignAsync(categoryId, departmentId, currentCategoryId, ct));
        var initiative = await _db.Initiatives.AsNoTracking().FirstOrDefaultAsync(x => x.Id == initiativeId, ct)
            ?? throw new AppException("Initiative was not found.", StatusCodes.Status404NotFound);
        if (initiative.DepartmentId != departmentId)
            throw new AppException("That initiative belongs to another department.", StatusCodes.Status403Forbidden);
        var linked = CategoryRules.LinkGoal(projectId, initiative.ProjectId, categoryId, initiative.WorkCategoryId);
        var assigned = await _categories.AssignAsync(linked.CategoryId, departmentId, currentCategoryId, ct);
        return (linked.ProjectId, assigned);
    }

    private async Task<IQueryable<Initiative>> FilteredAsync(string? search, Guid? departmentId, Guid? categoryId, string? status, string? priority, Guid? ownerId, string? stage, int? year, int? month, CancellationToken ct)
    {
        var me = await MeAsync(ct);
        var query = Detailed();
        if (me.Role is Roles.Admin or Roles.Ceo)
        {
            if (departmentId is Guid id && id != Guid.Empty) query = query.Where(x => x.DepartmentId == id);
        }
        else if (me.DepartmentId is Guid mine)
            query = query.Where(x => x.DepartmentId == mine);
        else
            query = query.Where(x => false);
        if (categoryId is Guid category && category != Guid.Empty) query = query.Where(x => x.WorkCategoryId == category);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(x => x.Priority == priority);
        if (ownerId is Guid owner && owner != Guid.Empty) query = query.Where(x => x.OwnerId == owner);
        if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(x => x.ResearchStage == stage);
        if (year is int reportYear && month is int reportMonth)
        {
            var start = new DateOnly(reportYear, reportMonth, 1);
            var end = start.AddMonths(1).AddDays(-1);
            var createdFrom = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var createdTo = DateTime.SpecifyKind(end.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(x => (x.TargetEnd >= start && x.TargetEnd <= end) || (x.TargetEnd == null && x.CreatedAt >= createdFrom && x.CreatedAt < createdTo));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(term) || x.Number.ToLower().Contains(term) || (x.Description != null && x.Description.ToLower().Contains(term)));
        }
        return query;
    }

    private IQueryable<Initiative> Detailed()
        => _db.Initiatives.AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.WorkCategory)
            .Include(x => x.Project)
            .Include(x => x.Owner)
            .Include(x => x.Collaborators).ThenInclude(x => x.User)
            .Include(x => x.Milestones).ThenInclude(x => x.Owner)
            .Include(x => x.Records);

    private async Task EnsureCanViewAsync(Guid departmentId, CancellationToken ct)
    {
        var me = await MeAsync(ct);
        if (me.Role is Roles.Admin or Roles.Ceo) return;
        if (me.DepartmentId != departmentId)
            throw new AppException("That initiative belongs to another department.", StatusCodes.Status403Forbidden);
    }

    private async Task<User> RequireEditorAsync(Guid? departmentId, CancellationToken ct)
    {
        var me = await MeAsync(ct);
        if (me.Role == Roles.Admin)
        {
            if (departmentId is null || departmentId == Guid.Empty) throw new AppException("Choose a department.");
            if (!await _db.Departments.AnyAsync(x => x.Id == departmentId && x.IsActive, ct))
                throw new AppException("Department was not found.", StatusCodes.Status404NotFound);
            return me;
        }
        var target = departmentId is null || departmentId == Guid.Empty ? me.DepartmentId ?? Guid.Empty : departmentId.Value;
        if (!CategoryRules.CanEditInitiative(me.Role, me.DepartmentId, target))
            throw new AppException("You can change initiatives only in your own department.", StatusCodes.Status403Forbidden);
        return me;
    }

    private async Task<User> MeAsync(CancellationToken ct)
        => await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == _current.UserId, ct)
            ?? throw new AppException("Your account was not found.", StatusCodes.Status401Unauthorized);

    private async Task<Guid?> ProjectForAsync(Guid? projectId, Guid departmentId, CancellationToken ct)
    {
        if (projectId is null || projectId == Guid.Empty) return null;
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId, ct)
            ?? throw new AppException("Project was not found.", StatusCodes.Status404NotFound);
        if (project.DepartmentId != departmentId)
            throw new AppException("That project belongs to another department.", StatusCodes.Status403Forbidden);
        return project.Id;
    }

    private async Task<Guid?> PersonForAsync(Guid? userId, Guid departmentId, CancellationToken ct)
    {
        var people = await PeopleForAsync(userId is Guid id ? [id] : [], departmentId, null, ct);
        return people.FirstOrDefault() is Guid person && person != Guid.Empty ? person : null;
    }

    private async Task<List<Guid>> PeopleForAsync(IEnumerable<Guid> ids, Guid departmentId, Guid? ownerId, CancellationToken ct)
    {
        var wanted = ids.Where(x => x != Guid.Empty && x != ownerId).Distinct().ToList();
        if (wanted.Count == 0) return [];
        var people = await _db.Users.AsNoTracking().Where(x => wanted.Contains(x.Id)).ToListAsync(ct);
        if (people.Count != wanted.Count) throw new AppException("A selected person was not found.", StatusCodes.Status404NotFound);
        if (people.Any(x => !x.IsActive || x.DepartmentId != departmentId || x.Role is Roles.Admin or Roles.Ceo))
            throw new AppException("Collaborators must be active people in this department.");
        return people.Select(x => x.Id).ToList();
    }

    private static void Validate(SaveInitiativeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) throw new AppException("Enter an initiative title.");
        if (!InitiativeStatuses.All.Contains(request.Status)) throw new AppException("Choose an initiative status.");
        if (!Priorities.All.Contains(request.Priority)) throw new AppException("Choose a priority.");
        if (!string.IsNullOrWhiteSpace(request.ResearchStage) && !ResearchStages.All.Contains(request.ResearchStage))
            throw new AppException("Choose a research stage.");
        if (request.TargetStart is DateOnly start && request.TargetEnd is DateOnly end && end < start)
            throw new AppException("Target completion must be on or after the target start.");
    }

    private static InitiativeDto Map(Initiative initiative, User actor, bool includeResearch)
    {
        var involved = initiative.OwnerId == actor.Id || initiative.Collaborators.Any(x => x.UserId == actor.Id);
        var dto = new InitiativeDto
        {
            Id = initiative.Id,
            Number = initiative.Number,
            Title = initiative.Title,
            Description = initiative.Description,
            DepartmentId = initiative.DepartmentId,
            DepartmentName = initiative.Department?.Name ?? "",
            WorkCategoryId = initiative.WorkCategoryId,
            WorkCategoryName = initiative.WorkCategory?.Name,
            ProjectId = initiative.ProjectId,
            ProjectName = initiative.Project?.Name,
            Status = initiative.Status,
            Priority = initiative.Priority,
            OwnerId = initiative.OwnerId,
            OwnerName = initiative.Owner?.Name,
            Collaborators = initiative.Collaborators.Select(x => new TeamPersonDto { Id = x.UserId, Name = x.User?.Name ?? "", Email = x.User?.Email ?? "", Role = x.User?.Role ?? "", IsActive = x.User?.IsActive ?? false, DepartmentId = x.User?.DepartmentId }).ToList(),
            TargetStart = initiative.TargetStart,
            TargetEnd = initiative.TargetEnd,
            Remarks = initiative.Remarks,
            ResearchStage = initiative.ResearchStage,
            CanEdit = CategoryRules.CanEditInitiative(actor.Role, actor.DepartmentId, initiative.DepartmentId),
            CanRecord = CategoryRules.CanAddResearch(actor.Role, actor.DepartmentId, initiative.DepartmentId, involved),
            UpdatedAt = initiative.UpdatedAt,
            Milestones = initiative.Milestones.OrderBy(x => x.TargetDate).Select(MapMilestone).ToList()
        };
        if (includeResearch)
        {
            dto.ResearchObjective = initiative.ResearchObjective;
            dto.Hypothesis = initiative.Hypothesis;
            dto.SuccessCriteria = initiative.SuccessCriteria;
            dto.ExperimentPlan = initiative.ExperimentPlan;
            dto.Conclusion = initiative.Conclusion;
            dto.Records = initiative.Records.OrderByDescending(x => x.RecordedOn).ThenByDescending(x => x.CreatedAt).Select(MapRecord).ToList();
        }
        return dto;
    }

    private static MilestoneDto MapMilestone(InitiativeMilestone milestone) => new()
    {
        Id = milestone.Id,
        Title = milestone.Title,
        Description = milestone.Description,
        OwnerId = milestone.OwnerId,
        OwnerName = milestone.Owner?.Name,
        TargetDate = milestone.TargetDate,
        Status = milestone.Status,
        CompletedOn = milestone.CompletedOn
    };

    private static ResearchRecordDto MapRecord(ResearchRecord record) => new()
    {
        Id = record.Id,
        Title = record.Title,
        RecordedOn = record.RecordedOn,
        Objective = record.Objective,
        Method = record.Method,
        Findings = record.Findings,
        Outcome = record.Outcome,
        EvidenceLinks = record.EvidenceLinks,
        NextSteps = record.NextSteps,
        CreatedAt = record.CreatedAt
    };

    private static List<CountDto> Count(IEnumerable<string> values)
        => values.GroupBy(x => x).Select(x => new CountDto { Name = x.Key, Count = x.Count() }).OrderByDescending(x => x.Count).ToList();
}
