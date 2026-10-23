using System.Globalization;
using ClosedXML.Excel;
using PrintFleet.WorkTracker.Api.DTOs;
using PrintFleet.WorkTracker.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PrintFleet.WorkTracker.Api.Services;

public interface IExportService
{
    byte[] MonthlyExcel(MonthlyReportDto report);
    byte[] MonthlyPdf(MonthlyReportDto report);
    byte[] TeamExcel(TeamReportDto report);
    byte[] TeamPdf(TeamReportDto report);
    byte[] DailyExcel(DayViewDto day);
    byte[] DailyPdf(DayViewDto day);
    byte[] MonthTimelinePdf(MonthTimelineDto timeline);
    byte[] RangeExcel(RangeReportDto report);
    byte[] RangePdf(RangeReportDto report);
}

public class ExportService : IExportService
{
    public byte[] MonthlyExcel(MonthlyReportDto report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Monthly Report");
        var row = 1;
        sheet.Cell(row, 1).Value = "Employee";
        sheet.Cell(row, 2).Value = report.Employee.Name;
        sheet.Cell(row, 3).Value = "Month";
        sheet.Cell(row++, 4).Value = ReportService.MonthName(report.Year, report.Month);
        sheet.Cell(row, 1).Value = "Planned tasks";
        sheet.Cell(row, 2).Value = report.PlannedTasks;
        sheet.Cell(row, 3).Value = "Completed";
        sheet.Cell(row++, 4).Value = report.Completed;
        sheet.Cell(row, 1).Value = "Planned hours";
        sheet.Cell(row, 2).Value = report.PlannedHours;
        sheet.Cell(row, 3).Value = "Actual hours";
        sheet.Cell(row++, 4).Value = report.ActualHours;
        sheet.Cell(row, 1).Value = "Completion %";
        sheet.Cell(row, 2).Value = report.CompletionPercent;
        sheet.Cell(row, 3).Value = "Average daily hours";
        sheet.Cell(row++, 4).Value = report.AverageDailyHours;
        row++;
        WriteHeader(sheet, row, "Task", "Project", "Priority", "Status", "Planned start", "Planned end", "Estimated hours", "Actual hours", "Remarks");
        foreach (var task in report.Tasks)
        {
            row++;
            sheet.Cell(row, 1).Value = task.Title;
            sheet.Cell(row, 2).Value = task.ProjectName ?? "";
            sheet.Cell(row, 3).Value = task.Priority;
            sheet.Cell(row, 4).Value = task.Status;
            sheet.Cell(row, 5).Value = task.PlannedStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            sheet.Cell(row, 6).Value = task.PlannedEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            sheet.Cell(row, 7).Value = task.EstimatedHours;
            sheet.Cell(row, 8).Value = task.ActualHours;
            sheet.Cell(row, 9).Value = task.Remarks ?? "";
        }

        var hours = workbook.AddWorksheet("Daily Hours");
        WriteHeader(hours, 1, "Date", "Hours", "Minutes", "Submitted", "Weekend");
        var hourRow = 1;
        foreach (var point in report.DailyHours)
        {
            hourRow++;
            hours.Cell(hourRow, 1).Value = point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            hours.Cell(hourRow, 2).Value = WorkMetrics.Hours(point.Minutes);
            hours.Cell(hourRow, 3).Value = point.Minutes;
            hours.Cell(hourRow, 4).Value = point.Submitted ? "Yes" : "No";
            hours.Cell(hourRow, 5).Value = point.IsWeekend ? "Yes" : "No";
        }

        sheet.Columns().AdjustToContents();
        hours.Columns().AdjustToContents();
        return Save(workbook);
    }

