using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BTFX.Common;
using BTFX.Models;

namespace BTFX.Helpers;

/// <summary>
/// 报告预览帮助类
/// 用于生成报告的FlowDocument预览
/// </summary>
public static class ReportPreviewHelper
{
    /// <summary>
    /// 生成报告FlowDocument
    /// </summary>
    /// <param name="report">报告数据</param>
    /// <param name="unitName">单位名称</param>
    /// <param name="logoPath">Logo路径（可选）</param>
    /// <returns>FlowDocument</returns>
    public static FlowDocument GenerateReportDocument(Report report, string unitName, string? logoPath = null)
    {
        var document = new FlowDocument
        {
            PageWidth = PrintHelper.A4WidthInPixels,
            PageHeight = PrintHelper.A4HeightInPixels,
            PagePadding = new Thickness(PrintHelper.DefaultMargin),
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 12
        };

        // 报告头部
        AddHeader(document, unitName, report.ReportNumber, logoPath);

        // 患者信息
        AddPatientInfo(document, report);

        // 测量信息
        AddMeasurementInfo(document, report);

        // 步态参数
        AddGaitParameters(document, report);

        // 医生意见
        AddDoctorOpinion(document, report);

        // 页脚
        AddFooter(document, report);

        return document;
    }

    /// <summary>
    /// 添加报告头部
    /// </summary>
    private static void AddHeader(FlowDocument document, string unitName, string reportNumber, string? logoPath)
    {
        // 单位名称
        if (!string.IsNullOrEmpty(unitName))
        {
            var unitParagraph = new Paragraph(new Run(unitName))
            {
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            document.Blocks.Add(unitParagraph);
        }

        // 报告标题
        var titleParagraph = new Paragraph(new Run("步态分析报告"))
        {
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };
        document.Blocks.Add(titleParagraph);

        // 报告编号
        var numberParagraph = new Paragraph(new Run($"报告编号：{reportNumber}"))
        {
            FontSize = 11,
            Foreground = Brushes.Gray,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        };
        document.Blocks.Add(numberParagraph);

        // 分隔线
        AddSeparator(document);
    }

    /// <summary>
    /// 添加患者信息
    /// </summary>
    private static void AddPatientInfo(FlowDocument document, Report report)
    {
        var patient = report.MeasurementRecord?.Patient;
        if (patient == null) return;

        AddSectionTitle(document, "患者信息");

        var table = CreateInfoTable();

        // 第一行：姓名、性别、年龄
        var row1 = new TableRow();
        row1.Cells.Add(CreateLabelCell("姓名"));
        row1.Cells.Add(CreateValueCell(patient.Name));
        row1.Cells.Add(CreateLabelCell("性别"));
        row1.Cells.Add(CreateValueCell(patient.Gender == Gender.Male ? "男" : "女"));
        row1.Cells.Add(CreateLabelCell("年龄"));
        row1.Cells.Add(CreateValueCell($"{patient.Age}岁"));
        table.RowGroups[0].Rows.Add(row1);

        // 第二行：电话、证件号
        var row2 = new TableRow();
        row2.Cells.Add(CreateLabelCell("联系电话"));
        row2.Cells.Add(CreateValueCell(patient.Phone ?? "--"));
        row2.Cells.Add(CreateLabelCell("证件号码"));
        row2.Cells.Add(CreateValueCell(MaskIdNumber(patient.IdNumber)));
        row2.Cells.Add(CreateLabelCell(""));
        row2.Cells.Add(CreateValueCell(""));
        table.RowGroups[0].Rows.Add(row2);

        document.Blocks.Add(table);
    }

    /// <summary>
    /// 添加测量信息
    /// </summary>
    private static void AddMeasurementInfo(FlowDocument document, Report report)
    {
        var measurement = report.MeasurementRecord;
        if (measurement == null) return;

        AddSectionTitle(document, "测量信息");

        var table = CreateInfoTable();

        // 第一行：测量日期、操作员
        var row1 = new TableRow();
        row1.Cells.Add(CreateLabelCell("测量日期"));
        row1.Cells.Add(CreateValueCell(measurement.MeasurementDate.ToString(Constants.DATETIME_FORMAT)));
        row1.Cells.Add(CreateLabelCell("操作员"));
        row1.Cells.Add(CreateValueCell(measurement.Operator?.Name ?? "--"));
        row1.Cells.Add(CreateLabelCell("测量状态"));
        row1.Cells.Add(CreateValueCell(GetMeasurementStatusText(measurement.Status)));
        table.RowGroups[0].Rows.Add(row1);

        // 第二行：测量时长
        var row2 = new TableRow();
        row2.Cells.Add(CreateLabelCell("测量时长"));
        row2.Cells.Add(CreateValueCell(measurement.DurationSeconds.HasValue ? $"{measurement.DurationSeconds.Value}秒" : "--"));
        row2.Cells.Add(CreateLabelCell("报告生成"));
        row2.Cells.Add(CreateValueCell(report.CreatedAt.ToString(Constants.DATETIME_FORMAT)));
        row2.Cells.Add(CreateLabelCell(""));
        row2.Cells.Add(CreateValueCell(""));
        table.RowGroups[0].Rows.Add(row2);

        document.Blocks.Add(table);
    }

    /// <summary>
    /// 添加步态参数
    /// </summary>
    private static void AddGaitParameters(FlowDocument document, Report report)
    {
        var gait = report.MeasurementRecord?.GaitParameters;

        AddSectionTitle(document, "步态参数");

        var table = new Table
        {
            CellSpacing = 4,
            Margin = new Thickness(0, 0, 0, 16)
        };

        // 定义列
        for (int i = 0; i < 4; i++)
        {
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        }

        table.RowGroups.Add(new TableRowGroup());

        // 第一行
        var row1 = new TableRow();
        row1.Cells.Add(CreateParameterCell("步幅（左）", gait?.StrideLengthLeft, "cm"));
        row1.Cells.Add(CreateParameterCell("步幅（右）", gait?.StrideLengthRight, "cm"));
        row1.Cells.Add(CreateParameterCell("步频", gait?.Cadence, "步/分"));
        row1.Cells.Add(CreateParameterCell("步速", gait?.Velocity, "m/s"));
        table.RowGroups[0].Rows.Add(row1);

        // 第二行
        var row2 = new TableRow();
        row2.Cells.Add(CreateParameterCell("左脚支撑相", gait?.StancePhaseLeft, "%"));
        row2.Cells.Add(CreateParameterCell("右脚支撑相", gait?.StancePhaseRight, "%"));
        row2.Cells.Add(CreateParameterCell("双支撑时间", gait?.DoubleSupport, "%"));
        row2.Cells.Add(CreateParameterCell("步宽", gait?.StepWidth, "cm"));
        table.RowGroups[0].Rows.Add(row2);

        // 第三行
        var row3 = new TableRow();
        row3.Cells.Add(CreateParameterCell("摆动相（左）", gait?.SwingPhaseLeft, "%"));
        row3.Cells.Add(CreateParameterCell("摆动相（右）", gait?.SwingPhaseRight, "%"));
        row3.Cells.Add(CreateParameterCell("对称性指数", gait?.SymmetryIndex, ""));
        row3.Cells.Add(CreateParameterCell("变异系数", gait?.VariabilityCoefficient, "%"));
        table.RowGroups[0].Rows.Add(row3);

        document.Blocks.Add(table);
    }

    /// <summary>
    /// 添加医生意见
    /// </summary>
    private static void AddDoctorOpinion(FlowDocument document, Report report)
    {
        AddSectionTitle(document, "医生意见");

        var opinion = report.DoctorOpinion;
        if (string.IsNullOrEmpty(opinion))
        {
            opinion = "（暂无医生意见）";
        }

        var border = new BlockUIContainer(new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
            Child = new TextBlock
            {
                Text = opinion,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                LineHeight = 20
            }
        });

        document.Blocks.Add(border);
    }

