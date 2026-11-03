using FluentValidation;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;

namespace PrintFleet.WorkTracker.Api.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(200);
    }
}

public class SaveDepartmentRequestValidator : AbstractValidator<SaveDepartmentRequest>
{
    public SaveDepartmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
    }
}

public class AssignDepartmentMemberRequestValidator : AbstractValidator<AssignDepartmentMemberRequest>
{
    public AssignDepartmentMemberRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role).Must(role => role is Roles.TeamLead or Roles.TeamMember).WithMessage("Choose Team Lead or Employee.");
    }
}

public class SaveDepartmentTeamRequestValidator : AbstractValidator<SaveDepartmentTeamRequest>
{
    public SaveDepartmentTeamRequestValidator()
    {
        RuleForEach(x => x.Members).ChildRules(member =>
        {
            member.RuleFor(x => x.UserId).NotEmpty();
            member.RuleFor(x => x.Role).Must(role => role is Roles.TeamLead or Roles.TeamMember)
                .WithMessage("Choose Team Lead or Employee.");
        });
        RuleForEach(x => x.NewMembers).ChildRules(member =>
        {
            member.RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            member.RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
            member.RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(200);
            member.RuleFor(x => x.JobTitle).MaximumLength(120);
            member.RuleFor(x => x.Role).Must(role => role is Roles.TeamLead or Roles.TeamMember)
                .WithMessage("Choose Team Lead or Employee.");
        });
    }
}

public class SaveUserRequestValidator : AbstractValidator<SaveUserRequest>
{
    public SaveUserRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.JobTitle).MaximumLength(120);
        RuleFor(x => x.Role).Must(role => Roles.All.Contains(role)).WithMessage("Choose a valid role.");
        RuleFor(x => x.Password).MinimumLength(8).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Password));
    }
}

public class SaveProjectRequestValidator : AbstractValidator<SaveProjectRequest>
{
    public SaveProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Status).Must(status => ProjectStatuses.All.Contains(status)).WithMessage("Choose a valid project status.");
        RuleFor(x => x.Priority).Must(priority => Priorities.All.Contains(priority)).WithMessage("Choose a valid priority.");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("Project end date must be on or after the start date.");
    }
}

public class SaveDepartmentPlanRequestValidator : AbstractValidator<SaveDepartmentPlanRequest>
{
    public SaveDepartmentPlanRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Status).Must(status => DepartmentPlanStatuses.All.Contains(status));
        RuleFor(x => x.Summary).MaximumLength(4000);
        RuleFor(x => x.Risks).MaximumLength(4000);
        RuleFor(x => x.Comments).MaximumLength(4000);
    }
}

public class SetOrganizationRoleRequestValidator : AbstractValidator<SetOrganizationRoleRequest>
{
    public SetOrganizationRoleRequestValidator()
    {
        RuleFor(x => x.Role).Must(role => role is Roles.Ceo or Roles.TeamMember)
            .WithMessage("Choose CEO or Employee.");
    }
}

public class EnsureSheetRequestValidator : AbstractValidator<EnsureSheetRequest>
{
    public EnsureSheetRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public class SaveTaskRequestValidator : AbstractValidator<SaveTaskRequest>
{
    public SaveTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Remarks).MaximumLength(2000);
        RuleFor(x => x.Priority).Must(priority => Priorities.All.Contains(priority)).WithMessage("Choose a valid priority.");
        RuleFor(x => x.Status).Must(status => TaskStatuses.All.Contains(status)).WithMessage("Choose a valid status.");
        RuleFor(x => x.EstimatedHours).InclusiveBetween(0, 1000);
        RuleFor(x => x.ProgressPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.PlannedEndDate).GreaterThanOrEqualTo(x => x.PlannedStartDate)
            .WithMessage("Planned end must be on or after the planned start.");
    }
}

public class SaveDailyEntryRequestValidator : AbstractValidator<SaveDailyEntryRequest>
{
    public SaveDailyEntryRequestValidator()
    {
        RuleFor(x => x.WorkDate).NotEmpty();
        RuleFor(x => x.MonthlyTaskId).NotEmpty().WithMessage("Select the related monthly task.");
        RuleFor(x => x.StartTime).NotEmpty().Matches(@"^\d{1,2}:\d{2}(:\d{2})?$").WithMessage("Enter the start time as HH:mm.");
        RuleFor(x => x.EndTime).NotEmpty().Matches(@"^\d{1,2}:\d{2}(:\d{2})?$").WithMessage("Enter the end time as HH:mm.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Remarks).MaximumLength(2000);
        RuleFor(x => x.Status).Must(status => EntryStatuses.All.Contains(status)).WithMessage("Choose a valid status.");
    }
}

public class MarkDayRequestValidator : AbstractValidator<MarkDayRequest>
{
    public MarkDayRequestValidator()
    {
        RuleFor(x => x.WorkDate).NotEmpty();
        RuleFor(x => x.DayType)
            .Must(type => string.IsNullOrWhiteSpace(type) || DayTypes.All.Contains(type))
            .WithMessage("Choose Holiday or Leave.");
    }
}

public class SubmitDayRequestValidator : AbstractValidator<SubmitDayRequest>
{
    public SubmitDayRequestValidator()
    {
        RuleFor(x => x.WorkDate).NotEmpty();
    }
}

public class ReopenDayRequestValidator : AbstractValidator<ReopenDayRequest>
{
    public ReopenDayRequestValidator()
    {
        RuleFor(x => x.WorkDate).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class AddTeamMemberRequestValidator : AbstractValidator<AddTeamMemberRequest>
{
    public AddTeamMemberRequestValidator() => RuleFor(x => x.UserId).NotEmpty();
}

public class InviteTeamMemberRequestValidator : AbstractValidator<InviteTeamMemberRequest>
{
    public InviteTeamMemberRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.JobTitle).MaximumLength(120);
    }
}

public class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(200);
    }
}

public class SendTeamNoticeRequestValidator : AbstractValidator<SendTeamNoticeRequest>
{
    public SendTeamNoticeRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.Category).NotEmpty();
        RuleFor(x => x.Channel).NotEmpty();
        RuleFor(x => x.UserIds).Must(ids => ids.Count <= 200);
    }
}

public class CreateCeoRequestValidator : AbstractValidator<CreateCeoRequest>
{
    public CreateCeoRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.JobTitle).MaximumLength(120);
    }
}

public class SaveWorkCategoryRequestValidator : AbstractValidator<SaveWorkCategoryRequest>
{
    public SaveWorkCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class SaveInitiativeRequestValidator : AbstractValidator<SaveInitiativeRequest>
{
    public SaveInitiativeRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Status).NotEmpty();
        RuleFor(x => x.Priority).NotEmpty();
        RuleFor(x => x.CollaboratorIds).Must(ids => ids.Count <= 50);
    }
}

public class SaveMilestoneRequestValidator : AbstractValidator<SaveMilestoneRequest>
{
    public SaveMilestoneRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Status).NotEmpty();
    }
}

public class SaveResearchRecordRequestValidator : AbstractValidator<SaveResearchRecordRequest>
{
    public SaveResearchRecordRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Findings).MaximumLength(4000);
        RuleFor(x => x.EvidenceLinks).MaximumLength(2000);
    }
}
