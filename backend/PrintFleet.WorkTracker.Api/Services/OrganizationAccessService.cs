using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IOrganizationAccessService
{
    Task<OrganizationAccessDto> GetAsync(CancellationToken ct);
    Task<CeoInvitationDto> InviteCeoAsync(CreateCeoRequest request, CancellationToken ct);
    Task RevokeInvitationAsync(Guid id, CancellationToken ct);
    Task AcceptAsync(AcceptInvitationRequest request, CancellationToken ct);
    Task<OrganizationPersonDto> SetPermissionAsync(Guid userId, string permission, bool granted, CancellationToken ct);
    Task<OrganizationPersonDto> SetCeoStatusAsync(Guid userId, bool isActive, CancellationToken ct);
}

public class OrganizationAccessService : IOrganizationAccessService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IDepartmentAccess _access;
    private readonly IAuditService _audit;
    private readonly IEmailSender _email;
    private readonly EmailOptions _options;
    private readonly ILogger<OrganizationAccessService> _logger;

    public OrganizationAccessService(AppDbContext db, ICurrentUser current, IDepartmentAccess access, IAuditService audit, IEmailSender email, IOptions<EmailOptions> options, ILogger<OrganizationAccessService> logger)
    {
        _db = db;
        _current = current;
        _access = access;
        _audit = audit;
        _email = email;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OrganizationAccessDto> GetAsync(CancellationToken ct)
    {
        await RequireAdminAsync(ct);
        var people = await _db.Users.AsNoTracking().Include(x => x.Department).OrderBy(x => x.Name).ToListAsync(ct);
        var grants = await _db.UserPermissions.AsNoTracking().ToListAsync(ct);
        var now = DateTime.UtcNow;
        var invitations = await _db.CeoInvitations.AsNoTracking()
            .Where(x => x.Status == InvitationStatuses.Pending && x.ExpiresAt > now)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return new OrganizationAccessDto
        {
            People = people.Select(person => Person(person, grants.Where(x => x.UserId == person.Id).Select(x => x.Permission).ToList())).ToList(),
            Invitations = invitations.Select(Invite).ToList()
        };
    }

    public async Task<CeoInvitationDto> InviteCeoAsync(CreateCeoRequest request, CancellationToken ct)
    {
        await RequireAdminAsync(ct);
        if (!_options.CanSend)
            throw new AppException("Email is not configured, so the activation link cannot be sent.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(x => x.Email == email, ct))
            throw new AppException("An account with that email already exists.", StatusCodes.Status409Conflict);

        var pending = await _db.CeoInvitations.Where(x => x.Email == email && x.Status == InvitationStatuses.Pending).ToListAsync(ct);
        foreach (var old in pending) old.Status = InvitationStatuses.Expired;

        var token = TeamNoticeRules.NewToken();
        var invitation = new CeoInvitation
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = email,
            JobTitle = WorkMetrics.Clean(request.JobTitle),
            ActivateOnAccept = request.IsActive,
            TokenHash = TeamNoticeRules.HashToken(token),
            Status = InvitationStatuses.Pending,
            InvitedById = _current.UserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(TeamNoticeRules.InvitationDays)
        };
        _db.CeoInvitations.Add(invitation);
        _audit.Stage(AuditActions.Created, "CeoInvitation", invitation.Id, $"Invited {invitation.Name} as CEO");
        await _db.SaveChangesAsync(ct);

        var link = $"{_options.SiteUrl.TrimEnd('/')}/activate?token={token}";
        var statusLine = invitation.ActivateOnAccept
            ? "After you set a password, the account is active with read-only organization access."
            : "After you set a password, an admin still needs to activate the account before you can sign in.";
        var text = $"Hello {invitation.Name},\n\n{_current.Name} invited you to WorkTracker as CEO.\n\nSet your password here (this link expires in 7 days):\n{link}\n\n{statusLine}\n\nThis role does not grant admin access. If you were not expecting this, ignore the email.";
        try
        {
            await _email.SendRichAsync(email, "WorkTracker CEO account", text, TeamNoticeRules.Html(text), null, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("CEO invitation mail failed with {ErrorType}", ex.GetType().Name);
            invitation.Status = InvitationStatuses.Revoked;
            await _db.SaveChangesAsync(ct);
            throw new AppException("The activation email could not be sent. The invitation was not left waiting.");
        }
        return Invite(invitation);
    }

    public async Task RevokeInvitationAsync(Guid id, CancellationToken ct)
    {
        await RequireAdminAsync(ct);
        var invitation = await _db.CeoInvitations.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Invitation was not found.", StatusCodes.Status404NotFound);
        if (invitation.Status == InvitationStatuses.Accepted)
            throw new AppException("That invitation was already accepted.");
        invitation.Status = InvitationStatuses.Revoked;
        _audit.Stage(AuditActions.Updated, "CeoInvitation", invitation.Id, $"Revoked CEO invitation for {invitation.Name}");
        await _db.SaveChangesAsync(ct);
    }

    public async Task AcceptAsync(AcceptInvitationRequest request, CancellationToken ct)
    {
        var hash = TeamNoticeRules.HashToken(request.Token.Trim());
        var invitation = await _db.CeoInvitations.FirstOrDefaultAsync(x => x.TokenHash == hash, ct)
            ?? throw new AppException("This activation link is not valid.");
        if (invitation.Status != InvitationStatuses.Pending || invitation.ExpiresAt <= DateTime.UtcNow)
        {
            if (invitation.Status == InvitationStatuses.Pending) invitation.Status = InvitationStatuses.Expired;
            await _db.SaveChangesAsync(ct);
            throw new AppException("This activation link has expired.");
        }
        if (await _db.Users.AnyAsync(x => x.Email == invitation.Email, ct))
            throw new AppException("An account with that email already exists.", StatusCodes.Status409Conflict);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = invitation.Name,
            Email = invitation.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = Roles.Ceo,
            JobTitle = invitation.JobTitle,
            IsActive = invitation.ActivateOnAccept,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        invitation.Status = InvitationStatuses.Accepted;
        _audit.Stage(AuditActions.Created, "User", user.Id, $"Created CEO account for {user.Name}");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<OrganizationPersonDto> SetPermissionAsync(Guid userId, string permission, bool granted, CancellationToken ct)
    {
        await RequireAdminAsync(ct);
        if (!OrganizationPermissions.Assignable.Contains(permission))
            throw new AppException("That permission is not available.");
        if (userId == _current.UserId)
            throw new AppException("You cannot change your own organization access.");

        var user = await _db.Users.Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new AppException("That person was not found.", StatusCodes.Status404NotFound);
        if (!AccessRules.CanReceiveGrant(user.Role))
            throw new AppException("Admin and CEO access comes from their role. It is not an extra permission.");

        var row = await _db.UserPermissions.FirstOrDefaultAsync(x => x.UserId == userId && x.Permission == permission, ct);
        if (granted && row is null)
        {
            _db.UserPermissions.Add(new UserPermission
            {
                UserId = userId,
                Permission = permission,
                GrantedById = _current.UserId,
                GrantedAt = DateTime.UtcNow
            });
            _audit.Stage(AuditActions.Updated, "User", user.Id, $"Granted {permission} to {user.Name}");
        }
        else if (!granted && row is not null)
        {
            _db.UserPermissions.Remove(row);
            _audit.Stage(AuditActions.Updated, "User", user.Id, $"Revoked {permission} from {user.Name}");
        }
        await _db.SaveChangesAsync(ct);
        var grants = await _db.UserPermissions.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.Permission).ToListAsync(ct);
        return Person(user, grants);
    }

    public async Task<OrganizationPersonDto> SetCeoStatusAsync(Guid userId, bool isActive, CancellationToken ct)
    {
        await RequireAdminAsync(ct);
        if (userId == _current.UserId)
            throw new AppException("You cannot change your own account.");
        var user = await _db.Users.Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new AppException("That person was not found.", StatusCodes.Status404NotFound);
        if (user.Role != Roles.Ceo)
            throw new AppException("Only a CEO account can be activated or deactivated here.");
        user.IsActive = isActive;
        _audit.Stage(AuditActions.Updated, "User", user.Id, isActive ? $"Activated CEO account for {user.Name}" : $"Deactivated CEO account for {user.Name}");
        await _db.SaveChangesAsync(ct);
        var grants = await _db.UserPermissions.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.Permission).ToListAsync(ct);
        return Person(user, grants);
    }

    private async Task RequireAdminAsync(CancellationToken ct)
    {
        if (!await _access.IsAdminAsync(ct))
            throw new AppException("Only an admin can manage organization access.", StatusCodes.Status403Forbidden);
    }

    private OrganizationPersonDto Person(User user, IReadOnlyCollection<string> grants) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        DepartmentName = user.Department?.Name,
        Role = user.Role,
        JobTitle = user.JobTitle,
        IsActive = user.IsActive,
        Overview = AccessRules.Overview(user.Role, grants.Contains(OrganizationPermissions.Overview)),
        Reports = AccessRules.Reports(user.Role, grants.Contains(OrganizationPermissions.Reports)),
        GrantsEditable = user.Id != _current.UserId && AccessRules.CanReceiveGrant(user.Role),
        StatusEditable = user.Id != _current.UserId && user.Role == Roles.Ceo
    };

    private static CeoInvitationDto Invite(CeoInvitation invitation) => new()
    {
        Id = invitation.Id,
        Name = invitation.Name,
        Email = invitation.Email,
        JobTitle = invitation.JobTitle,
        ActivateOnAccept = invitation.ActivateOnAccept,
        ExpiresAt = invitation.ExpiresAt
    };
}
