using HostelManagement.Utilities;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace HostelManagement.Forms;

/// <summary>
/// Takes the student's photo with the computer's webcam (client decision, version 1.2): a live picture,
/// Capture to freeze it, Retake to try again and Use Photo to keep it. The photo is saved as a JPG file in the
/// temporary folder; <see cref="PhotoFile"/> is set after DialogResult.OK and the student form stores it on Save.
/// Uses the Windows camera API, so it works with a built-in laptop camera or a USB webcam.
/// </summary>
public sealed class CameraForm : Form
{
    private const string NoCameraMessage =
        "No camera was found. Connect a webcam (or check that the laptop camera is switched on) and try again, " +
        "or use Choose Photo to pick a photo file.";

    private const string BlockedMessage =
        "Windows does not allow this program to use the camera. Open Windows Settings > Privacy & security > Camera, " +
        "switch on \"Camera access\" and \"Let desktop apps access your camera\", then try again.";

    private readonly PictureBox _preview;
    private readonly ComboBox _cameraBox;
    private readonly Button _captureButton;
    private readonly Button _retakeButton;
    private readonly Button _useButton;
    private readonly Label _statusLabel;

    private readonly SemaphoreSlim _frameGate = new(1, 1);
    private MediaCapture? _capture;
    private MediaFrameReader? _reader;
    private byte[]? _lastFrame;
    private bool _frozen;
    private bool _closing;

    public CameraForm()
    {
        Text = "Take Photo";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(560, 540);

        var cameraLabel = new Label { Text = "Camera", AutoSize = true, Location = new Point(14, 17) };
        _cameraBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(80, 13),
            Width = 466,
            DisplayMember = nameof(CameraChoice.Name),
        };
        _cameraBox.SelectionChangeCommitted += async (_, _) => await StartSelectedCameraAsync();

        _preview = new PictureBox
        {
            Location = new Point(14, 48),
            Size = new Size(532, 400),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black,
        };