    /// <summary>
    /// 添加页脚
    /// </summary>
    private static void AddFooter(FlowDocument document, Report report)
    {
        AddSeparator(document);

        var footerParagraph = new Paragraph
        {
            FontSize = 10,
            Foreground = Brushes.Gray,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };

        footerParagraph.Inlines.Add(new Run($"报告生成时间：{report.CreatedAt:yyyy年MM月dd日 HH:mm}"));
        footerParagraph.Inlines.Add(new Run("    |    "));
        footerParagraph.Inlines.Add(new Run($"状态：{GetReportStatusText(report.Status)}"));

        document.Blocks.Add(footerParagraph);
    }

    #region 辅助方法

    /// <summary>
    /// 添加区域标题
    /// </summary>
    private static void AddSectionTitle(FlowDocument document, string title)
    {
        var paragraph = new Paragraph(new Run(title))
        {
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 16, 0, 8),
            Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51))
        };
        document.Blocks.Add(paragraph);
    }

    /// <summary>
    /// 添加分隔线
    /// </summary>
    private static void AddSeparator(FlowDocument document)
    {
        var border = new BlockUIContainer(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            Margin = new Thickness(0, 8, 0, 8)
        });
        document.Blocks.Add(border);
    }

    /// <summary>
    /// 创建信息表格
    /// </summary>
    private static Table CreateInfoTable()
    {
        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // 6列：标签-值 x 3
        for (int i = 0; i < 6; i++)
        {
            var width = i % 2 == 0 ? 80 : 120;
            table.Columns.Add(new TableColumn { Width = new GridLength(width) });
        }

        table.RowGroups.Add(new TableRowGroup());
        return table;
    }

    /// <summary>
    /// 创建标签单元格
    /// </summary>
    private static TableCell CreateLabelCell(string text)
    {
        return new TableCell(new Paragraph(new Run(text))
        {
            FontSize = 11,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 4, 8, 4)
        });
    }

    /// <summary>
    /// 创建值单元格
    /// </summary>
    private static TableCell CreateValueCell(string text)
    {
        return new TableCell(new Paragraph(new Run(text))
        {
            FontSize = 11,
            FontWeight = FontWeights.Medium,
            Margin = new Thickness(0, 4, 16, 4)
        });
    }

    /// <summary>
    /// 创建参数单元格
    /// </summary>
    private static TableCell CreateParameterCell(string label, double? value, string unit)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 12, 8, 12),
            Margin = new Thickness(2)
        };

        var stack = new StackPanel();
        
        // 标签
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        // 值
        stack.Children.Add(new TextBlock
        {
            Text = value.HasValue ? $"{value.Value:F1}" : "--",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(25, 118, 210)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 2)
        });

        // 单位
        stack.Children.Add(new TextBlock
        {
            Text = unit,
            FontSize = 9,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        border.Child = stack;

        return new TableCell(new BlockUIContainer(border));
    }

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
    /// 获取测量状态文本
    /// </summary>
    private static string GetMeasurementStatusText(MeasurementStatus status)
    {
        return status switch
        {
            MeasurementStatus.Pending => "待处理",
            MeasurementStatus.InProgress => "进行中",
            MeasurementStatus.Completed => "已完成",
            MeasurementStatus.Cancelled => "已取消",
            MeasurementStatus.Failed => "失败",
            _ => "未知"
        };
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
