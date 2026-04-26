using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsNothIsland.Services;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;

namespace WindowsNothIsland
{
    public partial class MainWindow : Window
    {
        private readonly MediaService _mediaService = new();
        private readonly DispatcherTimer _mediaTimer = new();
        private readonly AudioService _audioService = new();
        private bool _wasExpanded = false;
        private bool _isExpanded = false;
        private bool _isHovering = false;
        private double _windowStartLeft;
        private bool _isDraggingTimeline = false;

        private bool _isDraggingIsland = false;
        private Point _dragStartScreenPoint;

        private TimeSpan _currentMediaDuration = TimeSpan.Zero;
        private Color _currentAlbumTint = Color.FromRgb(15, 15, 16);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;

        public MainWindow()
        {
            InitializeComponent();

            CollapsedView.Opacity = 1;
            ExpandedView.Opacity = 0;
            ClockView.Opacity = 0;
            ClockExpandedView.Opacity = 0;

            Loaded += (s, e) =>
            {
                var handle = new WindowInteropHelper(this).Handle;
                HwndSource source = HwndSource.FromHwnd(handle);
                source.AddHook(HwndHook);

                // Ctrl + Shift + Space
                RegisterHotKey(handle, HOTKEY_ID, 0x0002 | 0x0004, 0x20);
            };

            Loaded += (_, _) =>
            {
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                Top = 0;
                SetIslandBlack();
            };

            _mediaTimer.Interval = TimeSpan.FromMilliseconds(800);
            _mediaTimer.Tick += async (_, _) => await UpdateMediaInfo();
            _mediaTimer.Start();
        }

        private async Task UpdateMediaInfo()
        {
            var media = await _mediaService.GetCurrentMediaAsync();

            var title = string.IsNullOrWhiteSpace(media.Title)
                ? "Nothing playing"
                : media.Title;

            var artist = string.IsNullOrWhiteSpace(media.Artist)
                ? media.SourceApp
                : media.Artist;

            bool hasMedia = title != "Nothing playing";

            UpdateClock();

            if (!hasMedia)
            {
                CollapsedView.Visibility = Visibility.Collapsed;
                ExpandedView.Visibility = Visibility.Collapsed;
                SetIslandBlack();

                if (_isHovering)
                {
                    ClockView.Visibility = Visibility.Collapsed;
                    ShowWithFade(ClockExpandedView);
                    AnimateIsland(650, 220, 18);
                }
                else
                {
                    ClockExpandedView.Visibility = Visibility.Collapsed;
                    ShowWithFade(ClockView);

                    _isExpanded = false;
                    AnimateIsland(280, 74, 18);
                }

                return;
            }

            ClockView.Visibility = Visibility.Collapsed;
            ClockExpandedView.Visibility = Visibility.Collapsed;

            if (_isHovering)
            {
                _isExpanded = true;
                CollapsedView.Visibility = Visibility.Collapsed;
                ExpandedView.Visibility = Visibility.Visible;
                SetIslandTint();
            }
            else
            {
                _isExpanded = false;
                CollapsedView.Visibility = Visibility.Visible;
                ExpandedView.Visibility = Visibility.Collapsed;
                SetIslandBlack();
            }

            TitleText.Text = title;
            ExpandedTitle.Text = title;

            var sourceName = CleanSourceApp(media.SourceApp);

            ExpandedArtist.Text = string.IsNullOrWhiteSpace(sourceName)
                ? artist
                : $"{artist} • {sourceName}";

            if (media.Thumbnail != null)
            {
                using var ms = new MemoryStream(media.Thumbnail);
                var image = new BitmapImage();

                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();

                ExpandedAlbumArt.Source = image;
                AlbumArt.Source = image;

                _currentAlbumTint = GetTintFromImage(image);

                if (_isExpanded)
                    SetIslandTint();
            }

            PlayPauseIcon.Text = media.IsPlaying ? "⏸" : "▶";

            if (media.Duration.TotalSeconds > 0)
            {
                _currentMediaDuration = media.Duration;

                if (!_isDraggingTimeline)
                {
                    double ratio = media.Position.TotalSeconds / media.Duration.TotalSeconds;
                    ratio = Math.Clamp(ratio, 0, 1);

                    double trackWidth = 320;
                    double progressWidth = trackWidth * ratio;

                    ProgressBar.Width = progressWidth;
                    Canvas.SetLeft(ProgressDot, Math.Max(0, progressWidth - 4));

                    CurrentTimeText.Text = FormatTime(media.Position);
                    DurationText.Text = FormatTime(media.Duration);
                }
            }
            else
            {
                _currentMediaDuration = TimeSpan.Zero;

                ProgressBar.Width = 0;
                Canvas.SetLeft(ProgressDot, 0);

                CurrentTimeText.Text = "0:00";
                DurationText.Text = "0:00";
            }
        }

