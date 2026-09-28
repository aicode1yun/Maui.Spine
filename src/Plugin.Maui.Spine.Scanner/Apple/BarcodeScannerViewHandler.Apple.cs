#if IOS || MACCATALYST
using System.Diagnostics;
using AVFoundation;
using CoreFoundation;
using CoreGraphics;
using CoreMedia;
using CoreVideo;
using Foundation;
using ImageIO;
using Microsoft.Maui.Handlers;
using Plugin.Maui.Spine.Barcodes;
using UIKit;
using Vision;

namespace Plugin.Maui.Spine.Scanner;

public sealed class BarcodeScannerViewHandler() : ViewHandler<BarcodeScannerView, ScannerPreviewView>(Mapper, CommandMapper)
{
    public static readonly IPropertyMapper<BarcodeScannerView, BarcodeScannerViewHandler> Mapper =
        new PropertyMapper<BarcodeScannerView, BarcodeScannerViewHandler>(ViewMapper)
        {
            [nameof(BarcodeScannerView.Formats)] = static (h, v) => h.PlatformView.Reader.Formats = v.Formats,
            [nameof(BarcodeScannerView.LightGrid)] = static (h, v) => h.PlatformView.Reader.SetLightGrid(v.LightGrid),
            [nameof(BarcodeScannerView.IsScanning)] = static (h, v) => h.PlatformView.SetWanted(v.IsScanning),
            [nameof(BarcodeScannerView.IsTorchOn)] = static (h, v) => h.PlatformView.SetTorch(v.IsTorchOn),
        };

    public static readonly CommandMapper<BarcodeScannerView, BarcodeScannerViewHandler> CommandMapper = new(ViewCommandMapper);

    protected override ScannerPreviewView CreatePlatformView() => new();

    protected override void ConnectHandler(ScannerPreviewView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Detected = r => VirtualView?.RaiseDetected(r);
        platformView.Problem = (p, m) => VirtualView?.RaiseProblem(p, m);
        platformView.TorchAvailable = a => VirtualView?.SetTorchAvailable(a);
        platformView.DiagnosticsChanged = d => VirtualView?.SetDiagnostics(d);
        platformView.TorchChanged = on => { if (VirtualView is { } v && v.IsTorchOn != on) v.IsTorchOn = on; };
        platformView.SetOnScreen(platformView.Window is not null);
    }

    protected override void DisconnectHandler(ScannerPreviewView platformView)
    {
        platformView.Shutdown();
        base.DisconnectHandler(platformView);
    }
}

/// <summary>
/// The camera behind <see cref="BarcodeScannerView"/> on iOS and Mac Catalyst. Frames arrive on a serial queue
/// as bi-planar full-range YUV: Vision reads the standard codes from the pixel buffer, and the light-grid reader
/// reads the Y plane, which is luminance as it is.
/// </summary>
public sealed class ScannerPreviewView : UIView
{
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(2);

    private readonly AVCaptureSession _session = new();
    private readonly AVCaptureVideoPreviewLayer _previewLayer;
    private readonly DispatchQueue _queue = new("spine.scanner.frames");
    private readonly List<NSObject> _observers = [];
    private AVCaptureDevice? _device;
    private FrameDelegate? _frames;
    private NSTimer? _watchdog;
    private bool _configured, _configuring, _wanted = true, _onScreen;
    private volatile bool _torch, _shutdown;
    private int _torchQueued;
    private long _lastFrame;
    private int _processing;

    internal readonly FrameReader Reader = new();
    internal Action<BarcodeScanResult>? Detected;

    /// <summary>Places a hit's corners, in buffer pixels, in this view; main thread only.</summary>
    private BarcodeScanResult Place(FrameHit hit)
    {
        // The buffer is in the sensor's orientation, which is the space of the device point of interest
        var corners = hit.Corners
            .Select(c => _previewLayer.PointForCaptureDevicePointOfInterest(new CGPoint(c.X / hit.ImageWidth, c.Y / hit.ImageHeight)))
            .Select(p => new Point(p.X, p.Y))
            .ToArray();
        return hit.Result with { Corners = corners };
    }
    internal Action<ScannerProblem?, string?>? Problem;
    internal Action<bool>? TorchAvailable;
    internal Action<bool>? TorchChanged;
    internal Action<string?>? DiagnosticsChanged;

