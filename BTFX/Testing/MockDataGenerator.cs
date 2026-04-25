using System.IO;
using System.Text.Json;
using System.Windows.Documents;
using BTFX.Common;
using BTFX.Helpers;
using BTFX.Models;
using BTFX.Models.Analysis;
using BTFX.ViewModels;
using BTFX.ViewModels.Measurement;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace BTFX.Testing;

/// <summary>
/// 模拟数据生成器，用于 UI 测试和界面预览
/// </summary>
internal static class MockDataGenerator
{
    /// <summary>
    /// 创建模拟患者
    /// </summary>
    internal static Patient CreateMockPatient() => new()
    {
        Id = 999,
        Name = "张三（测试）",
        Gender = Gender.Male,
        BirthDate = new DateTime(1990, 5, 15),
        Height = 175,
        Weight = 70,
        Phone = "13800138000",
        Remark = "UI 测试用模拟数据"
    };

    /// <summary>
    /// 创建模拟测量记录
    /// </summary>
    internal static MeasurementRecord CreateMockMeasurement(Patient patient) => new()
    {
        Id = 999,
        PatientId = patient.Id,
        Patient = patient,
        OperatorId = 1,
        MeasurementDate = DateTime.Now.AddMinutes(-10),
        Status = MeasurementStatus.Completed,
        MeasurementName = "UI测试测量",
        MeasurementType = MeasurementType.NormalWalk,
        FrontVideoPath = "mock_front.mp4",
        SideVideoPath = "mock_side.mp4",
        VideoSpec = VideoSpec.P1080_30fps,
        WalkwayLength = 6.0
    };

    /// <summary>
    /// 创建 A 级（优秀）质量的模拟分析结果
    /// </summary>
    internal static AnalysisResult CreateGradeAResult() => CreateMockResult(
        confidence: 0.92, validFrameRatio: 0.98,
        occlusion: false, missingPoints: false);

    /// <summary>
    /// 创建 B 级（良好）质量的模拟分析结果
    /// </summary>
    internal static AnalysisResult CreateGradeBResult() => CreateMockResult(
        confidence: 0.78, validFrameRatio: 0.88,
        occlusion: false, missingPoints: true);

    /// <summary>
    /// 创建 C 级（较差）质量的模拟分析结果
    /// </summary>
    internal static AnalysisResult CreateGradeCResult() => CreateMockResult(
        confidence: 0.62, validFrameRatio: 0.72,
        occlusion: true, missingPoints: true);

    /// <summary>
    /// 创建模拟分析结果（核心方法）
    /// </summary>
    private static AnalysisResult CreateMockResult(
        double confidence, double validFrameRatio,
        bool occlusion, bool missingPoints)
    {
        return new AnalysisResult
        {
            Id = 999,
            MeasurementId = 999,
            RequestId = "MOCK_TEST_001",
            ProtocolVersion = "1.0",
            AlgorithmVersion = "v2.1.0-mock",
            ModelVersion = "v1.0-mock",
            TaskStatus = "completed",
            Success = true,
            OutputDirectory = "mock_output",
            AnnotatedVideoPath = null, // 无真实视频
            AnnotatedVideoDurationS = 8.5,
            AnalysisDurationSeconds = 45.2,
            CreatedAt = DateTime.Now,

            // 步态事件参数
            GaitCycleDurationS = 1.12,
            StanceTimeS = 0.67,
            SwingTimeS = 0.45,
            DoubleSupportTimeS = 0.22,
            SingleSupportTimeS = 0.45,
            StepLengthM = 0.65,
            StrideLengthM = 1.28,
            GaitSpeedMPerS = 1.14,

            // 运动学汇总
            KinematicSummary = new KinematicSummary
            {
                Id = 999,
                AnalysisResultId = 999,
                HipRomDeg = 42.5,
                KneeRomDeg = 58.3,
                AnkleRomDeg = 28.7,
                PelvisCoronalRomDeg = 8.2
            },

            // 质量控制
            QualityControl = new QualityControlInfo
            {
                Id = 999,
                AnalysisResultId = 999,
                MeanKeypointConfidence = confidence,
                ValidFrameRatio = validFrameRatio,
                OcclusionWarning = occlusion,
                MissingPointWarning = missingPoints
            }
        };
    }

    /// <summary>
    /// 生成模拟关节角度曲线图（正弦波模拟）
    /// </summary>
    internal static (PlotModel hip, PlotModel knee, PlotModel ankle, PlotModel pelvis) CreateMockPlotModels()
    {
        const double duration = 8.5;
        const int fps = 30;
        var totalFrames = (int)(duration * fps);

        var hip = BuildMockPlot("髋关节角度 (°)", totalFrames, fps,
            t => 20 + 20 * Math.Sin(2 * Math.PI * t / 1.12), OxyColors.SteelBlue, duration);
        var knee = BuildMockPlot("膝关节角度 (°)", totalFrames, fps,
            t => 10 + 30 * Math.Sin(2 * Math.PI * t / 1.12 + 0.5), OxyColors.ForestGreen, duration);
        var ankle = BuildMockPlot("踝关节角度 (°)", totalFrames, fps,
            t => -5 + 15 * Math.Sin(2 * Math.PI * t / 1.12 + 1.0), OxyColors.OrangeRed, duration);
        var pelvis = BuildMockPlot("骨盆角度 (°)", totalFrames, fps,
            t => 2 + 4 * Math.Sin(2 * Math.PI * t / 1.12 + 0.3), OxyColors.MediumPurple, duration);

        return (hip, knee, ankle, pelvis);
    }

