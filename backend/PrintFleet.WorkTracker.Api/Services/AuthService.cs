using Microsoft.EntityFrameworkCore;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<UserDto> MeAsync(CancellationToken ct);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _tokens;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext db, IJwtTokenService tokens, ICurrentUser current, IAuditService audit, ILogger<AuthService> logger)
    {
        _db = db;
        _tokens = tokens;
        _current = current;
        _audit = audit;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(x => x.Department).FirstOrDefaultAsync(x => x.Email == email, ct);
        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login for {Email}", email);
            throw new AppException("Email or password is incorrect.", StatusCodes.Status401Unauthorized);
        }

        var (token, expires) = _tokens.CreateToken(user);
        _logger.LogInformation("User {Email} signed in", user.Email);
        return new LoginResponse { Token = token, ExpiresAt = expires, User = UserMapper.ToDto(user) };
    }

    public async Task<UserDto> MeAsync(CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == _current.UserId && x.IsActive, ct)
            ?? throw new AppException("Account was not found.", StatusCodes.Status401Unauthorized);
        return UserMapper.ToDto(user);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == _current.UserId, ct)
            ?? throw new AppException("Account was not found.", StatusCodes.Status401Unauthorized);

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new AppException("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        _audit.Stage(AuditActions.Updated, "User", user.Id, "Changed password");
        await _db.SaveChangesAsync(ct);
    }
}

public static class UserMapper
{
    public static UserDto ToDto(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        DepartmentId = user.DepartmentId,
        DepartmentName = user.Department?.Name,
        Role = user.Role,
        JobTitle = user.JobTitle,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt
    };
}
