namespace BTFX.Common;

/// <summary>
/// 应用程序常量定义
/// </summary>
public static class Constants
{
    #region 版本信息

    /// <summary>
    /// 发布版本号（显示在主界面、登录界面）
    /// </summary>
    public const string VERSION_DISPLAY = "V1";

    /// <summary>
    /// 完整版本号（显示在"关于我们"界面）
    /// </summary>
    public const string VERSION_FULL = "V1.0.0.1";

    /// <summary>
    /// 版权声明（显示在"关于我们"界面）
    /// </summary>
    public const string ALLRIGHTSRESERVED = "Copyright \u00A9 2024-2026";

    /// <summary>
    /// 内部版本号（用于开发测试）
    /// </summary>
    public const string VERSION_INTERNAL = "V1.0.0.20260108_alpha01";

    #endregion

    #region 项目标识

    /// <summary>
    /// 应用名称
    /// </summary>
    public const string APP_NAME = "BTFX";

    /// <summary>
    /// 应用显示名称
    /// </summary>
    public const string APP_DISPLAY_NAME = "步态智能分析系统";

    /// <summary>
    /// 应用英文显示名称
    /// </summary>
    public const string APP_DISPLAY_NAME_EN = "Gait Intelligent Analysis System";

    /// <summary>
    /// 单实例 Mutex 名称
    /// </summary>
    public const string MUTEX_NAME = @"Global\BTFX_SingleInstance_Mutex";

    #endregion

    #region 默认账号

    /// <summary>
    /// 默认密码
    /// </summary>
    public const string DEFAULT_PASSWORD = "688626";

    /// <summary>
    /// 管理员账号
    /// </summary>
    public const string ADMIN_USERNAME = "admin";

    /// <summary>
    /// 普通用户账号
    /// </summary>
    public const string USER_USERNAME = "user";

    /// <summary>
    /// 游客账号
    /// </summary>
    public const string GUEST_USERNAME = "guest";

    #endregion

    #region 验证规则

    /// <summary>
    /// 账号最小长度
    /// </summary>
    public const int USERNAME_MIN_LENGTH = 3;

    /// <summary>
    /// 账号最大长度
    /// </summary>
    public const int USERNAME_MAX_LENGTH = 20;

    /// <summary>
    /// 密码最小长度
    /// </summary>
    public const int PASSWORD_MIN_LENGTH = 6;

    /// <summary>
    /// 密码最大长度
    /// </summary>
    public const int PASSWORD_MAX_LENGTH = 20;

    /// <summary>
    /// 姓名最小长度
    /// </summary>
    public const int NAME_MIN_LENGTH = 2;

    /// <summary>
    /// 姓名最大长度
    /// </summary>
    public const int NAME_MAX_LENGTH = 30;

    /// <summary>
    /// 电话最小长度
    /// </summary>
    public const int PHONE_MIN_LENGTH = 3;

    /// <summary>
    /// 电话最大长度
    /// </summary>
    public const int PHONE_MAX_LENGTH = 12;

    /// <summary>
    /// 证件号最大长度
    /// </summary>
    public const int ID_NUMBER_MAX_LENGTH = 18;

    /// <summary>
    /// 搜索框最大长度
    /// </summary>
    public const int SEARCH_TEXT_MAX_LENGTH = 10;

    /// <summary>
    /// 单位名称最大长度
    /// </summary>
    public const int UNIT_NAME_MAX_LENGTH = 30;

    /// <summary>
    /// 科室名称最大长度
    /// </summary>
    public const int DEPARTMENT_NAME_MAX_LENGTH = 20;

    #endregion

    #region 数值范围

    /// <summary>
    /// 身高最小值 (cm)
    /// </summary>
    public const double HEIGHT_MIN = 1;

    /// <summary>
    /// 身高最大值 (cm)
    /// </summary>
    public const double HEIGHT_MAX = 300;

    /// <summary>
    /// 体重最小值 (kg)
    /// </summary>
    public const double WEIGHT_MIN = 1;

    /// <summary>
    /// 体重最大值 (kg)
    /// </summary>
    public const double WEIGHT_MAX = 500;