    public ScannerPreviewView()
    {
        BackgroundColor = UIColor.Black;
        _previewLayer = new AVCaptureVideoPreviewLayer(_session) { VideoGravity = AVLayerVideoGravity.ResizeAspectFill };
        Layer.AddSublayer(_previewLayer);
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        _previewLayer.Frame = Bounds;
    }

    // The one signal that always comes: a sheet can close without its page or MAUI saying so,
    // and a camera left running there fights the next scanner for the device
    public override void MovedToWindow()
    {
        base.MovedToWindow();
        SetOnScreen(Window is not null);
    }

    internal void SetWanted(bool wanted)
    {
        _wanted = wanted;
        Update();
    }

    internal void SetOnScreen(bool onScreen)
    {
        _onScreen = onScreen;
        Update();
    }

    internal void SetTorch(bool on)
    {
        _torch = on;
        QueueTorch();
    }

    // Switching the torch locks the camera and can wait for the session, so it runs on the session's queue,
    // never the main thread; taps that arrive while one is pending fold into it and only the last wish is applied
    private void QueueTorch()
    {
        if (Interlocked.Exchange(ref _torchQueued, 1) == 1) return;
        _queue.DispatchAsync(() =>
        {
            Volatile.Write(ref _torchQueued, 0);
            ApplyTorch(_torch && !_shutdown);
        });
    }

    /// <summary>Stops the camera for good and lets go of every buffer; the view is not reused after this.</summary>
    internal void Shutdown()
    {
        if (_shutdown) return;
        _shutdown = true;
        StopWatchdog();
        foreach (var o in _observers) o.Dispose();
        _observers.Clear();
        _queue.DispatchAsync(() =>
        {
            ApplyTorch(false);
            _session.StopRunning();
            _session.BeginConfiguration();
            foreach (var output in _session.Outputs) _session.RemoveOutput(output);
            foreach (var input in _session.Inputs) _session.RemoveInput(input);
            _session.CommitConfiguration();
        });
    }

    private async void Update()
    {
        if (_shutdown) return;
        bool run = _wanted && _onScreen;
        if (!run)
        {
            StopWatchdog();
            // The preview keeps the last frame, so a stop after a hit shows the code that was read
            if (_previewLayer.Connection is { } connection) connection.Enabled = false;
            _queue.DispatchAsync(() =>
            {
                ApplyTorch(false);
                if (_session.Running) _session.StopRunning();
            });
            return;
        }

        if (!_configured)
        {
            if (_configuring) return;
            _configuring = true;
            try { _configured = await ConfigureAsync(); }
            catch (Exception ex) { Report(ScannerProblem.Failed, $"{ScannerStrings.For(ScannerProblem.Failed)} ({ex.Message})"); }
            finally { _configuring = false; }
            if (!_configured || _shutdown || !(_wanted && _onScreen)) return;
        }

        Interlocked.Exchange(ref _lastFrame, Stopwatch.GetTimestamp());
        if (_previewLayer.Connection is { } live) live.Enabled = true;
        _queue.DispatchAsync(() =>
        {
            if (!_session.Running) _session.StartRunning();
            ApplyTorch(_torch && !_shutdown);
        });
        StartWatchdog();
    }

    private async Task<bool> ConfigureAsync()
    {
        // Asking without the usage string kills the app with no message; say what is missing instead
        if (NSBundle.MainBundle.ObjectForInfoDictionary("NSCameraUsageDescription") is null)
        {
            Report(ScannerProblem.Failed, "Add NSCameraUsageDescription to Info.plist to use the camera.");
            return false;
        }
        if (!await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVAuthorizationMediaType.Video))
        {
            Report(ScannerProblem.PermissionDenied);
            return false;
        }

