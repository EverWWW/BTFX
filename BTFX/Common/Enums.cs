using System.ComponentModel;

namespace BTFX.Common;

/// <summary>
/// �û���ɫ
/// </summary>
public enum UserRole
{
    /// <summary>
    /// ����Ա - ӵ��ϵͳ���Ȩ��
    /// </summary>
    [Description("����Ա")]
    Administrator = 0,

    /// <summary>
    /// ����Ա - ӵ�к���ҵ�����Ȩ��
    /// </summary>
    [Description("����Ա")]
    Operator = 1,

    /// <summary>
    /// �ο� - ���޹���
    /// </summary>
    [Description("�ο�")]
    Guest = 2
}

/// <summary>
/// �Ա�
/// </summary>
public enum Gender
{
    /// <summary>
    /// ��
    /// </summary>
    [Description("��")]
    Male = 0,

    /// <summary>
    /// Ů
    /// </summary>
    [Description("Ů")]
    Female = 1
}

/// <summary>
/// ����״̬���߼�ɾ����
/// </summary>
public enum PatientStatus
{
    /// <summary>
    /// ����
    /// </summary>
    [Description("����")]
    Active = 0,

    /// <summary>
    /// ��ɾ��
    /// </summary>
    [Description("��ɾ��")]
    Deleted = 1
}

/// <summary>
/// ����״̬
/// </summary>
public enum MeasurementStatus
{
    /// <summary>
    /// ������
    /// </summary>
    [Description("������")]
    Pending = 0,

    /// <summary>
    /// ������
    /// </summary>
    [Description("������")]
    InProgress = 1,

    /// <summary>
    /// �����
    /// </summary>
    [Description("�����")]
    Completed = 2,

    /// <summary>
    /// ��ȡ��
    /// </summary>
    [Description("��ȡ��")]
    Cancelled = 3,

    /// <summary>
    /// ����ʧ��
    /// </summary>
    [Description("����ʧ��")]
    Failed = 4
}

/// <summary>
/// ����״̬
/// </summary>
public enum ReportStatus
{
    /// <summary>
    /// �ݸ�
    /// </summary>
    [Description("�ݸ�")]
    Draft = 0,

    /// <summary>
    /// �����
    /// </summary>
    [Description("�����")]
    Completed = 1,

    /// <summary>
    /// �Ѵ�ӡ
    /// </summary>
    [Description("�Ѵ�ӡ")]
    Printed = 2
}

/// <summary>
/// �豸����״̬
/// </summary>
public enum DeviceConnectionStatus
{
    /// <summary>
    /// δ����
    /// </summary>
    [Description("δ����")]
    Disconnected = 0,

    /// <summary>
    /// ������
    /// </summary>
    [Description("������")]
    Connecting = 1,

    /// <summary>
    /// ������
    /// </summary>
    [Description("������")]
    Connected = 2,

    /// <summary>
    /// ����ʧ��
    /// </summary>
    [Description("����ʧ��")]
    Failed = 3
}

/// <summary>
/// Ӧ������
/// </summary>
public enum AppTheme
{
    /// <summary>
    /// ǳɫ����
    /// </summary>
    [Description("ǳɫ")]
    Light = 0,

    /// <summary>
    /// ��ɫ����
    /// </summary>
    [Description("��ɫ")]
    Dark = 1
}

/// <summary>
/// Ӧ������
/// </summary>
public enum AppLanguage
{
    /// <summary>
    /// ��������
    /// </summary>
    [Description("��������")]
    ChineseSimplified = 0,

    /// <summary>
    /// Ӣ��
    /// </summary>
    [Description("English")]
    English = 1
}

/// <summary>
/// ������ʽ
/// </summary>
public enum ExportFormat
{
    /// <summary>
    /// Excel��ʽ
    /// </summary>
    [Description("Excel")]
    Excel = 0,

    /// <summary>
    /// CSV��ʽ
    /// </summary>
    [Description("CSV")]
    CSV = 1,

    /// <summary>
    /// PDF��ʽ
    /// </summary>
    [Description("PDF")]
    PDF = 2
}

