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

namespace WindowsNothIsland
{
    public partial class MainWindow : Window
    {
        private readonly MediaService _mediaService = new();
        private readonly DispatcherTimer _mediaTimer = new();

        private bool _isExpanded = false;
        private bool _isHovering = false;
        private bool _isDraggingTimeline = false;

        private TimeSpan _currentMediaDuration = TimeSpan.Zero;
        private Color _currentAlbumTint = Color.FromRgb(15, 15, 16);

        public MainWindow()
        {
            InitializeComponent();

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
                    ClockExpandedView.Visibility = Visibility.Visible;
                    AnimateIsland(650, 220, 18);
                }
                else
                {
                    ClockView.Visibility = Visibility.Visible;
                    ClockExpandedView.Visibility = Visibility.Collapsed;
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
            ExpandedArtist.Text = artist;

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
                    Canvas.SetLeft(ProgressDot, progressWidth - 4);

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

        private async void Island_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
                await _mediaService.NextSessionAsync();
            else
                await _mediaService.PreviousSessionAsync();

            await UpdateMediaInfo();
        }

        private async void Timeline_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_currentMediaDuration.TotalSeconds <= 0)
                return;

            _isDraggingTimeline = true;
            TimelineCanvas.CaptureMouse();

            await SeekTimelineFromMouse(e);
        }

        private async void Timeline_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDraggingTimeline)
                return;

            await SeekTimelineFromMouse(e);
        }

        private async void Timeline_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isDraggingTimeline)
                return;

            _isDraggingTimeline = false;
            TimelineCanvas.ReleaseMouseCapture();

            await SeekTimelineFromMouse(e);
        }

        private async Task SeekTimelineFromMouse(System.Windows.Input.MouseEventArgs e)
        {
            double x = e.GetPosition(TimelineCanvas).X;
            double trackWidth = TimelineCanvas.Width;

            double ratio = Math.Clamp(x / trackWidth, 0, 1);
            double newSeconds = _currentMediaDuration.TotalSeconds * ratio;

            var newPosition = TimeSpan.FromSeconds(newSeconds);

            ProgressBar.Width = trackWidth * ratio;
            Canvas.SetLeft(ProgressDot, ProgressBar.Width - 4);
            CurrentTimeText.Text = FormatTime(newPosition);

            await _mediaService.SeekAsync(newPosition);
        }

        private void Island_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isHovering = true;

            if (ClockView.Visibility == Visibility.Visible)
            {
                SetIslandBlack();

                ClockView.Visibility = Visibility.Collapsed;
                ClockExpandedView.Visibility = Visibility.Visible;

                AnimateIsland(650, 220, 18);
                return;
            }

            _isExpanded = true;

            CollapsedView.Visibility = Visibility.Collapsed;
            ExpandedView.Visibility = Visibility.Visible;

            SetIslandTint();
            AnimateIsland(650, 220, 18);
        }

        private void Island_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isHovering = false;

            if (ClockExpandedView.Visibility == Visibility.Visible)
            {
                SetIslandBlack();

                ClockView.Visibility = Visibility.Visible;
                ClockExpandedView.Visibility = Visibility.Collapsed;

                AnimateIsland(280, 74, 18);
                return;
            }

            _isExpanded = false;

            CollapsedView.Visibility = Visibility.Visible;
            ExpandedView.Visibility = Visibility.Collapsed;

            SetIslandBlack();
            AnimateIsland(280, 74, 18);
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