        private string CleanSourceApp(string sourceApp)
        {
            if (string.IsNullOrWhiteSpace(sourceApp))
                return "";

            sourceApp = sourceApp.ToLower();

            if (sourceApp.Contains("spotify"))
                return "Spotify";

            if (sourceApp.Contains("chrome") || sourceApp.Contains("youtube"))
                return "YouTube";

            if (sourceApp.Contains("firefox"))
                return "Firefox";

            if (sourceApp.Contains("vlc"))
                return "VLC";

            if (sourceApp.Contains("msedge"))
                return "Edge";

            return "";
        }

        private Color GetTintFromImage(BitmapImage bitmap)
        {
            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);

            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];

            converted.CopyPixels(pixels, stride, 0);

            long r = 0;
            long g = 0;
            long b = 0;
            int count = 0;

            for (int i = 0; i < pixels.Length; i += 80)
            {
                b += pixels[i];
                g += pixels[i + 1];
                r += pixels[i + 2];
                count++;
            }

            if (count == 0)
                return Color.FromRgb(15, 15, 16);

            byte avgR = (byte)(r / count);
            byte avgG = (byte)(g / count);
            byte avgB = (byte)(b / count);

            return Color.FromRgb(
                (byte)Math.Clamp(avgR * 0.55, 20, 115),
                (byte)Math.Clamp(avgG * 0.55, 20, 115),
                (byte)Math.Clamp(avgB * 0.55, 20, 115)
            );
        }

        private void SetIslandBlack()
        {
            Island.Background = new SolidColorBrush(Color.FromRgb(15, 15, 16));
            ExpandedView.Background = new SolidColorBrush(Color.FromRgb(15, 15, 16));
        }

        private void SetIslandTint()
        {
            var brush = new SolidColorBrush((Color)Island.Background.GetValue(SolidColorBrush.ColorProperty));

            var anim = new ColorAnimation
            {
                To = _currentAlbumTint,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
            Island.Background = brush;
            ExpandedView.Background = brush;
        }

        private void FadeTo(UIElement element, double opacity, int durationMs = 180)
        {
            var anim = new DoubleAnimation
            {
                To = opacity,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void ShowWithFade(UIElement element)
        {
            element.Opacity = 0;
            element.Visibility = Visibility.Visible;
            FadeTo(element, 1);
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;

            ClockTimeText.Text = now.ToString("HH:mm");
            ClockDateText.Text = now.ToString("ddd, MMM d");

            ClockDayExpandedText.Text = now.ToString("dddd");
            ClockFullDateExpandedText.Text = now.ToString("MMM d, yyyy");

            double seconds = now.Second;
            double minutes = now.Minute + seconds / 60.0;
            double hours = (now.Hour % 12) + minutes / 60.0;

            SecondHandRotate.Angle = seconds * 6;
            MinuteHandRotate.Angle = minutes * 6;
            HourHandRotate.Angle = hours * 30;
        }

        private string FormatTime(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";

            return $"{time.Minutes}:{time.Seconds:D2}";
        }

        private async void PreviousButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            await _mediaService.PreviousAsync();
            await UpdateMediaInfo();
        }

        private async void PlayPauseButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            await _mediaService.TogglePlayPauseAsync();
            await UpdateMediaInfo();
        }

        private async void NextButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            await _mediaService.NextAsync();
            await UpdateMediaInfo();
        }

        private async void Island_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                _audioService.ChangeVolume(e.Delta > 0 ? 0.05f : -0.05f);
                return;
            }

            if (e.Delta > 0)
                await _mediaService.NextSessionAsync();
            else
                await _mediaService.PreviousSessionAsync();

            await UpdateMediaInfo();
        }

        private async void Timeline_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isDraggingTimeline)
                return;

            _isDraggingTimeline = false;
            TimelineCanvas.ReleaseMouseCapture();

            var newPosition = GetTimelinePositionFromMouse(e);
            await _mediaService.SeekAsync(newPosition);
            await Task.Delay(80);
            await UpdateMediaInfo();
        }

        private void Island_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Don't start dragging if clicking timeline or controls
            if (e.OriginalSource is FrameworkElement fe)
            {
                if (fe.Name == "TimelineCanvas" ||
                    fe.Name == "ProgressBar" ||
                    fe.Name == "ProgressDot")
                {
                    return;
                }
            }

            if (e.ClickCount == 2)
            {
                _isDraggingIsland = false;
                Island.ReleaseMouseCapture();
                ResetIslandToCenter();
                e.Handled = true;
                return;
            }

            if (e.ClickCount != 1)
                return;

            // clear reset animation so dragging works again
            BeginAnimation(Window.LeftProperty, null);

            _isDraggingIsland = true;
            _dragStartScreenPoint = PointToScreen(e.GetPosition(this));
            _windowStartLeft = Left;

            Island.CaptureMouse();
            e.Handled = true;
        }

        private void Timeline_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_currentMediaDuration.TotalSeconds <= 0)
                return;

            _isDraggingTimeline = true;
            TimelineCanvas.CaptureMouse();

            PreviewTimelineFromMouse(e);
        }

        private void ResetIslandToCenter()
        {
            double targetLeft = (SystemParameters.PrimaryScreenWidth - Width) / 2;

            BeginAnimation(Window.LeftProperty, null);

            var anim = new DoubleAnimation
            {
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(Window.LeftProperty, anim);
        }

        private void Island_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDraggingIsland)
                return;

            var currentScreenPoint = PointToScreen(e.GetPosition(this));
            double dx = currentScreenPoint.X - _dragStartScreenPoint.X;

            double newLeft = _windowStartLeft + dx;

            double collapsedIslandWidth = 280;
            double windowWidth = Width;

            double islandOffsetInsideWindow = (windowWidth - collapsedIslandWidth) / 2;

            double minLeft = -islandOffsetInsideWindow;
            double maxLeft = SystemParameters.PrimaryScreenWidth - collapsedIslandWidth - islandOffsetInsideWindow;

            newLeft = Math.Max(minLeft, Math.Min(maxLeft, newLeft));

            Left = newLeft;
        }

        private void Timeline_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDraggingTimeline)
                return;

            PreviewTimelineFromMouse(e);
        }

        private void Island_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isDraggingIsland)
                return;

            _isDraggingIsland = false;
            Island.ReleaseMouseCapture();
        }

        private void PreviewTimelineFromMouse(System.Windows.Input.MouseEventArgs e)
        {
            var newPosition = GetTimelinePositionFromMouse(e);

            double ratio = _currentMediaDuration.TotalSeconds <= 0
                ? 0
                : newPosition.TotalSeconds / _currentMediaDuration.TotalSeconds;

            double trackWidth = TimelineCanvas.Width;
            double progressWidth = trackWidth * ratio;

            ProgressBar.Width = progressWidth;
            Canvas.SetLeft(ProgressDot, Math.Max(0, progressWidth - 4));
            CurrentTimeText.Text = FormatTime(newPosition);
        }

        private TimeSpan GetTimelinePositionFromMouse(System.Windows.Input.MouseEventArgs e)
        {
            double x = e.GetPosition(TimelineCanvas).X;
            double trackWidth = TimelineCanvas.Width;

            double ratio = Math.Clamp(x / trackWidth, 0, 1);
            double newSeconds = _currentMediaDuration.TotalSeconds * ratio;

            return TimeSpan.FromSeconds(newSeconds);
        }

        private void Island_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isHovering = true;

            if (ClockView.Visibility == Visibility.Visible)
            {
                SetIslandBlack();

                ClockView.Visibility = Visibility.Collapsed;
                ShowWithFade(ClockExpandedView);

                AnimateIsland(650, 220, 18);
                return;
            }

            _isExpanded = true;

            CollapsedView.Visibility = Visibility.Collapsed;
            ExpandedView.Visibility = Visibility.Visible;

            ShowWithFade(ExpandedView);

            SetIslandTint();
            AnimateIsland(650, 220, 18);
        }

        private void Island_MouseLeave(object sender, MouseEventArgs e)
        {
            _isHovering = false;
            _isExpanded = false;

            // Hide ALL expanded views
            ExpandedView.Visibility = Visibility.Collapsed;
            ClockExpandedView.Visibility = Visibility.Collapsed;

            // Decide what collapsed view to show
            bool hasMedia = TitleText.Text != "Nothing playing";

            if (hasMedia)
            {
                ShowWithFade(CollapsedView);
            }
            else
            {
                ShowWithFade(ClockView);
            }

            SetIslandBlack();
            AnimateIsland(280, 74, 18);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleIsland();
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void ToggleIsland()
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.Hide();
            }
            else
            {
                this.Show();
                this.Activate();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(handle, HOTKEY_ID);
            base.OnClosed(e);
        }

        private void AnimateIsland(double newWidth, double newHeight, double bottomRadius)
        {
            var duration = TimeSpan.FromMilliseconds(350);

            Island.BeginAnimation(WidthProperty, new DoubleAnimation
            {
                To = newWidth,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

            Island.BeginAnimation(HeightProperty, new DoubleAnimation
            {
                To = newHeight,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

            // only animate scale when state changes
            if (_wasExpanded != _isExpanded)
            {
                Island.RenderTransformOrigin = new Point(0.5, 0);

                if (Island.RenderTransform is not ScaleTransform scale)
                {
                    scale = new ScaleTransform(1, 1);
                    Island.RenderTransform = scale;
                }

                var scaleAnim = new DoubleAnimation
                {
                    To = _isExpanded ? 1.02 : 1.0,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

                _wasExpanded = _isExpanded;
            }

            Island.BeginAnimation(Border.CornerRadiusProperty, new CornerRadiusAnimation
            {
                To = new CornerRadius(0, 0, bottomRadius, bottomRadius),
                Duration = duration
            });
        }
    }

    public class CornerRadiusAnimation : AnimationTimeline
    {
        public CornerRadius? From { get; set; }
        public CornerRadius? To { get; set; }

        public override Type TargetPropertyType => typeof(CornerRadius);

        protected override Freezable CreateInstanceCore()
        {
            return new CornerRadiusAnimation();
        }

        public override object GetCurrentValue(
            object defaultOriginValue,
            object defaultDestinationValue,
            AnimationClock animationClock)
        {
            var from = From ?? (CornerRadius)defaultOriginValue;
            var to = To ?? (CornerRadius)defaultDestinationValue;

            if (animationClock.CurrentProgress == null)
                return from;

            double progress = animationClock.CurrentProgress.Value;

            return new CornerRadius(
                from.TopLeft + (to.TopLeft - from.TopLeft) * progress,
                from.TopRight + (to.TopRight - from.TopRight) * progress,
                from.BottomRight + (to.BottomRight - from.BottomRight) * progress,
                from.BottomLeft + (to.BottomLeft - from.BottomLeft) * progress
            );
        }
    }
}