/// <summary>
/// ����Ƶ��
/// </summary>
public enum BackupFrequency
{
    /// <summary>
    /// ÿ��
    /// </summary>
    [Description("ÿ��")]
    Daily = 0,

    /// <summary>
    /// ÿ��
    /// </summary>
    [Description("ÿ��")]
    Weekly = 1,

    /// <summary>
    /// ÿ��
    /// </summary>
    [Description("ÿ��")]
    Monthly = 2
}

/// <summary>
/// ������־����
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// ��Ϣ
    /// </summary>
    [Description("��Ϣ")]
    Info = 0,

    /// <summary>
    /// ����
    /// </summary>
    [Description("����")]
    Warning = 1,

    /// <summary>
    /// ����
    /// </summary>
    [Description("����")]
    Error = 2
}

/// <summary>
/// �û�״̬
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// ����
    /// </summary>
    [Description("����")]
    Enabled = 0,

    /// <summary>
    /// ����
    /// </summary>
    [Description("����")]
    Disabled = 1
}

#region ��������ģ��ö��

/// <summary>
/// ��������
/// </summary>
public enum MeasurementType
{
    /// <summary>
    /// ��Ȼ����
    /// </summary>
    [Description("��Ȼ����")]
    NormalWalk = 0,

    /// <summary>
    /// ����
    /// </summary>
    [Description("����")]
    FastWalk = 1,

    /// <summary>
    /// ����
    /// </summary>
    [Description("����")]
    SlowWalk = 2,

    /// <summary>
    /// ����
    /// </summary>
    [Description("����")]
    Other = 3
}

/// <summary>
/// �����׶�
/// </summary>
public enum AnalysisStage
{
    /// <summary>
    /// δ����
    /// </summary>
    [Description("δ����")]
    None = 0,

    /// <summary>
    /// �ؼ���ʶ��
    /// </summary>
    [Description("�ؼ���")]
    Keypoints = 1,

    /// <summary>
    /// ��̬�¼����
    /// </summary>
    [Description("��̬�¼�")]
    Events = 2,

    /// <summary>
    /// �˶�ѧ����
    /// </summary>
    [Description("�˶�ѧ")]
    Kinematics = 3
}

/// <summary>
/// ��Ƶ���
/// </summary>
public enum VideoSpec
{
    /// <summary>
    /// 1080P 30fps
    /// </summary>
    [Description("1080P / 30 FPS")]
    P1080_30fps = 0,

    /// <summary>
    /// 1440P 30fps
    /// </summary>
    [Description("1440P / 30 FPS")]
    P1440_30fps = 1
}

/// <summary>
/// �������
/// </summary>
public enum ImportStrategy
{
    /// <summary>
    /// ���Ƶ�����Ŀ¼
    /// </summary>
    [Description("���Ƶ�����Ŀ¼")]
    CopyToFolder = 0,

    /// <summary>
    /// ������ԭ·��
    /// </summary>
    [Description("������ԭ·��")]
    ReferenceOnly = 1
}

/// <summary>
/// ��������״̬
/// </summary>
public enum AnalysisTaskStatus
{
    /// <summary>
    /// δ����
    /// </summary>
    [Description("δ����")]
    NotRun = 0,

    /// <summary>
    /// ������
    /// </summary>
    [Description("������")]
    Running = 1,

    /// <summary>
    /// �����
    /// </summary>
    [Description("�����")]
    Completed = 2,

    /// <summary>
    /// ʧ��
    /// </summary>
    [Description("ʧ��")]
    Failed = 3
}

/// <summary>
/// ��Ƶ����ģʽ
/// </summary>
public enum VideoImportMode
{
    /// <summary>
    /// ����������Ƶ
    /// </summary>
    [Description("����")]
    Import = 0,

    /// <summary>
    /// ʵʱ�ɼ�
    /// </summary>
    [Description("�ɼ�")]
    Capture = 1
}

/// <summary>
/// �ɼ����沼��
/// </summary>
public enum CaptureLayout
{
    /// <summary>
    /// ����Ƶģʽ�����������棩
    /// </summary>
    [Description("����Ƶ")]
    Single = 0,

