using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BTFX.Helpers;

/// <summary>
/// 报告PDF导出器
/// 使用QuestPDF生成报告PDF文件
/// </summary>
public class ReportPdfExporter
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ReportPdfExporter>? _logger;

    static ReportPdfExporter()
    {
        // 设置QuestPDF许可证
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportPdfExporter(ISettingsService settingsService, ILogger<ReportPdfExporter>? logger = null)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// 导出报告为PDF
    /// </summary>
    /// <param name="report">报告数据</param>
    /// <param name="filePath">输出文件路径</param>
    /// <returns>是否成功</returns>
    public bool ExportToPdf(Report report, string filePath)
    {
        try
        {
            _logger?.LogInformation("开始导出报告PDF: {FilePath}", filePath);

            var directory = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            var unitName = _settingsService.CurrentSettings?.Unit?.Name ?? Constants.APP_DISPLAY_NAME;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    // 页面设置 - A4
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Microsoft YaHei UI"));

                    // 页眉
                    page.Header().Element(c => ComposeHeader(c, report, unitName));

                    // 内容
                    page.Content().Element(c => ComposeContent(c, report));

                    // 页脚
                    page.Footer().Element(c => ComposeFooter(c, report));
                });
            }).GeneratePdf(filePath);

            _logger?.LogInformation("报告PDF导出成功");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "导出报告PDF失败");
            return false;
        }
    }

    /// <summary>
    /// 导出报告为PDF字节数组
    /// </summary>
    public byte[]? ExportToPdfBytes(Report report)
    {
        try
        {
            var unitName = _settingsService.CurrentSettings?.Unit?.Name ?? Constants.APP_DISPLAY_NAME;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Microsoft YaHei UI"));

                    page.Header().Element(c => ComposeHeader(c, report, unitName));
                    page.Content().Element(c => ComposeContent(c, report));
                    page.Footer().Element(c => ComposeFooter(c, report));
                });
            }).GeneratePdf();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "导出报告PDF字节失败");
            return null;
        }
    }

    #region 报告组成部分

    /// <summary>
    /// 页眉
    /// </summary>
    private void ComposeHeader(IContainer container, Report report, string unitName)
    {
        container.Column(column =>
        {
            // 单位名称
            if (!string.IsNullOrEmpty(unitName))
            {
                column.Item().Text(unitName)
                    .FontSize(14)
                    .Bold()
                    .AlignCenter();

                column.Item().PaddingVertical(4);
            }

            // 报告标题
            column.Item().Text("步态分析报告")
                .FontSize(22)
                .Bold()
                .AlignCenter();

            column.Item().PaddingVertical(4);

            // 报告编号
            column.Item().Text($"报告编号：{report.ReportNumber}")
                .FontSize(9)
                .FontColor(Colors.Grey.Medium)
                .AlignCenter();

            column.Item().PaddingVertical(10);

            // 分隔线
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    /// <summary>
    /// 内容
    /// </summary>
    private void ComposeContent(IContainer container, Report report)
    {
        container.Column(column =>
        {
            // 患者信息
            column.Item().Element(c => ComposePatientInfo(c, report));

            column.Item().PaddingVertical(8);

            // 测量信息
            column.Item().Element(c => ComposeMeasurementInfo(c, report));

            column.Item().PaddingVertical(8);

            // 步态参数
            column.Item().Element(c => ComposeGaitParameters(c, report));

            column.Item().PaddingVertical(8);

            // 医生意见
            column.Item().Element(c => ComposeDoctorOpinion(c, report));
        });
    }

    /// <summary>
    /// 患者信息
    /// </summary>
    private void ComposePatientInfo(IContainer container, Report report)
    {
        var patient = report.MeasurementRecord?.Patient;

        container.Column(column =>
        {
            column.Item().Text("患者信息")
                .FontSize(12)
                .Bold();

            column.Item().PaddingVertical(6);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(60);
                    columns.RelativeColumn();
                    columns.ConstantColumn(60);
                    columns.RelativeColumn();
                    columns.ConstantColumn(60);
                    columns.RelativeColumn();
                });

                // 第一行
                table.Cell().Text("姓名").FontColor(Colors.Grey.Medium);
                table.Cell().Text(patient?.Name ?? "--").Bold();
                table.Cell().Text("性别").FontColor(Colors.Grey.Medium);
                table.Cell().Text(patient?.Gender == Gender.Male ? "男" : "女").Bold();
                table.Cell().Text("年龄").FontColor(Colors.Grey.Medium);
                table.Cell().Text($"{patient?.Age ?? 0}岁").Bold();

                // 第二行
                table.Cell().Text("电话").FontColor(Colors.Grey.Medium);
                table.Cell().Text(patient?.Phone ?? "--");
                table.Cell().Text("证件号").FontColor(Colors.Grey.Medium);
                table.Cell().ColumnSpan(3).Text(MaskIdNumber(patient?.IdNumber));
            });
        });
    }

    /// <summary>
    /// 测量信息
    /// </summary>
    private void ComposeMeasurementInfo(IContainer container, Report report)
    {
        var measurement = report.MeasurementRecord;

        container.Column(column =>
        {
            column.Item().Text("测量信息")
                .FontSize(12)
                .Bold();

            column.Item().PaddingVertical(6);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(60);
                    columns.RelativeColumn();
                    columns.ConstantColumn(60);
                    columns.RelativeColumn();
                });

                table.Cell().Text("测量日期").FontColor(Colors.Grey.Medium);
                table.Cell().Text(measurement?.MeasurementDate.ToString(Constants.DATETIME_FORMAT) ?? "--");
                table.Cell().Text("操作员").FontColor(Colors.Grey.Medium);
                table.Cell().Text(measurement?.Operator?.Name ?? "--");

                table.Cell().Text("测量时长").FontColor(Colors.Grey.Medium);
                table.Cell().Text(measurement?.DurationSeconds.HasValue == true ? $"{measurement.DurationSeconds.Value}秒" : "--");
                table.Cell().Text("报告生成").FontColor(Colors.Grey.Medium);
                table.Cell().Text(report.CreatedAt.ToString(Constants.DATETIME_FORMAT));
            });
        });
    }

    /// <summary>
    /// 步态参数
    /// </summary>
    private void ComposeGaitParameters(IContainer container, Report report)
    {
        var gait = report.MeasurementRecord?.GaitParameters;

        container.Column(column =>
        {
            column.Item().Text("步态参数")
                .FontSize(12)
                .Bold();

            column.Item().PaddingVertical(6);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    for (int i = 0; i < 4; i++)
                        columns.RelativeColumn();
                });

                // 第一行
                table.Cell().Element(c => CreateParameterCell(c, "步幅（左）", gait?.StrideLengthLeft, "cm"));
                table.Cell().Element(c => CreateParameterCell(c, "步幅（右）", gait?.StrideLengthRight, "cm"));
                table.Cell().Element(c => CreateParameterCell(c, "步频", gait?.Cadence, "步/分"));
                table.Cell().Element(c => CreateParameterCell(c, "步速", gait?.Velocity, "m/s"));

                // 第二行
                table.Cell().Element(c => CreateParameterCell(c, "左脚支撑相", gait?.StancePhaseLeft, "%"));
                table.Cell().Element(c => CreateParameterCell(c, "右脚支撑相", gait?.StancePhaseRight, "%"));
                table.Cell().Element(c => CreateParameterCell(c, "双支撑时间", gait?.DoubleSupport, "%"));
                table.Cell().Element(c => CreateParameterCell(c, "步宽", gait?.StepWidth, "cm"));

                // 第三行
                table.Cell().Element(c => CreateParameterCell(c, "摆动相（左）", gait?.SwingPhaseLeft, "%"));
                table.Cell().Element(c => CreateParameterCell(c, "摆动相（右）", gait?.SwingPhaseRight, "%"));
                table.Cell().Element(c => CreateParameterCell(c, "对称性指数", gait?.SymmetryIndex, ""));
                table.Cell().Element(c => CreateParameterCell(c, "变异系数", gait?.VariabilityCoefficient, "%"));
            });
        });
    }

    /// <summary>
    /// 创建参数单元格
    /// </summary>
    private void CreateParameterCell(IContainer container, string label, double? value, string unit)
    {
        container
            .Background(Colors.Grey.Lighten4)
            .Padding(8)
            .Column(column =>
            {
                column.Item().Text(label)
                    .FontSize(8)
                    .FontColor(Colors.Grey.Medium)
                    .AlignCenter();

                column.Item().PaddingVertical(2);

                column.Item().Text(value.HasValue ? $"{value.Value:F1}" : "--")
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Blue.Medium)
                    .AlignCenter();

                column.Item().Text(unit)
                    .FontSize(7)
                    .FontColor(Colors.Grey.Medium)
                    .AlignCenter();
            });
    }

    /// <summary>
    /// 医生意见
    /// </summary>
    private void ComposeDoctorOpinion(IContainer container, Report report)
    {
        container.Column(column =>
        {
            column.Item().Text("医生意见")
                .FontSize(12)
                .Bold();

            column.Item().PaddingVertical(6);

            var opinion = report.DoctorOpinion;
            if (string.IsNullOrEmpty(opinion))
            {
                opinion = "（暂无医生意见）";
            }

            column.Item()
                .Background(Colors.Grey.Lighten4)
                .Padding(12)
                .Text(opinion)
                .FontSize(10)
                .LineHeight(1.5f);
        });
    }

    /// <summary>
    /// 页脚
    /// </summary>
    private void ComposeFooter(IContainer container, Report report)
    {
            container.Column(column =>
            {
                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                column.Item().PaddingVertical(4);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"生成时间：{report.CreatedAt:yyyy年MM月dd日 HH:mm}")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Medium);

                    row.RelativeItem().AlignCenter().Text(text =>
                    {
                        text.Span("第 ").FontSize(8).FontColor(Colors.Grey.Medium);
                        text.CurrentPageNumber().FontSize(8);
                        text.Span(" 页 / 共 ").FontSize(8).FontColor(Colors.Grey.Medium);
                        text.TotalPages().FontSize(8);
                        text.Span(" 页").FontSize(8).FontColor(Colors.Grey.Medium);
                    });

                    row.RelativeItem().AlignRight().Text(GetReportStatusText(report.Status))
                        .FontSize(8)
                        .FontColor(Colors.Grey.Medium);
                });
            });
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 掩码证件号
        /// </summary>
        private static string MaskIdNumber(string? idNumber)
        {
            if (string.IsNullOrEmpty(idNumber)) return "--";
            if (idNumber.Length <= 6) return idNumber;
            return idNumber[..3] + "****" + idNumber[^4..];
    }

    /// <summary>
    /// 获取报告状态文本
    /// </summary>
    private static string GetReportStatusText(ReportStatus status)
    {
        return status switch
        {
            ReportStatus.Draft => "草稿",
            ReportStatus.Completed => "已完成",
            ReportStatus.Printed => "已打印",
            _ => "未知"
        };
    }

    #endregion
}
