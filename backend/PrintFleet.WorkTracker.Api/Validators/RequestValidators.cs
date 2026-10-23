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
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("Project end date must be on or after the start date.");
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
