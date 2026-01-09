using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ToolHelper.LoggingDiagnostics.Abstractions;
using ToolHelper.LoggingDiagnostics.Configuration;
using ToolHelper.LoggingDiagnostics.ErrorManagement;
using ToolHelper.LoggingDiagnostics.Extensions;

namespace ToolHelperTest.Examples.LoggingDiagnostics;

/// <summary>
/// ErrorCodeManager 使用示例
/// 演示多语言错误描述、错误码注册和查询功能
/// </summary>
public class ErrorCodeManagerExample
{
    /// <summary>
    /// 示例 1: 基本错误码使用
    /// </summary>
    public static void BasicErrorCodeUsage()
    {
        Console.WriteLine("=== ErrorCodeManager 基本使用示例 ===\n");

        var options = Options.Create(new ErrorCodeOptions
        {
            DefaultCulture = "zh-CN"
        });

        var errorManager = new ErrorCodeManager(options);

        // 使用内置的通用错误码
        Console.WriteLine("内置错误码测试:");
        Console.WriteLine($"  E0001: {errorManager.GetMessage("E0001", args: "连接超时")}");
        Console.WriteLine($"  E0002: {errorManager.GetMessage("E0002", args: "userId")}");
        Console.WriteLine($"  E1001: {errorManager.GetMessage("E1001", args: "无法连接服务器")}");

        Console.WriteLine("\n? 基本错误码使用完成\n");
    }

    /// <summary>
    /// 示例 2: 自定义错误码注册
    /// </summary>
    public static void CustomErrorCodeRegistration()
    {
        Console.WriteLine("=== ErrorCodeManager 自定义错误码示例 ===\n");

        var options = Options.Create(new ErrorCodeOptions
        {
            DefaultCulture = "zh-CN",
            AllowOverwrite = true
        });

        var errorManager = new ErrorCodeManager(options);

        // 注册自定义错误码
        errorManager.Register(new ErrorCodeInfo
        {
            Code = "BIZ1001",
            Category = "Business",
            Severity = ErrorSeverity.Error,
            DefaultMessage = "订单不存在",
            LocalizedMessages = new Dictionary<string, string>
            {
                ["zh-CN"] = "订单 {0} 不存在",
                ["en-US"] = "Order {0} does not exist",
                ["ja-JP"] = "注文 {0} は存在しません"
            },
            SuggestedSolution = "请检查订单号是否正确",
            IsRetryable = false
        });

        errorManager.Register(new ErrorCodeInfo
        {
            Code = "BIZ1002",
            Category = "Business",
            Severity = ErrorSeverity.Warning,
            DefaultMessage = "库存不足",
            LocalizedMessages = new Dictionary<string, string>
            {
                ["zh-CN"] = "商品 {0} 库存不足，当前库存: {1}",
                ["en-US"] = "Product {0} out of stock, current stock: {1}"
            },
            IsRetryable = true
        });

        // 获取不同语言的消息
        Console.WriteLine("订单错误 (中文): " + 
            errorManager.GetMessage("BIZ1001", CultureInfo.GetCultureInfo("zh-CN"), "ORD-12345"));
        Console.WriteLine("订单错误 (英文): " + 
            errorManager.GetMessage("BIZ1001", CultureInfo.GetCultureInfo("en-US"), "ORD-12345"));
        Console.WriteLine("订单错误 (日文): " + 
            errorManager.GetMessage("BIZ1001", CultureInfo.GetCultureInfo("ja-JP"), "ORD-12345"));

        Console.WriteLine("\n库存警告: " + 
            errorManager.GetMessage("BIZ1002", args: new object[] { "SKU-001", 5 }));

        Console.WriteLine("\n? 自定义错误码注册完成\n");
    }

