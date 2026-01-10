using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Xps;

namespace BTFX.Helpers;

/// <summary>
/// WPF打印辅助类
/// 提供FlowDocument打印、打印预览等功能
/// </summary>
public static class PrintHelper
{
    #region 打印设置常量

    /// <summary>
    /// A4纸张宽度（96 DPI下的像素值）
    /// </summary>
    public const double A4WidthInPixels = 793.7; // 210mm * 96 / 25.4

    /// <summary>
    /// A4纸张高度（96 DPI下的像素值）
    /// </summary>
    public const double A4HeightInPixels = 1122.5; // 297mm * 96 / 25.4

    /// <summary>
    /// 默认页边距（像素）
    /// </summary>
    public const double DefaultMargin = 56.7; // 约15mm

    #endregion

    #region 打印方法

    /// <summary>
    /// 打印FlowDocument
    /// </summary>
    /// <param name="document">要打印的文档</param>
    /// <param name="documentName">文档名称（显示在打印队列中）</param>
    /// <param name="showDialog">是否显示打印对话框</param>
    /// <returns>是否成功打印</returns>
    public static bool PrintDocument(FlowDocument document, string documentName, bool showDialog = true)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        try
        {
            var printDialog = new PrintDialog();

            // 设置默认为A4纵向
            ConfigurePrintDialogForA4(printDialog);

            if (showDialog)
            {
                var result = printDialog.ShowDialog();
                if (result != true)
                    return false;
            }

            // 创建文档分页器
            var paginator = CreatePaginator(document, printDialog);

            // 执行打印
            printDialog.PrintDocument(paginator, documentName);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"打印失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 打印FixedDocument
    /// </summary>
    /// <param name="document">要打印的固定文档</param>
    /// <param name="documentName">文档名称</param>
    /// <param name="showDialog">是否显示打印对话框</param>
    /// <returns>是否成功打印</returns>
    public static bool PrintFixedDocument(FixedDocument document, string documentName, bool showDialog = true)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        try
        {
            var printDialog = new PrintDialog();
            ConfigurePrintDialogForA4(printDialog);

            if (showDialog)
            {
                var result = printDialog.ShowDialog();
                if (result != true)
                    return false;
            }

            printDialog.PrintDocument(document.DocumentPaginator, documentName);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"打印失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 打印Visual元素
    /// </summary>
    /// <param name="visual">要打印的可视化元素</param>
    /// <param name="documentName">文档名称</param>
    /// <param name="showDialog">是否显示打印对话框</param>
    /// <returns>是否成功打印</returns>
    public static bool PrintVisual(Visual visual, string documentName, bool showDialog = true)
    {
        if (visual == null)
            throw new ArgumentNullException(nameof(visual));

        try
        {
            var printDialog = new PrintDialog();
            ConfigurePrintDialogForA4(printDialog);

            if (showDialog)
            {
                var result = printDialog.ShowDialog();
                if (result != true)
                    return false;
            }

            printDialog.PrintVisual(visual, documentName);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"打印失败: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region 打印预览

    /// <summary>
    /// 显示打印预览窗口
    /// </summary>
    /// <param name="document">要预览的FlowDocument</param>
    /// <param name="title">预览窗口标题</param>
    public static void ShowPrintPreview(FlowDocument document, string title = "打印预览")
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        // 创建预览窗口
        var previewWindow = new Window
        {
            Title = title,
            Width = 900,
            Height = 700,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        // 创建文档查看器
        var documentViewer = new FlowDocumentReader
        {
            Document = CloneDocument(document),
            ViewingMode = FlowDocumentReaderViewingMode.Page,
            IsFindEnabled = true,
            IsPrintEnabled = true
        };

        previewWindow.Content = documentViewer;
        previewWindow.ShowDialog();
    }

    /// <summary>
    /// 显示FixedDocument打印预览
    /// </summary>
    /// <param name="document">要预览的FixedDocument</param>
    /// <param name="title">预览窗口标题</param>
    public static void ShowFixedDocumentPreview(FixedDocument document, string title = "打印预览")
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        var previewWindow = new Window
        {
            Title = title,
            Width = 900,
            Height = 700,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        var documentViewer = new DocumentViewer
        {
            Document = document
        };

        previewWindow.Content = documentViewer;
        previewWindow.ShowDialog();
    }

    #endregion

    #region 打印机管理

    /// <summary>
    /// 获取所有可用的打印机名称
    /// </summary>
    /// <returns>打印机名称列表</returns>
    public static IEnumerable<string> GetAvailablePrinters()
    {
        var printers = new List<string>();

        try
        {
            var printServer = new LocalPrintServer();
            var queues = printServer.GetPrintQueues();

            foreach (var queue in queues)
            {
                printers.Add(queue.FullName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取打印机列表失败: {ex.Message}");
        }

        return printers;
    }

    /// <summary>
    /// 获取默认打印机名称
    /// </summary>
    /// <returns>默认打印机名称，如果没有则返回null</returns>
    public static string? GetDefaultPrinter()
    {
        try
        {
            var printServer = new LocalPrintServer();
            var defaultQueue = printServer.DefaultPrintQueue;
            return defaultQueue?.FullName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 检查打印机是否可用
    /// </summary>
    /// <param name="printerName">打印机名称</param>
    /// <returns>是否可用</returns>
    public static bool IsPrinterAvailable(string printerName)
    {
        try
        {
            var printServer = new LocalPrintServer();
            var queue = printServer.GetPrintQueue(printerName);
            return queue != null && !queue.IsOffline;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 文档创建辅助方法

    /// <summary>
    /// 创建A4尺寸的FlowDocument
    /// </summary>
    /// <returns>配置好的FlowDocument</returns>
    public static FlowDocument CreateA4FlowDocument()
    {
        return new FlowDocument
        {
            PageWidth = A4WidthInPixels,
            PageHeight = A4HeightInPixels,
            PagePadding = new Thickness(DefaultMargin),
            ColumnWidth = double.PositiveInfinity, // 单列
            FontFamily = new FontFamily("Microsoft YaHei, SimSun"),
            FontSize = 12
        };
    }

    /// <summary>
    /// 为FlowDocument添加标题
    /// </summary>
    /// <param name="document">目标文档</param>
    /// <param name="title">标题文本</param>
    /// <param name="fontSize">字体大小</param>
    public static void AddTitle(FlowDocument document, string title, double fontSize = 24)
    {
        var paragraph = new Paragraph(new Run(title))
        {
            FontSize = fontSize,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20)
        };
        document.Blocks.Add(paragraph);
    }

    /// <summary>
    /// 为FlowDocument添加段落
    /// </summary>
    /// <param name="document">目标文档</param>
    /// <param name="text">段落文本</param>
    /// <param name="fontSize">字体大小</param>
    public static void AddParagraph(FlowDocument document, string text, double fontSize = 12)
    {
        var paragraph = new Paragraph(new Run(text))
        {
            FontSize = fontSize,
            Margin = new Thickness(0, 0, 0, 10)
        };
        document.Blocks.Add(paragraph);
    }

    /// <summary>
    /// 为FlowDocument添加表格
    /// </summary>
    /// <param name="document">目标文档</param>
    /// <param name="headers">表头</param>
    /// <param name="rows">数据行</param>
    public static void AddTable(FlowDocument document, string[] headers, IEnumerable<string[]> rows)
    {
        var table = new Table
        {
            CellSpacing = 0,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1)
        };

        // 添加列
        foreach (var _ in headers)
        {
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        }

        // 添加表头行组
        var headerGroup = new TableRowGroup();
        var headerRow = new TableRow { Background = Brushes.LightGray };
        foreach (var header in headers)
        {
            var cell = new TableCell(new Paragraph(new Run(header) { FontWeight = FontWeights.Bold }))
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(5)
            };
            headerRow.Cells.Add(cell);
        }
        headerGroup.Rows.Add(headerRow);
        table.RowGroups.Add(headerGroup);

        // 添加数据行组
        var dataGroup = new TableRowGroup();
        foreach (var row in rows)
        {
            var dataRow = new TableRow();
            foreach (var cellText in row)
            {
                var cell = new TableCell(new Paragraph(new Run(cellText ?? "")))
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(5)
                };
                dataRow.Cells.Add(cell);
            }
            dataGroup.Rows.Add(dataRow);
        }
        table.RowGroups.Add(dataGroup);

        document.Blocks.Add(table);
    }

    /// <summary>
    /// 添加分页符
    /// </summary>
    /// <param name="document">目标文档</param>
    public static void AddPageBreak(FlowDocument document)
    {
        var paragraph = new Paragraph
        {
            BreakPageBefore = true
        };
        document.Blocks.Add(paragraph);
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 配置打印对话框为A4纵向
    /// </summary>
    private static void ConfigurePrintDialogForA4(PrintDialog printDialog)
    {
        try
        {
            // 设置打印区域为A4尺寸
            printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
            printDialog.PrintTicket.PageOrientation = PageOrientation.Portrait;
        }
        catch
        {
            // 某些打印机可能不支持设置，忽略错误
        }
    }

    /// <summary>
    /// 创建文档分页器
    /// </summary>
    private static DocumentPaginator CreatePaginator(FlowDocument document, PrintDialog printDialog)
    {
        // 克隆文档以避免修改原始文档
        var clonedDoc = CloneDocument(document);

        // 设置文档尺寸以匹配打印区域
        clonedDoc.PageWidth = printDialog.PrintableAreaWidth;
        clonedDoc.PageHeight = printDialog.PrintableAreaHeight;
        clonedDoc.ColumnWidth = double.PositiveInfinity;

        // 获取分页器
        var paginator = ((IDocumentPaginatorSource)clonedDoc).DocumentPaginator;
        paginator.PageSize = new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);

        return paginator;
    }

    /// <summary>
    /// 克隆FlowDocument
    /// </summary>
    private static FlowDocument CloneDocument(FlowDocument source)
    {
        // 使用XAML序列化来克隆文档
        var xaml = System.Windows.Markup.XamlWriter.Save(source);
        using var reader = new System.IO.StringReader(xaml);
        using var xmlReader = System.Xml.XmlReader.Create(reader);
        return (FlowDocument)System.Windows.Markup.XamlReader.Load(xmlReader);
    }

    #endregion
}

/// <summary>
/// 打印设置
/// </summary>
public class PrintSettings
{
    /// <summary>
    /// 打印机名称
    /// </summary>
    public string? PrinterName { get; set; }

    /// <summary>
    /// 是否彩色打印
    /// </summary>
    public bool IsColorPrint { get; set; } = true;

    /// <summary>
    /// 打印份数
    /// </summary>
    public int Copies { get; set; } = 1;

    /// <summary>
    /// 纸张大小
    /// </summary>
    public PageMediaSizeName PageSize { get; set; } = PageMediaSizeName.ISOA4;

    /// <summary>
    /// 页面方向
    /// </summary>
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

    /// <summary>
    /// 是否双面打印
    /// </summary>
    public bool IsDuplex { get; set; } = false;
}
