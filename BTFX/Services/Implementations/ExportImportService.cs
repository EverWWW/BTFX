using System.IO;
using System.Text;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BTFX.Common;
using BTFX.Data;
using BTFX.Models;
using BTFX.Models.Analysis;
using BTFX.Services.Interfaces;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using ToolHelper.Database.Sqlite;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 导出导入服务实现
/// </summary>
public class ExportImportService : IExportImportService
{
    private readonly ILogHelper? _logHelper;
    private readonly ILocalizationService? _localizationService;
    private static readonly JsonSerializerOptions ArchiveJsonOptions = new()
    {
        WriteIndented = true
    };

    public ExportImportService()
    {
        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
            _localizationService = App.Services?.GetService(typeof(ILocalizationService)) as ILocalizationService;
        }
        catch { }
    }

    /// <inheritdoc/>
    public async Task<bool> ExportPatientsAsync(List<Patient> patients, ExportFormat format, string filePath)
    {
        try
        {
            _logHelper?.Information($"开始导出患者数据：{patients.Count}条，格式：{format}");

            var exportData = patients.Select(p => new PatientExportModel
            {
                姓名 = p.Name,
                性别 = p.Gender == Gender.Male ? "男" : "女",
                出生日期 = p.BirthDate?.ToString("yyyy-MM-dd") ?? "",
                年龄 = p.Age?.ToString() ?? "",
                电话 = p.Phone,
                证件号 = p.IdNumber ?? "",
                就诊号 = p.HospitalNumber ?? "",
                身高cm = p.Height?.ToString("F1") ?? "",
                体重kg = p.Weight?.ToString("F1") ?? "",
                地址 = p.Address ?? "",
                病史 = p.MedicalHistory ?? "",
                备注 = p.Remark ?? "",
                创建时间 = p.CreatedAt.ToString(Constants.DATETIME_FORMAT)
            }).ToList();

            return format switch
            {
                ExportFormat.Excel => await ExportToExcelAsync(exportData, filePath),
                ExportFormat.CSV => await ExportToCsvAsync(exportData, filePath),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logHelper?.Error("导出患者数据失败", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ExportMeasurementsAsync(List<MeasurementRecord> measurements, ExportFormat format, string filePath)
    {
        try
        {
            _logHelper?.Information($"开始导出测量数据：{measurements.Count}条，格式：{format}");

            var exportData = measurements.Select(m => new MeasurementExportModel
            {
                患者姓名 = m.Patient?.Name ?? "",
                性别 = m.Patient?.Gender == Gender.Male ? "男" : "女",
                年龄 = m.Patient?.Age?.ToString() ?? "",
                测量日期 = m.MeasurementDate.ToString(Constants.DATETIME_FORMAT),
                测量状态 = GetStatusText(m.Status),
                测量时长秒 = m.DurationSeconds?.ToString() ?? "",
                操作员 = m.Operator?.Name ?? "",
                左脚步幅cm = m.GaitParameters?.StrideLengthLeft?.ToString("F2") ?? "",
                右脚步幅cm = m.GaitParameters?.StrideLengthRight?.ToString("F2") ?? "",
                步频步每分 = m.GaitParameters?.Cadence?.ToString("F1") ?? "",
                步速m每s = m.GaitParameters?.Velocity?.ToString("F2") ?? "",
                左脚支撑相百分比 = m.GaitParameters?.StancePhaseLeft?.ToString("F1") ?? "",
                右脚支撑相百分比 = m.GaitParameters?.StancePhaseRight?.ToString("F1") ?? "",
                双支撑时间百分比 = m.GaitParameters?.DoubleSupport?.ToString("F1") ?? "",
                备注 = m.Remark ?? ""
            }).ToList();

            return format switch
            {
                ExportFormat.Excel => await ExportToExcelAsync(exportData, filePath),
                ExportFormat.CSV => await ExportToCsvAsync(exportData, filePath),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logHelper?.Error("导出测量数据失败", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<MeasurementArchiveExportResult> ExportMeasurementArchiveAsync(
        List<MeasurementRecord> measurements,
        string filePath,
        IProgress<OperationProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ReportProgress(progress, 0, L("ArchiveExport.Progress.PrepareStage"), L("ArchiveExport.Progress.PrepareMessage"));

            if (measurements.Count == 0)
            {
                return new(false, L("ArchiveExport.NoMeasurements"), 0, filePath);
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create, Encoding.UTF8);

            var manifest = new MeasurementArchiveManifest
            {
                CreatedAt = DateTime.Now,
                ExcludesRawVideos = true
            };

            for (var measurementIndex = 0; measurementIndex < measurements.Count; measurementIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sourceRecord = measurements[measurementIndex];
                var measurement = await db.GetByIdAsync<MeasurementRecord>(sourceRecord.Id) ?? sourceRecord;
                var measurementStart = CalculateSegmentPoint(5, 92, measurementIndex, measurements.Count, 0);
                var measurementEnd = CalculateSegmentPoint(5, 92, measurementIndex, measurements.Count, 1);
                ReportProgress(
                    progress,
                    measurementStart,
                    L("ArchiveExport.Progress.ReadMeasurementStage"),
                    L("ArchiveExport.Progress.ReadMeasurementMessage", measurement.MeasurementName ?? L("ArchiveExport.DefaultMeasurementName", measurement.Id)));

                var patient = await db.GetByIdAsync<Patient>(measurement.PatientId) ?? measurement.Patient;
                var gaitParameters = await db.GetFirstAsync<GaitParameters>(g => g.MeasurementRecordId == measurement.Id);
                var analysisResults = await db.Queryable<AnalysisResult>()
                    .Where(r => r.MeasurementId == measurement.Id)
                    .ToListAsync();
                var reports = await db.Queryable<Report>()
                    .Where(r => r.MeasurementId == measurement.Id)
                    .ToListAsync();

                var measurementRoot = $"measurements/{measurement.Id}";
                var manifestMeasurement = new MeasurementArchiveMeasurement
                {
                    OriginalMeasurementId = measurement.Id,
                    MeasurementName = measurement.MeasurementName ?? $"测量_{measurement.MeasurementDate:yyyyMMdd_HHmmss}",
                    PatientName = patient?.Name ?? string.Empty,
                    Status = measurement.Status.ToString()
                };

                WriteJsonEntry(archive, $"{measurementRoot}/patient.json", patient);
                WriteJsonEntry(archive, $"{measurementRoot}/measurement.json", CreateMeasurementSnapshot(measurement));
                WriteJsonEntry(archive, $"{measurementRoot}/gait_parameters.json", gaitParameters);
                WriteJsonEntry(archive, $"{measurementRoot}/reports.json", reports.Select(CreateReportSnapshot).ToList());
                ReportProgress(progress, measurementStart + (measurementEnd - measurementStart) * 0.22, L("ArchiveExport.Progress.WriteSnapshotStage"), L("ArchiveExport.Progress.WriteSnapshotMessage"));

                var rawVideoPaths = new HashSet<string>(
                    new[]
                    {
                        measurement.VideoFilePath,
                        measurement.SideVideoPath,
                        measurement.FrontVideoPath
                    }.Where(p => !string.IsNullOrWhiteSpace(p))
                     .Select(p => Path.GetFullPath(p!)),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var result in analysisResults)
                {
                    var analysisIndex = analysisResults.IndexOf(result);
                    var analysisStart = Interpolate(measurementStart, measurementEnd, 0.28 + 0.62 * analysisIndex / Math.Max(analysisResults.Count, 1));
                    var analysisEnd = Interpolate(measurementStart, measurementEnd, 0.28 + 0.62 * (analysisIndex + 1) / Math.Max(analysisResults.Count, 1));
                    ReportProgress(
                        progress,
                        analysisStart,
                        L("ArchiveExport.Progress.PackageAnalysisStage"),
                        L("ArchiveExport.Progress.PackageAnalysisMessage", result.RequestId));

                    var analysisRoot = $"{measurementRoot}/analysis_results/{result.Id}";
                    var csvFiles = await db.Queryable<AnalysisCsvFile>()
                        .Where(c => c.AnalysisResultId == result.Id)
                        .ToListAsync();
                    var kinematicSummary = await db.GetFirstAsync<KinematicSummary>(
                        k => k.AnalysisResultId == result.Id);
                    var qualityControl = await db.GetFirstAsync<QualityControlInfo>(
                        q => q.AnalysisResultId == result.Id);

                    WriteJsonEntry(archive, $"{analysisRoot}/analysis_result.json", CreateAnalysisResultSnapshot(result));
                    WriteJsonEntry(archive, $"{analysisRoot}/csv_files.json", csvFiles.Select(CreateCsvFileSnapshot).ToList());
                    WriteJsonEntry(archive, $"{analysisRoot}/kinematic_summary.json", kinematicSummary);
                    WriteJsonEntry(archive, $"{analysisRoot}/quality_control.json", qualityControl);

                    var relatedReports = reports
                        .Where(r => r.AnalysisResultId == result.Id || r.AnalysisResultId is null)
                        .ToList();
                    var fileMaps = AddAnalysisFilesToArchive(
                        archive,
                        result,
                        csvFiles,
                        relatedReports,
                        analysisRoot,
                        rawVideoPaths,
                        progress,
                        cancellationToken,
                        analysisStart,
                        analysisEnd);

                    WriteJsonEntry(archive, $"{analysisRoot}/files.json", fileMaps);
                    manifestMeasurement.AnalysisResults.Add(new MeasurementArchiveAnalysis
                    {
                        OriginalAnalysisResultId = result.Id,
                        RequestId = result.RequestId,
                        Success = result.Success,
                        FileCount = fileMaps.Count
                    });
                }

                ReportProgress(progress, measurementEnd, L("ArchiveExport.Progress.MeasurementCompleteStage"), L("ArchiveExport.Progress.MeasurementCompleteMessage", manifestMeasurement.MeasurementName));
                manifest.Measurements.Add(manifestMeasurement);
            }

            WriteJsonEntry(archive, "manifest.json", manifest);
            ReportProgress(progress, 100, L("ArchiveExport.Progress.CompleteStage"), L("ArchiveExport.Progress.CompleteMessage"));
            _logHelper?.Information($"测量结果包导出完成：{filePath}, Count={measurements.Count}");
            return new(true, L("ArchiveExport.SuccessFormat", measurements.Count), measurements.Count, filePath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"测量结果包导出失败：{filePath}", ex);
            return new(false, L("ArchiveExport.FailedFormat", ex.Message), 0, filePath);
        }
    }

    /// <inheritdoc/>
    public async Task<MeasurementArchiveImportResult> ImportMeasurementArchiveAsync(
        string filePath,
        IProgress<OperationProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ReportProgress(progress, 0, L("ArchiveImport.Progress.PrepareStage"), L("ArchiveImport.Progress.PrepareMessage"));

            if (!File.Exists(filePath))
            {
                return new(false, L("ArchiveImport.FileMissing"), 0, []);
            }

            using var archive = ZipFile.OpenRead(filePath);
            var manifest = ReadJsonEntry<MeasurementArchiveManifest>(archive, "manifest.json");
            if (manifest is null || manifest.Measurements.Count == 0)
            {
                return new(false, L("ArchiveImport.InvalidPackage"), 0, []);
            }

            var importKey = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}";
            var stagingContainer = Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "Temp",
                "ImportStaging",
                importKey);
            var importRoot = Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "ImportedResults",
                importKey);
            using var stagingSession = new ImportStagingSession(stagingContainer, importRoot);
            Directory.CreateDirectory(stagingSession.PayloadDirectory);
            var archivedFilePaths = await StageArchiveFilesAsync(
                archive,
                manifest,
                stagingSession.PayloadDirectory,
                importRoot,
                progress,
                cancellationToken);

            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            var importedIds = new List<int>();
            var currentUserId = await ResolveCurrentUserIdAsync(db);
            db.BeginTran();
            try
            {
                for (var measurementIndex = 0; measurementIndex < manifest.Measurements.Count; measurementIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var item = manifest.Measurements[measurementIndex];
                var measurementStart = CalculateSegmentPoint(5, 92, measurementIndex, manifest.Measurements.Count, 0);
                var measurementEnd = CalculateSegmentPoint(5, 92, measurementIndex, manifest.Measurements.Count, 1);
                ReportProgress(
                    progress,
                    measurementStart,
                    L("ArchiveImport.Progress.ImportMeasurementStage"),
                    L("ArchiveImport.Progress.ImportMeasurementMessage", item.MeasurementName));

                var measurementRoot = $"measurements/{item.OriginalMeasurementId}";
                var patient = ReadJsonEntry<Patient>(archive, $"{measurementRoot}/patient.json");
                var measurement = ReadJsonEntry<MeasurementRecord>(archive, $"{measurementRoot}/measurement.json");
                var gaitParameters = ReadJsonEntry<GaitParameters>(archive, $"{measurementRoot}/gait_parameters.json");
                var reports = ReadJsonEntry<List<Report>>(archive, $"{measurementRoot}/reports.json") ?? [];

                if (patient is null || measurement is null)
                {
                    _logHelper?.Warning($"跳过无效测量归档：{measurementRoot}");
                    continue;
                }

                var oldMeasurementId = measurement.Id;
                var oldPatientId = patient.Id;
                var oldToNewAnalysisIds = new Dictionary<int, int>();
                var oldToNewFilePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var newPatientId = await ResolveImportedPatientIdAsync(db, patient, currentUserId);

                measurement.Id = 0;
                measurement.PatientId = newPatientId;
                measurement.OperatorId = currentUserId;
                measurement.Status = item.AnalysisResults.Any(r => r.Success)
                    ? MeasurementStatus.Completed
                    : MeasurementStatus.Pending;
                measurement.VideoFilePath = null;
                measurement.SideVideoPath = null;
                measurement.FrontVideoPath = null;
                measurement.VideoImportMode = VideoImportMode.Import;
                measurement.MeasurementFolderPath = Path.Combine("Data", "ImportedResults", Path.GetFileName(importRoot), $"measurement_{oldMeasurementId}");
                measurement.Remark = AppendArchiveRemark(measurement.Remark, L("ArchiveImport.ImportedRemark"));
                measurement.CreatedAt = DateTime.Now;
                measurement.UpdatedAt = DateTime.Now;
                var newMeasurementId = (int)await db.InsertReturnIdentityAsync(measurement);
                importedIds.Add(newMeasurementId);

                if (gaitParameters is not null)
                {
                    gaitParameters.Id = 0;
                    gaitParameters.MeasurementRecordId = newMeasurementId;
                    gaitParameters.AnalysisResultId = null;
                    gaitParameters.CreatedAt = DateTime.Now;
                    await db.InsertAsync(gaitParameters);
                }

                    foreach (var analysis in item.AnalysisResults)
                {
                    var analysisRoot = $"{measurementRoot}/analysis_results/{analysis.OriginalAnalysisResultId}";
                    var result = ReadJsonEntry<AnalysisResult>(archive, $"{analysisRoot}/analysis_result.json");
                    if (result is null)
                    {
                        continue;
                    }

                    var analysisIndex = item.AnalysisResults.IndexOf(analysis);
                    var analysisStart = Interpolate(measurementStart, measurementEnd, 0.25 + 0.58 * analysisIndex / Math.Max(item.AnalysisResults.Count, 1));
                    var analysisEnd = Interpolate(measurementStart, measurementEnd, 0.25 + 0.58 * (analysisIndex + 1) / Math.Max(item.AnalysisResults.Count, 1));
                    ReportProgress(
                        progress,
                        analysisStart,
                        L("ArchiveImport.Progress.RestoreFilesStage"),
                        L("ArchiveImport.Progress.RestoreAnalysisMessage", analysis.RequestId));

                    var resultDir = Path.Combine(importRoot, $"measurement_{oldMeasurementId}", $"analysis_{analysis.OriginalAnalysisResultId}");

                    var fileMaps = ReadJsonEntry<List<MeasurementArchiveFile>>(archive, $"{analysisRoot}/files.json") ?? [];
                    for (var fileIndex = 0; fileIndex < fileMaps.Count; fileIndex++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var fileMap = fileMaps[fileIndex];
                        ReportProgress(
                            progress,
                            CalculateProgress(analysisStart, analysisEnd, fileIndex, fileMaps.Count),
                            L("ArchiveImport.Progress.RestoreFilesStage"),
                            L("ArchiveImport.Progress.RestoreFileMessage", Path.GetFileName(fileMap.RelativePath)));

                        if (!archivedFilePaths.TryGetValue(NormalizeEntryName(fileMap.EntryName), out var finalPath))
                        {
                            throw new InvalidDataException($"结果包文件未完成校验：{fileMap.EntryName}");
                        }

                        if (!string.IsNullOrWhiteSpace(fileMap.OriginalPath))
                        {
                            oldToNewFilePath[fileMap.OriginalPath] = finalPath;
                        }
                    }
                    ReportProgress(progress, analysisEnd, L("ArchiveImport.Progress.RestoreCompleteStage"), L("ArchiveImport.Progress.RestoreCompleteMessage", fileMaps.Count));

                    var oldAnalysisResultId = result.Id;
                    result.Id = 0;
                    result.MeasurementId = newMeasurementId;
                    result.OutputDirectory = resultDir;
                    result.ConfigFilePath = MapArchivedPath(result.ConfigFilePath, oldToNewFilePath);
                    result.SummaryFilePath = MapArchivedPath(result.SummaryFilePath, oldToNewFilePath);
                    result.AnnotatedVideoPath = MapArchivedPath(result.AnnotatedVideoPath, oldToNewFilePath);
                    result.PackagePath = null;
                    result.PackageCreatedAt = null;
                    result.PackageValidationStatus = "Imported";
                    result.PackageValidationMessage = L("ArchiveImport.PackageValidationMessage");
                    result.CreatedAt = DateTime.Now;
                    var newAnalysisResultId = (int)await db.InsertReturnIdentityAsync(result);
                    oldToNewAnalysisIds[oldAnalysisResultId] = newAnalysisResultId;

                    var kinematicSummary = ReadJsonEntry<KinematicSummary>(archive, $"{analysisRoot}/kinematic_summary.json");
                    if (kinematicSummary is not null)
                    {
                        kinematicSummary.Id = 0;
                        kinematicSummary.AnalysisResultId = newAnalysisResultId;
                        kinematicSummary.CreatedAt = DateTime.Now;
                        await db.InsertAsync(kinematicSummary);
                    }

                    var qualityControl = ReadJsonEntry<QualityControlInfo>(archive, $"{analysisRoot}/quality_control.json");
                    if (qualityControl is not null)
                    {
                        qualityControl.Id = 0;
                        qualityControl.AnalysisResultId = newAnalysisResultId;
                        qualityControl.CreatedAt = DateTime.Now;
                        await db.InsertAsync(qualityControl);
                    }

                    var csvFiles = ReadJsonEntry<List<AnalysisCsvFile>>(archive, $"{analysisRoot}/csv_files.json") ?? [];
                    foreach (var csv in csvFiles)
                    {
                        csv.Id = 0;
                        csv.AnalysisResultId = newAnalysisResultId;
                        csv.FilePath = MapArchivedPath(csv.FilePath, oldToNewFilePath) ?? csv.FilePath;
                        csv.FileExists = !string.IsNullOrWhiteSpace(csv.FilePath)
                            && archivedFilePaths.Values.Contains(csv.FilePath, StringComparer.OrdinalIgnoreCase);
                        csv.CreatedAt = DateTime.Now;
                    }

                    if (csvFiles.Count > 0)
                    {
                        await db.InsertRangeAsync(csvFiles);
                    }
                }

                foreach (var report in reports)
                {
                    report.Id = 0;
                    report.MeasurementId = newMeasurementId;
                    report.PatientId = newPatientId;
                    report.CreatedBy = currentUserId;
                    if (report.AnalysisResultId.HasValue && oldToNewAnalysisIds.TryGetValue(report.AnalysisResultId.Value, out var newAnalysisId))
                    {
                        report.AnalysisResultId = newAnalysisId;
                    }
                    report.PdfFilePath = MapArchivedPath(report.PdfFilePath, oldToNewFilePath);
                    report.WordFilePath = MapArchivedPath(report.WordFilePath, oldToNewFilePath);
                    report.Status = ReportStatus.Completed;
                    report.CreatedAt = DateTime.Now;
                    report.UpdatedAt = DateTime.Now;
                    await db.InsertAsync(report);
                }

                    _logHelper?.Information($"测量结果包导入完成：OldMeasurementId={oldMeasurementId}, NewMeasurementId={newMeasurementId}, OldPatientId={oldPatientId}, NewPatientId={newPatientId}");
                    ReportProgress(progress, measurementEnd, L("ArchiveImport.Progress.MeasurementCompleteStage"), L("ArchiveImport.Progress.MeasurementCompleteMessage", item.MeasurementName));
                }

                stagingSession.Promote();
                db.CommitTran();
                stagingSession.Commit();
            }
            catch
            {
                db.RollbackTran();
                stagingSession.Rollback();
                throw;
            }

            ReportProgress(progress, 100, L("ArchiveImport.Progress.CompleteStage"), L("ArchiveImport.Progress.CompleteMessage"));
            return importedIds.Count > 0
                ? new(true, L("ArchiveImport.SuccessFormat", importedIds.Count), importedIds.Count, importedIds)
                : new(false, L("ArchiveImport.NoImportedMeasurements"), 0, importedIds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"测量结果包导入失败：{filePath}", ex);
            return new(false, L("ArchiveImport.FailedFormat", ex.Message), 0, []);
        }
    }

    /// <inheritdoc/>
    public async Task<List<Patient>> ImportPatientsAsync(string filePath)
    {
        var patients = new List<Patient>();

        try
        {
            if (!File.Exists(filePath))
            {
                _logHelper?.Error($"导入文件不存在: {filePath}");
                return patients;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            _logHelper?.Information($"开始导入患者数据: {filePath}");

            if (extension == ".csv")
            {
                patients = await ImportPatientsFromCsvAsync(filePath);
            }
            else if (extension == ".xlsx" || extension == ".xls")
            {
                patients = await ImportPatientsFromExcelAsync(filePath);
            }
            else
            {
                _logHelper?.Error($"不支持的文件格式: {extension}");
            }

            _logHelper?.Information($"导入完成，成功导入 {patients.Count} 条患者数据");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("导入患者数据失败", ex);
        }

        return patients;
    }

    /// <summary>
    /// 从 CSV 导入患者数据
    /// </summary>
    private async Task<List<Patient>> ImportPatientsFromCsvAsync(string filePath)
    {
        var patients = new List<Patient>();

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
            if (lines.Length < 2)
            {
                _logHelper?.Warning("CSV 文件为空或只有表头");
                return patients;
            }

            // 解析表头
            var headers = ParseCsvLine(lines[0]);
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
            {
                headerMap[headers[i].Trim()] = i;
            }

            // 解析数据行
            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var values = ParseCsvLine(line);
                    var patient = BuildPatientFromFields(name => GetField(headerMap, values, name));
                    if (string.IsNullOrEmpty(patient.Name))
                    {
                        _logHelper?.Warning($"第 {lineIndex + 1} 行缺少姓名，跳过");
                        continue;
                    }

                    patients.Add(patient);
                }
                catch (Exception ex)
                {
                    _logHelper?.Warning($"解析第 {lineIndex + 1} 行失败: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("解析 CSV 文件失败", ex);
        }

        return patients;
    }

    private async Task<List<Patient>> ImportPatientsFromExcelAsync(string filePath)
    {
        var patients = new List<Patient>();

        try
        {
            await Task.Yield();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var workbook = WorkbookFactory.Create(stream);
            var sheet = workbook.NumberOfSheets > 0 ? workbook.GetSheetAt(0) : null;
            if (sheet is null || sheet.LastRowNum < 1)
            {
                _logHelper?.Warning("Excel 文件为空或只有表头");
                return patients;
            }

            var headerRow = sheet.GetRow(0);
            if (headerRow is null)
            {
                return patients;
            }

            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headerRow.LastCellNum; i++)
            {
                var header = GetCellText(headerRow.GetCell(i)).Trim();
                if (!string.IsNullOrWhiteSpace(header))
                {
                    headerMap[header] = i;
                }
            }

            for (var rowIndex = 1; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                var row = sheet.GetRow(rowIndex);
                if (row is null)
                {
                    continue;
                }

                var patient = BuildPatientFromFields(name => GetField(headerMap, row, name));
                if (string.IsNullOrWhiteSpace(patient.Name))
                {
                    _logHelper?.Warning($"第 {rowIndex + 1} 行缺少姓名，跳过");
                    continue;
                }

                patients.Add(patient);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("解析 Excel 文件失败", ex);
        }

        return patients;
    }

    private static Patient BuildPatientFromFields(Func<string, string> getField)
    {
        var now = DateTime.Now;
        return new Patient
        {
            Name = FirstField(getField, "姓名", "Name", "Patient Name").Trim(),
            Gender = ParseGender(FirstField(getField, "性别", "Gender", "Sex")),
            BirthDate = ParseDate(FirstField(getField, "出生日期", "Birth Date", "BirthDate", "Date of Birth")),
            Phone = Truncate(FirstField(getField, "电话", "手机号", "Phone", "Mobile", "Phone Number"), 12),
            IdNumber = EmptyToNull(Truncate(FirstField(getField, "证件号", "身份证号", "ID Number", "Identity Number", "ID"), 20)),
            HospitalNumber = EmptyToNull(Truncate(FirstField(getField, "就诊号", "住院号", "Visit Number", "Visit No.", "Hospital Number", "Medical Record Number"), 20)),
            Height = ParseDouble(FirstField(getField, "身高cm", "身高(CM)", "身高(cm)", "Height(cm)", "Height (cm)", "Height")),
            Weight = ParseDouble(FirstField(getField, "体重kg", "体重(KG)", "体重(kg)", "Weight(kg)", "Weight (kg)", "Weight")),
            Address = EmptyToNull(FirstField(getField, "地址", "Address")),
            MedicalHistory = EmptyToNull(FirstField(getField, "病史", "Medical History", "MedicalHistory")),
            Remark = EmptyToNull(FirstField(getField, "备注", "Remark", "Notes")),
            Status = PatientStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static string FirstField(Func<string, string> getField, params string[] names)
    {
        foreach (var name in names)
        {
            var value = getField(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string GetField(Dictionary<string, int> headerMap, string[] values, string name)
    {
        return headerMap.TryGetValue(name, out var index) && index >= 0 && index < values.Length
            ? values[index].Trim()
            : string.Empty;
    }

    private static string GetField(Dictionary<string, int> headerMap, IRow row, string name)
    {
        return headerMap.TryGetValue(name, out var index)
            ? GetCellText(row.GetCell(index)).Trim()
            : string.Empty;
    }

    private static string GetCellText(ICell? cell)
    {
        if (cell is null)
        {
            return string.Empty;
        }

        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue,
            CellType.Numeric when DateUtil.IsCellDateFormatted(cell) => cell.DateCellValue?.ToString("yyyy-MM-dd") ?? string.Empty,
            CellType.Numeric => cell.NumericCellValue.ToString("0.########"),
            CellType.Boolean => cell.BooleanCellValue ? "是" : "否",
            CellType.Formula => cell.ToString() ?? string.Empty,
            _ => cell.ToString() ?? string.Empty
        };
    }

    private static Gender ParseGender(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "女" or "female" or "f" or "woman" ? Gender.Female : Gender.Male;
    }

    private static DateTime? ParseDate(string value)
    {
        return DateTime.TryParse(value.Trim(), out var date) ? date.Date : null;
    }

    private static double? ParseDouble(string value)
    {
        return double.TryParse(value.Trim(), out var result) ? result : null;
    }

    private static string? EmptyToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        var text = value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    /// <summary>
    /// 解析 CSV 行
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // 检查是否是转义的引号
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // 跳过下一个引号
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    /// <inheritdoc/>
    public Task<bool> ExportReportToExcelAsync(int reportId, string filePath)
    {
        // TODO: 导出报告为Excel（第四阶段完善）
        _logHelper?.Information($"导出报告为Excel：ID={reportId}, 文件={filePath}");
        return Task.FromResult(false);
    }

    private static void WriteJsonEntry<T>(ZipArchive archive, string entryName, T value)
    {
        var entry = archive.CreateEntry(NormalizeEntryName(entryName), CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, value, ArchiveJsonOptions);
    }

    private static T? ReadJsonEntry<T>(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(NormalizeEntryName(entryName));
        if (entry is null)
        {
            return default;
        }

        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, ArchiveJsonOptions);
    }

    private List<MeasurementArchiveFile> AddAnalysisFilesToArchive(
        ZipArchive archive,
        AnalysisResult result,
        List<AnalysisCsvFile> csvFiles,
        List<Report> reports,
        string analysisRoot,
        HashSet<string> rawVideoPaths,
        IProgress<OperationProgressInfo>? progress,
        CancellationToken cancellationToken,
        double startProgress,
        double endProgress)
    {
        var fileMaps = new List<MeasurementArchiveFile>();
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outputDirectory = string.IsNullOrWhiteSpace(result.OutputDirectory)
            ? null
            : Path.GetFullPath(result.OutputDirectory);
        var candidates = new List<(string Path, string RelativePath, string Role)>();

        if (!string.IsNullOrWhiteSpace(outputDirectory) && Directory.Exists(outputDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories))
            {
                if (ShouldSkipArchiveFile(path, rawVideoPaths))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(outputDirectory, path);
                candidates.Add((path, relativePath, "analysis_output"));
            }
        }

        foreach (var path in EnumerateExplicitPaths(result, csvFiles, reports))
        {
            if (ShouldSkipArchiveFile(path, rawVideoPaths))
            {
                continue;
            }

            var relativePath = outputDirectory is not null && IsUnderDirectory(path, outputDirectory)
                ? Path.GetRelativePath(outputDirectory, path)
                : Path.Combine("extra", Path.GetFileName(path));
            candidates.Add((path, relativePath, "referenced_file"));
        }

        if (candidates.Count == 0)
        {
            ReportProgress(progress, endProgress, L("ArchiveExport.Progress.PackageAnalysisStage"), L("ArchiveExport.Progress.NoResultFiles"));
            return fileMaps;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (path, relativePath, role) = candidates[i];
            ReportProgress(
                progress,
                CalculateProgress(startProgress, endProgress, i, candidates.Count),
                L("ArchiveExport.Progress.WriteResultFileStage"),
                L("ArchiveExport.Progress.WriteResultFileMessage", Path.GetFileName(path)));

            AddFileToArchive(archive, path, $"{analysisRoot}/files/{ToArchivePath(relativePath)}", relativePath, role, fileMaps, added);
        }

        ReportProgress(progress, endProgress, L("ArchiveExport.Progress.WriteResultCompleteStage"), L("ArchiveExport.Progress.WriteResultCompleteMessage", fileMaps.Count));
        return fileMaps;
    }

    private static IEnumerable<string> EnumerateExplicitPaths(AnalysisResult result, List<AnalysisCsvFile> csvFiles, List<Report> reports)
    {
        if (!string.IsNullOrWhiteSpace(result.SummaryFilePath))
        {
            yield return result.SummaryFilePath;
        }

        if (!string.IsNullOrWhiteSpace(result.AnnotatedVideoPath))
        {
            yield return result.AnnotatedVideoPath;
        }

        if (!string.IsNullOrWhiteSpace(result.ConfigFilePath))
        {
            yield return result.ConfigFilePath;
        }

        foreach (var csv in csvFiles)
        {
            if (!string.IsNullOrWhiteSpace(csv.FilePath))
            {
                yield return csv.FilePath;
            }
        }

        foreach (var report in reports)
        {
            if (!string.IsNullOrWhiteSpace(report.PdfFilePath))
            {
                yield return report.PdfFilePath;
            }

            if (!string.IsNullOrWhiteSpace(report.WordFilePath))
            {
                yield return report.WordFilePath;
            }
        }
    }

    private static void AddFileToArchive(
        ZipArchive archive,
        string sourcePath,
        string entryName,
        string relativePath,
        string role,
        List<MeasurementArchiveFile> fileMaps,
        HashSet<string> added)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(sourcePath);
        if (!added.Add(fullPath))
        {
            return;
        }

        var normalizedEntryName = NormalizeEntryName(entryName);
        archive.CreateEntryFromFile(fullPath, normalizedEntryName, CompressionLevel.Optimal);
        var info = new FileInfo(fullPath);
        fileMaps.Add(new MeasurementArchiveFile
        {
            OriginalPath = fullPath,
            EntryName = normalizedEntryName,
            RelativePath = ToArchivePath(relativePath),
            Role = role,
            Size = info.Length,
            Sha256 = ComputeSha256(fullPath)
        });
    }

    private async Task<Dictionary<string, string>> StageArchiveFilesAsync(
        ZipArchive archive,
        MeasurementArchiveManifest manifest,
        string stagingRoot,
        string finalRoot,
        IProgress<OperationProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allFiles = new List<(int MeasurementId, int AnalysisId, MeasurementArchiveFile File)>();
        foreach (var measurement in manifest.Measurements)
        {
            foreach (var analysis in measurement.AnalysisResults)
            {
                var analysisRoot = $"measurements/{measurement.OriginalMeasurementId}/analysis_results/{analysis.OriginalAnalysisResultId}";
                Directory.CreateDirectory(Path.Combine(
                    stagingRoot,
                    $"measurement_{measurement.OriginalMeasurementId}",
                    $"analysis_{analysis.OriginalAnalysisResultId}"));
                var files = ReadJsonEntry<List<MeasurementArchiveFile>>(archive, $"{analysisRoot}/files.json") ?? [];
                allFiles.AddRange(files.Select(file => (
                    measurement.OriginalMeasurementId,
                    analysis.OriginalAnalysisResultId,
                    file)));
            }
        }

        for (var index = 0; index < allFiles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = allFiles[index];
            ReportProgress(
                progress,
                CalculateProgress(1, 5, index, allFiles.Count),
                L("ArchiveImport.Progress.RestoreFilesStage"),
                L("ArchiveImport.Progress.RestoreFileMessage", Path.GetFileName(item.File.RelativePath)));

            var stagingAnalysisRoot = Path.Combine(
                stagingRoot,
                $"measurement_{item.MeasurementId}",
                $"analysis_{item.AnalysisId}");
            var finalAnalysisRoot = Path.Combine(
                finalRoot,
                $"measurement_{item.MeasurementId}",
                $"analysis_{item.AnalysisId}");
            Directory.CreateDirectory(stagingAnalysisRoot);
            var stagedPath = await ArchiveImportFileStager.ExtractAndValidateAsync(
                archive,
                item.File,
                stagingAnalysisRoot,
                cancellationToken);
            var relativePath = Path.GetRelativePath(stagingAnalysisRoot, stagedPath);
            var finalPath = ArchiveImportFileStager.ResolveSafePath(finalAnalysisRoot, relativePath);
            if (!paths.TryAdd(NormalizeEntryName(item.File.EntryName), finalPath))
            {
                throw new InvalidDataException($"结果包包含重复文件：{item.File.EntryName}");
            }
        }

        return paths;
    }

    private static string? MapArchivedPath(string? originalPath, Dictionary<string, string> oldToNewFilePath)
    {
        if (string.IsNullOrWhiteSpace(originalPath))
        {
            return originalPath;
        }

        var fullPath = Path.GetFullPath(originalPath);
        return oldToNewFilePath.TryGetValue(fullPath, out var newPath) ? newPath : originalPath;
    }

    private async Task<int> ResolveCurrentUserIdAsync(SqliteSugarHelper db)
    {
        try
        {
            if (App.Services?.GetService(typeof(ISessionService)) is ISessionService sessionService
                && sessionService.CurrentUser?.Id > 0)
            {
                return sessionService.CurrentUser.Id;
            }

            var user = await db.Queryable<User>().OrderBy(u => u.Id).FirstAsync();
            return user?.Id ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<int> ResolveImportedPatientIdAsync(SqliteSugarHelper db, Patient importedPatient, int currentUserId)
    {
        var existingPatients = await db.Queryable<Patient>()
            .Where(p => p.Status == PatientStatus.Active)
            .ToListAsync();

        var match = existingPatients.FirstOrDefault(p =>
            HasSameNonEmptyValue(p.IdNumber, importedPatient.IdNumber)
            || HasSameNonEmptyValue(p.Phone, importedPatient.Phone)
            || HasSameNonEmptyValue(p.HospitalNumber, importedPatient.HospitalNumber))
            ?? existingPatients.FirstOrDefault(p =>
                string.Equals(NormalizePatientText(p.Name), NormalizePatientText(importedPatient.Name), StringComparison.OrdinalIgnoreCase)
                && p.Gender == importedPatient.Gender
                && Nullable.Equals(p.BirthDate?.Date, importedPatient.BirthDate?.Date));

        if (match is null)
        {
            importedPatient.Id = 0;
            importedPatient.CreatedAt = DateTime.Now;
            importedPatient.UpdatedAt = DateTime.Now;
            importedPatient.Status = PatientStatus.Active;
            importedPatient.CreatedBy = currentUserId;
            return (int)await db.InsertReturnIdentityAsync(importedPatient);
        }

        MergeImportedPatientFields(match, importedPatient);
        match.UpdatedAt = DateTime.Now;
        await db.UpdateAsync(match);
        _logHelper?.Information($"导入测量包复用已有患者：ExistingPatientId={match.Id}, Name={match.Name}");
        return match.Id;
    }

    private static void MergeImportedPatientFields(Patient target, Patient source)
    {
        target.Phone = UseExistingOrImported(target.Phone, source.Phone);
        target.IdNumber = UseExistingOrImported(target.IdNumber, source.IdNumber);
        target.HospitalNumber = UseExistingOrImported(target.HospitalNumber, source.HospitalNumber);
        target.Address = UseExistingOrImported(target.Address, source.Address);
        target.MedicalHistory = UseExistingOrImported(target.MedicalHistory, source.MedicalHistory);
        target.Remark = UseExistingOrImported(target.Remark, source.Remark);
        target.BirthDate ??= source.BirthDate;
        target.Height ??= source.Height;
        target.Weight ??= source.Weight;
    }

    private static string UseExistingOrImported(string? currentValue, string? importedValue)
    {
        return string.IsNullOrWhiteSpace(currentValue) ? importedValue ?? string.Empty : currentValue;
    }

    private static bool HasSameNonEmptyValue(string? left, string? right)
    {
        var normalizedLeft = NormalizePatientText(left);
        var normalizedRight = NormalizePatientText(right);
        return !string.IsNullOrWhiteSpace(normalizedLeft)
               && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePatientText(string? value) => (value ?? string.Empty).Trim();

    private static bool ShouldSkipArchiveFile(string path, HashSet<string> rawVideoPaths)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return true;
        }

        var fullPath = Path.GetFullPath(path);
        if (rawVideoPaths.Contains(fullPath))
        {
            return true;
        }

        var extension = Path.GetExtension(path);
        return extension.Equals(".btfxpkg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEntryName(string entryName) => entryName.Replace('\\', '/');

    private static string ToArchivePath(string path) => path.Replace('\\', '/');

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string AppendArchiveRemark(string? remark, string importedNote)
    {
        var note = importedNote;
        return string.IsNullOrWhiteSpace(remark) ? note : $"{remark}\n{note}";
    }

    private static MeasurementRecord CreateMeasurementSnapshot(MeasurementRecord source) => new()
    {
        Id = source.Id,
        PatientId = source.PatientId,
        OperatorId = source.OperatorId,
        MeasurementDate = source.MeasurementDate,
        Status = source.Status,
        VideoFilePath = null,
        DurationSeconds = source.DurationSeconds,
        IsGuestData = source.IsGuestData,
        Remark = source.Remark,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
        MeasurementName = source.MeasurementName,
        MeasurementType = source.MeasurementType,
        FrontVideoPath = null,
        SideVideoPath = null,
        VideoSpec = source.VideoSpec,
        WalkwayLength = source.WalkwayLength,
        ImportStrategy = source.ImportStrategy,
        VideoImportMode = source.VideoImportMode,
        CurrentAnalysisStage = source.CurrentAnalysisStage,
        KeypointsCompleted = source.KeypointsCompleted,
        EventsCompleted = source.EventsCompleted,
        KinematicsCompleted = source.KinematicsCompleted,
        MeasurementFolderPath = source.MeasurementFolderPath
    };

    private static AnalysisResult CreateAnalysisResultSnapshot(AnalysisResult source) => new()
    {
        Id = source.Id,
        MeasurementId = source.MeasurementId,
        RequestId = source.RequestId,
        ProtocolVersion = source.ProtocolVersion,
        AlgorithmVersion = source.AlgorithmVersion,
        ModelVersion = source.ModelVersion,
        TaskStatus = source.TaskStatus,
        Success = source.Success,
        ErrorCode = source.ErrorCode,
        ErrorMessage = source.ErrorMessage,
        OutputDirectory = source.OutputDirectory,
        ConfigFilePath = source.ConfigFilePath,
        SummaryFilePath = source.SummaryFilePath,
        AnnotatedVideoPath = source.AnnotatedVideoPath,
        AnnotatedVideoDurationS = source.AnnotatedVideoDurationS,
        AnalysisDurationSeconds = source.AnalysisDurationSeconds,
        PackagePath = null,
        PackageCreatedAt = null,
        PackageValidationStatus = source.PackageValidationStatus,
        PackageValidationMessage = source.PackageValidationMessage,
        CreatedAt = source.CreatedAt
    };

    private static AnalysisCsvFile CreateCsvFileSnapshot(AnalysisCsvFile source) => new()
    {
        Id = source.Id,
        AnalysisResultId = source.AnalysisResultId,
        FileType = source.FileType,
        FilePath = source.FilePath,
        FileExists = source.FileExists,
        CreatedAt = source.CreatedAt
    };

    private static Report CreateReportSnapshot(Report source) => new()
    {
        Id = source.Id,
        MeasurementId = source.MeasurementId,
        PatientId = source.PatientId,
        CreatedBy = source.CreatedBy,
        ReportNumber = source.ReportNumber,
        Title = source.Title,
        ReportDate = source.ReportDate,
        DoctorOpinion = source.DoctorOpinion,
        Status = source.Status,
        PdfFilePath = source.PdfFilePath,
        AnalysisResultId = source.AnalysisResultId,
        ReportOptionsJson = source.ReportOptionsJson,
        WordFilePath = source.WordFilePath,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt
    };

    private static void ReportProgress(
        IProgress<OperationProgressInfo>? progress,
        double percent,
        string stage,
        string message,
        bool isIndeterminate = false)
    {
        progress?.Report(new OperationProgressInfo(Math.Clamp(percent, 0, 100), stage, message, isIndeterminate));
    }

    private string L(string key, params object[] args)
    {
        if (_localizationService is null)
        {
            return args.Length == 0 ? key : string.Format(key, args);
        }

        return args.Length == 0
            ? _localizationService.GetString(key)
            : _localizationService.GetString(key, args);
    }

    private static double CalculateProgress(double start, double end, int index, int total)
    {
        if (total <= 0)
        {
            return start;
        }

        var ratio = Math.Clamp(index / (double)total, 0, 1);
        return start + (end - start) * ratio;
    }

    private static double CalculateSegmentPoint(double start, double end, int index, int total, double innerRatio)
    {
        if (total <= 0)
        {
            return start;
        }

        var itemStartRatio = Math.Clamp(index / (double)total, 0, 1);
        var itemEndRatio = Math.Clamp((index + 1d) / total, 0, 1);
        return start + (end - start) * (itemStartRatio + (itemEndRatio - itemStartRatio) * Math.Clamp(innerRatio, 0, 1));
    }

    private static double Interpolate(double start, double end, double ratio)
    {
        return start + (end - start) * Math.Clamp(ratio, 0, 1);
    }

    #region 私有方法

    /// <summary>
    /// 导出到Excel
    /// </summary>
    private async Task<bool> ExportToExcelAsync<T>(List<T> data, string filePath) where T : class, new()
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var properties = typeof(T).GetProperties();
            IWorkbook workbook = Path.GetExtension(filePath).Equals(".xls", StringComparison.OrdinalIgnoreCase)
                ? new HSSFWorkbook()
                : new XSSFWorkbook();
            var sheet = workbook.CreateSheet("数据");
            var headerStyle = workbook.CreateCellStyle();
            var headerFont = workbook.CreateFont();
            headerFont.IsBold = true;
            headerStyle.SetFont(headerFont);

            var headerRow = sheet.CreateRow(0);
            for (var i = 0; i < properties.Length; i++)
            {
                var cell = headerRow.CreateCell(i);
                cell.SetCellValue(properties[i].Name);
                cell.CellStyle = headerStyle;
            }

            for (var rowIndex = 0; rowIndex < data.Count; rowIndex++)
            {
                var row = sheet.CreateRow(rowIndex + 1);
                var item = data[rowIndex];
                for (var columnIndex = 0; columnIndex < properties.Length; columnIndex++)
                {
                    row.CreateCell(columnIndex).SetCellValue(properties[columnIndex].GetValue(item)?.ToString() ?? string.Empty);
                }
            }

            for (var i = 0; i < properties.Length; i++)
            {
                sheet.AutoSizeColumn(i);
            }

            await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.Write(stream, leaveOpen: false);

            _logHelper?.Information($"Excel导出成功：{filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"Excel导出失败：{filePath}", ex);
            return false;
        }
    }

    /// <summary>
    /// 转义XML特殊字符
    /// </summary>
    private static string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    /// <summary>
    /// 导出到CSV
    /// </summary>
    private async Task<bool> ExportToCsvAsync<T>(List<T> data, string filePath) where T : class, new()
    {
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var properties = typeof(T).GetProperties();
            var sb = new StringBuilder();

            // 写入表头
            sb.AppendLine(string.Join(",", properties.Select(p => EscapeCsvField(p.Name))));

            // 写入数据
            foreach (var item in data)
            {
                var values = properties.Select(p => EscapeCsvField(p.GetValue(item)?.ToString() ?? ""));
                sb.AppendLine(string.Join(",", values));
            }

            // 写入文件（使用UTF-8 with BOM）
            await File.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(true));

            _logHelper?.Information($"CSV导出成功：{filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"CSV导出失败：{filePath}", ex);
            return false;
        }
    }

    /// <summary>
    /// 转义CSV字段
    /// </summary>
    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";

        // 如果包含逗号、引号或换行符，需要用引号包围
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    /// <summary>
    /// 获取状态文本
    /// </summary>
    private string GetStatusText(MeasurementStatus status)
    {
        return status switch
        {
            MeasurementStatus.Pending => L("DataManagement.Status.Pending"),
            MeasurementStatus.InProgress => L("DataManagement.Status.InProgress"),
            MeasurementStatus.Completed => L("DataManagement.Status.Completed"),
            MeasurementStatus.Cancelled => L("DataManagement.Status.Pending"),
            MeasurementStatus.Failed => L("DataManagement.Status.Failed"),
            _ => L("Unknown")
        };
    }

    #endregion
}

#region 测量结果包模型

internal sealed class MeasurementArchiveManifest
{
    public string PackageVersion { get; set; } = "1.0";
    public DateTime CreatedAt { get; set; }
    public bool ExcludesRawVideos { get; set; }
    public List<MeasurementArchiveMeasurement> Measurements { get; set; } = [];
}

internal sealed class MeasurementArchiveMeasurement
{
    public int OriginalMeasurementId { get; set; }
    public string MeasurementName { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<MeasurementArchiveAnalysis> AnalysisResults { get; set; } = [];
}

internal sealed class MeasurementArchiveAnalysis
{
    public int OriginalAnalysisResultId { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int FileCount { get; set; }
}

internal sealed class MeasurementArchiveFile
{
    public string OriginalPath { get; set; } = string.Empty;
    public string EntryName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

#endregion

#region 导出数据模型

/// <summary>
/// 患者导出模型
/// </summary>
public class PatientExportModel
{
    public string 姓名 { get; set; } = "";
    public string 性别 { get; set; } = "";
    public string 出生日期 { get; set; } = "";
    public string 年龄 { get; set; } = "";
    public string 电话 { get; set; } = "";
    public string 证件号 { get; set; } = "";
    public string 就诊号 { get; set; } = "";
    public string 身高cm { get; set; } = "";
    public string 体重kg { get; set; } = "";
    public string 地址 { get; set; } = "";
    public string 病史 { get; set; } = "";
    public string 备注 { get; set; } = "";
    public string 创建时间 { get; set; } = "";
}

/// <summary>
/// 测量数据导出模型
/// </summary>
public class MeasurementExportModel
{
    public string 患者姓名 { get; set; } = "";
    public string 性别 { get; set; } = "";
    public string 年龄 { get; set; } = "";
    public string 测量日期 { get; set; } = "";
    public string 测量状态 { get; set; } = "";
    public string 测量时长秒 { get; set; } = "";
    public string 操作员 { get; set; } = "";
    public string 左脚步幅cm { get; set; } = "";
    public string 右脚步幅cm { get; set; } = "";
    public string 步频步每分 { get; set; } = "";
    public string 步速m每s { get; set; } = "";
    public string 左脚支撑相百分比 { get; set; } = "";
    public string 右脚支撑相百分比 { get; set; } = "";
    public string 双支撑时间百分比 { get; set; } = "";
    public string 备注 { get; set; } = "";
}

#endregion