    /// <summary>
    /// 示例 3: 创建错误结果
    /// </summary>
    public static void CreateErrorResult()
    {
        Console.WriteLine("=== ErrorCodeManager 创建错误结果示例 ===\n");

        var options = Options.Create(new ErrorCodeOptions
        {
            DefaultCulture = "zh-CN"
        });

        var errorManager = new ErrorCodeManager(options);

        // 注册错误码
        errorManager.Register(new ErrorCodeInfo
        {
            Code = "PAY001",
            Category = "Payment",
            Severity = ErrorSeverity.Critical,
            DefaultMessage = "支付失败: {0}",
            LocalizedMessages = new Dictionary<string, string>
            {
                ["zh-CN"] = "支付失败: {0}",
                ["en-US"] = "Payment failed: {0}"
            },
            SuggestedSolution = "请检查支付账户余额或联系客服"
        });

        // 创建错误结果
        var error = errorManager.CreateError("PAY001", 
            args: new object[] { "余额不足" },
            context: new Dictionary<string, object>
            {
                ["OrderId"] = "ORD-12345",
                ["Amount"] = 299.99m,
                ["PaymentMethod"] = "Alipay"
            });

        Console.WriteLine($"错误码: {error.Code}");
        Console.WriteLine($"消息: {error.Message}");
        Console.WriteLine($"级别: {error.Severity}");
        Console.WriteLine($"时间: {error.Timestamp}");
        Console.WriteLine($"解决方案: {error.SuggestedSolution}");
        Console.WriteLine("上下文:");
        if (error.Context != null)
        {
            foreach (var kvp in error.Context)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
        }

        Console.WriteLine("\n? 创建错误结果完成\n");
    }

    /// <summary>
    /// 示例 4: 按类别查询错误码
    /// </summary>
    public static void QueryErrorCodesByCategory()
    {
        Console.WriteLine("=== ErrorCodeManager 按类别查询示例 ===\n");

        var options = Options.Create(new ErrorCodeOptions());
        var errorManager = new ErrorCodeManager(options);

        // 批量注册错误码
        errorManager.RegisterRange(new[]
        {
            new ErrorCodeInfo { Code = "AUTH001", Category = "Authentication", Severity = ErrorSeverity.Error, DefaultMessage = "用户未登录" },
            new ErrorCodeInfo { Code = "AUTH002", Category = "Authentication", Severity = ErrorSeverity.Error, DefaultMessage = "Token已过期" },
            new ErrorCodeInfo { Code = "AUTH003", Category = "Authentication", Severity = ErrorSeverity.Warning, DefaultMessage = "权限不足" },
            new ErrorCodeInfo { Code = "VALID001", Category = "Validation", Severity = ErrorSeverity.Warning, DefaultMessage = "必填字段缺失" },
            new ErrorCodeInfo { Code = "VALID002", Category = "Validation", Severity = ErrorSeverity.Warning, DefaultMessage = "格式不正确" }
        });

        // 按类别查询
        Console.WriteLine("认证相关错误码:");
        foreach (var code in errorManager.GetByCategory("Authentication"))
        {
            Console.WriteLine($"  {code.Code}: {code.DefaultMessage} ({code.Severity})");
        }

        Console.WriteLine("\n验证相关错误码:");
        foreach (var code in errorManager.GetByCategory("Validation"))
        {
            Console.WriteLine($"  {code.Code}: {code.DefaultMessage} ({code.Severity})");
        }

        Console.WriteLine($"\n总错误码数量: {errorManager.GetAll().Count}");
        Console.WriteLine("\n? 按类别查询完成\n");
    }

    /// <summary>
    /// 示例 5: 错误码导出与加载
    /// </summary>
    public static async Task ExportAndLoadAsync()
    {
        Console.WriteLine("=== ErrorCodeManager 导出与加载示例 ===\n");

        var options = Options.Create(new ErrorCodeOptions());
        var errorManager = new ErrorCodeManager(options);

        // 注册一些错误码
        errorManager.RegisterRange(new[]
        {
            new ErrorCodeInfo 
            { 
                Code = "APP001", 
                Category = "Application", 
                Severity = ErrorSeverity.Error, 
                DefaultMessage = "应用启动失败",
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["zh-CN"] = "应用启动失败: {0}",
                    ["en-US"] = "Application startup failed: {0}"
                }
            },
            new ErrorCodeInfo 
            { 
                Code = "APP002", 
                Category = "Application", 
                Severity = ErrorSeverity.Critical, 
                DefaultMessage = "配置加载失败",
                SuggestedSolution = "请检查配置文件格式"
            }
        });

