using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface ITeamLeadService
{
    Task<MyTeamDto> GetAsync(CancellationToken ct);
    Task<IReadOnlyList<TeamPersonDto>> SearchAsync(string? query, CancellationToken ct);
    Task AddExistingAsync(AddTeamMemberRequest request, CancellationToken ct);
    Task<TeamInviteDto> InviteAsync(InviteTeamMemberRequest request, CancellationToken ct);
    Task RevokeInviteAsync(Guid id, CancellationToken ct);
    Task AcceptInviteAsync(AcceptInvitationRequest request, CancellationToken ct);
    Task RemoveAsync(Guid userId, CancellationToken ct);
    Task<TeamNoticeHistoryDto> SendAsync(SendTeamNoticeRequest request, CancellationToken ct);
    Task<IReadOnlyList<TeamNoticeHistoryDto>> HistoryAsync(CancellationToken ct);
    Task<IReadOnlyList<TeamNoticeHistoryDto>> OrganizationHistoryAsync(CancellationToken ct);
    Task<bool> RetryAsync(Guid noticeId, CancellationToken ct);
    Task<int> DispatchDueAsync(CancellationToken ct);
}

public class TeamLeadService : ITeamLeadService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;
    private readonly IEmailSender _email;
    private readonly EmailOptions _options;
    private readonly ILogger<TeamLeadService> _logger;

    public TeamLeadService(AppDbContext db, ICurrentUser current, IAuditService audit, IEmailSender email, IOptions<EmailOptions> options, ILogger<TeamLeadService> logger)
    {
        _db = db;
        _current = current;
        _audit = audit;
        _email = email;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MyTeamDto> GetAsync(CancellationToken ct)
    {
        var me = await MeAsync(ct);
        if (me.DepartmentId is null)
            throw new AppException("This page is for people assigned to a department.", StatusCodes.Status403Forbidden);
        var department = await _db.Departments.AsNoTracking().FirstAsync(x => x.Id == me.DepartmentId, ct);
        var members = await _db.Users.AsNoTracking()
            .Where(x => x.DepartmentId == department.Id && x.Role != Roles.Admin && x.Role != Roles.Ceo)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        var pending = await PendingInvites(department.Id).ToListAsync(ct);
        var projects = await _db.Projects.AsNoTracking()
            .Where(x => x.DepartmentId == department.Id && (x.Status == ProjectStatuses.Active || x.Status == ProjectStatuses.OnHold))
            .OrderBy(x => x.Name)
            .Select(x => new TeamProjectOptionDto { Id = x.Id, Name = x.Name })
            .ToListAsync(ct);
        var lead = members.FirstOrDefault(x => x.IsActive && x.Role == Roles.TeamLead);
        var manage = me.Role == Roles.TeamLead;
        var people = members.Select(Person).ToList();
        foreach (var person in people) person.DepartmentName = department.Name;
        return new MyTeamDto
        {
            DepartmentId = department.Id,
            DepartmentName = department.Name,
            TeamLeadName = lead?.Name,
            ActiveMembers = members.Count(x => x.IsActive),
            PendingInvitations = pending.Count,
            ActiveProjects = await _db.Projects.CountAsync(x => x.DepartmentId == department.Id && x.Status == ProjectStatuses.Active, ct),
            CanManage = manage,
            Members = people,
            Invitations = manage ? pending.Select(Invite).ToList() : [],
            Projects = manage ? projects : []
        };
    }

    public async Task<IReadOnlyList<TeamPersonDto>> SearchAsync(string? query, CancellationToken ct)
    {
        await RequireLeadAsync(ct);
        var term = (query ?? "").Trim().ToLowerInvariant();
        if (term.Length < 2) return [];
        var people = await _db.Users.AsNoTracking()
            .Include(x => x.Department)
            .Where(x => x.Role != Roles.Admin && x.Role != Roles.Ceo && (x.Name.ToLower().Contains(term) || x.Email.ToLower().Contains(term)))
            .OrderBy(x => x.Name)
            .Take(20)
            .ToListAsync(ct);
        return people.Select(Person).ToList();
    }

    public async Task AddExistingAsync(AddTeamMemberRequest request, CancellationToken ct)
    {
        var (me, department) = await RequireLeadAsync(ct);
        var person = await _db.Users.Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == request.UserId, ct)
            ?? throw new AppException("Employee was not found.", StatusCodes.Status404NotFound);
        if (person.Id == me.Id)
            throw new AppException("You are already on this team.");
        if (person.DepartmentId == department.Id)
            throw new AppException("That person is already on this team.", StatusCodes.Status409Conflict);
        if (TeamNoticeRules.RequiresAdminTransfer(person.DepartmentId, department.Id))
            throw new AppException($"An admin must transfer {person.Name} from {person.Department?.Name ?? "their department"}.", StatusCodes.Status409Conflict);
        if (!TeamNoticeRules.CanAddExisting(person.Role, person.IsActive, person.DepartmentId, department.Id))
            throw new AppException("That account cannot be added to a department.", StatusCodes.Status403Forbidden);

        person.DepartmentId = department.Id;
        if (person.Role != Roles.TeamMember) person.Role = Roles.TeamMember;
        _audit.Stage(AuditActions.Updated, "User", person.Id, $"Added {person.Name} to {department.Name}");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TeamInviteDto> InviteAsync(InviteTeamMemberRequest request, CancellationToken ct)
    {
        var (me, department) = await RequireLeadAsync(ct);
        if (!_options.CanSend)
            throw new AppException("Email is not configured, so the invitation was not sent.");
        var email = request.Email.Trim().ToLowerInvariant();
        if (!MailboxAddress.TryParse(email, out _))
            throw new AppException("Enter a valid work email.");
        if (await _db.Users.AnyAsync(x => x.Email == email, ct))
            throw new AppException("That email already has an account. Add the existing employee instead.", StatusCodes.Status409Conflict);
        var now = DateTime.UtcNow;
        var existing = await _db.TeamInvitations.Where(x => x.Email == email && x.Status == InvitationStatuses.Pending).ToListAsync(ct);
        var expired = false;
        foreach (var invite in existing.Where(x => x.ExpiresAt <= now))
        {
            invite.Status = InvitationStatuses.Expired;
            expired = true;
        }
        if (expired) await _db.SaveChangesAsync(ct);
        if (existing.Any(x => x.Status == InvitationStatuses.Pending))
            throw new AppException("An invitation for that email is already waiting.", StatusCodes.Status409Conflict);

        var token = TeamNoticeRules.NewToken();
        var invitation = new TeamInvitation
        {
            Id = Guid.NewGuid(),
            DepartmentId = department.Id,
            Name = request.Name.Trim(),
            Email = email,
            JobTitle = WorkMetrics.Clean(request.JobTitle),
            TokenHash = TeamNoticeRules.HashToken(token),
            Status = InvitationStatuses.Pending,
            InvitedBy = me.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(TeamNoticeRules.InvitationDays),
            CreatedAt = DateTime.UtcNow
        };
        _db.TeamInvitations.Add(invitation);
        _audit.Stage(AuditActions.Created, "TeamInvitation", invitation.Id, $"Invited {invitation.Name} to {department.Name}");
        await _db.SaveChangesAsync(ct);

        var link = $"{_options.SiteUrl.TrimEnd('/')}/join?token={token}";
        var text = $"Hello {invitation.Name},\n\n{me.Name} invited you to the {department.Name} team in WorkTracker.\n\nSet your password here (this link expires in 7 days):\n{link}\n\nIf you were not expecting this, ignore the email.";
        try
        {
            await _email.SendRichAsync(email, $"Invitation to {department.Name}", text, TeamNoticeRules.Html(text), me.Email, me.Name, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Invitation mail failed with {ErrorType}", ex.GetType().Name);
            invitation.Status = InvitationStatuses.Revoked;
            await _db.SaveChangesAsync(ct);
            throw new AppException("The invitation email could not be sent. Nothing is waiting for that address.");
        }
        return Invite(invitation);
    }

    public async Task RevokeInviteAsync(Guid id, CancellationToken ct)
    {
        var (_, department) = await RequireLeadAsync(ct);
        var invitation = await _db.TeamInvitations.FirstOrDefaultAsync(x => x.Id == id && x.DepartmentId == department.Id, ct)
            ?? throw new AppException("Invitation was not found.", StatusCodes.Status404NotFound);
        if (invitation.Status == InvitationStatuses.Accepted)
            throw new AppException("That invitation was already accepted.");
        invitation.Status = InvitationStatuses.Revoked;
        _audit.Stage(AuditActions.Updated, "TeamInvitation", invitation.Id, $"Revoked invitation for {invitation.Name}");
        await _db.SaveChangesAsync(ct);
    }

    public async Task AcceptInviteAsync(AcceptInvitationRequest request, CancellationToken ct)
    {
        var hash = TeamNoticeRules.HashToken(request.Token.Trim());
        var invitation = await _db.TeamInvitations.FirstOrDefaultAsync(x => x.TokenHash == hash, ct)
            ?? throw new AppException("This invitation is no longer valid.");
        if (invitation.Status != InvitationStatuses.Pending || invitation.ExpiresAt <= DateTime.UtcNow)
        {
            if (invitation.Status == InvitationStatuses.Pending) invitation.Status = InvitationStatuses.Expired;
            await _db.SaveChangesAsync(ct);
            throw new AppException("This invitation is no longer valid.");
        }
        if (await _db.Users.AnyAsync(x => x.Email == invitation.Email, ct))
            throw new AppException("An account with that email already exists. Sign in instead.", StatusCodes.Status409Conflict);

        var user = new User
        {
            Id = Guid.NewGuid(),
            DepartmentId = invitation.DepartmentId,
            Name = invitation.Name,
            Email = invitation.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = Roles.TeamMember,
            JobTitle = invitation.JobTitle,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        invitation.Status = InvitationStatuses.Accepted;
        invitation.AcceptedUserId = user.Id;
        _db.Users.Add(user);
        _audit.Stage(AuditActions.Created, "User", user.Id, $"Accepted invitation for {user.Name}");
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid userId, CancellationToken ct)
    {
        var (me, department) = await RequireLeadAsync(ct);
        var person = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.DepartmentId == department.Id, ct)
            ?? throw new AppException("That person is not on your team.", StatusCodes.Status404NotFound);
        if (!TeamNoticeRules.CanRemoveMember(me.Id, person.Id, person.Role))
            throw new AppException("You cannot remove yourself or the team lead. Ask an admin to change the team lead.", StatusCodes.Status403Forbidden);
        person.DepartmentId = null;
        _audit.Stage(AuditActions.Updated, "User", person.Id, $"Removed {person.Name} from {department.Name}");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TeamNoticeHistoryDto> SendAsync(SendTeamNoticeRequest request, CancellationToken ct)
    {
        var (me, department) = await RequireLeadAsync(ct);
        if (!NoticeCategories.All.Contains(request.Category))
            throw new AppException("Choose a notification category.");
        if (!NoticeChannels.All.Contains(request.Channel))
            throw new AppException("Choose email, in-app, or both.");
        var title = TeamNoticeRules.Subject(request.Title);
        var body = TeamNoticeRules.Body(request.Body);
        if (title.Length == 0 || body.Length == 0)
            throw new AppException("Enter a subject and a message.");

        var since = DateTime.UtcNow.AddHours(-1);
        var sentThisHour = await _db.TeamNotices.CountAsync(x => x.SenderId == me.Id && x.CreatedAt >= since, ct);
        if (sentThisHour >= TeamNoticeRules.MaxSendsPerHour)
            throw new AppException("You have sent too many team messages this hour. Wait and try again.", StatusCodes.Status429TooManyRequests);

        var duplicateSince = DateTime.UtcNow.AddSeconds(-TeamNoticeRules.DuplicateWindowSeconds);
        var duplicate = await _db.TeamNotices
            .Include(x => x.Recipients).ThenInclude(x => x.Delivery)
            .Include(x => x.Sender)
            .Where(x => x.SenderId == me.Id && x.Title == title && x.Body == body && x.CreatedAt >= duplicateSince)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (duplicate is not null) return HistoryItem(duplicate);

        var members = await _db.Users.AsNoTracking()
            .Where(x => x.DepartmentId == department.Id && x.IsActive && (x.Role == Roles.TeamLead || x.Role == Roles.TeamMember))
            .Select(x => new { x.Id, x.Email, x.Name })
            .ToListAsync(ct);
        var allowed = members.Select(x => x.Id).ToHashSet();
        var recipientIds = TeamNoticeRules.ResolveRecipients(allowed, request.UserIds, request.Everyone, request.CopyToMe, me.Id);
        Guid? projectDepartment = null;
        if (request.ProjectId is Guid projectId)
            projectDepartment = await _db.Projects.Where(x => x.Id == projectId).Select(x => x.DepartmentId).FirstOrDefaultAsync(ct);
        var link = TeamNoticeRules.Link(request.LinkPlan, request.ProjectId, projectDepartment, department.Id);
        var wantsEmail = request.Channel is NoticeChannels.Email or NoticeChannels.Both;
        var now = DateTime.UtcNow;
        var notice = new TeamNotice
        {
            Id = Guid.NewGuid(),
            DepartmentId = department.Id,
            SenderId = me.Id,
            Title = title,
            Body = body,
            Category = request.Category,
            Channel = request.Channel,
            ProjectId = request.ProjectId,
            LinkPath = link,
            CreatedAt = now
        };
        foreach (var id in recipientIds)
        {
            var person = members.First(x => x.Id == id);
            var row = new TeamNoticeRecipient
            {
                Id = Guid.NewGuid(),
                NoticeId = notice.Id,
                UserId = person.Id,
                Email = person.Email
            };
            if (wantsEmail)
            {
                row.Delivery = new EmailDelivery
                {
                    Id = Guid.NewGuid(),
                    RecipientId = row.Id,
                    Status = EmailDeliveryStatuses.Pending,
                    NextAttemptAt = now,
                    UpdatedAt = now
                };
            }
            notice.Recipients.Add(row);
        }
        _db.TeamNotices.Add(notice);
        _audit.Stage(AuditActions.Created, "TeamNotice", notice.Id, $"Sent {title} to {recipientIds.Count} people");
        await _db.SaveChangesAsync(ct);
        notice.Sender = me;
        return HistoryItem(notice);
    }

    public async Task<IReadOnlyList<TeamNoticeHistoryDto>> HistoryAsync(CancellationToken ct)
    {
        var (_, department) = await RequireLeadAsync(ct);
        var notices = await _db.TeamNotices.AsNoTracking()
            .Include(x => x.Sender)
            .Include(x => x.Recipients).ThenInclude(x => x.Delivery)
            .Where(x => x.DepartmentId == department.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return notices.Select(HistoryItem).ToList();
    }

    public async Task<IReadOnlyList<TeamNoticeHistoryDto>> OrganizationHistoryAsync(CancellationToken ct)
    {
        var me = await MeAsync(ct);
        if (me.Role != Roles.Admin)
            throw new AppException("Only the admin can view every department.", StatusCodes.Status403Forbidden);
        var notices = await _db.TeamNotices.AsNoTracking()
            .Include(x => x.Sender)
            .Include(x => x.Recipients).ThenInclude(x => x.Delivery)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return notices.Select(HistoryItem).ToList();
    }

    public async Task<bool> RetryAsync(Guid noticeId, CancellationToken ct)
    {
        var me = await MeAsync(ct);
        var notice = await _db.TeamNotices.Include(x => x.Recipients).ThenInclude(x => x.Delivery)
            .FirstOrDefaultAsync(x => x.Id == noticeId, ct)
            ?? throw new AppException("Message was not found.", StatusCodes.Status404NotFound);
        var allowed = me.Role == Roles.Admin || (me.Role == Roles.TeamLead && me.DepartmentId == notice.DepartmentId);
        if (!allowed)
            throw new AppException("You can retry messages only for your own department.", StatusCodes.Status403Forbidden);
        var now = DateTime.UtcNow;
        var queued = false;
        foreach (var delivery in notice.Recipients.Select(x => x.Delivery).Where(x => x is not null))
            queued |= TeamNoticeRules.QueueRetry(delivery!, now);
        if (queued) await _db.SaveChangesAsync(ct);
        return queued;
    }

    public async Task<int> DispatchDueAsync(CancellationToken ct)
    {
        if (!_email.CanSend) return 0;
        var now = DateTime.UtcNow;
        var due = await _db.EmailDeliveries
            .Include(x => x.Recipient).ThenInclude(x => x.Notice).ThenInclude(x => x.Sender)
            .Where(x => x.Status == EmailDeliveryStatuses.Pending && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.UpdatedAt)
            .Take(20)
            .ToListAsync(ct);
        var sent = 0;
        foreach (var delivery in due)
        {
            if (delivery.Status == EmailDeliveryStatuses.SentToProvider) continue;
            var recipient = delivery.Recipient;
            var notice = recipient.Notice;
            var text = $"{notice.Body}\n\n{notice.Sender.Name} sent this to your team in WorkTracker.";
            try
            {
                if (!MailboxAddress.TryParse(recipient.Email, out _))
                    throw new InvalidAddressException();
                await _email.SendRichAsync(recipient.Email, notice.Title, text, TeamNoticeRules.Html(text), notice.Sender.Email, notice.Sender.Name, ct);
                TeamNoticeRules.RecordAttempt(delivery, true, null, false, DateTime.UtcNow);
                sent++;
            }
            catch (Exception ex)
            {
                var graph = ex as GraphMailException;
                var permanent = graph is { Transient: false } || ex is InvalidAddressException || ex is ParseException || (ex is SmtpCommandException smtp && (int)smtp.StatusCode >= 500);
                var errorType = graph?.ErrorType
                    ?? (ex is InvalidAddressException or ParseException ? "InvalidAddress" : permanent ? "Permanent" : ex.GetType().Name);
                TeamNoticeRules.RecordAttempt(delivery, false, errorType, permanent, DateTime.UtcNow);
                _logger.LogWarning("Team mail {DeliveryId} failed with {ErrorType}", delivery.Id, errorType);
            }
            await _db.SaveChangesAsync(ct);
        }
        return sent;
    }

    private static TeamNoticeHistoryDto HistoryItem(TeamNotice notice)
    {
        var email = notice.Channel is NoticeChannels.Email or NoticeChannels.Both;
        var statuses = notice.Recipients.Where(x => x.Delivery is not null).Select(x => x.Delivery!.Status);
        return new TeamNoticeHistoryDto
        {
            Id = notice.Id,
            Title = notice.Title,
            Body = notice.Body,
            Category = notice.Category,
            SenderName = notice.Sender?.Name ?? "",
            CreatedAt = notice.CreatedAt,
            RecipientCount = notice.Recipients.Count,
            Channel = notice.Channel,
            Status = TeamNoticeRules.Overall(statuses, email),
            FailedRecipients = notice.Recipients.Where(x => x.Delivery is not null && x.Delivery.Status is EmailDeliveryStatuses.Failed or EmailDeliveryStatuses.Bounced).Select(x => x.Email).ToList(),
            CanRetry = notice.Recipients.Any(x => x.Delivery is not null && TeamNoticeRules.CanRetry(x.Delivery.Status, x.Delivery.Attempts, x.Delivery.ErrorType))
        };
    }

    private async Task<(User Me, Department Department)> RequireLeadAsync(CancellationToken ct)
    {
        var me = await MeAsync(ct);
        if (me.Role != Roles.TeamLead || me.DepartmentId is null)
            throw new AppException("Only the team lead of a department can do that.", StatusCodes.Status403Forbidden);
        var department = await _db.Departments.FirstAsync(x => x.Id == me.DepartmentId, ct);
        if (!department.IsActive)
            throw new AppException("That department is inactive.");
        return (me, department);
    }

    private async Task<User> MeAsync(CancellationToken ct)
        => await _db.Users.FirstOrDefaultAsync(x => x.Id == _current.UserId, ct)
            ?? throw new AppException("Your account was not found.", StatusCodes.Status401Unauthorized);

    private IQueryable<TeamInvitation> PendingInvites(Guid departmentId)
        => _db.TeamInvitations.AsNoTracking().Where(x => x.DepartmentId == departmentId && x.Status == InvitationStatuses.Pending && x.ExpiresAt > DateTime.UtcNow);

    private static TeamPersonDto Person(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        JobTitle = user.JobTitle,
        Role = user.Role,
        IsActive = user.IsActive,
        DepartmentId = user.DepartmentId,
        DepartmentName = user.Department?.Name
    };

    private static TeamInviteDto Invite(TeamInvitation invitation) => new()
    {
        Id = invitation.Id,
        Name = invitation.Name,
        Email = invitation.Email,
        JobTitle = invitation.JobTitle,
        Status = invitation.Status,
        ExpiresAt = invitation.ExpiresAt
    };

    private sealed class InvalidAddressException : Exception;
}
