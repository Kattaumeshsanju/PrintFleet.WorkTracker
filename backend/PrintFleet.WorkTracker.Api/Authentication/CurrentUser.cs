using System.Security.Claims;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Authentication;

public interface ICurrentUser
{
    Guid UserId { get; }
    string Name { get; }
    string Email { get; }
    string Role { get; }
    bool IsTeamLead { get; }
    void EnsureLead();
}

public class CurrentUser : ICurrentUser
{
    public CurrentUser(IHttpContextAccessor accessor)
    {
        var principal = accessor.HttpContext?.User;
        var rawId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(rawId, out var id)) UserId = id;
        Name = principal?.FindFirstValue(ClaimTypes.Name) ?? "";
        Email = principal?.FindFirstValue(ClaimTypes.Email) ?? "";
        Role = principal?.FindFirstValue(ClaimTypes.Role) ?? "";
    }

    public Guid UserId { get; }
    public string Name { get; }
    public string Email { get; }
    public string Role { get; }
    public bool IsTeamLead => Role == Roles.TeamLead;

    public void EnsureLead()
    {
        if (!IsTeamLead)
            throw new AppException("Only the Team Lead can do that.", StatusCodes.Status403Forbidden);
    }
}