        _statusLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(532, 0),
            Location = new Point(14, 454),
            ForeColor = UiTheme.TextMuted,
            Text = "Starting the camera...",
        };

        _captureButton = new Button { Text = "Capture", Location = new Point(14, 494), Enabled = false };
        UiTheme.StylePrimaryButton(_captureButton);
        _captureButton.Click += (_, _) => Freeze();

        _retakeButton = new Button { Text = "Retake", Location = new Point(134, 494), Enabled = false };
        UiTheme.StyleSecondaryButton(_retakeButton);
        _retakeButton.Click += (_, _) => Unfreeze();

        _useButton = new Button { Text = "Use Photo", Location = new Point(306, 494), Enabled = false };
        UiTheme.StylePrimaryButton(_useButton);
        _useButton.Click += (_, _) => UsePhoto();

        var cancelButton = new Button { Text = "Cancel", Location = new Point(426, 494), DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(cancelButton);
        CancelButton = cancelButton;

        Controls.AddRange([cameraLabel, _cameraBox, _preview, _statusLabel, _captureButton, _retakeButton, _useButton, cancelButton]);

        Shown += async (_, _) => await FindCamerasAsync();
        FormClosing += async (_, _) => await StopAsync(closing: true);
        FormClosed += (_, _) => _preview.Image?.Dispose();
    }

    /// <summary>The captured photo (a JPG in the temporary folder), set after DialogResult.OK.</summary>
    public string? PhotoFile { get; private set; }

    private async Task FindCamerasAsync()
    {
        try
        {
            IReadOnlyList<MediaFrameSourceGroup> groups = await MediaFrameSourceGroup.FindAllAsync();
            List<CameraChoice> cameras = groups
                .Where(g => g.SourceInfos.Any(IsColorVideo))
                .Select(g => new CameraChoice(g.DisplayName, g))
                .ToList();
            if (cameras.Count == 0)
            {
                ShowProblem(NoCameraMessage);
                return;
            }

            _cameraBox.DataSource = cameras;
            _cameraBox.Enabled = cameras.Count > 1;
            await StartSelectedCameraAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not list the cameras.", ex);
            ShowProblem(NoCameraMessage);
        }
    }

    private async Task StartSelectedCameraAsync()
    {
        if (_cameraBox.SelectedItem is not CameraChoice camera)
        {
            return;
        }

        await StopAsync(closing: false);
        Unfreeze();
        _statusLabel.Text = "Starting the camera...";
        try
        {
            _capture = new MediaCapture();
            await _capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                SourceGroup = camera.Group,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                StreamingCaptureMode = StreamingCaptureMode.Video,
            });

            MediaFrameSource source = _capture.FrameSources.Values.First(s => IsColorVideo(s.Info));
            _reader = await _capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8);
            _reader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
            _reader.FrameArrived += OnFrameArrived;
            if (await _reader.StartAsync() != MediaFrameReaderStartStatus.Success)
            {
                ShowProblem("The camera could not be started. It may be in use by another program (for example a video call).");
                return;
            }

            _statusLabel.Text = "Ask the student to look at the camera, then click Capture.";
            _captureButton.Enabled = true;
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLogger.Error("Camera access was refused.", ex);
            ShowProblem(BlockedMessage);
        }
        catch (Exception ex)
        {
            AppLogger.Error("The camera could not be started.", ex);
            ShowProblem("The camera could not be started. It may be in use by another program (for example a video call).");
        }
    }

    private static bool IsColorVideo(MediaFrameSourceInfo info) =>
        info.SourceKind == MediaFrameSourceKind.Color &&
        (info.MediaStreamType == MediaStreamType.VideoPreview || info.MediaStreamType == MediaStreamType.VideoRecord);

    /// <summary>Converts each camera frame to a JPG and shows it, skipping frames while the previous one is still being handled.</summary>
    private async void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (_frozen || _closing || !await _frameGate.WaitAsync(0))
        {
            return;
        }
        try
        {
            using MediaFrameReference? frame = sender.TryAcquireLatestFrame();
            SoftwareBitmap? bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
            if (bitmap is null)
            {
                return;
            }

            byte[] jpg = await EncodeJpegAsync(bitmap);
            if (_frozen || _closing || IsDisposed)
            {
                return;
            }
            BeginInvoke(() => ShowFrame(jpg));
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            // The window closed while a frame was being handled.
        }
        finally
        {
            _frameGate.Release();
        }
    }

    private static async Task<byte[]> EncodeJpegAsync(SoftwareBitmap frame)
    {
        using SoftwareBitmap bgra = SoftwareBitmap.Convert(frame, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
        using var stream = new InMemoryRandomAccessStream();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
        encoder.SetSoftwareBitmap(bgra);
        await encoder.FlushAsync();

        using Stream data = stream.AsStreamForRead();
        data.Position = 0;
        using var copy = new MemoryStream();
        await data.CopyToAsync(copy);
        return copy.ToArray();
    }

    private void ShowFrame(byte[] jpg)
    {
        if (_frozen || IsDisposed)
        {
            return;
        }
        try
        {
            using var stream = new MemoryStream(jpg);
            using Image image = Image.FromStream(stream);
            Image? old = _preview.Image;
            _preview.Image = new Bitmap(image);
            old?.Dispose();
            _lastFrame = jpg;
        }
        catch (ArgumentException)
        {
            // An incomplete frame: the next one replaces it.
        }
    }

    private void Freeze()
    {
        if (_lastFrame is null)
        {
            return;
        }
        _frozen = true;
        _captureButton.Enabled = false;
        _retakeButton.Enabled = true;
        _useButton.Enabled = true;
        _statusLabel.Text = "Click Use Photo to keep this photo, or Retake to try again.";
    }

    private void Unfreeze()
    {
        _frozen = false;
        _captureButton.Enabled = _reader is not null;
        _retakeButton.Enabled = false;
        _useButton.Enabled = false;
        if (_reader is not null)
        {
            _statusLabel.Text = "Ask the student to look at the camera, then click Capture.";
        }
    }

    private void UsePhoto()
    {
        if (_lastFrame is null)
        {
            return;
        }
        try
        {
            string file = Path.Combine(Path.GetTempPath(), $"HostelManagement_Camera_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
            File.WriteAllBytes(file, _lastFrame);
            PhotoFile = file;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The photo could not be saved.");
        }
    }

    private void ShowProblem(string message)
    {
        _statusLabel.ForeColor = UiTheme.Danger;
        _statusLabel.Text = message;
        _captureButton.Enabled = false;
    }

    private async Task StopAsync(bool closing)
    {
        _closing = closing;
        MediaFrameReader? reader = _reader;
        MediaCapture? capture = _capture;
        _reader = null;
        _capture = null;
        try
        {
            if (reader is not null)
            {
                reader.FrameArrived -= OnFrameArrived;
                await reader.StopAsync();
                reader.Dispose();
            }
            capture?.Dispose();
        }
        catch (Exception ex)
        {
            AppLogger.Error("The camera could not be stopped cleanly.", ex);
        }
    }

    private sealed record CameraChoice(string Name, MediaFrameSourceGroup Group);
}