        // 导出到文件
        var exportPath = "error_codes_export.json";
        await errorManager.ExportToFileAsync(exportPath);
        Console.WriteLine($"? 错误码已导出到: {exportPath}");

        // 显示导出的内容
        var content = await File.ReadAllTextAsync(exportPath);
        Console.WriteLine("\n导出内容预览:");
        Console.WriteLine(content[..Math.Min(500, content.Length)] + "...");

        // 清理
        File.Delete(exportPath);

        Console.WriteLine("\n? 导出与加载示例完成\n");
    }

    /// <summary>
    /// 示例 6: 切换语言
    /// </summary>
    public static void LanguageSwitching()
    {
        Console.WriteLine("=== ErrorCodeManager 语言切换示例 ===\n");

        var options = Options.Create(new ErrorCodeOptions
        {
            DefaultCulture = "zh-CN"
        });

        var errorManager = new ErrorCodeManager(options);

        errorManager.Register(new ErrorCodeInfo
        {
            Code = "MSG001",
            Category = "Message",
            Severity = ErrorSeverity.Info,
            DefaultMessage = "操作成功",
            LocalizedMessages = new Dictionary<string, string>
            {
                ["zh-CN"] = "操作成功完成",
                ["zh-TW"] = "操作成功完成",
                ["en-US"] = "Operation completed successfully",
                ["ja-JP"] = "操作が正常に完了しました",
                ["ko-KR"] = "??? ????? ???????"
            }
        });

        // 切换不同语言
        var cultures = new[] { "zh-CN", "en-US", "ja-JP", "ko-KR" };
        
        foreach (var cultureName in cultures)
        {
            errorManager.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            var message = errorManager.GetMessage("MSG001");
            Console.WriteLine($"  [{cultureName}] {message}");
        }

        Console.WriteLine("\n? 语言切换示例完成\n");
    }

    /// <summary>
    /// 示例 7: 使用依赖注入
    /// </summary>
    public static void DependencyInjectionExample()
    {
        Console.WriteLine("=== ErrorCodeManager 依赖注入示例 ===\n");

        var services = new ServiceCollection();
        services.AddErrorCodeManager(options =>
        {
            options.DefaultCulture = "zh-CN";
            options.AllowOverwrite = true;
        });

        using var serviceProvider = services.BuildServiceProvider();
        var errorManager = serviceProvider.GetRequiredService<IErrorCodeManager>();

        errorManager.Register(new ErrorCodeInfo
        {
            Code = "DI001",
            Category = "Test",
            Severity = ErrorSeverity.Info,
            DefaultMessage = "依赖注入测试成功"
        });

        Console.WriteLine(errorManager.GetMessage("DI001"));
        Console.WriteLine("\n? 依赖注入示例完成\n");
    }

    /// <summary>
    /// 运行所有示例
    /// </summary>
    public static async Task RunAllAsync()
    {
        Console.WriteLine("╔════════════════════════════════════════╗");
        Console.WriteLine("║    ErrorCodeManager 使用示例演示       ║");
        Console.WriteLine("╚════════════════════════════════════════╝\n");

        BasicErrorCodeUsage();
        CustomErrorCodeRegistration();
        CreateErrorResult();
        QueryErrorCodesByCategory();
        await ExportAndLoadAsync();
        LanguageSwitching();
        DependencyInjectionExample();

        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("所有 ErrorCodeManager 示例执行完成！");
        Console.WriteLine("═══════════════════════════════════════════\n");
    }
}
