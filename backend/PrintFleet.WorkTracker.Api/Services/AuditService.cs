using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IAuditService
{
    void Stage(string action, string entityType, Guid? entityId, string? details);
    Task<PagedResult<AuditLogDto>> ListAsync(int page, int pageSize, CancellationToken ct);
}

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public AuditService(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public void Stage(string action, string entityType, Guid? entityId, string? details)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _current.UserId == Guid.Empty ? null : _current.UserId,
            UserName = string.IsNullOrWhiteSpace(_current.Name) ? null : _current.Name,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<PagedResult<AuditLogDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        _current.EnsureLead();
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 30 : pageSize;

        var query = _db.AuditLogs.AsNoTracking().OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                UserId = x.UserId,
                UserName = x.UserName,
                Action = x.Action,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                Details = x.Details,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<AuditLogDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }
}
