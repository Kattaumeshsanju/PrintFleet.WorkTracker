using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public static class TeamNoticeRules
{
    public const int InvitationDays = 7;
    public const int MaxSendsPerHour = 10;
    public const int MaxEmailAttempts = 3;
    public const int DuplicateWindowSeconds = 60;

    public static bool CanAddExisting(string personRole, bool personActive, Guid? personDepartmentId, Guid targetDepartmentId)
    {
        if (!personActive || personRole is Roles.Admin or Roles.Ceo) return false;
        return personDepartmentId is null || personDepartmentId == targetDepartmentId;
    }

    public static bool RequiresAdminTransfer(Guid? personDepartmentId, Guid targetDepartmentId)
        => personDepartmentId is Guid current && current != targetDepartmentId;

    public static bool CanRemoveMember(Guid actorId, Guid targetId, string targetRole)
        => actorId != targetId && targetRole == Roles.TeamMember;

    public static IReadOnlyList<Guid> ResolveRecipients(IReadOnlySet<Guid> departmentMemberIds, IReadOnlyCollection<Guid> requestedIds, bool everyone, bool copyToSelf, Guid senderId)
    {
        if (!everyone && requestedIds.Any(id => !departmentMemberIds.Contains(id)))
            throw new AppException("Choose people from your own department.", StatusCodes.Status403Forbidden);

        var chosen = (everyone ? departmentMemberIds : requestedIds)
            .Where(departmentMemberIds.Contains)
            .Distinct()
            .ToList();
        if (copyToSelf && departmentMemberIds.Contains(senderId) && !chosen.Contains(senderId))
            chosen.Add(senderId);
        if (chosen.Count == 0)
            throw new AppException("Choose at least one person on your team.");
        return chosen;
    }

    public static string Subject(string value)
    {
        var cleaned = new string((value ?? "").Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        cleaned = Regex.Replace(cleaned, " +", " ").Trim();
        return cleaned.Length <= 160 ? cleaned : cleaned[..160];
    }

    public static string Body(string value)
    {
        var stripped = Regex.Replace(value ?? "", "<[^>]*>", " ");
        stripped = stripped.Replace("\r\n", "\n").Replace('\r', '\n');
        var kept = new string(stripped.Where(c => c is '\n' or '\t' || !char.IsControl(c)).ToArray()).Trim();
        return kept.Length <= 4000 ? kept : kept[..4000];
    }

    public static string Html(string plain)
        => WebUtility.HtmlEncode(plain).Replace("\n", "<br>");

    public static string? Link(bool plan, Guid? projectId, Guid? projectDepartmentId, Guid departmentId)
    {
        if (projectId is not null)
        {
            if (projectDepartmentId != departmentId)
                throw new AppException("That project is not in your department.", StatusCodes.Status403Forbidden);
            return "/projects";
        }
        return plan ? "/plans" : null;
    }

    public static string Overall(IEnumerable<string> emailStatuses, bool emailRequested)
    {
        if (!emailRequested) return "InApp";
        var statuses = emailStatuses.ToList();
        if (statuses.Count == 0 || statuses.Any(status => status == EmailDeliveryStatuses.Pending)) return EmailDeliveryStatuses.Pending;
        if (statuses.All(status => status == EmailDeliveryStatuses.SentToProvider)) return EmailDeliveryStatuses.SentToProvider;
        if (statuses.All(status => status is EmailDeliveryStatuses.Failed or EmailDeliveryStatuses.Bounced)) return EmailDeliveryStatuses.Failed;
        return "Partial";
    }

    public static bool CanRetry(string status, int attempts, string? errorType)
        => status == EmailDeliveryStatuses.Failed
           && attempts < MaxEmailAttempts
           && errorType is not ("InvalidAddress" or "Permanent" or "Bounced");

    public static void RecordAttempt(EmailDelivery delivery, bool accepted, string? errorType, bool permanent, DateTime utcNow)
    {
        delivery.Attempts++;
        delivery.UpdatedAt = utcNow;
        delivery.NextAttemptAt = null;
        if (accepted)
        {
            delivery.Status = EmailDeliveryStatuses.SentToProvider;
            delivery.ErrorType = null;
            return;
        }

        delivery.ErrorType = string.IsNullOrWhiteSpace(errorType) ? "Failed" : errorType;
        delivery.Status = delivery.ErrorType == "Bounced" ? EmailDeliveryStatuses.Bounced : EmailDeliveryStatuses.Failed;
        if (permanent) delivery.Attempts = MaxEmailAttempts;
    }

    public static bool QueueRetry(EmailDelivery delivery, DateTime utcNow)
    {
        if (delivery.Status == EmailDeliveryStatuses.SentToProvider) return false;
        if (!CanRetry(delivery.Status, delivery.Attempts, delivery.ErrorType)) return false;
        delivery.Status = EmailDeliveryStatuses.Pending;
        delivery.NextAttemptAt = utcNow;
        delivery.UpdatedAt = utcNow;
        return true;
    }

    public static bool IsDuplicate(DateTime previousUtc, DateTime nowUtc)
        => nowUtc >= previousUtc && (nowUtc - previousUtc).TotalSeconds < DuplicateWindowSeconds;

    public static string NewToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
