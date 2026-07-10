using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BTFX.Models.Camera;
using BTFX.Services.Interfaces;
using GxIAPINET;

namespace BTFX.Services.Implementations;

public sealed class DahengCameraRecordingService : ICameraRecordingService
{
    private readonly DahengCameraRuntime _runtime;

    public DahengCameraRecordingService(DahengCameraRuntime runtime)
    {
        _runtime = runtime;
    }

    public async Task<IReadOnlyList<CameraRecordingResult>> RecordAsync(
        CameraRecordingOptions options,
        IProgress<string>? logProgress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);

        var saveDirectory = Path.GetFullPath(options.SaveDirectory);
        Directory.CreateDirectory(saveDirectory);

        var slots = new List<DahengRecordingSlot>();

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var targetFrameRate = Math.Min(90, Math.Max(1, options.FrameRate));
            var targetFrameCount = Math.Max(1, targetFrameRate * Math.Max(1, options.DurationSeconds));

            Report(logProgress, "STAGE:PREPARING");
            Report(logProgress, "准备大恒相机录制");
            Report(logProgress, $"规格: {options.VideoSize} @ {targetFrameRate}fps, {options.DurationSeconds}s");

            var preparationWatch = Stopwatch.StartNew();
            await Task.Run(() =>
            {
                _runtime.Execute(factory =>
                {
                    foreach (var serialNumber in options.CameraNames)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        options.TransformOptionsByCameraName.TryGetValue(serialNumber, out var transformOptions);

                        var slot = new DahengRecordingSlot(
                            factory,
                            serialNumber,
                            options.FfmpegPath,
                            saveDirectory,
                            timestamp,
                            options.VideoSize,
                            targetFrameRate,
                            targetFrameCount,
                            transformOptions ?? new CameraTransformOptions(),
                            options.PreviewFrameReceived,
                            line => Report(logProgress, line));
                        slots.Add(slot);
                        slot.OpenAndPrepare();
                    }
                });
            }, cancellationToken).ConfigureAwait(false);
            preparationWatch.Stop();

            var startAt = DahengRecordingSchedule.StartStreamsThenResolveStartAt(
                slots.Select(slot => (Action)slot.StartAcquisition).ToList(),
                options.ScheduledStartAtUtc ?? DateTimeOffset.UtcNow,
                () => DateTimeOffset.UtcNow,
                TimeSpan.FromMilliseconds(500));
            var endAt = startAt.AddSeconds(options.DurationSeconds);
            Report(logProgress, $"STAGE:COUNTDOWN:{startAt:O}");
            Report(logProgress, $"大恒录制准备完成，耗时 {preparationWatch.Elapsed.TotalSeconds:0.000}s");
            foreach (var slot in slots)
            {
                slot.BeginRecording(startAt, endAt);
            }

            var startDelay = startAt - DateTimeOffset.UtcNow;
            if (startDelay > TimeSpan.Zero)
            {
                await Task.Delay(startDelay, cancellationToken).ConfigureAwait(false);
            }

            Report(logProgress, "STAGE:RECORD_START");

            await TrackRecordingAsync(slots, targetFrameCount, endAt, logProgress, cancellationToken).ConfigureAwait(false);

            await Task.WhenAll(slots.Select(slot => slot.StopRecordingAsync(fillRemaining: true))).ConfigureAwait(false);

            Report(logProgress, "STAGE:RECORD_DONE");
            Report(logProgress, "大恒录制阶段完成");
            Report(logProgress, "STAGE:TRANSCODE_START");
            Report(logProgress, "大恒录制已直接输出 MP4");
            Report(logProgress, "STAGE:DONE");

            return slots
                .Select(slot => new CameraRecordingResult(slot.SerialNumber, string.Empty, slot.OutputPath))
                .ToList();
        }
        catch
        {
            await Task.WhenAll(slots.Select(slot => slot.StopRecordingAsync(fillRemaining: false))).ConfigureAwait(false);

            throw;
        }
        finally
        {
            foreach (var slot in slots)
            {
                slot.Dispose();
            }
        }
    }

    private static async Task TrackRecordingAsync(
        IReadOnlyList<DahengRecordingSlot> slots,
        int targetFrameCount,
        DateTimeOffset endAt,
        IProgress<string>? logProgress,
        CancellationToken cancellationToken)
    {
        var timeoutAt = endAt.AddSeconds(8);
        while (!cancellationToken.IsCancellationRequested)
        {
            var status = string.Join("，", slots.Select(slot => $"{slot.SerialNumber}:{slot.QueuedFrameCount}/{targetFrameCount}"));
            Report(logProgress, $"大恒录制中 {status}");

            if (slots.All(slot => slot.QueuedFrameCount >= targetFrameCount)
                || DateTimeOffset.UtcNow >= endAt.AddMilliseconds(300))
            {
                break;
            }

            if (DateTimeOffset.UtcNow >= timeoutAt)
            {
                Report(logProgress, $"大恒录制超时：{status}");
                break;
            }

            await Task.Delay(100, cancellationToken);
        }
    }

    private static void ValidateOptions(CameraRecordingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.FfmpegPath) || !File.Exists(options.FfmpegPath))
        {
            throw new FileNotFoundException("找不到 ffmpeg.exe，请检查路径。", options.FfmpegPath);
        }

        if (options.CameraNames.Count == 0 || options.CameraNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("至少需要配置一个有效的大恒相机 SN。", nameof(options));
        }

        if (options.CameraNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.CameraNames.Count)
        {
            throw new ArgumentException("侧面相机和正面相机不能使用同一个大恒相机 SN。", nameof(options));
        }
    }

    private static void Report(IProgress<string>? progress, string message) => progress?.Report(message);

    private sealed class DahengRecordingSlot : IDisposable
    {
        private const int RecordingQueueCapacity = 6;
        private static readonly TimeSpan RecordingPreviewInterval = TimeSpan.FromMilliseconds(100);
        private readonly object _convertLock = new();
        private readonly object _lastFrameLock = new();
        private readonly object _previewUiLock = new();
        private readonly IGXFactory _factory;
        private readonly string _ffmpegPath;
        private readonly string _saveDirectory;
        private readonly string _timestamp;
        private readonly string _resolution;
        private readonly int _frameRate;
        private readonly int _targetFrameCount;
        private readonly CameraTransformOptions _transformOptions;
        private readonly Action<string, ImageSource>? _previewFrameReceived;
        private readonly Action<string> _log;
        private IGXDevice? _device;
        private IGXStream? _stream;
        private IGXFeatureControl? _featureControl;
        private IGXImageFormatConvert? _formatConvert;
        private IntPtr _convertBuffer = IntPtr.Zero;
        private ulong _convertBufferSize;
        private ArrayPool<byte>? _recordingBufferPool;
        private Channel<DahengFramePacket>? _recordingChannel;
        private Task? _writerTask;
        private Process? _process;
        private DateTimeOffset _recordingStartAt;
        private int _nextFrameIndex;
        private PooledFrameLease? _lastFrameLease;
        private int _lastFrameLength;
        private byte[]? _previewUiBuffer;
        private bool _previewUiFramePending;
        private WriteableBitmap? _previewBitmap;
        private int _previewBitmapWidth;
        private int _previewBitmapHeight;
        private bool _previewBitmapAssigned;
        private DateTimeOffset _lastPreviewFrameAt = DateTimeOffset.MinValue;
        private DateTimeOffset? _firstRecordingFrameAt;
        private long _capturedFrameCount;
        private long _queuedFrameCount;
        private long _writtenFrameCount;
        private long _duplicatedFrameCount;
        private long _queuePressureCount;
        private long _queueDepth;
        private long _queuePeakDepth;
        private bool _isRecording;

        public DahengRecordingSlot(
            IGXFactory factory,
            string serialNumber,
            string ffmpegPath,
            string saveDirectory,
            string timestamp,
            string resolution,
            int frameRate,
            int targetFrameCount,
            CameraTransformOptions transformOptions,
            Action<string, ImageSource>? previewFrameReceived,
            Action<string> log)
        {
            _factory = factory;
            SerialNumber = serialNumber;
            _ffmpegPath = ffmpegPath;
            _saveDirectory = saveDirectory;
            _timestamp = timestamp;
            _resolution = resolution;
            _frameRate = frameRate;
            _targetFrameCount = targetFrameCount;
            _transformOptions = transformOptions;
            _previewFrameReceived = previewFrameReceived;
            _log = log;
        }

        public string SerialNumber { get; }

        public string OutputPath { get; private set; } = string.Empty;

        public long QueuedFrameCount => Interlocked.Read(ref _queuedFrameCount);

        public void OpenAndPrepare()
        {
            try
            {
                DahengRecordingRetry.Execute(
                    maxAttempts: 3,
                    action: OpenDeviceAndStream,
                    waitBeforeRetry: attempt =>
                    {
                        CloseDeviceAndStream();
                        Thread.Sleep(250 * attempt);
                    });
            }
            catch (Exception ex)
            {
                CloseDeviceAndStream();
                throw new InvalidOperationException(
                    $"大恒相机 {SerialNumber} 的采集流打开失败，请稍候重试或重新连接相机。",
                    ex);
            }

            ConfigureDevice();

            var sourceWidth = (int)TryReadInt(_featureControl, "Width", 2048);
            var sourceHeight = (int)TryReadInt(_featureControl, "Height", 1536);
            var frameLength = checked(sourceWidth * sourceHeight * 3);
            _recordingBufferPool = ArrayPool<byte>.Create(frameLength, RecordingQueueCapacity + 4);
            OutputPath = Path.Combine(_saveDirectory, $"{_timestamp}_Daheng_{SafeFileName(SerialNumber)}.mp4");

            _recordingChannel = Channel.CreateBounded<DahengFramePacket>(new BoundedChannelOptions(RecordingQueueCapacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });
            _process = StartFfmpegProcess(
                _ffmpegPath,
                OutputPath,
                sourceWidth,
                sourceHeight,
                _frameRate,
                _transformOptions);
            _writerTask = Task.Run(() => WriteFramesAsync(_process, _recordingChannel));
            _log($"大恒相机 {SerialNumber} 录制管道已准备：{OutputPath}");
        }

        private void OpenDeviceAndStream()
        {
            _device = _factory.OpenDeviceBySN(SerialNumber, GX_ACCESS_MODE.GX_ACCESS_EXCLUSIVE)
                      ?? throw new InvalidOperationException($"未能打开大恒相机 {SerialNumber}。");
            _featureControl = _device.GetRemoteFeatureControl()
                              ?? throw new InvalidOperationException($"未能读取大恒相机 {SerialNumber} 的功能控制器。");
            _stream = _device.OpenStream(0)
                      ?? throw new InvalidOperationException($"未能打开大恒相机 {SerialNumber} 的采集流。");
            _formatConvert = _factory.CreateImageFormatConvert()
                             ?? throw new InvalidOperationException("未能创建大恒图像格式转换器。");
        }

        private void CloseDeviceAndStream()
        {
            try
            {
                _stream?.Close();
            }
            catch
            {
            }

            try
            {
                _device?.Close();
            }
            catch
            {
            }

            _stream = null;
            _device = null;
            _featureControl = null;
            _formatConvert = null;
        }

        public void BeginRecording(DateTimeOffset startAt, DateTimeOffset endAt)
        {
            _recordingStartAt = startAt;
            _nextFrameIndex = 0;
            ReleaseLastFrame();
            _lastFrameLength = 0;
            _firstRecordingFrameAt = null;
            Interlocked.Exchange(ref _capturedFrameCount, 0);
            Interlocked.Exchange(ref _queuedFrameCount, 0);
            Interlocked.Exchange(ref _writtenFrameCount, 0);
            Interlocked.Exchange(ref _duplicatedFrameCount, 0);
            Interlocked.Exchange(ref _queuePressureCount, 0);
            Interlocked.Exchange(ref _queueDepth, 0);
            Interlocked.Exchange(ref _queuePeakDepth, 0);
            _isRecording = true;
        }

        public void StartAcquisition()
        {
            if (_stream is null || _featureControl is null)
            {
                return;
            }

            _stream.RegisterCaptureCallback(this, OnFrameCallback);
            _stream.StartGrab();
            _featureControl.GetCommandFeature("AcquisitionStart").Execute();
        }

        public async Task StopRecordingAsync(bool fillRemaining)
        {
            _isRecording = false;
            try
            {
                _featureControl?.GetCommandFeature("AcquisitionStop").Execute();
            }
            catch
            {
            }

            try
            {
                _stream?.StopGrab();
                _stream?.UnregisterCaptureCallback();
            }
            catch
            {
            }

            if (fillRemaining)
            {
                await QueueRemainingFramesAsync().ConfigureAwait(false);
            }

            var channel = _recordingChannel;
            var process = _process;
            var writerTask = _writerTask;
            _recordingChannel = null;
            _process = null;
            _writerTask = null;

            try
            {
                channel?.Writer.TryComplete();
                if (writerTask is not null)
                {
                    await writerTask.WaitAsync(TimeSpan.FromSeconds(20));
                }
            }
            catch
            {
            }

            try
            {
                if (process is not null && !process.HasExited)
                {
                    await process.StandardInput.BaseStream.FlushAsync();
                    process.StandardInput.Close();
                    await process.WaitForExitAsync();
                }
            }
            catch
            {
                TryKill(process);
            }
            finally
            {
                process?.Dispose();
            }

            ReleaseLastFrame();
            _log(
                $"大恒相机 {SerialNumber} 录制统计：采集 {Interlocked.Read(ref _capturedFrameCount)} 帧，" +
                $"入队 {QueuedFrameCount} 帧，写入 {Interlocked.Read(ref _writtenFrameCount)} 帧，" +
                $"补帧 {Interlocked.Read(ref _duplicatedFrameCount)} 帧，队列压力 {Interlocked.Read(ref _queuePressureCount)} 次，" +
                $"队列峰值 {Interlocked.Read(ref _queuePeakDepth)}/{RecordingQueueCapacity}，" +
                $"首个录制帧 {_firstRecordingFrameAt?.ToString("O") ?? "--"}，文件 {OutputPath}。");
        }

        private void ConfigureDevice()
        {
            if (_featureControl is null)
            {
                return;
            }

            TryLoadUserSet0(_featureControl);
            TrySetEnum(_featureControl, "AcquisitionMode", "Continuous");
            TrySetEnum(_featureControl, "TriggerMode", "Off");

            var (width, height) = ParseResolution(_resolution);
            TrySetInt(_featureControl, "Width", width);
            TrySetInt(_featureControl, "Height", height);
            TrySetBool(_featureControl, "AcquisitionFrameRateEnable", true);
            TrySetEnum(_featureControl, "AcquisitionFrameRateMode", "On");
            TrySetFloat(_featureControl, "AcquisitionFrameRate", _frameRate);
        }

        private void OnFrameCallback(object userParam, IFrameData frameData)
        {
            if (!_isRecording || _recordingChannel is null || _frameRate <= 0)
            {
                return;
            }

            if (frameData.GetStatus() != GX_FRAME_STATUS_LIST.GX_FRAME_STATUS_SUCCESS)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            PooledBgrFrame? frame = null;
            try
            {
                frame = ConvertFrameToPooledBgrFrame(frameData);
                PublishPreviewFrame(frame, now);
                if (now < _recordingStartAt)
                {
                    return;
                }

                Interlocked.Increment(ref _capturedFrameCount);
                RememberLastFrame(frame.Lease, frame.Length);
                _firstRecordingFrameAt ??= now;
                var desiredFrameIndex = (int)Math.Floor((now - _recordingStartAt).TotalSeconds * _frameRate);
                desiredFrameIndex = Math.Clamp(desiredFrameIndex, 0, _targetFrameCount - 1);
                var repeatCount = Math.Min(
                    desiredFrameIndex - _nextFrameIndex + 1,
                    _targetFrameCount - _nextFrameIndex);
                if (repeatCount > 0 && TryQueueFrame(frame.Lease, frame.Length, repeatCount))
                {
                    _nextFrameIndex += repeatCount;
                }
            }
            catch (Exception ex)
            {
                _log($"大恒相机 {SerialNumber} 帧处理失败：{ex.Message}");
            }
            finally
            {
                frame?.Lease.Release();
            }
        }

        private void PublishPreviewFrame(PooledBgrFrame frame, DateTimeOffset now)
        {
            if (_previewFrameReceived is null)
            {
                return;
            }

            if (now - _lastPreviewFrameAt < RecordingPreviewInterval)
            {
                return;
            }

            int outputWidth;
            int outputHeight;
            int outputStride;
            lock (_previewUiLock)
            {
                if (_previewUiFramePending)
                {
                    return;
                }

                var rotated = _transformOptions.Orientation == CameraOrientation.PortraitClockwise;
                outputWidth = rotated ? frame.Height : frame.Width;
                outputHeight = rotated ? frame.Width : frame.Height;
                outputStride = checked(outputWidth * 3);
                var outputLength = checked(outputStride * outputHeight);
                _previewUiBuffer = EnsureManagedBuffer(_previewUiBuffer, outputLength);
                CopyPreviewPixels(frame, _previewUiBuffer, outputWidth, outputHeight, outputStride);
                _previewUiFramePending = true;
                _lastPreviewFrameAt = now;
            }

            try
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        UpdatePreviewBitmap(outputWidth, outputHeight, outputStride);
                    }
                    finally
                    {
                        lock (_previewUiLock)
                        {
                            _previewUiFramePending = false;
                        }
                    }
                }, DispatcherPriority.Render);
            }
            catch
            {
                lock (_previewUiLock)
                {
                    _previewUiFramePending = false;
                }
            }
        }

        private PooledBgrFrame ConvertFrameToPooledBgrFrame(IFrameData frameData)
        {
            lock (_convertLock)
            {
                if (_formatConvert is null)
                {
                    throw new InvalidOperationException("图像格式转换器未初始化。");
                }

                _formatConvert.SetDstFormat(GX_PIXEL_FORMAT_ENTRY.GX_PIXEL_FORMAT_BGR8);
                _formatConvert.SetValidBits(GX_VALID_BIT_LIST.GX_BIT_0_7);
                var requiredSize = _formatConvert.GetBufferSizeForConversion(frameData);
                EnsureConvertBuffer(requiredSize);
                _formatConvert.Convert(frameData, _convertBuffer, requiredSize, false);

                var width = checked((int)frameData.GetWidth());
                var height = checked((int)frameData.GetHeight());
                var stride = checked(width * 3);
                var length = checked(stride * height);
                var lease = new PooledFrameLease(_recordingBufferPool ?? ArrayPool<byte>.Shared, length);
                try
                {
                    Marshal.Copy(_convertBuffer, lease.Buffer, 0, length);
                    return new PooledBgrFrame(lease, length, width, height, stride);
                }
                catch
                {
                    lease.Release();
                    throw;
                }
            }
        }

        private void CopyPreviewPixels(
            PooledBgrFrame frame,
            byte[] output,
            int outputWidth,
            int outputHeight,
            int outputStride)
        {
            var rotated = _transformOptions.Orientation == CameraOrientation.PortraitClockwise;
            var flipped = _transformOptions.FlipHorizontal;
            if (!rotated && !flipped)
            {
                Buffer.BlockCopy(frame.Lease.Buffer, 0, output, 0, frame.Length);
                return;
            }

            for (var y = 0; y < outputHeight; y++)
            {
                var transformedY = flipped ? outputHeight - 1 - y : y;
                for (var x = 0; x < outputWidth; x++)
                {
                    var sourceX = rotated ? transformedY : x;
                    var sourceY = rotated ? frame.Height - 1 - x : transformedY;
                    var sourceIndex = sourceY * frame.Stride + sourceX * 3;
                    var targetIndex = y * outputStride + x * 3;
                    output[targetIndex] = frame.Lease.Buffer[sourceIndex];
                    output[targetIndex + 1] = frame.Lease.Buffer[sourceIndex + 1];
                    output[targetIndex + 2] = frame.Lease.Buffer[sourceIndex + 2];
                }
            }
        }

        private void UpdatePreviewBitmap(int width, int height, int stride)
        {
            if (_previewUiBuffer is null)
            {
                return;
            }

            if (_previewBitmap is null || _previewBitmapWidth != width || _previewBitmapHeight != height)
            {
                _previewBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr24, null);
                _previewBitmapWidth = width;
                _previewBitmapHeight = height;
                _previewBitmapAssigned = false;
            }

            _previewBitmap.WritePixels(new Int32Rect(0, 0, width, height), _previewUiBuffer, stride, 0);
            if (!_previewBitmapAssigned)
            {
                _previewFrameReceived?.Invoke(SerialNumber, _previewBitmap);
                _previewBitmapAssigned = true;
            }
        }

        private static byte[] EnsureManagedBuffer(byte[]? buffer, int requiredLength)
        {
            return buffer is not null && buffer.Length >= requiredLength
                ? buffer
                : new byte[requiredLength];
        }

        private void RememberLastFrame(PooledFrameLease lease, int length)
        {
            lease.AddReference();
            PooledFrameLease? previous;
            lock (_lastFrameLock)
            {
                previous = _lastFrameLease;
                _lastFrameLease = lease;
                _lastFrameLength = length;
            }

            previous?.Release();
        }

        private bool TryQueueFrame(PooledFrameLease lease, int length, int repeatCount)
        {
            var channel = _recordingChannel;
            if (channel is null || repeatCount <= 0)
            {
                return false;
            }

            lease.AddReference();
            if (!channel.Writer.TryWrite(new DahengFramePacket(lease, length, repeatCount)))
            {
                lease.Release();
                Interlocked.Increment(ref _queuePressureCount);
                return false;
            }

            var depth = Interlocked.Increment(ref _queueDepth);
            UpdateQueuePeak(depth);
            Interlocked.Add(ref _queuedFrameCount, repeatCount);
            if (repeatCount > 1)
            {
                Interlocked.Add(ref _duplicatedFrameCount, repeatCount - 1);
            }

            return true;
        }

        private async Task QueueRemainingFramesAsync()
        {
            var remaining = _targetFrameCount - _nextFrameIndex;
            if (remaining <= 0 || _recordingChannel is null)
            {
                return;
            }

            PooledFrameLease? lease;
            int length;
            lock (_lastFrameLock)
            {
                lease = _lastFrameLease;
                length = _lastFrameLength;
                lease?.AddReference();
            }

            if (lease is null)
            {
                return;
            }

            try
            {
                await _recordingChannel.Writer.WriteAsync(new DahengFramePacket(lease, length, remaining)).ConfigureAwait(false);
                lease = null;
                var depth = Interlocked.Increment(ref _queueDepth);
                UpdateQueuePeak(depth);
                Interlocked.Add(ref _queuedFrameCount, remaining);
                Interlocked.Add(ref _duplicatedFrameCount, remaining);
                _nextFrameIndex += remaining;
            }
            finally
            {
                lease?.Release();
            }
        }

        private async Task WriteFramesAsync(Process process, Channel<DahengFramePacket> channel)
        {
            await foreach (var packet in channel.Reader.ReadAllAsync())
            {
                Interlocked.Decrement(ref _queueDepth);
                try
                {
                    if (process.HasExited)
                    {
                        continue;
                    }

                    for (var index = 0; index < packet.RepeatCount; index++)
                    {
                        await process.StandardInput.BaseStream.WriteAsync(packet.Lease.Buffer.AsMemory(0, packet.Length)).ConfigureAwait(false);
                        Interlocked.Increment(ref _writtenFrameCount);
                    }
                }
                finally
                {
                    packet.Lease.Release();
                }
            }
        }

        private void UpdateQueuePeak(long depth)
        {
            while (true)
            {
                var currentPeak = Interlocked.Read(ref _queuePeakDepth);
                if (depth <= currentPeak
                    || Interlocked.CompareExchange(ref _queuePeakDepth, depth, currentPeak) == currentPeak)
                {
                    return;
                }
            }
        }

        private void ReleaseLastFrame()
        {
            PooledFrameLease? lease;
            lock (_lastFrameLock)
            {
                lease = _lastFrameLease;
                _lastFrameLease = null;
                _lastFrameLength = 0;
            }

            lease?.Release();
        }

        private void EnsureConvertBuffer(ulong requiredSize)
        {
            if (_convertBuffer != IntPtr.Zero && _convertBufferSize >= requiredSize)
            {
                return;
            }

            ReleaseConvertBuffer();
            _convertBuffer = Marshal.AllocCoTaskMem(checked((int)requiredSize));
            _convertBufferSize = requiredSize;
        }

        private void ReleaseConvertBuffer()
        {
            if (_convertBuffer == IntPtr.Zero)
            {
                return;
            }

            Marshal.FreeCoTaskMem(_convertBuffer);
            _convertBuffer = IntPtr.Zero;
            _convertBufferSize = 0;
        }

        public void Dispose()
        {
            try
            {
                StopRecordingAsync(fillRemaining: false).GetAwaiter().GetResult();
            }
            catch
            {
            }

            try
            {
                _stream?.Close();
            }
            catch
            {
            }

            try
            {
                _device?.Close();
            }
            catch
            {
            }

            ReleaseConvertBuffer();
            ReleaseLastFrame();
            _previewUiBuffer = null;
            _previewBitmap = null;
            _recordingBufferPool = null;
        }
    }

    private sealed record PooledBgrFrame(
        PooledFrameLease Lease,
        int Length,
        int Width,
        int Height,
        int Stride);

    private static Process StartFfmpegProcess(
        string ffmpegPath,
        string outputPath,
        int width,
        int height,
        int frameRate,
        CameraTransformOptions transformOptions)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        process.StartInfo.ArgumentList.Add("-y");
        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add("rawvideo");
        process.StartInfo.ArgumentList.Add("-pix_fmt");
        process.StartInfo.ArgumentList.Add("bgr24");
        process.StartInfo.ArgumentList.Add("-video_size");
        process.StartInfo.ArgumentList.Add($"{width}x{height}");
        process.StartInfo.ArgumentList.Add("-framerate");
        process.StartInfo.ArgumentList.Add(frameRate.ToString());
        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add("-");
        process.StartInfo.ArgumentList.Add("-an");
        process.StartInfo.ArgumentList.Add("-c:v");
        process.StartInfo.ArgumentList.Add("libx264");
        process.StartInfo.ArgumentList.Add("-preset");
        process.StartInfo.ArgumentList.Add("ultrafast");
        process.StartInfo.ArgumentList.Add("-crf");
        process.StartInfo.ArgumentList.Add("20");
        var filter = BuildVideoFilter(transformOptions);
        if (!string.IsNullOrWhiteSpace(filter))
        {
            process.StartInfo.ArgumentList.Add("-vf");
            process.StartInfo.ArgumentList.Add(filter);
        }

        process.StartInfo.ArgumentList.Add("-pix_fmt");
        process.StartInfo.ArgumentList.Add("yuv420p");
        process.StartInfo.ArgumentList.Add("-movflags");
        process.StartInfo.ArgumentList.Add("+faststart");
        process.StartInfo.ArgumentList.Add(outputPath);
        process.Start();

        _ = Task.Run(async () =>
        {
            try
            {
                while (!process.HasExited)
                {
                    _ = await process.StandardError.ReadLineAsync();
                }
            }
            catch
            {
            }
        });

        return process;
    }

    private static string BuildVideoFilter(CameraTransformOptions transformOptions)
    {
        var filters = new List<string>();
        if (transformOptions.Orientation == CameraOrientation.PortraitClockwise)
        {
            filters.Add("transpose=1");
        }

        if (transformOptions.FlipHorizontal)
        {
            filters.Add("vflip");
        }

        return string.Join(",", filters);
    }

    private static void TryLoadUserSet0(IGXFeatureControl featureControl)
    {
        try
        {
            if (featureControl.IsImplemented("UserSetSelector")
                && featureControl.IsWritable("UserSetSelector")
                && featureControl.IsImplemented("UserSetLoad"))
            {
                featureControl.GetEnumFeature("UserSetSelector").SetValue("UserSet0");
                featureControl.GetCommandFeature("UserSetLoad").Execute();
            }
        }
        catch
        {
        }
    }

    private static void TrySetEnum(IGXFeatureControl featureControl, string featureName, string value)
    {
        try
        {
            if (featureControl.IsImplemented(featureName) && featureControl.IsWritable(featureName))
            {
                featureControl.GetEnumFeature(featureName).SetValue(value);
            }
        }
        catch
        {
        }
    }

    private static void TrySetInt(IGXFeatureControl featureControl, string featureName, long value)
    {
        try
        {
            if (!featureControl.IsImplemented(featureName) || !featureControl.IsWritable(featureName))
            {
                return;
            }

            var feature = featureControl.GetIntFeature(featureName);
            feature.SetValue(Math.Clamp(value, feature.GetMin(), feature.GetMax()));
        }
        catch
        {
        }
    }

    private static void TrySetFloat(IGXFeatureControl featureControl, string featureName, double value)
    {
        try
        {
            if (!featureControl.IsImplemented(featureName) || !featureControl.IsWritable(featureName))
            {
                return;
            }

            var feature = featureControl.GetFloatFeature(featureName);
            feature.SetValue(Math.Clamp(value, feature.GetMin(), feature.GetMax()));
        }
        catch
        {
        }
    }

    private static void TrySetBool(IGXFeatureControl featureControl, string featureName, bool value)
    {
        try
        {
            if (featureControl.IsImplemented(featureName) && featureControl.IsWritable(featureName))
            {
                featureControl.GetBoolFeature(featureName).SetValue(value);
            }
        }
        catch
        {
        }
    }

    private static long TryReadInt(IGXFeatureControl? featureControl, string featureName, long fallback)
    {
        try
        {
            return featureControl is not null
                   && featureControl.IsImplemented(featureName)
                   && featureControl.IsReadable(featureName)
                ? featureControl.GetIntFeature(featureName).GetValue()
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static (long Width, long Height) ParseResolution(string value)
    {
        var parts = value.Split('x', 'X');
        if (parts.Length == 2
            && long.TryParse(parts[0], out var width)
            && long.TryParse(parts[1], out var height)
            && width > 0
            && height > 0)
        {
            return (width, height);
        }

        return (2048, 1536);
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    private static void TryKill(Process? process)
    {
        try
        {
            if (process is not null && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
