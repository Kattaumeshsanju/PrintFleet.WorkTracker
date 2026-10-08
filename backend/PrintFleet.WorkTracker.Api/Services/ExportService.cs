using System.Globalization;
using ClosedXML.Excel;
using PrintFleet.WorkTracker.Api.DTOs;
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
                column.Item().Text($"Planned {report.PlannedTasks}    Completed {report.CompletedTasks}    Planned hours {report.PlannedHours:0.##}    Actual hours {report.ActualHours:0.##}    Completion {report.CompletionPercent:0.#}%");
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