    public byte[] TeamExcel(TeamReportDto report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Team Report");
        sheet.Cell(1, 1).Value = "Month";
        sheet.Cell(1, 2).Value = ReportService.MonthName(report.Year, report.Month);
        sheet.Cell(2, 1).Value = "Includes team lead";
        sheet.Cell(2, 2).Value = report.IncludesSelf ? "Yes" : "No";
        sheet.Cell(3, 1).Value = "Completion %";
        sheet.Cell(3, 2).Value = report.CompletionPercent;
        WriteHeader(sheet, 5, "Employee", "Role", "Planned", "Completed", "In progress", "Blocked", "Cancelled", "Planned hours", "Actual hours", "Completion %");
        var row = 5;
        foreach (var item in report.Rows)
        {
            row++;
            sheet.Cell(row, 1).Value = item.Name;
            sheet.Cell(row, 2).Value = item.Role;
            sheet.Cell(row, 3).Value = item.Planned;
            sheet.Cell(row, 4).Value = item.Completed;
            sheet.Cell(row, 5).Value = item.InProgress;
            sheet.Cell(row, 6).Value = item.Blocked;
            sheet.Cell(row, 7).Value = item.Cancelled;
            sheet.Cell(row, 8).Value = item.PlannedHours;
            sheet.Cell(row, 9).Value = item.ActualHours;
            sheet.Cell(row, 10).Value = item.CompletionPercent;
        }
        sheet.Columns().AdjustToContents();

        var kpi = workbook.AddWorksheet("KPIs");
        kpi.Cell(1, 1).Value = "How these KPIs are calculated";
        kpi.Cell(2, 1).Value = "Plan completion";
        kpi.Cell(2, 2).Value = "Completed tasks divided by all monthly tasks.";
        kpi.Cell(3, 1).Value = "Hours attainment";
        kpi.Cell(3, 2).Value = "Logged hours divided by planned hours on the monthly tasks.";
        kpi.Cell(4, 1).Value = "Utilization";
        kpi.Cell(4, 2).Value = $"Logged hours divided by {report.ExpectedHoursPerDay:0} hours for each weekday that is not Holiday or Leave, up to today.";
        kpi.Cell(5, 1).Value = "Update compliance";
        kpi.Cell(5, 2).Value = "Weekdays with a submitted timeline, excluding Holiday and Leave, up to today.";
        kpi.Cell(6, 1).Value = "Low-hour day";
        kpi.Cell(6, 2).Value = "A weekday with some time logged, but under 6 hours, and not marked Holiday or Leave.";
        kpi.Cell(8, 1).Value = "Team plan completion";
        kpi.Cell(8, 2).Value = report.CompletionPercent;
        kpi.Cell(9, 1).Value = "Team hours attainment";
        kpi.Cell(9, 2).Value = report.HoursAttainment;
        kpi.Cell(10, 1).Value = "Team utilization";
        kpi.Cell(10, 2).Value = report.UtilizationPercent;
        kpi.Cell(11, 1).Value = "Team update compliance";
        kpi.Cell(11, 2).Value = report.UpdateCompliance;
        kpi.Cell(12, 1).Value = "Missing days";
        kpi.Cell(12, 2).Value = report.MissingDays;
        kpi.Cell(13, 1).Value = "Blocked tasks";
        kpi.Cell(13, 2).Value = report.BlockedTasks;
        WriteHeader(kpi, 15, "Employee", "Plan completion", "Hours attainment", "Utilization", "Update compliance", "Available days", "Days logged", "Missing", "Low hours", "Leave", "Holiday", "Blocked");
        var kpiRow = 15;
        foreach (var item in report.Rows)
        {
            kpiRow++;
            kpi.Cell(kpiRow, 1).Value = item.Name;
            kpi.Cell(kpiRow, 2).Value = item.CompletionPercent;
            kpi.Cell(kpiRow, 3).Value = item.HoursAttainment;
            kpi.Cell(kpiRow, 4).Value = item.UtilizationPercent;
            kpi.Cell(kpiRow, 5).Value = item.UpdateCompliance;
            kpi.Cell(kpiRow, 6).Value = item.AvailableDays;
            kpi.Cell(kpiRow, 7).Value = item.DaysLogged;
            kpi.Cell(kpiRow, 8).Value = item.MissingDays;
            kpi.Cell(kpiRow, 9).Value = item.LowHourDays;
            kpi.Cell(kpiRow, 10).Value = item.LeaveDays;
            kpi.Cell(kpiRow, 11).Value = item.HolidayDays;
            kpi.Cell(kpiRow, 12).Value = item.Blocked;
        }
        kpi.Columns().AdjustToContents();
        return Save(workbook);
    }