        _device = AVCaptureDevice.GetDefaultDevice(AVCaptureDeviceType.BuiltInWideAngleCamera, AVMediaTypes.Video, AVCaptureDevicePosition.Back)
            ?? AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
        if (_device is null)
        {
            Report(ScannerProblem.NoCamera);
            return false;
        }

        _session.BeginConfiguration();
        try
        {
            // Enough detail for a 12 × 12 grid across a room, small enough to read every frame
            _session.SessionPreset = _session.CanSetSessionPreset(AVCaptureSession.Preset1280x720)
                ? AVCaptureSession.Preset1280x720
                : AVCaptureSession.PresetHigh;
            var input = AVCaptureDeviceInput.FromDevice(_device, out var error);
            if (input is null || !_session.CanAddInput(input))
            {
                Report(ScannerProblem.Failed, $"{ScannerStrings.For(ScannerProblem.Failed)} ({error?.LocalizedDescription})");
                return false;
            }
            _session.AddInput(input);

            var output = new AVCaptureVideoDataOutput
            {
                AlwaysDiscardsLateVideoFrames = true,
                WeakVideoSettings = new CVPixelBufferAttributes { PixelFormatType = CVPixelFormatType.CV420YpCbCr8BiPlanarFullRange }.Dictionary,
            };
            _frames = new FrameDelegate(this);
            output.SetSampleBufferDelegate(_frames, _queue);
            if (!_session.CanAddOutput(output))
            {
                Report(ScannerProblem.Failed);
                return false;
            }
            _session.AddOutput(output);
        }
        finally
        {
            _session.CommitConfiguration();
        }

        if (_device.LockForConfiguration(out _))
        {
            if (_device.IsFocusModeSupported(AVCaptureFocusMode.ContinuousAutoFocus)) _device.FocusMode = AVCaptureFocusMode.ContinuousAutoFocus;
            _device.UnlockForConfiguration();
        }
        // After the permission prompt this may run off the main thread; the header only follows main-thread changes
        bool hasTorch = _device.HasTorch;
        BeginInvokeOnMainThread(() => { if (!_shutdown) TorchAvailable?.Invoke(hasTorch); });

