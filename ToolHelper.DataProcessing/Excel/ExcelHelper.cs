using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using ToolHelper.DataProcessing.Abstractions;
using ToolHelper.DataProcessing.Configuration;

namespace ToolHelper.DataProcessing.Excel;

/// <summary>
/// Excel 文件处理帮助类
/// 提供 Excel 文件的读写、样式设置等功能
/// 基于 NPOI 实现，支持 .xls 和 .xlsx 格式
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class ExcelHelper<T> : IFileReader<T>, IFileWriter<T> where T : class, new()
{
    private readonly ExcelOptions _options;
    private readonly ILogger<ExcelHelper<T>>? _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">Excel配置选项（可选，使用默认配置时可为null）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public ExcelHelper(IOptions<ExcelOptions>? options = null, ILogger<ExcelHelper<T>>? logger = null)
    {
        _options = options?.Value ?? new ExcelOptions();
        _logger = logger;
    }

    #region IFileReader 实现

    /// <inheritdoc/>
    public async Task<IEnumerable<T>> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始读取Excel文件: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        var result = new List<T>();

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var workbook = CreateWorkbook(stream, filePath);
        var sheet = GetSheet(workbook);

        if (sheet == null)
        {
            _logger?.LogWarning("工作表 {SheetName} 不存在", _options.SheetName);
            return result;
        }

        var properties = typeof(T).GetProperties();
        var startRow = _options.StartRow + (_options.HasHeader ? 1 : 0);

        for (int i = startRow; i <= sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (row == null) continue;

            try
            {
                var obj = MapRowToObject(row, properties);
                result.Add(obj);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "解析第 {RowIndex} 行时出错", i);
            }
        }

        _logger?.LogInformation("Excel文件读取完成，共 {Count} 条记录", result.Count);
        return result;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T> ReadStreamAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始流式读取Excel文件: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var workbook = CreateWorkbook(stream, filePath);
        var sheet = GetSheet(workbook);

        if (sheet == null) yield break;

        var properties = typeof(T).GetProperties();
        var startRow = _options.StartRow + (_options.HasHeader ? 1 : 0);

        for (int i = startRow; i <= sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (row == null) continue;

            T? obj = null;
            try
            {
                obj = MapRowToObject(row, properties);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "解析第 {RowIndex} 行时出错", i);
            }

            if (obj != null)
            {
                yield return obj;
            }
        }
    }

    #endregion

    #region IFileWriter 实现

    /// <inheritdoc/>
    public async Task WriteAsync(string filePath, IEnumerable<T> data, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始写入Excel文件: {FilePath}", filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var workbook = CreateNewWorkbook(filePath);
        var sheet = workbook.CreateSheet(_options.SheetName);
        var properties = typeof(T).GetProperties();

        int rowIndex = _options.StartRow;

        // 写入标题行
        if (_options.HasHeader)
        {
            var headerRow = sheet.CreateRow(rowIndex++);
            var headerStyle = CreateHeaderStyle(workbook);

            for (int i = 0; i < properties.Length; i++)
            {
                var cell = headerRow.CreateCell(i + _options.StartColumn);
                cell.SetCellValue(properties[i].Name);
                cell.CellStyle = headerStyle;
            }
        }

        // 写入数据行
        foreach (var item in data)
        {
            var dataRow = sheet.CreateRow(rowIndex++);

            for (int i = 0; i < properties.Length; i++)
            {
                var cell = dataRow.CreateCell(i + _options.StartColumn);
                var value = properties[i].GetValue(item);
                SetCellValue(cell, value);
            }
        }

        // 自动调整列宽
        if (_options.AutoSizeColumns)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                sheet.AutoSizeColumn(i + _options.StartColumn);
            }
        }

        // 保存文件
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        workbook.Write(stream);

        _logger?.LogInformation("Excel文件写入完成");
    }

    /// <inheritdoc/>
    public async Task AppendAsync(string filePath, IEnumerable<T> data, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("开始追加写入Excel文件: {FilePath}", filePath);

        IWorkbook workbook;
        ISheet sheet;

        if (File.Exists(filePath))
        {
            // 打开现有文件
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            workbook = CreateWorkbook(stream, filePath);
            sheet = GetSheet(workbook) ?? workbook.CreateSheet(_options.SheetName);
        }
        else
        {
            // 创建新文件
            workbook = CreateNewWorkbook(filePath);
            sheet = workbook.CreateSheet(_options.SheetName);
        }

        var properties = typeof(T).GetProperties();
        int rowIndex = sheet.LastRowNum + 1;

        // 如果是新文件且需要标题行
        if (rowIndex == 0 && _options.HasHeader)
        {
            var headerRow = sheet.CreateRow(rowIndex++);
            var headerStyle = CreateHeaderStyle(workbook);

            for (int i = 0; i < properties.Length; i++)
            {
                var cell = headerRow.CreateCell(i + _options.StartColumn);
                cell.SetCellValue(properties[i].Name);
                cell.CellStyle = headerStyle;
            }
        }

        // 追加数据行
        foreach (var item in data)
        {
            var dataRow = sheet.CreateRow(rowIndex++);

            for (int i = 0; i < properties.Length; i++)
            {
                var cell = dataRow.CreateCell(i + _options.StartColumn);
                var value = properties[i].GetValue(item);
                SetCellValue(cell, value);
            }
        }

        // 保存文件
        using var outStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        workbook.Write(outStream);

        _logger?.LogInformation("Excel文件追加完成");
    }

    #endregion

    #region 私有辅助方法

    private IWorkbook CreateWorkbook(Stream stream, string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return extension == ".xlsx" 
            ? new XSSFWorkbook(stream) 
            : new HSSFWorkbook(stream);
    }

    private IWorkbook CreateNewWorkbook(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return extension == ".xlsx" 
            ? new XSSFWorkbook() 
            : new HSSFWorkbook();
    }

    private ISheet? GetSheet(IWorkbook workbook)
    {
        return workbook.GetSheet(_options.SheetName) ?? workbook.GetSheetAt(0);
    }

    private ICellStyle CreateHeaderStyle(IWorkbook workbook)
    {
        var style = workbook.CreateCellStyle();
        var font = workbook.CreateFont();
        font.IsBold = true;
        style.SetFont(font);
        style.FillForegroundColor = IndexedColors.Grey25Percent.Index;
        style.FillPattern = FillPattern.SolidForeground;
        style.Alignment = HorizontalAlignment.Center;
        style.VerticalAlignment = VerticalAlignment.Center;
        return style;
    }

    private T MapRowToObject(IRow row, System.Reflection.PropertyInfo[] properties)
    {
        var obj = new T();

        for (int i = 0; i < properties.Length && i < row.LastCellNum; i++)
        {
            var cell = row.GetCell(i + _options.StartColumn);
            if (cell == null) continue;

            try
            {
                var value = GetCellValue(cell);
                if (value != null && properties[i].CanWrite)
                {
                    SetPropertyValue(properties[i], obj, value);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "设置属性 {PropertyName} 值时出错", properties[i].Name);
            }
        }

        return obj;
    }

    private object? GetCellValue(ICell cell)
    {
        return cell.CellType switch
        {
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell) 
                ? cell.DateCellValue 
                : cell.NumericCellValue,
            CellType.String => cell.StringCellValue,
            CellType.Boolean => cell.BooleanCellValue,
            CellType.Formula => cell.StringCellValue,
            _ => null
        };
    }

    private void SetCellValue(ICell cell, object? value)
    {
        if (value == null)
        {
            cell.SetCellValue(string.Empty);
            return;
        }

        switch (value)
        {
            case int intValue:
                cell.SetCellValue(intValue);
                break;
            case long longValue:
                cell.SetCellValue(longValue);
                break;
            case double doubleValue:
                cell.SetCellValue(doubleValue);
                break;
            case decimal decimalValue:
                cell.SetCellValue((double)decimalValue);
                break;
            case DateTime dateValue:
                cell.SetCellValue(dateValue);
                break;
            case bool boolValue:
                cell.SetCellValue(boolValue);
                break;
            default:
                cell.SetCellValue(value.ToString());
                break;
        }
    }

    private void SetPropertyValue(System.Reflection.PropertyInfo property, T obj, object value)
    {
        try
        {
            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (value.GetType() == targetType)
            {
                property.SetValue(obj, value);
                return;
            }

            var convertedValue = Convert.ChangeType(value, targetType);
            property.SetValue(obj, convertedValue);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "设置属性 {PropertyName} 值时类型转换失败", property.Name);
        }
    }

    #endregion
}
