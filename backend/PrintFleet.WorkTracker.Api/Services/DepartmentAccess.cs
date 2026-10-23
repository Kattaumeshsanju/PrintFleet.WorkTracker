using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IDepartmentAccess
{
    Task<bool> IsAdminAsync(CancellationToken ct);
    Task<Guid?> MyDepartmentAsync(CancellationToken ct);
    Task EnsureSameDepartmentAsync(Guid userId, CancellationToken ct);
    Task EnsureCanManageUserAsync(Guid userId, CancellationToken ct);
    Task<IQueryable<User>> ScopeUsersAsync(IQueryable<User> query, Guid? departmentId, CancellationToken ct);
    Task<Guid?> DepartmentForUserAsync(Guid? requested, string role, CancellationToken ct);
    Task<Guid> DepartmentForProjectAsync(Guid? requested, CancellationToken ct);
}

public class DepartmentAccess : IDepartmentAccess
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private (string Role, Guid? DepartmentId)? _profile;

    public DepartmentAccess(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<bool> IsAdminAsync(CancellationToken ct)
        => (await ProfileAsync(ct)).Role == Roles.Admin;

    public async Task<Guid?> MyDepartmentAsync(CancellationToken ct)
        => (await ProfileAsync(ct)).DepartmentId;

    public async Task EnsureSameDepartmentAsync(Guid userId, CancellationToken ct)
    {
        if (userId == _current.UserId || await IsAdminAsync(ct)) return;
        var mine = await MyDepartmentAsync(ct);
        var theirs = await _db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.DepartmentId).FirstOrDefaultAsync(ct);
        if (mine is null || theirs != mine)
            throw new AppException("That person is in another department.", StatusCodes.Status403Forbidden);
    }

    public async Task EnsureCanManageUserAsync(Guid userId, CancellationToken ct)
    {
        _current.EnsureLead();
        if (await IsAdminAsync(ct) || userId == _current.UserId) return;
        await EnsureSameDepartmentAsync(userId, ct);
    }

    public async Task<IQueryable<User>> ScopeUsersAsync(IQueryable<User> query, Guid? departmentId, CancellationToken ct)
    {
        if (await IsAdminAsync(ct))
            return departmentId is Guid id && id != Guid.Empty ? query.Where(x => x.DepartmentId == id) : query;

        var mine = await MyDepartmentAsync(ct);
        return query.Where(x => x.DepartmentId == mine);
    }

    public async Task<Guid?> DepartmentForUserAsync(Guid? requested, string role, CancellationToken ct)
    {
        if (role == Roles.Admin)
        {
            if (!await IsAdminAsync(ct))
                throw new AppException("Only the admin can assign the admin role.", StatusCodes.Status403Forbidden);
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
        if (!await IsAdminAsync(ct))
            return await MyDepartmentAsync(ct) ?? throw new AppException("Your account is not in a department.");

        if (requested is null || requested == Guid.Empty)
            throw new AppException("Choose a department.");
        if (!await _db.Departments.AnyAsync(x => x.Id == requested, ct))
            throw new AppException("Department was not found.", StatusCodes.Status404NotFound);
        return requested.Value;
    }

    private async Task<(string Role, Guid? DepartmentId)> ProfileAsync(CancellationToken ct)
    {
        if (_profile is not null) return _profile.Value;
        var profile = await _db.Users.AsNoTracking()
            .Where(x => x.Id == _current.UserId)
            .Select(x => new { x.Role, x.DepartmentId })
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException("Your account was not found.", StatusCodes.Status401Unauthorized);
        _profile = (profile.Role, profile.DepartmentId);
        return _profile.Value;
    }
}