    /// <summary>
    /// 出生日期最早年份偏移（150年前）
    /// </summary>
    public const int BIRTH_YEAR_OFFSET = 150;

    #endregion

    #region 分页设置

    /// <summary>
    /// 患者列表每页条数
    /// </summary>
    public const int PATIENT_PAGE_SIZE = 8;

    /// <summary>
    /// 默认每页条数
    /// </summary>
    public const int DEFAULT_PAGE_SIZE = 20;

    /// <summary>
    /// 医生意见最大长度
    /// </summary>
    public const int DOCTOR_OPINION_MAX_LENGTH = 500;

    #endregion

    #region 文件路径

    /// <summary>
    /// 数据库文件名
    /// </summary>
    public const string DATABASE_FILENAME = "BTFX.db";

    /// <summary>
    /// 配置文件名
    /// </summary>
    public const string CONFIG_FILENAME = "appsettings.json";

    /// <summary>
    /// 备份文件前缀
    /// </summary>
    public const string BACKUP_PREFIX = "BTFX_Backup_";

    /// <summary>
    /// 日志文件前缀
    /// </summary>
    public const string LOG_PREFIX = "BTFX_";

    /// <summary>
    /// 数据库目录
    /// </summary>
    public const string DATABASE_DIRECTORY = "Data/Database";

    /// <summary>
    /// 备份目录
    /// </summary>
    public const string BACKUP_DIRECTORY = "Data/Backups";

    /// <summary>
    /// 日志目录
    /// </summary>
    public const string LOG_DIRECTORY = "Data/Logs";

    /// <summary>
    /// 报告目录
    /// </summary>
    public const string REPORT_DIRECTORY = "Data/Reports";

    /// <summary>
    /// 视频目录
    /// </summary>
    public const string VIDEO_DIRECTORY = "Data/Videos";

    /// <summary>
    /// 临时目录（游客数据）
    /// </summary>
    public const string TEMP_DIRECTORY = "Data/Temp";

    /// <summary>
    /// 配置目录
    /// </summary>
    public const string CONFIG_DIRECTORY = "Data/Config";

    #endregion

    #region 时间格式

    /// <summary>
    /// 日期格式
    /// </summary>
    public const string DATE_FORMAT = "yyyy-MM-dd";

    /// <summary>
    /// 日期时间格式
    /// </summary>
    public const string DATETIME_FORMAT = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// 包含星期的日期时间格式
    /// </summary>
    public const string DATETIME_WITH_WEEKDAY_FORMAT = "yyyy-MM-dd dddd HH:mm:ss";

    /// <summary>
    /// 列表显示的日期时间格式
    /// </summary>
    public const string DATETIME_LIST_FORMAT = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// 报告日期格式
    /// </summary>
    public const string DATE_REPORT_FORMAT = "yyyy年MM月dd日";

    /// <summary>
    /// 备份文件时间戳格式
    /// </summary>
    public const string BACKUP_TIMESTAMP_FORMAT = "yyyyMMdd_HHmmss";

    #endregion

    #region 日志设置

    /// <summary>
    /// 日志文件最大大小 (MB)
    /// </summary>
    public const int LOG_MAX_FILE_SIZE_MB = 10;

    /// <summary>
    /// 日志保留天数
    /// </summary>
    public const int LOG_RETENTION_DAYS = 30;

    #endregion

    #region 备份设置

    /// <summary>
    /// 默认备份时间
    /// </summary>
    public const string BACKUP_DEFAULT_TIME = "02:00";

    /// <summary>
    /// 默认保留备份数
    /// </summary>
    public const int BACKUP_DEFAULT_RETAIN_COUNT = 7;

    /// <summary>
    /// 操作日志保留天数
    /// </summary>
    public const int OPERATION_LOG_RETENTION_DAYS = 90;

    #endregion

    #region Logo设置

    /// <summary>
    /// Logo 最大文件大小 (KB)
    /// </summary>
    public const int LOGO_MAX_SIZE_KB = 300;

    #endregion

    #region 窗口尺寸

