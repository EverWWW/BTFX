using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ToolHelper.DataProcessing.Compression;
using ToolHelper.DataProcessing.Configuration;
using ToolHelper.DataProcessing.Csv;
using ToolHelper.DataProcessing.Excel;
using ToolHelper.DataProcessing.Ini;
using ToolHelper.DataProcessing.Json;
using ToolHelper.DataProcessing.Pdf;
using ToolHelper.DataProcessing.Xml;
using ToolHelper.DataProcessing.Yaml;

namespace ToolHelper.DataProcessing.Extensions;

/// <summary>
/// 数据处理模块依赖注入扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加所有数据处理服务（不包括需要额外NuGet包的服务）
    /// </summary>
    public static IServiceCollection AddDataProcessing(this IServiceCollection services)
    {
        services.AddCsvHelper();
        services.AddJsonHelper();
        services.AddXmlHelper();
        services.AddIniHelper();

        return services;
    }

    /// <summary>
    /// 添加CSV处理服务
    /// </summary>
    public static IServiceCollection AddCsvHelper(
        this IServiceCollection services,
        Action<CsvOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            // 注册默认配置
            services.Configure<CsvOptions>(options => { });
        }

        services.TryAddTransient(typeof(CsvHelper<>));

        return services;
    }

    /// <summary>
    /// 添加JSON处理服务
    /// </summary>
    public static IServiceCollection AddJsonHelper(
        this IServiceCollection services,
        Action<JsonOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            // 注册默认配置
            services.Configure<JsonOptions>(options => { });
        }

        services.TryAddSingleton<JsonHelper>();

        return services;
    }

    /// <summary>
    /// 添加XML处理服务
    /// </summary>
    public static IServiceCollection AddXmlHelper(
        this IServiceCollection services,
        Action<XmlOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            // 注册默认配置
            services.Configure<XmlOptions>(options => { });
        }

        services.TryAddSingleton<XmlHelper>();

        return services;
    }

    /// <summary>
    /// 添加INI文件处理服务
    /// </summary>
    public static IServiceCollection AddIniHelper(
        this IServiceCollection services,
        Action<IniOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            // 注册默认配置
            services.Configure<IniOptions>(options => { });
        }

        services.TryAddTransient<IniFileHelper>();

        return services;
    }

    /// <summary>
    /// 添加YAML处理服务
    /// 注意：需要先安装 YamlDotNet NuGet 包
    /// </summary>
    public static IServiceCollection AddYamlHelper(
        this IServiceCollection services,
        Action<YamlOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            // 注册默认配置
            services.Configure<YamlOptions>(options => { });
        }

        services.TryAddSingleton<YamlHelper>();

        return services;
    }

    /// <summary>
    /// 添加Excel处理服务
    /// 注意：需要先安装 NPOI 或 EPPlus NuGet 包
    /// </summary>
    public static IServiceCollection AddExcelHelper(
        this IServiceCollection services,
        Action<ExcelOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            // 注册默认配置
            services.Configure<ExcelOptions>(options => { });
        }

        services.TryAddTransient(typeof(ExcelHelper<>));

        return services;
    }

    /// <summary>
    /// 添加PDF处理服务
    /// 注意：需要先安装 QuestPDF 或 iText7 NuGet 包
    /// </summary>
    public static IServiceCollection AddPdfHelper(
        this IServiceCollection services,
        Action<PdfOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            // 注册默认配置
            services.Configure<PdfOptions>(options => { });
        }

                services.TryAddSingleton<PdfHelper>();

                return services;
            }

            /// <summary>
            /// 添加ZIP压缩助手服务
            /// </summary>
            public static IServiceCollection AddZipHelper(
                this IServiceCollection services,
                Action<ZipOptions>? configure = null)
            {
                if (configure != null)
                {
                    services.Configure(configure);
                }
                else
                {
                    // 注册默认配置
                    services.Configure<ZipOptions>(options => { });
                }

                services.TryAddSingleton<ZipHelper>();

                return services;
            }
        }
