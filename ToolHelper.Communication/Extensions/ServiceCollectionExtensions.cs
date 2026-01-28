using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ToolHelper.Communication.Bluetooth;
using ToolHelper.Communication.Configuration;
using ToolHelper.Communication.Http;
using ToolHelper.Communication.Modbus;
using ToolHelper.Communication.SerialPort;
using ToolHelper.Communication.Tcp;
using ToolHelper.Communication.Udp;
using ToolHelper.Communication.WebSocket;

namespace ToolHelper.Communication.Extensions;

/// <summary>
/// 通信模块依赖注入扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加通信模块所有服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddCommunication(this IServiceCollection services)
    {
        services.AddTcpClient();
        services.AddTcpServer();
        services.AddUdp();
        services.AddHttp();
        services.AddSerialPort();
        services.AddWebSocket();
        services.AddModbusTcp();
        services.AddModbusRtu();
        services.AddBluetooth();

        return services;
    }

    /// <summary>
    /// 添加 TCP 客户端服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddTcpClient(
        this IServiceCollection services,
        Action<TcpClientOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.TryAddTransient<TcpClientHelper>();

        return services;
    }

    /// <summary>
    /// 添加 TCP 服务器服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddTcpServer(
        this IServiceCollection services,
        Action<TcpServerOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<TcpServerHelper>();

        return services;
    }

    /// <summary>
    /// 添加 UDP 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddUdp(
        this IServiceCollection services,
        Action<UdpOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.TryAddTransient<UdpHelper>();

        return services;
    }

        /// <summary>
        /// 添加 HTTP 服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configure">配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddHttp(
            this IServiceCollection services,
            Action<HttpOptions>? configure = null)
        {
            if (configure != null)
            {
                services.Configure(configure);
            }

            services.TryAddTransient<HttpHelper>();

            return services;
        }

        /// <summary>
        /// 添加串口通信服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configure">配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddSerialPort(
            this IServiceCollection services,
            Action<SerialPortOptions>? configure = null)
        {
            if (configure != null)
            {
                services.Configure(configure);
            }

            services.TryAddTransient<SerialPortHelper>();

            return services;
        }

        /// <summary>
        /// 添加 WebSocket 服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configure">配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddWebSocket(
            this IServiceCollection services,
            Action<WebSocketOptions>? configure = null)
        {
            if (configure != null)
            {
                services.Configure(configure);
            }

            services.TryAddTransient<WebSocketHelper>();

            return services;
        }

        /// <summary>
        /// 添加 WebSocket 服务端服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configure">配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddWebSocketServer(
            this IServiceCollection services,
            Action<WebSocketServerOptions>? configure = null)
        {
            if (configure != null)
            {
                services.Configure(configure);
            }

            services.TryAddSingleton<WebSocketServerHelper>();

            return services;
        }

        /// <summary>
        /// 添加 Modbus TCP 服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configure">配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddModbusTcp(
            this IServiceCollection services,
            Action<ModbusTcpOptions>? configure = null)
        {
            if (configure != null)
            {
                services.Configure(configure);
            }

            services.TryAddTransient<ModbusTcpHelper>();

            return services;
        }

        /// <summary>
        /// 添加 Modbus RTU 服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configure">配置委托</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddModbusRtu(
            this IServiceCollection services,
            Action<ModbusRtuOptions>? configure = null)
        {
            if (configure != null)
            {
                services.Configure(configure);
            }

                services.TryAddTransient<ModbusRtuHelper>();

                    return services;
                }

                /// <summary>
                /// 添加蓝牙通讯服务
                /// </summary>
                /// <param name="services">服务集合</param>
                /// <param name="configure">配置委托</param>
                /// <returns>服务集合</returns>
                public static IServiceCollection AddBluetooth(
                    this IServiceCollection services,
                    Action<BluetoothOptions>? configure = null)
                {
                    if (configure != null)
                    {
                        services.Configure(configure);
                    }

                    services.TryAddTransient<BluetoothHelper>();

                    return services;
                }
            }
