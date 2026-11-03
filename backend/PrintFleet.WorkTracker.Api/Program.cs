using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PrintFleet.WorkTracker.Api.Authentication;
using PrintFleet.WorkTracker.Api.Data;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Middleware;
using PrintFleet.WorkTracker.Api.Services;
using PrintFleet.WorkTracker.Api.Validators;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Set ConnectionStrings__DefaultConnection before starting the API.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Set Jwt__Key before starting the API.");
if (builder.Environment.IsProduction() && jwtKey.Contains("DEV_ONLY", StringComparison.Ordinal))
    throw new InvalidOperationException("Replace the development JWT key before running in Production.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers(options => options.Filters.Add(new ValidationActionFilter()));
builder.Services.AddScoped<IValidator<SaveDepartmentRequest>, SaveDepartmentRequestValidator>();
builder.Services.AddScoped<IValidator<AssignDepartmentMemberRequest>, AssignDepartmentMemberRequestValidator>();
builder.Services.AddScoped<IValidator<SaveDepartmentTeamRequest>, SaveDepartmentTeamRequestValidator>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>();
builder.Services.AddScoped<IValidator<SaveUserRequest>, SaveUserRequestValidator>();
builder.Services.AddScoped<IValidator<SaveProjectRequest>, SaveProjectRequestValidator>();
builder.Services.AddScoped<IValidator<SaveDepartmentPlanRequest>, SaveDepartmentPlanRequestValidator>();
builder.Services.AddScoped<IValidator<SetOrganizationRoleRequest>, SetOrganizationRoleRequestValidator>();
builder.Services.AddScoped<IValidator<EnsureSheetRequest>, EnsureSheetRequestValidator>();
builder.Services.AddScoped<IValidator<SaveTaskRequest>, SaveTaskRequestValidator>();
builder.Services.AddScoped<IValidator<SaveDailyEntryRequest>, SaveDailyEntryRequestValidator>();
builder.Services.AddScoped<IValidator<SubmitDayRequest>, SubmitDayRequestValidator>();
builder.Services.AddScoped<IValidator<MarkDayRequest>, MarkDayRequestValidator>();
builder.Services.AddScoped<IValidator<ReopenDayRequest>, ReopenDayRequestValidator>();
builder.Services.AddScoped<IValidator<AddTeamMemberRequest>, AddTeamMemberRequestValidator>();
builder.Services.AddScoped<IValidator<InviteTeamMemberRequest>, InviteTeamMemberRequestValidator>();
builder.Services.AddScoped<IValidator<AcceptInvitationRequest>, AcceptInvitationRequestValidator>();
builder.Services.AddScoped<IValidator<SendTeamNoticeRequest>, SendTeamNoticeRequestValidator>();
builder.Services.AddScoped<IValidator<SaveWorkCategoryRequest>, SaveWorkCategoryRequestValidator>();
builder.Services.AddScoped<IValidator<SaveInitiativeRequest>, SaveInitiativeRequestValidator>();
builder.Services.AddScoped<IValidator<SaveMilestoneRequest>, SaveMilestoneRequestValidator>();
builder.Services.AddScoped<IValidator<SaveResearchRecordRequest>, SaveResearchRecordRequestValidator>();
builder.Services.AddScoped<IValidator<CreateCeoRequest>, CreateCeoRequestValidator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IDepartmentAccess, DepartmentAccess>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IDepartmentPlanService, DepartmentPlanService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IOrganizationAccessService, OrganizationAccessService>();
builder.Services.AddScoped<IMonthlyWorkService, MonthlyWorkService>();
builder.Services.AddScoped<IDailyWorkService, DailyWorkService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITeamLeadService, TeamLeadService>();
builder.Services.AddScoped<IWorkCategoryService, WorkCategoryService>();
builder.Services.AddScoped<IInitiativeService, InitiativeService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<GraphMailOptions>(builder.Configuration.GetSection("Email:Graph"));
builder.Services.AddHttpClient("graph");
builder.Services.AddSingleton<SmtpEmailSender>();
builder.Services.AddSingleton<IGraphMailbox, GraphMailbox>();
builder.Services.AddSingleton<IEmailSender, RoutingEmailSender>();
builder.Services.AddScoped<PlanReminderCatalog>();
builder.Services.AddScoped<IPlanReminderAdminService, PlanReminderAdminService>();
builder.Services.AddHostedService<DailyReminderService>();
builder.Services.AddHostedService<PlanReminderService>();
builder.Services.AddHostedService<TeamMailDispatcher>();

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    policy.SetIsOriginAllowed(origin => IsFrontendOrigin(origin, origins))
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddOpenApi();

var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var designTime = AppDomain.CurrentDomain.GetAssemblies()
    .Any(assembly => assembly.GetName().Name == "Microsoft.EntityFrameworkCore.Design");
if (!designTime)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, app.Environment.IsDevelopment() || app.Configuration.GetValue("Seed:Enabled", false));
    if (app.Environment.IsDevelopment())
    {
        app.Logger.LogInformation("Development logins use password {Password}. Team Lead: lead@company.com", DbSeeder.DevPassword);
    }
}

app.Run();

static bool IsFrontendOrigin(string origin, string[] configured)
{
    if (configured.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
    if (uri.Scheme is not "http" and not "https") return false;

    var host = uri.IdnHost;
    if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host is "127.0.0.1" or "::1")
        return true;
    if (!IPAddress.TryParse(host, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
        return !host.Contains('.');

    var bytes = ip.GetAddressBytes();
    return bytes[0] == 10
        || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
        || (bytes[0] == 192 && bytes[1] == 168);
}
