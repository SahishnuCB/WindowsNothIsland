using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        public MainWindow()
        {
            InitializeComponent();

            Loaded += (_, _) =>
            {
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                Top = 0;
            };

            _mediaTimer.Interval = TimeSpan.FromSeconds(1);
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

            TitleText.Text = title;
            ArtistText.Text = artist;

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

                AlbumArt.Source = image;
                ExpandedAlbumArt.Source = image;
            }
        }

        private void Island_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            CollapsedView.Visibility = Visibility.Collapsed;
            ExpandedView.Visibility = Visibility.Visible;

            AnimateIsland(650, 220, 18);
        }

        private void Island_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            CollapsedView.Visibility = Visibility.Visible;
            ExpandedView.Visibility = Visibility.Collapsed;

            AnimateIsland(260, 58, 18);
        }

        private void AnimateIsland(double newWidth, double newHeight, double bottomRadius)
        {
            var duration = TimeSpan.FromMilliseconds(350);

            var widthAnim = new DoubleAnimation
            {
                To = newWidth,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var heightAnim = new DoubleAnimation
            {
                To = newHeight,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var radiusAnim = new CornerRadiusAnimation
            {
                To = new CornerRadius(0, 0, bottomRadius, bottomRadius),
                Duration = duration
            };

            Island.BeginAnimation(WidthProperty, widthAnim);
            Island.BeginAnimation(HeightProperty, heightAnim);
            Island.BeginAnimation(Border.CornerRadiusProperty, radiusAnim);
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

        public override object GetCurrentValue(object defaultOriginValue,
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