        _observers.Add(AVCaptureSession.Notifications.ObserveRuntimeError(_session, (_, e) =>
            Report(ScannerProblem.Failed, $"{ScannerStrings.For(ScannerProblem.Failed)} ({e.Error?.LocalizedDescription})")));
        _observers.Add(AVCaptureSession.Notifications.ObserveWasInterrupted(_session, (_, _) => Report(ScannerProblem.Interrupted)));
        _observers.Add(AVCaptureSession.Notifications.ObserveInterruptionEnded(_session, (_, _) => Report(null)));
        return true;
    }

    /// <summary>Runs on the session queue only.</summary>
    private void ApplyTorch(bool on)
    {
        if (_device is not { HasTorch: true } device || !_session.Running) return;
        var mode = on ? AVCaptureTorchMode.On : AVCaptureTorchMode.Off;
        if (device.TorchMode == mode) return;
        try
        {
            if (!device.LockForConfiguration(out var error))
            {
                Reader.ReportError("torch", new InvalidOperationException(error?.LocalizedDescription ?? "the camera is locked"));
                return;
            }
            if (device.IsTorchModeSupported(mode)) device.TorchMode = mode;
            device.UnlockForConfiguration();
        }
        catch (Exception ex)
        {
            Reader.ReportError("torch", ex);
        }
    }

    // No frame for two seconds: say so and restart, rather than show a frozen preview
    private void StartWatchdog()
    {
        _watchdog ??= NSTimer.CreateRepeatingScheduledTimer(0.5, _ =>
        {
            DiagnosticsChanged?.Invoke(Reader.TakeDiagnostics());
            // A frame still being read is slow, not stuck; only a silent camera on screen is restarted
            if (Window is null || !_wanted || Volatile.Read(ref _processing) != 0 || Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastFrame)) < FrameTimeout) return;
            Report(ScannerProblem.NoFrames);
            Interlocked.Exchange(ref _lastFrame, Stopwatch.GetTimestamp());
            _queue.DispatchAsync(() =>
            {
                if (_session.Running) _session.StopRunning();
                _session.StartRunning();
                ApplyTorch(_torch && !_shutdown);
            });
        });
    }

    private void StopWatchdog()
    {
        _watchdog?.Invalidate();
        _watchdog = null;
    }

    private void Report(ScannerProblem? problem, string? message = null)
    {
        var text = message ?? (problem is { } p ? ScannerStrings.For(p) : null);
        BeginInvokeOnMainThread(() => { if (!_shutdown) Problem?.Invoke(problem, text); });
    }

    private sealed class FrameDelegate(ScannerPreviewView owner) : AVCaptureVideoDataOutputSampleBufferDelegate
    {
        private readonly VNDetectBarcodesRequest _barcodes = new((_, _) => { });
        private BarcodeFormat _requested = BarcodeFormat.None;
        private bool _healthy = true;

        public override void DidOutputSampleBuffer(AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
        {
            try
            {
                if (owner._shutdown) return;
                Interlocked.Exchange(ref owner._lastFrame, Stopwatch.GetTimestamp());
                Volatile.Write(ref owner._processing, 1);

                using var pixels = sampleBuffer.GetImageBuffer() as CVPixelBuffer;
                if (pixels is null) return;
                long started = Stopwatch.GetTimestamp();

                var formats = owner.Reader.Formats;
                FrameHit? result = null;
                // Its own guard: a Vision failure must not keep the light-grid reader from the frame
                if (formats != BarcodeFormat.None)
                {
                    try { result = ReadStandard(pixels, formats); }
                    catch (Exception ex) { owner.Reader.ReportError("Vision", ex); }
                }

                if (result is null && owner.Reader.WantsLightGrid)
                {
                    pixels.Lock(CVPixelBufferLock.ReadOnly);
                    try
                    {
                        result = owner.Reader.ReadLightGrid(Plane(pixels, out int w, out int h, out int stride), w, h, stride);
                    }
                    finally
                    {
                        pixels.Unlock(CVPixelBufferLock.ReadOnly);
                    }
                }

                owner.Reader.CountFrame(Stopwatch.GetElapsedTime(started).TotalMilliseconds, result is not null);
                if (!_healthy)
                {
                    _healthy = true;
                    owner.Report(null);
                }
                if (result is not null)
                    owner.BeginInvokeOnMainThread(() => { if (!owner._shutdown) owner.Detected?.Invoke(owner.Place(result)); });
            }
            catch (Exception ex)
            {
                owner.Reader.ReportError("frame", ex);
                // One bad frame is a miss; keep going, and say so once
                if (_healthy)
                {
                    _healthy = false;
                    owner.Report(ScannerProblem.Failed, $"{ScannerStrings.For(ScannerProblem.Failed)} ({ex.Message})");
                }
            }
            finally
            {
                Volatile.Write(ref owner._processing, 0);
                // Holding a sample buffer stalls the capture pipeline after a few frames
                sampleBuffer.Dispose();
            }
        }

        private FrameHit? ReadStandard(CVPixelBuffer pixels, BarcodeFormat formats)
        {
            if (formats != _requested)
            {
                _barcodes.Symbologies = ToVision(formats);
                _requested = formats;
            }
            // The buffer's own orientation: codes read the same either way, and the corners stay in buffer space
            using var handler = new VNImageRequestHandler(pixels, CGImagePropertyOrientation.Up, new VNImageOptions());
            if (!handler.Perform([_barcodes], out var error))
                throw new InvalidOperationException(error?.LocalizedDescription ?? "Vision failed");
            float w = pixels.Width, h = pixels.Height;
            // Vision's points are normalised with the origin at the bottom left
            System.Numerics.Vector2 Pixel(CGPoint p) => new((float)p.X * w, (1 - (float)p.Y) * h);
            foreach (var observation in _barcodes.GetResults<VNBarcodeObservation>() ?? [])
                if (observation.PayloadStringValue is { Length: > 0 } value && FromVision(observation.Symbology) is var format && format != BarcodeFormat.None)
                    return new FrameHit(new BarcodeScanResult(value, format),
                        [Pixel(observation.TopLeft), Pixel(observation.TopRight), Pixel(observation.BottomRight), Pixel(observation.BottomLeft)], w, h);
            return null;
        }

        private static unsafe ReadOnlySpan<byte> Plane(CVPixelBuffer pixels, out int width, out int height, out int stride)
        {
            width = (int)pixels.GetWidthOfPlane(0);
            height = (int)pixels.GetHeightOfPlane(0);
            stride = (int)pixels.GetBytesPerRowOfPlane(0);
            return new ReadOnlySpan<byte>((void*)pixels.GetBaseAddress(0), stride * height);
        }
    }

    private static VNBarcodeSymbology[] ToVision(BarcodeFormat formats)
    {
        var list = new List<VNBarcodeSymbology>();
        void Add(BarcodeFormat f, params VNBarcodeSymbology[] s) { if ((formats & f) != 0) list.AddRange(s); }
        Add(BarcodeFormat.QrCode, VNBarcodeSymbology.QR);
        Add(BarcodeFormat.DataMatrix, VNBarcodeSymbology.DataMatrix);
        Add(BarcodeFormat.Aztec, VNBarcodeSymbology.Aztec);
        Add(BarcodeFormat.Pdf417, VNBarcodeSymbology.Pdf417);
        Add(BarcodeFormat.Code128, VNBarcodeSymbology.Code128);
        Add(BarcodeFormat.Code39, VNBarcodeSymbology.Code39, VNBarcodeSymbology.Code39FullAscii);
        Add(BarcodeFormat.Code93, VNBarcodeSymbology.Code93);
        // Vision reports UPC-A as EAN-13 with a leading zero
        Add(BarcodeFormat.Ean13 | BarcodeFormat.UpcA, VNBarcodeSymbology.Ean13);
        Add(BarcodeFormat.Ean8, VNBarcodeSymbology.Ean8);
        Add(BarcodeFormat.UpcE, VNBarcodeSymbology.Upce);
        Add(BarcodeFormat.Itf, VNBarcodeSymbology.I2OF5, VNBarcodeSymbology.Itf14);
        Add(BarcodeFormat.Codabar, VNBarcodeSymbology.Codabar);
        return [.. list.Distinct()];
    }

    private static BarcodeFormat FromVision(VNBarcodeSymbology symbology) => symbology switch
    {
        VNBarcodeSymbology.QR => BarcodeFormat.QrCode,
        VNBarcodeSymbology.DataMatrix => BarcodeFormat.DataMatrix,
        VNBarcodeSymbology.Aztec => BarcodeFormat.Aztec,
        VNBarcodeSymbology.Pdf417 => BarcodeFormat.Pdf417,
        VNBarcodeSymbology.Code128 => BarcodeFormat.Code128,
        VNBarcodeSymbology.Code39 or VNBarcodeSymbology.Code39FullAscii => BarcodeFormat.Code39,
        VNBarcodeSymbology.Code93 => BarcodeFormat.Code93,
        VNBarcodeSymbology.Ean13 => BarcodeFormat.Ean13,
        VNBarcodeSymbology.Ean8 => BarcodeFormat.Ean8,
        VNBarcodeSymbology.Upce => BarcodeFormat.UpcE,
        VNBarcodeSymbology.I2OF5 or VNBarcodeSymbology.Itf14 => BarcodeFormat.Itf,
        VNBarcodeSymbology.Codabar => BarcodeFormat.Codabar,
        _ => BarcodeFormat.None,
    };
}
#endif
