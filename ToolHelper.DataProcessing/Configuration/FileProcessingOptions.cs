namespace ToolHelper.DataProcessing.Configuration;

/// <summary>
/// CSV 文件处理配置选项
/// </summary>
public class CsvOptions
{
    /// <summary>
    /// 字段分隔符，默认为逗号
    /// </summary>
    public string Delimiter { get; set; } = ",";

    /// <summary>
    /// 是否包含标题行
    /// </summary>
    public bool HasHeader { get; set; } = true;

    /// <summary>
    /// 文本引用符，默认为双引号
    /// </summary>
    public char Quote { get; set; } = '"';

    /// <summary>
    /// 编码格式
    /// </summary>
    public string Encoding { get; set; } = "UTF-8";

    /// <summary>
    /// 流式读取时的批次大小
    /// </summary>
    public int StreamBatchSize { get; set; } = 1000;

    /// <summary>
    /// 是否自动检测编码
    /// </summary>
    public bool AutoDetectEncoding { get; set; } = true;

    /// <summary>
    /// 是否忽略空行
    /// </summary>
    public bool IgnoreEmptyLines { get; set; } = true;

    /// <summary>
    /// 是否去除字段首尾空格
    /// </summary>
    public bool TrimFields { get; set; } = true;
}

/// <summary>
/// JSON 处理配置选项
/// </summary>
public class JsonOptions
{
    /// <summary>
    /// 是否缩进格式化
    /// </summary>
    public bool Indented { get; set; } = true;

    /// <summary>
    /// 是否忽略空值
    /// </summary>
    public bool IgnoreNullValues { get; set; } = false;

    /// <summary>
    /// 是否允许注释
    /// </summary>
    public bool AllowComments { get; set; } = true;

    /// <summary>
    /// 属性命名策略（CamelCase, PascalCase等）
    /// </summary>
    public string PropertyNamingPolicy { get; set; } = "CamelCase";

    /// <summary>
    /// 是否使用字符串枚举值
    /// </summary>
    public bool UseStringEnumConverter { get; set; } = true;

    /// <summary>
    /// 最大读取深度
    /// </summary>
    public int MaxDepth { get; set; } = 64;
}

/// <summary>
/// XML 处理配置选项
/// </summary>
public class XmlOptions
{
    /// <summary>
    /// 根元素名称
    /// </summary>
    public string RootElement { get; set; } = "Root";

    /// <summary>
    /// 是否格式化输出
    /// </summary>
    public bool Indent { get; set; } = true;

    /// <summary>
    /// 缩进字符
    /// </summary>
    public string IndentChars { get; set; } = "  ";

    /// <summary>
    /// 编码格式
    /// </summary>
    public string Encoding { get; set; } = "UTF-8";

    /// <summary>
    /// 是否省略XML声明
    /// </summary>
    public bool OmitXmlDeclaration { get; set; } = false;

    /// <summary>
    /// 命名空间URI
    /// </summary>
    public string? NamespaceUri { get; set; }
}

/// <summary>
/// INI 文件处理配置选项
/// </summary>
public class IniOptions
{
    /// <summary>
    /// 注释字符
    /// </summary>
    public char CommentChar { get; set; } = ';';

    /// <summary>
    /// 编码格式
    /// </summary>
    public string Encoding { get; set; } = "UTF-8";

    /// <summary>
    /// 是否区分大小写
    /// </summary>
    public bool CaseSensitive { get; set; } = false;

    /// <summary>
    /// 键值分隔符
    /// </summary>
    public char Separator { get; set; } = '=';

    /// <summary>
    /// 是否去除键值首尾空格
    /// </summary>
    public bool TrimValues { get; set; } = true;
}

/// <summary>
/// YAML 文件处理配置选项
/// </summary>
public class YamlOptions
{
    /// <summary>
    /// 编码格式
    /// </summary>
    public string Encoding { get; set; } = "UTF-8";

    /// <summary>
    /// 缩进空格数
    /// </summary>
    public int IndentSize { get; set; } = 2;

    /// <summary>
    /// 是否忽略别名
    /// </summary>
    public bool IgnoreAliases { get; set; } = false;

    /// <summary>
    /// 最大递归深度
    /// </summary>
    public int MaxRecursion { get; set; } = 50;
}

/// <summary>
/// Excel 文件处理配置选项
/// </summary>
public class ExcelOptions
{
    /// <summary>
    /// 工作表名称
    /// </summary>
    public string SheetName { get; set; } = "Sheet1";

    /// <summary>
    /// 是否包含标题行
    /// </summary>
    public bool HasHeader { get; set; } = true;

    /// <summary>
    /// 起始行索引（从0开始）
    /// </summary>
    public int StartRow { get; set; } = 0;

    /// <summary>
    /// 起始列索引（从0开始）
    /// </summary>
    public int StartColumn { get; set; } = 0;

    /// <summary>
    /// 日期格式
    /// </summary>
    public string DateFormat { get; set; } = "yyyy-MM-dd";

    /// <summary>
    /// 是否自动调整列宽
    /// </summary>
    public bool AutoSizeColumns { get; set; } = true;

    /// <summary>
    /// 默认列宽
    /// </summary>
    public int DefaultColumnWidth { get; set; } = 15;
}

/// <summary>
/// PDF 处理配置选项
/// </summary>
public class PdfOptions
{
    /// <summary>
    /// 页面大小（A4, A3等）
    /// </summary>
    public string PageSize { get; set; } = "A4";

    /// <summary>
    /// 页边距（单位：毫米）
    /// </summary>
    public float Margin { get; set; } = 20;

    /// <summary>
    /// 是否包含页码
    /// </summary>
    public bool IncludePageNumbers { get; set; } = true;

    /// <summary>
    /// 字体名称
    /// </summary>
    public string FontName { get; set; } = "宋体";

    /// <summary>
    /// 字体大小
    /// </summary>
    public int FontSize { get; set; } = 12;

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 作者
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// 主题
    /// </summary>
    public string Subject { get; set; } = string.Empty;
}
