using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ToolHelper.DataProcessing.Configuration;

namespace ToolHelper.DataProcessing.Pdf;

/// <summary>
/// PDF 文件处理帮助类
/// 提供 PDF 生成、报表输出等功能
/// 基于 QuestPDF 实现
/// </summary>
public class PdfHelper
{
    private readonly PdfOptions _options;
    private readonly ILogger<PdfHelper>? _logger;

    static PdfHelper()
    {
        // 设置 QuestPDF 许可证（社区版免费）
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">PDF配置选项（可选，使用默认配置时可为null）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public PdfHelper(IOptions<PdfOptions>? options = null, ILogger<PdfHelper>? logger = null)
    {
        _options = options?.Value ?? new PdfOptions();
        _logger = logger;
    }

    #region PDF 生成

    /// <summary>
    /// 生成简单文本PDF
    /// </summary>
    /// <param name="filePath">输出文件路径</param>
    /// <param name="content">文本内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task GenerateTextPdfAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始生成文本PDF: {FilePath}", filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page);

                page.Header().Column(column =>
                {
                    if (!string.IsNullOrEmpty(_options.Title))
                    {
                        column.Item().Text(_options.Title)
                            .FontSize(20)
                            .Bold()
                            .AlignCenter();
                    }
                });

                page.Content().Text(content)
                    .FontSize(_options.FontSize);

                ConfigureFooter(page);
            });
        }).GeneratePdf(filePath);

        _logger?.LogInformation("PDF生成完成");
        await Task.CompletedTask;
    }

    /// <summary>
    /// 生成表格PDF
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="filePath">输出文件路径</param>
    /// <param name="data">表格数据</param>
    /// <param name="title">表格标题</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task GenerateTablePdfAsync<T>(
        string filePath,
        IEnumerable<T> data,
        string? title = null,
        CancellationToken cancellationToken = default) where T : class
    {
        _logger?.LogInformation("开始生成表格PDF: {FilePath}", filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var properties = typeof(T).GetProperties();
        var dataList = data.ToList();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page);

                // 页眉
                page.Header().Column(column =>
                {
                    var headerTitle = title ?? _options.Title;
                    if (!string.IsNullOrEmpty(headerTitle))
                    {
                        column.Item().Text(headerTitle)
                            .FontSize(20)
                            .Bold()
                            .AlignCenter();

                        column.Item().PaddingVertical(10);
                    }
                });

                // 表格内容
                page.Content().Table(table =>
                {
                    // 定义列
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var prop in properties)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    // 表头
                    table.Header(header =>
                    {
                        foreach (var prop in properties)
                        {
                            header.Cell()
                                .Background(Colors.Grey.Lighten2)
                                .Border(1)
                                .Padding(5)
                                .Text(prop.Name)
                                .FontSize(_options.FontSize)
                                .Bold();
                        }
                    });

                    // 数据行
                    foreach (var item in dataList)
                    {
                        foreach (var prop in properties)
                        {
                            var value = prop.GetValue(item)?.ToString() ?? "";
                            table.Cell()
                                .Border(1)
                                .Padding(5)
                                .Text(value)
                                .FontSize(_options.FontSize);
                        }
                    }
                });

                // 页脚
                ConfigureFooter(page);
            });
        }).GeneratePdf(filePath);

        _logger?.LogInformation("表格PDF生成完成，共 {Count} 行数据", dataList.Count);
        await Task.CompletedTask;
    }

    /// <summary>
    /// 生成报表PDF（包含标题、表格和统计信息）
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="filePath">输出文件路径</param>
    /// <param name="data">报表数据</param>
    /// <param name="reportTitle">报表标题</param>
    /// <param name="summary">统计信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task GenerateReportPdfAsync<T>(
        string filePath,
        IEnumerable<T> data,
        string reportTitle,
        Dictionary<string, string>? summary = null,
        CancellationToken cancellationToken = default) where T : class
    {
        _logger?.LogInformation("开始生成报表PDF: {FilePath}", filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var properties = typeof(T).GetProperties();
        var dataList = data.ToList();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page);

                // 页眉
                page.Header().Column(column =>
                {
                    column.Item().Text(reportTitle)
                        .FontSize(24)
                        .Bold()
                        .AlignCenter();

                    column.Item().PaddingVertical(5);

                    column.Item().Text($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                        .FontSize(10)
                        .AlignRight();

                    column.Item().PaddingVertical(10);
                });

                // 内容
                page.Content().Column(column =>
                {
                    // 统计信息
                    if (summary != null && summary.Count > 0)
                    {
                        column.Item().Text("统计信息")
                            .FontSize(16)
                            .Bold();

                        column.Item().PaddingVertical(5);

                        foreach (var item in summary)
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"{item.Key}:")
                                    .FontSize(_options.FontSize);
                                row.RelativeItem().Text(item.Value)
                                    .FontSize(_options.FontSize)
                                    .Bold();
                            });
                        }

                        column.Item().PaddingVertical(15);
                    }

                    // 数据表格
                    column.Item().Text("详细数据")
                        .FontSize(16)
                        .Bold();

                    column.Item().PaddingVertical(5);

                    column.Item().Table(table =>
                    {
                        // 定义列
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var prop in properties)
                            {
                                columns.RelativeColumn();
                            }
                        });

                        // 表头
                        table.Header(header =>
                        {
                            foreach (var prop in properties)
                            {
                                header.Cell()
                                    .Background(Colors.Blue.Lighten3)
                                    .Border(1)
                                    .Padding(5)
                                    .Text(prop.Name)
                                    .FontSize(_options.FontSize)
                                    .Bold();
                            }
                        });

                        // 数据行
                        int rowIndex = 0;
                        foreach (var item in dataList)
                        {
                            var backgroundColor = rowIndex % 2 == 0 
                                ? Colors.White 
                                : Colors.Grey.Lighten4;

                            foreach (var prop in properties)
                            {
                                var value = prop.GetValue(item)?.ToString() ?? "";
                                table.Cell()
                                    .Background(backgroundColor)
                                    .Border(1)
                                    .Padding(5)
                                    .Text(value)
                                    .FontSize(_options.FontSize);
                            }
                            rowIndex++;
                        }
                    });
                });

                // 页脚
                ConfigureFooter(page);
            });
        }).GeneratePdf(filePath);

        _logger?.LogInformation("报表PDF生成完成");
        await Task.CompletedTask;
    }

    #endregion

    #region 私有辅助方法

    private void ConfigurePage(PageDescriptor page)
    {
        // 设置页面大小
        page.Size(_options.PageSize.ToUpper() switch
        {
            "A3" => PageSizes.A3,
            "A4" => PageSizes.A4,
            "A5" => PageSizes.A5,
            "LETTER" => PageSizes.Letter,
            _ => PageSizes.A4
        });

        // 设置页边距
        page.Margin(_options.Margin, Unit.Millimetre);

        // 设置默认字体
        page.DefaultTextStyle(x => x.FontSize(_options.FontSize));
    }

    private void ConfigureFooter(PageDescriptor page)
    {
        if (_options.IncludePageNumbers || !string.IsNullOrEmpty(_options.Author))
        {
            page.Footer().Row(row =>
            {
                if (_options.IncludePageNumbers)
                {
                    row.RelativeItem().AlignCenter().Text(text =>
                    {
                        text.Span("第 ");
                        text.CurrentPageNumber();
                        text.Span(" 页");
                    });
                }

                if (!string.IsNullOrEmpty(_options.Author))
                {
                    row.RelativeItem().AlignRight()
                        .Text($"作者: {_options.Author}");
                }
            });
        }
    }

    #endregion
}