    /// <summary>
    /// 构建单条模拟曲线
    /// </summary>
    private static PlotModel BuildMockPlot(string title, int totalFrames, int fps,
        Func<double, double> valueFunc, OxyColor color, double maxTime)
    {
        var model = new PlotModel
        {
            Title = title,
            TitleFontSize = 10,
            Padding = new OxyThickness(0),
            PlotMargins = new OxyThickness(40, 2, 10, 20)
        };

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "s",
            TitleFontSize = 9,
            FontSize = 9,
            Minimum = 0,
            Maximum = maxTime,
            IsPanEnabled = false,
            IsZoomEnabled = false,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(230, 230, 230)
        });

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "°",
            TitleFontSize = 10,
            FontSize = 9,
            IsPanEnabled = false,
            IsZoomEnabled = false,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(230, 230, 230)
        });

        var series = new LineSeries
        {
            Color = color,
            StrokeThickness = 1.5,
            MarkerType = MarkerType.None
        };

        for (int i = 0; i < totalFrames; i++)
        {
            double t = (double)i / fps;
            series.Points.Add(new DataPoint(t, valueFunc(t)));
        }

        model.Series.Add(series);

        model.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Vertical,
            X = 0,
            Color = OxyColors.Red,
            StrokeThickness = 1.5,
            LineStyle = LineStyle.Solid,
            Tag = "TrackerLine"
        });

        return model;
    }

    /// <summary>
    /// 创建模拟报告列表项
    /// </summary>
    internal static List<ReportItem> CreateMockReportItems()
    {
        var patient = CreateMockPatient();
        var reports = new List<ReportItem>();

        var statuses = new[] { ReportStatus.Draft, ReportStatus.Completed, ReportStatus.Printed };
        for (int i = 1; i <= 5; i++)
        {
            var measurement = new MeasurementRecord
            {
                Id = i,
                PatientId = patient.Id,
                Patient = patient,
                MeasurementName = $"测试测量_{i}",
                MeasurementDate = DateTime.Now.AddDays(-i)
            };

            var report = new Report
            {
                Id = i,
                MeasurementId = i,
                MeasurementRecord = measurement,
                PatientId = patient.Id,
                Patient = patient,
                CreatedBy = 1,
                ReportNumber = $"RPT-{DateTime.Now:yyyyMMdd}-{i:D3}",
                ReportDate = DateTime.Now.AddDays(-i + 1),
                Status = statuses[(i - 1) % statuses.Length],
                DoctorOpinion = i % 2 == 0 ? "步态基本正常，建议定期复查。" : null,
                CreatedAt = DateTime.Now.AddDays(-i + 1)
            };

            reports.Add(new ReportItem(report, i));
        }

        return reports;
    }

    /// <summary>
    /// 创建模拟测量记录列表项（用于报告生成选择）
    /// </summary>
    internal static List<MeasurementRecordItem> CreateMockMeasurementRecordItems()
    {
        var patient = CreateMockPatient();
        var items = new List<MeasurementRecordItem>();

        for (int i = 1; i <= 8; i++)
        {
            var record = new MeasurementRecord
            {
                Id = i,
                PatientId = patient.Id,
                Patient = patient,
                MeasurementName = $"步态测量_{i}",
                MeasurementType = i % 3 == 0 ? MeasurementType.FastWalk : MeasurementType.NormalWalk,
                MeasurementDate = DateTime.Now.AddDays(-i),
                Status = i <= 6 ? MeasurementStatus.Completed : MeasurementStatus.Failed
            };

            items.Add(new MeasurementRecordItem(record, i));
        }

        return items;
    }

    /// <summary>
    /// 创建用于分析详情页预览的模拟测量与分析结果。
    /// </summary>
    internal static (MeasurementRecord record, AnalysisResult result) CreateAnalysisDetailPreviewData()
    {
        var patient = CreateMockPatient();
        patient.Id = 1001;
        patient.Name = "李晓晨（分析详情预览）";
        patient.Height = 168;
        patient.Weight = 58;
        patient.Remark = "近期术后步态恢复随访，重点观察右侧摆动期控制。";

        var record = CreateMockMeasurement(patient);
        record.Id = 2001;
        record.PatientId = patient.Id;
        record.MeasurementName = "术后第 6 周步态复查";
        record.MeasurementDate = DateTime.Now.AddHours(-3);
        record.MeasurementFolderPath = @"D:\DevPreview\Measurements\2026-04-10-2001";
        record.CurrentAnalysisStage = AnalysisStage.Kinematics;
        record.KeypointsCompleted = true;
        record.EventsCompleted = true;
        record.KinematicsCompleted = true;
        record.GaitParameters = new GaitParameters
        {
            Id = 3001,
            MeasurementRecordId = record.Id,
            AnalysisResultId = 4001,
            Cadence = 107.2,
            Velocity = 1.12,
            GaitCycleDurationS = 1.11,
            StepLengthM = 0.63,
            StrideLengthM = 1.25,
            GaitSpeedMPerS = 1.12,
            StanceTimeS = 0.68,
            SwingTimeS = 0.43,
            DoubleSupportTimeS = 0.21,
            SingleSupportTimeS = 0.47,
            StrideLengthLeft = 126.4,
            StrideLengthRight = 124.8,
            StepLengthLeft = 63.1,
            StepLengthRight = 62.4,
            StancePhaseLeft = 61.2,
            StancePhaseRight = 60.7,
            SwingPhaseLeft = 38.8,
            SwingPhaseRight = 39.3,
            DoubleSupport = 20.8,
            SingleSupport = 39.6,
            SymmetryIndex = 0.94,
            StepWidth = 10.6,
            VariabilityCoefficient = 4.1
        };

        var result = CreateGradeBResult();
        result.Id = 4001;
        result.MeasurementId = record.Id;
        result.RequestId = "GAIT_PREVIEW_20260410_001";
        result.OutputDirectory = @"D:\DevPreview\Measurements\2026-04-10-2001\analysis-result-4001";
        result.SummaryFilePath = Path.Combine(result.OutputDirectory, "summary.json");
        result.AnnotatedVideoPath = Path.Combine(result.OutputDirectory, "annotated_side.mp4");
        result.CreatedAt = DateTime.Now.AddHours(-2);
        result.AnalysisDurationSeconds = 52.7;
        result.GaitCycleDurationS = 1.11;
        result.StanceTimeS = 0.68;
        result.SwingTimeS = 0.43;
        result.DoubleSupportTimeS = 0.21;
        result.SingleSupportTimeS = 0.47;
        result.StepLengthM = 0.63;
        result.StrideLengthM = 1.25;
        result.GaitSpeedMPerS = 1.12;
        result.CsvFiles =
        [
            new AnalysisCsvFile
            {
                Id = 1,
                AnalysisResultId = result.Id,
                FileType = CsvFileType.KeypointTrajectory,
                FilePath = Path.Combine(result.OutputDirectory, "gait_parameters.csv"),
                FileExists = true
            },
            new AnalysisCsvFile
            {
                Id = 2,
                AnalysisResultId = result.Id,
                FileType = CsvFileType.JointAngle,
                FilePath = Path.Combine(result.OutputDirectory, "joint_angles.csv"),
                FileExists = false
            },
            new AnalysisCsvFile
            {
                Id = 3,
                AnalysisResultId = result.Id,
                FileType = CsvFileType.JointAngularVelocity,
                FilePath = Path.Combine(result.OutputDirectory, "quality_control.csv"),
                FileExists = true
            }
        ];

        record.LatestAnalysisResult = result;
        record.AnalysisResults = [result];

        return (record, result);
    }

    /// <summary>
    /// 创建用于报告预览页的模拟报告数据。
    /// </summary>
    internal static (Report report, FlowDocument document) CreateReportPreviewData()
    {
        var (record, result) = CreateAnalysisDetailPreviewData();
        var report = new Report
        {
            Id = 5001,
            MeasurementId = record.Id,
            MeasurementRecord = record,
            PatientId = record.PatientId,
            Patient = record.Patient,
            CreatedBy = 1,
            ReportNumber = $"RPT-{DateTime.Now:yyyyMMdd}-501",
            Title = "步态分析报告（预览稿）",
            ReportDate = DateTime.Now,
            DoctorOpinion = "整体步态节律基本恢复，右侧摆动期仍存在轻度保护性代偿，建议继续进行髋膝踝联合稳定训练。",
            Status = ReportStatus.Draft,
            AnalysisResultId = result.Id,
            AnalysisResult = result,
            KinematicSummary = result.KinematicSummary,
            QualityControl = result.QualityControl,
            PdfFilePath = Path.Combine(result.OutputDirectory, "步态分析报告（预览稿）.pdf"),
            WordFilePath = Path.Combine(result.OutputDirectory, "步态分析报告（预览稿）.docx"),
            CreatedAt = DateTime.Now.AddMinutes(-20),
            UpdatedAt = DateTime.Now.AddMinutes(-5),
            ReportOptionsJson = JsonSerializer.Serialize(new ReportDraftOptions(
                IncludeSpatiotemporalParameters: true,
                IncludeKinematicSummary: true,
                IncludeQualityControl: true,
                IncludeResultFiles: false))
        };

        var document = ReportPreviewHelper.GenerateReportDocument(report, "步态智能分析系统 · DevTest 预览");
        return (report, document);
    }
}