    /// <summary>
    /// 窗口最小宽度
    /// </summary>
    public const double WINDOW_MIN_WIDTH = 1280;

    /// <summary>
    /// 窗口最小高度
    /// </summary>
    public const double WINDOW_MIN_HEIGHT = 720;

    /// <summary>
    /// 窗口默认宽度
    /// </summary>
    public const double WINDOW_DEFAULT_WIDTH = 1920;

    /// <summary>
    /// 窗口默认高度
    /// </summary>
    public const double WINDOW_DEFAULT_HEIGHT = 1080;

    #endregion

    #region 算法分析常量

    /// <summary>
    /// 算法程序默认目录
    /// </summary>
    public const string ALGORITHM_DIRECTORY = "Algorithm";

    /// <summary>
    /// 算法程序默认文件名
    /// </summary>
    public const string ALGORITHM_EXE_FILENAME = "gait_analysis.exe";

    /// <summary>
    /// 算法默认超时时间（分钟）
    /// </summary>
    public const int ALGORITHM_DEFAULT_TIMEOUT_MINUTES = 10;

    /// <summary>
    /// 进程优雅关闭等待时间（秒）
    /// </summary>
    public const int ALGORITHM_GRACEFUL_SHUTDOWN_SECONDS = 5;

    /// <summary>
    /// 通讯协议版本
    /// </summary>
    public const string PROTOCOL_VERSION = "V2.0";

    /// <summary>
    /// 默认算法版本
    /// </summary>
    public const string DEFAULT_ALGORITHM_VERSION = "1.0.0";

    /// <summary>
    /// 默认模型版本
    /// </summary>
    public const string DEFAULT_MODEL_VERSION = "1.0.0";

    /// <summary>
    /// 任务类型标识
    /// </summary>
    public const string TASK_TYPE = "gait_analysis";

    /// <summary>
    /// 分析模式
    /// </summary>
    public const string ANALYSIS_MODE = "offline";

    /// <summary>
    /// 请求ID前缀
    /// </summary>
    public const string REQUEST_ID_PREFIX = "GAIT";

    /// <summary>
    /// 分析结果目录名
    /// </summary>
    public const string ANALYSIS_DIRECTORY = "Analysis";

    /// <summary>
    /// 算法输入配置文件名
    /// </summary>
    public const string TASK_CONFIG_FILENAME = "task_config.json";

    /// <summary>
    /// 算法输出汇总文件名
    /// </summary>
    public const string SUMMARY_FILENAME = "summary.json";

    /// <summary>
    /// 标注视频文件名
    /// </summary>
    public const string ANNOTATED_VIDEO_FILENAME = "annotated_video.mp4";

    /// <summary>
    /// 关节角度时间序列 CSV 文件名
    /// </summary>
    public const string JOINT_ANGLE_CSV_FILENAME = "joint_angle_timeseries.csv";

    /// <summary>
    /// 关键点轨迹 CSV 文件名
    /// </summary>
    public const string KEYPOINT_TRAJECTORY_CSV_FILENAME = "keypoint_trajectory.csv";

    /// <summary>
    /// 关键点速度 CSV 文件名
    /// </summary>
    public const string KEYPOINT_VELOCITY_CSV_FILENAME = "keypoint_velocity.csv";

    /// <summary>
    /// 关节角速度 CSV 文件名
    /// </summary>
    public const string JOINT_ANGULAR_VELOCITY_CSV_FILENAME = "joint_angular_velocity.csv";

    /// <summary>
    /// 算法调试日志文件名
    /// </summary>
    public const string ALGORITHM_DEBUG_LOG_FILENAME = "debug.log";

    /// <summary>
    /// 算法 stdout 状态消息类型标识
    /// </summary>
    public const string STATUS_MESSAGE_TYPE = "status";

    /// <summary>
    /// 视频预览 Timer 采样间隔（毫秒），约 30fps
    /// </summary>
    public const int VIDEO_TIMER_INTERVAL_MS = 33;

    /// <summary>
    /// 视频快进/快退步长（秒）
    /// </summary>
    public const double VIDEO_STEP_SECONDS = 1.0;

    #endregion
}
