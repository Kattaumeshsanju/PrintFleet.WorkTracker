using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IDepartmentAccess
{
    Task<bool> IsAdminAsync(CancellationToken ct);
    Task<bool> IsOrganizationViewerAsync(CancellationToken ct);
    Task<bool> CanViewOrganizationOverviewAsync(CancellationToken ct);
    Task<bool> CanViewOrganizationReportsAsync(CancellationToken ct);
    Task<Guid?> MyDepartmentAsync(CancellationToken ct);
    Task EnsureCanViewPlanAsync(Guid userId, CancellationToken ct);
    Task EnsureSameDepartmentAsync(Guid userId, CancellationToken ct);
    Task EnsureCanEditPlanAsync(Guid userId, CancellationToken ct);
    Task EnsureCanViewDailyAsync(Guid userId, CancellationToken ct);
    Task EnsureCanManageUserAsync(Guid userId, CancellationToken ct);
    Task<IQueryable<User>> ScopeUsersAsync(IQueryable<User> query, Guid? departmentId, CancellationToken ct);
    Task<Guid?> DepartmentForUserAsync(Guid? requested, string role, CancellationToken ct);
    Task<Guid> DepartmentForProjectAsync(Guid? requested, CancellationToken ct);
}

public class DepartmentAccess : IDepartmentAccess
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private (string Role, Guid? DepartmentId, bool OverviewGrant, bool ReportsGrant)? _profile;

    public DepartmentAccess(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<bool> IsAdminAsync(CancellationToken ct)
        => (await ProfileAsync(ct)).Role == Roles.Admin;

    public async Task<bool> IsOrganizationViewerAsync(CancellationToken ct)
        => DepartmentRules.CanViewOrganization((await ProfileAsync(ct)).Role);

    public async Task<bool> CanViewOrganizationOverviewAsync(CancellationToken ct)
    {
        var profile = await ProfileAsync(ct);
        return AccessRules.Overview(profile.Role, profile.OverviewGrant);
    }

    public async Task<bool> CanViewOrganizationReportsAsync(CancellationToken ct)
    {
        var profile = await ProfileAsync(ct);
        return AccessRules.Reports(profile.Role, profile.ReportsGrant);
    }

    public async Task<Guid?> MyDepartmentAsync(CancellationToken ct)
        => (await ProfileAsync(ct)).DepartmentId;

    public async Task EnsureCanViewPlanAsync(Guid userId, CancellationToken ct)
    {
        if (userId == _current.UserId || await IsOrganizationViewerAsync(ct)) return;
        await EnsureSameDepartmentCoreAsync(userId, ct);
    }

    public async Task EnsureSameDepartmentAsync(Guid userId, CancellationToken ct)
    {
        var profile = await ProfileAsync(ct);
        if (userId != _current.UserId && profile.Role == Roles.Ceo)
            throw new AppException("The CEO can review plans but cannot change them.", StatusCodes.Status403Forbidden);
        if (userId == _current.UserId || profile.Role == Roles.Admin) return;
        await EnsureSameDepartmentCoreAsync(userId, ct);
    }

    public async Task EnsureCanEditPlanAsync(Guid userId, CancellationToken ct)
    {
        var profile = await ProfileAsync(ct);
        if (userId != _current.UserId && profile.Role == Roles.Ceo)
            throw new AppException("The CEO can review plans but cannot change them.", StatusCodes.Status403Forbidden);
        if (userId == _current.UserId || profile.Role == Roles.Admin) return;
        if (profile.Role != Roles.TeamLead)
            throw new AppException("You can only change your own monthly plan.", StatusCodes.Status403Forbidden);
        await EnsureSameDepartmentCoreAsync(userId, ct);
    }

    private async Task EnsureSameDepartmentCoreAsync(Guid userId, CancellationToken ct)
    {
        var mine = await MyDepartmentAsync(ct);
        var theirs = await _db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.DepartmentId).FirstOrDefaultAsync(ct);
        if (mine is null || theirs != mine)
            throw new AppException("That person is in another department.", StatusCodes.Status403Forbidden);
    }

    public async Task EnsureCanViewDailyAsync(Guid userId, CancellationToken ct)
    {
        var profile = await ProfileAsync(ct);
        if (!DepartmentRules.CanViewDaily(_current.UserId, profile.Role, userId))
            throw new AppException("Daily work is private to that employee.", StatusCodes.Status403Forbidden);
    }

    public async Task EnsureCanManageUserAsync(Guid userId, CancellationToken ct)
    {
        _current.EnsureLead();
        if (await IsAdminAsync(ct) || userId == _current.UserId) return;
        await EnsureSameDepartmentAsync(userId, ct);
    }

    public async Task<IQueryable<User>> ScopeUsersAsync(IQueryable<User> query, Guid? departmentId, CancellationToken ct)
    {
        if (await IsOrganizationViewerAsync(ct))
            return departmentId is Guid id && id != Guid.Empty ? query.Where(x => x.DepartmentId == id) : query;

        var mine = await MyDepartmentAsync(ct);
        return query.Where(x => x.DepartmentId == mine);
    }

    public async Task<Guid?> DepartmentForUserAsync(Guid? requested, string role, CancellationToken ct)
    {
        if (role is Roles.Admin or Roles.Ceo)
        {
            if (!await IsAdminAsync(ct))
                throw new AppException("Only the admin can assign an organization role.", StatusCodes.Status403Forbidden);
            return null;
        }

        if (!await IsAdminAsync(ct))
        {
            if (role != Roles.TeamMember)
                throw new AppException("Only the admin can assign a team lead.", StatusCodes.Status403Forbidden);
            return await MyDepartmentAsync(ct) ?? throw new AppException("Your account is not in a department.");
        }

        if (requested is null || requested == Guid.Empty)
            throw new AppException("Choose a department.");
        if (!await _db.Departments.AnyAsync(x => x.Id == requested, ct))
            throw new AppException("Department was not found.", StatusCodes.Status404NotFound);
        return requested;
    }

    public async Task<Guid> DepartmentForProjectAsync(Guid? requested, CancellationToken ct)
    {
        var profile = await ProfileAsync(ct);
        if (!DepartmentRules.CanEditDepartmentWork(profile.Role))
            throw new AppException("You can review projects but cannot change them.", StatusCodes.Status403Forbidden);
        if (profile.Role != Roles.Admin)
            return profile.DepartmentId ?? throw new AppException("Your account is not in a department.");

        if (requested is null || requested == Guid.Empty)
            throw new AppException("Choose a department.");
        if (!await _db.Departments.AnyAsync(x => x.Id == requested, ct))
            throw new AppException("Department was not found.", StatusCodes.Status404NotFound);
        return requested.Value;
    }

    private async Task<(string Role, Guid? DepartmentId, bool OverviewGrant, bool ReportsGrant)> ProfileAsync(CancellationToken ct)
    {
        if (_profile is not null) return _profile.Value;
        var profile = await _db.Users.AsNoTracking()
            .Where(x => x.Id == _current.UserId)
            .Select(x => new { x.Role, x.DepartmentId, x.IsActive })
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException("Your account was not found.", StatusCodes.Status401Unauthorized);
        if (!profile.IsActive)
            throw new AppException("Your account is not active.", StatusCodes.Status401Unauthorized);
        var grants = await _db.UserPermissions.AsNoTracking()
            .Where(x => x.UserId == _current.UserId)
            .Select(x => x.Permission)
            .ToListAsync(ct);
        _profile = (profile.Role, profile.DepartmentId, grants.Contains(OrganizationPermissions.Overview), grants.Contains(OrganizationPermissions.Reports));
        return _profile.Value;
    }
}