    /// <summary>
    /// ˫��Ƶģʽ������ + ���棩
    /// </summary>
    [Description("˫��Ƶ")]
    Dual = 1
}

/// <summary>
/// ¼��ʱ��ѡ��
/// </summary>
public enum RecordDuration
{
    /// <summary>
    /// 30 ��
    /// </summary>
    [Description("30��")]
    Seconds30 = 30,

    /// <summary>
    /// 1 ����
    /// </summary>
    [Description("1����")]
    Seconds60 = 60
}

/// <summary>
/// �ɼ�¼��״̬
/// </summary>
public enum CaptureState
{
    /// <summary>
    /// ���� - �ȴ���ʼ¼��
    /// </summary>
    Idle = 0,

    /// <summary>
    /// ¼����
    /// </summary>
    Recording = 1,

    /// <summary>
    /// ¼�����
    /// </summary>
    Completed = 2
}

/// <summary>
/// ����ģ�� UI ״̬
/// </summary>
public enum AnalysisState
{
    /// <summary>
    /// �������ȴ���ʼ����
    /// </summary>
    [Description("����")]
    Ready = 0,

    /// <summary>
    /// ����������
    /// </summary>
    [Description("������")]
    Running = 1,

    /// <summary>
    /// ������ɣ���ƵԤ����
    /// </summary>
    [Description("Ԥ����")]
    Previewing = 2,

    /// <summary>
    /// ����ʧ��
    /// </summary>
    [Description("ʧ��")]
    Failed = 3
}

/// <summary>
/// �㷨�˳�������
/// </summary>
public enum AnalysisErrorCode
{
    /// <summary>
    /// �ɹ�
    /// </summary>
    [Description("�ɹ�")]
    Success = 0,

    /// <summary>
    /// �����ļ�����
    /// </summary>
    [Description("�����ļ�����")]
    ConfigError = 1,

    /// <summary>
    /// �����ļ�������
    /// </summary>
    [Description("�����ļ�������")]
    InputFileNotFound = 2,

    /// <summary>
    /// ��Ƶ��ȡʧ��
    /// </summary>
    [Description("��Ƶ��ȡʧ��")]
    VideoReadFailed = 3,

    /// <summary>
    /// ��������ʧ��
    /// </summary>
    [Description("��������ʧ��")]
    AnalysisFailed = 4,

    /// <summary>
    /// �������ʧ��
    /// </summary>
    [Description("�������ʧ��")]
    ExportFailed = 5,

    /// <summary>
    /// δ֪����
    /// </summary>
    [Description("δ֪����")]
    Unknown = 9
}

/// <summary>
/// ��Ƶ�����ٶ�
/// </summary>
public enum PlaybackSpeed
{
    /// <summary>
    /// 0.25 ����
    /// </summary>
    [Description("0.25x")]
    Quarter = 0,

    /// <summary>
    /// 0.5 ����
    /// </summary>
    [Description("0.5x")]
    Half = 1,

    /// <summary>
    /// 1.0 ���٣�������
    /// </summary>
    [Description("1x")]
    Normal = 2,

    /// <summary>
    /// 1.5 ����
    /// </summary>
    [Description("1.5x")]
    OneAndHalf = 3,

    /// <summary>
    /// 2.0 ����
    /// </summary>
    [Description("2x")]
    Double = 4
}

/// <summary>
/// CSV �ļ����ͱ�ʶ
/// </summary>
public enum CsvFileType
{
    /// <summary>
    /// �ؽڽǶ�ʱ������
    /// </summary>
    [Description("�ؽڽǶ�")]
    JointAngle,

    /// <summary>
    /// �ؼ����˶��켣
    /// </summary>
    [Description("�ؼ���켣")]
    KeypointTrajectory,

    /// <summary>
    /// �ؼ����ٶ�
    /// </summary>
    [Description("�ؼ����ٶ�")]
    KeypointVelocity,

    /// <summary>
    /// �ؽڽ��ٶ�
    /// </summary>
    [Description("�ؽڽ��ٶ�")]
    JointAngularVelocity
}

#endregion
