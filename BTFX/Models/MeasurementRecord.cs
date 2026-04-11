using BTFX.Common;
using BTFX.Models.Analysis;
using SqlSugar;

namespace BTFX.Models;

/// <summary>
/// ������¼ģ��
/// </summary>
[SugarTable("MeasurementRecords")]
public class MeasurementRecord
{
    /// <summary>
    /// ��¼ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// ����ID
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public int PatientId { get; set; }

    /// <summary>
    /// ������Ϣ���������ԣ�
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public Patient? Patient { get; set; }

    /// <summary>
    /// ����ԱID����Ӧ���ݿ� UserId �ֶΣ�
    /// </summary>
    [SugarColumn(ColumnName = "UserId", IsNullable = false)]
    public int OperatorId { get; set; }

    /// <summary>
    /// ����Ա��Ϣ���������ԣ�
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public User? Operator { get; set; }

    /// <summary>
    /// ��������ʱ��
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime MeasurementDate { get; set; } = DateTime.Now;

    /// <summary>
    /// ����״̬
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public MeasurementStatus Status { get; set; } = MeasurementStatus.Pending;

    /// <summary>
    /// ��Ƶ�ļ�·������Ӧ���ݿ� VideoPath �ֶΣ�
    /// </summary>
    [SugarColumn(ColumnName = "VideoPath", Length = 500, IsNullable = true)]
    public string? VideoFilePath { get; set; }

    /// <summary>
    /// ��������ʱ�� (��)����Ӧ���ݿ� Duration �ֶΣ�
    /// </summary>
    [SugarColumn(ColumnName = "Duration", IsNullable = true)]
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// �Ƿ�Ϊ�ο�����
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool IsGuestData { get; set; } = false;

    /// <summary>
    /// ��ע
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Remark { get; set; }

    /// <summary>
    /// ����ʱ��
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// ����ʱ��
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// ��̬����ID�����ԣ������������ݿ⣩
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public int? GaitParametersId { get; set; }

    /// <summary>
    /// ��̬�������������ԣ�
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public GaitParameters? GaitParameters { get; set; }

    #region ��������ģ����չ�ֶ�

    /// <summary>
    /// ��������
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? MeasurementName { get; set; }

    /// <summary>
    /// ��������
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public MeasurementType MeasurementType { get; set; } = MeasurementType.NormalWalk;

    /// <summary>
    /// ������Ƶ·��
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? FrontVideoPath { get; set; }

    /// <summary>
    /// ������Ƶ·��
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? SideVideoPath { get; set; }

    /// <summary>
    /// ��Ƶ���
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public VideoSpec VideoSpec { get; set; } = VideoSpec.P1080_30fps;

    /// <summary>
    /// �������� (��)
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public double WalkwayLength { get; set; } = 6.0;

    /// <summary>
    /// ������ԣ�����/���ã�
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public ImportStrategy ImportStrategy { get; set; } = ImportStrategy.CopyToFolder;

    /// <summary>
    /// ��Ƶ����ģʽ������/�ɼ���
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public VideoImportMode VideoImportMode { get; set; } = VideoImportMode.Import;

    /// <summary>
    /// ��ǰ�����׶�
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public AnalysisStage CurrentAnalysisStage { get; set; } = AnalysisStage.None;

    /// <summary>
    /// �ؼ���������
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool KeypointsCompleted { get; set; } = false;

    /// <summary>
    /// �¼��������
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool EventsCompleted { get; set; } = false;

    /// <summary>
    /// �˶�ѧ�������
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool KinematicsCompleted { get; set; } = false;

    /// <summary>
    /// ����Ŀ¼·�������·����
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? MeasurementFolderPath { get; set; }

    #endregion

    #region �������ԣ������ݿ��ֶΣ�

    /// <summary>
    /// �Ƿ���������Ƶ
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public bool HasFrontVideo => !string.IsNullOrEmpty(FrontVideoPath);

    /// <summary>
    /// �Ƿ��в�����Ƶ
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public bool HasSideVideo => !string.IsNullOrEmpty(SideVideoPath);

    /// <summary>
    /// �Ƿ���˫��Ƶ���ɽ��з�����
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public bool HasDualVideo => HasFrontVideo && HasSideVideo;

    /// <summary>
    /// ��������б����������ԣ�һ�β����ɹ�����η��������
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public List<AnalysisResult>? AnalysisResults { get; set; }

    /// <summary>
    /// ���³ɹ�����������������ԣ�
    /// </summary>
    [SugarColumn(IsIgnore = true)]
    public AnalysisResult? LatestAnalysisResult { get; set; }

    #endregion
}