    public byte[] DailyExcel(DayViewDto day)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Daily Report");
        sheet.Cell(1, 1).Value = "Employee";
        sheet.Cell(1, 2).Value = day.UserName;
        sheet.Cell(2, 1).Value = "Date";
        sheet.Cell(2, 2).Value = day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        sheet.Cell(3, 1).Value = "Total";
        sheet.Cell(3, 2).Value = WorkMetrics.FormatMinutes(day.TotalMinutes);
        sheet.Cell(4, 1).Value = "Submission";
        sheet.Cell(4, 2).Value = day.SubmissionStatus;
        WriteHeader(sheet, 6, "Start", "End", "Duration", "Monthly task", "Project", "Activity", "Status", "Remarks");
        var row = 6;
        foreach (var entry in day.Entries)
        {
            row++;
            sheet.Cell(row, 1).Value = entry.StartTime;
            sheet.Cell(row, 2).Value = entry.EndTime;
            sheet.Cell(row, 3).Value = WorkMetrics.FormatMinutes(entry.DurationMinutes);
            sheet.Cell(row, 4).Value = entry.MonthlyTaskTitle;
            sheet.Cell(row, 5).Value = entry.ProjectName ?? "";
            sheet.Cell(row, 6).Value = entry.Description;
            sheet.Cell(row, 7).Value = entry.Status;
            sheet.Cell(row, 8).Value = entry.Remarks ?? "";
        }
        sheet.Columns().AdjustToContents();
        return Save(workbook);
    }

    public byte[] MonthlyPdf(MonthlyReportDto report) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10));
            page.Header().Text($"{report.Employee.Name}  ·  {ReportService.MonthName(report.Year, report.Month)}").FontSize(18).SemiBold();
            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(4);
                column.Item().Text($"Planned tasks {report.PlannedTasks}    Completed {report.Completed}    In progress {report.InProgress}    Blocked {report.Blocked}    Cancelled {report.Cancelled}");
                column.Item().Text($"Planned hours {report.PlannedHours:0.##}    Actual hours {report.ActualHours:0.##}    Average daily {report.AverageDailyHours:0.#}    Completion {report.CompletionPercent:0.#}%");
                column.Item().PaddingTop(8).Text("Tasks").FontSize(13).SemiBold();
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Task");
                        HeaderCell(header.Cell(), "Project");
                        HeaderCell(header.Cell(), "Status");
                        HeaderCell(header.Cell(), "Hours");
                    });
                    foreach (var task in report.Tasks)
                    {
                        BodyCell(table.Cell(), task.Title);
                        BodyCell(table.Cell(), task.ProjectName ?? "—");
                        BodyCell(table.Cell(), task.Status);
                        BodyCell(table.Cell(), $"{task.ActualHours:0.##} / {task.EstimatedHours:0.##}");
                    }
                });
            });
            Footer(page);
        });
    }).GeneratePdf();

    public byte[] TeamPdf(TeamReportDto report) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10));
            page.Header().Text($"Team report  ·  {ReportService.MonthName(report.Year, report.Month)}").FontSize(18).SemiBold();
            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(4);
                column.Item().Text($"Plan completion {report.CompletionPercent:0.#}%    Hours attainment {report.HoursAttainment:0.#}%    Utilization {report.UtilizationPercent:0.#}%    Updates {report.UpdateCompliance:0.#}%");
                column.Item().Text($"Planned {report.PlannedTasks}    Completed {report.CompletedTasks}    Blocked {report.BlockedTasks}    Missing days {report.MissingDays}    Low-hour days {report.LowHourDays}");
                column.Item().Text($"Utilization uses {report.ExpectedHoursPerDay:0} hours on each weekday that is not Holiday or Leave, through today. A low-hour day is under 6 hours.");
                column.Item().Text(report.IncludesSelf ? "Includes the Team Lead's own work." : "Team members only. The Team Lead is excluded.");
                column.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Employee");
                        HeaderCell(header.Cell(), "Planned");
                        HeaderCell(header.Cell(), "Completed");
                        HeaderCell(header.Cell(), "Hours");
                        HeaderCell(header.Cell(), "Completion");
                    });
                    foreach (var item in report.Rows)
                    {
                        BodyCell(table.Cell(), item.Name);
                        BodyCell(table.Cell(), item.Planned.ToString(CultureInfo.InvariantCulture));
                        BodyCell(table.Cell(), item.Completed.ToString(CultureInfo.InvariantCulture));
                        BodyCell(table.Cell(), item.ActualHours.ToString("0.##", CultureInfo.InvariantCulture));
                        BodyCell(table.Cell(), $"{item.CompletionPercent:0.#}%");
                    }
                });
            });
            Footer(page);
        });
    }).GeneratePdf();

    public byte[] DailyPdf(DayViewDto day) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10));
            page.Header().Text($"{day.UserName}  ·  {day.Date:dd MMMM yyyy}").FontSize(18).SemiBold();
            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(4);
                column.Item().Text($"Total {WorkMetrics.FormatMinutes(day.TotalMinutes)}    Status {day.SubmissionStatus}");
                column.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(70);
                        columns.ConstantColumn(55);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Time");
                        HeaderCell(header.Cell(), "Duration");
                        HeaderCell(header.Cell(), "Task");
                        HeaderCell(header.Cell(), "Activity");
                        HeaderCell(header.Cell(), "Status");
                    });
                    foreach (var entry in day.Entries)
                    {
                        BodyCell(table.Cell(), $"{entry.StartTime}–{entry.EndTime}");
                        BodyCell(table.Cell(), WorkMetrics.FormatMinutes(entry.DurationMinutes));
                        BodyCell(table.Cell(), entry.MonthlyTaskTitle);
                        BodyCell(table.Cell(), entry.Description);
                        BodyCell(table.Cell(), entry.Status);
                    }
                });
            });
            Footer(page);
        });
    }).GeneratePdf();

    public byte[] MonthTimelinePdf(MonthTimelineDto timeline) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10));
            page.Header().Text($"{timeline.UserName}  ·  {ReportService.MonthName(timeline.Year, timeline.Month)}").FontSize(18).SemiBold();
            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(8);
                column.Item().Text($"Daily timeline from {timeline.Days[0].Date:dd MMM yyyy} to {timeline.Days[^1].Date:dd MMM yyyy}    Total {WorkMetrics.FormatMinutes(timeline.TotalMinutes)}");
                foreach (var day in timeline.Days)
                {
                    column.Item().Column(block =>
                    {
                        block.Spacing(2);
                        block.Item().Text(DayHeading(day)).SemiBold();
                        if (day.Entries.Count == 0)
                        {
                            block.Item().Text(day.DayType ?? (WorkMetrics.IsWeekend(day.Date) ? "Weekend" : "No activities")).FontColor("#6B675E");
                            return;
                        }

                        block.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(55);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn();
                            });
                            table.Header(header =>
                            {
                                HeaderCell(header.Cell(), "Time");
                                HeaderCell(header.Cell(), "Duration");
                                HeaderCell(header.Cell(), "Task");
                                HeaderCell(header.Cell(), "Activity");
                                HeaderCell(header.Cell(), "Status");
                            });
                            foreach (var entry in day.Entries)
                            {
                                BodyCell(table.Cell(), $"{entry.StartTime}–{entry.EndTime}");
                                BodyCell(table.Cell(), WorkMetrics.FormatMinutes(entry.DurationMinutes));
                                BodyCell(table.Cell(), entry.MonthlyTaskTitle);
                                BodyCell(table.Cell(), entry.Description);
                                BodyCell(table.Cell(), entry.Status);
                            }
                        });
                    });
                }
            });
            Footer(page);
        });
    }).GeneratePdf();

    public byte[] RangeExcel(RangeReportDto report)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.AddWorksheet("Summary");
        summary.Cell(1, 1).Value = "Employee";
        summary.Cell(1, 2).Value = report.Employee.Name;
        summary.Cell(2, 1).Value = "Start date";
        summary.Cell(2, 2).Value = report.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        summary.Cell(3, 1).Value = "End date";
        summary.Cell(3, 2).Value = report.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        summary.Cell(4, 1).Value = "Activities";
        summary.Cell(4, 2).Value = report.ActivityCount;
        summary.Cell(5, 1).Value = "Total hours";
        summary.Cell(5, 2).Value = report.TotalHours;
        summary.Cell(6, 1).Value = "Total";
        summary.Cell(6, 2).Value = WorkMetrics.FormatMinutes(report.TotalMinutes);
        if (report.ActivityCount == 0)
            summary.Cell(8, 1).Value = "No activities in this date range.";
        else
        {
            WriteHeader(summary, 8, "Project", "Activities", "Hours", "Minutes");
            var row = 8;
            foreach (var project in report.Projects)
            {
                row++;
                summary.Cell(row, 1).Value = project.ProjectName;
                summary.Cell(row, 2).Value = project.ActivityCount;
                summary.Cell(row, 3).Value = project.TotalHours;
                summary.Cell(row, 4).Value = project.TotalMinutes;
            }
        }

        var activities = workbook.AddWorksheet("Activities");
        WriteHeader(activities, 1, "Date", "Start", "End", "Duration", "Project", "Monthly task", "Activity", "Status", "Remarks");
        if (report.ActivityCount == 0)
            activities.Cell(2, 1).Value = "No activities in this date range.";
        else
        {
            var row = 1;
            foreach (var entry in report.Activities)
            {
                row++;
                activities.Cell(row, 1).Value = entry.WorkDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                activities.Cell(row, 2).Value = entry.StartTime;
                activities.Cell(row, 3).Value = entry.EndTime;
                activities.Cell(row, 4).Value = WorkMetrics.FormatMinutes(entry.DurationMinutes);
                activities.Cell(row, 5).Value = entry.ProjectName ?? "No project";
                activities.Cell(row, 6).Value = entry.MonthlyTaskTitle;
                activities.Cell(row, 7).Value = entry.Description;
                activities.Cell(row, 8).Value = entry.Status;
                activities.Cell(row, 9).Value = entry.Remarks ?? "";
            }
        }

        summary.Columns().AdjustToContents();
        activities.Columns().AdjustToContents();
        return Save(workbook);
    }

    public byte[] RangePdf(RangeReportDto report) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10));
            page.Header().Text($"{report.Employee.Name}  ·  {report.From:dd MMM yyyy} to {report.To:dd MMM yyyy}").FontSize(16).SemiBold();
            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(4);
                column.Item().Text($"Activities {report.ActivityCount}    Total {WorkMetrics.FormatMinutes(report.TotalMinutes)}    Hours {report.TotalHours:0.##}");
                column.Item().PaddingTop(8).Text("Projects").FontSize(13).SemiBold();
                if (report.Projects.Count == 0)
                    column.Item().Text("No activities in this date range.").FontColor("#6B675E");
                else
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });
                        table.Header(header =>
                        {
                            HeaderCell(header.Cell(), "Project");
                            HeaderCell(header.Cell(), "Activities");
                            HeaderCell(header.Cell(), "Hours");
                        });
                        foreach (var project in report.Projects)
                        {
                            BodyCell(table.Cell(), project.ProjectName);
                            BodyCell(table.Cell(), project.ActivityCount.ToString(CultureInfo.InvariantCulture));
                            BodyCell(table.Cell(), project.TotalHours.ToString("0.##", CultureInfo.InvariantCulture));
                        }
                    });
                }

                column.Item().PaddingTop(10).Text("Activities").FontSize(13).SemiBold();
                if (report.Activities.Count == 0)
                    column.Item().Text("No activities in this date range.").FontColor("#6B675E");
                else
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(58);
                            columns.ConstantColumn(68);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                            columns.ConstantColumn(48);
                        });
                        table.Header(header =>
                        {
                            HeaderCell(header.Cell(), "Date");
                            HeaderCell(header.Cell(), "Time");
                            HeaderCell(header.Cell(), "Project");
                            HeaderCell(header.Cell(), "Activity");
                            HeaderCell(header.Cell(), "Hours");
                        });
                        foreach (var entry in report.Activities)
                        {
                            BodyCell(table.Cell(), entry.WorkDate.ToString("dd MMM", CultureInfo.InvariantCulture));
                            BodyCell(table.Cell(), $"{entry.StartTime}–{entry.EndTime}");
                            BodyCell(table.Cell(), entry.ProjectName ?? "No project");
                            BodyCell(table.Cell(), entry.Description);
                            BodyCell(table.Cell(), WorkMetrics.FormatMinutes(entry.DurationMinutes));
                        }
                    });
                }
            });
            Footer(page);
        });
    }).GeneratePdf();

    private static string DayHeading(DayViewDto day)
    {
        var mark = day.DayType is null ? "" : $"    {day.DayType}";
        var status = day.SubmissionStatus == SubmissionStatuses.Submitted ? "    Submitted" : "";
        return $"{day.Date:ddd dd MMM}    {WorkMetrics.FormatMinutes(day.TotalMinutes)}{mark}{status}";
    }

    private static void WriteHeader(IXLWorksheet sheet, int row, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1C2433");
            cell.Style.Font.FontColor = XLColor.White;
        }
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void HeaderCell(IContainer cell, string text)
        => cell.Background("#1C2433").Padding(4).Text(text).FontColor(Colors.White).SemiBold();

    private static void BodyCell(IContainer cell, string text)
        => cell.BorderBottom(0.5f).BorderColor("#E6E2D8").Padding(4).Text(text);

    private static void Footer(PageDescriptor page)
        => page.Footer().AlignRight().Text(text =>
        {
            text.Span("WorkTracker  ·  Page ");
            text.CurrentPageNumber();
        });
}
