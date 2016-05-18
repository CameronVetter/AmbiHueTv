using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Nito.AsyncEx;

namespace zecil.AmbiHueTv
{

    // ReSharper disable once RedundantExtendsListEntry
    public sealed partial class MainPage : Page
    {
        private Hue _hue;
        private MediaCapture _mediaCapture;
        private bool _isSyncing;

        public INotifyTaskCompletion Initialization { get; private set; }

        #region UI Helper Functions

        private void UpdateStatus(string newStatus)
        {

            if (!string.IsNullOrEmpty(status.Text)) status.Text += "\n";
            status.Text += newStatus;

            var grid = (Grid)VisualTreeHelper.GetChild(status, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer)) continue;
                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);
                break;
            }
        }

        private void SetStartupButtonVisibility()
        {

            video_init.IsEnabled = true;
            register.IsEnabled = false;
            cleanup.IsEnabled = false;
        }

        private void SetDisabledButtonVisibility()
        {

            video_init.IsEnabled = false;
            register.IsEnabled = false;
            cleanup.IsEnabled = false;
        }

        private void SetRegisterButtonVisibility()
        {

            video_init.IsEnabled = false;
            register.IsEnabled = true;
            cleanup.IsEnabled = false;
        }

        private void SetRunningButtonVisibility()
        {

            video_init.IsEnabled = false;
            register.IsEnabled = false;
            cleanup.IsEnabled = true;
        }


        #endregion


        public MainPage()
        {
            InitializeComponent();

            SetStartupButtonVisibility();


            algorithm.SelectedIndex = (int)Settings.TheAnalysisAlgorithm;
            Bias.SelectedIndex = (int)Settings.TheBiasAlgorithm;

            _isSyncing = false;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Initialization = NotifyTaskCompletion.Create(Start());
        }

        private void Cleanup()
        {
            if (_mediaCapture != null)
            {
                // Cleanup MediaCapture object
                if (_isSyncing)
                {
                    _isSyncing = false;
                }
            }
        }


        private async void init_Click(object sender, RoutedEventArgs e)
        {
            await Start();
        }

        private async void register_Click(object sender, RoutedEventArgs e)
        {
            await StartRegisterAndRest();
        }

        private void algorithm_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.TheAnalysisAlgorithm = (AnalysisAlgorithm) algorithm.SelectedIndex;
        }

        private void Bias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.TheBiasAlgorithm = (BiasAlgorithm)Bias.SelectedIndex;
        }

        private async Task Start()
        {
            _hue = new Hue();

            if (string.IsNullOrEmpty(Settings.DefaultBridgeIp))
            {
                UpdateStatus("Locating Bridges!");

                Settings.DefaultBridgeIp = await _hue.FindFirstBridge();

                if (!string.IsNullOrEmpty(Settings.DefaultBridgeIp))
                {
                    UpdateStatus($"Bridges Found! - {Settings.DefaultBridgeIp}");
                }
            }
            else
            {
                UpdateStatus($"Connecting to saved bridge {Settings.DefaultBridgeIp}");
            }

            if (string.IsNullOrEmpty(Settings.TheAppKey))
            {
                UpdateStatus("Please press the link button on your bridge then click register!");
                SetRegisterButtonVisibility();
            }
            else
            {
                await StartRegisterAndRest();
            }
        }

        private async Task StartRegisterAndRest()
        {
            var startVideoTask = InitVideo();

            await _hue.RegisterIfNeeded();
            UpdateStatus($"Connected to registered Bridge {Settings.TheAppKey}");
            await _hue.Initialize();

            _hue.FilterLightsByNameContaining("strip");

            await _hue.TurnOnFilteredLights();

            await startVideoTask;
            SetRunningButtonVisibility();
            await WatchFrames();
        }

        private async Task WatchFrames()
        {
            long count = 0;
            long changes = 0;
            VideoFrame currentFrame = new VideoFrame(BitmapPixelFormat.Rgba8, 320, 180);
            FrameAnalysis analysis = new FrameAnalysis();

            while (_isSyncing)
            {
                try
                {
                    await _mediaCapture.GetPreviewFrameAsync(currentFrame);
                    analysis.AnalyzeFrame(ref currentFrame, Settings.TheAnalysisAlgorithm, Settings.TheBiasAlgorithm);
                    count++;

                    if (await _hue.ChangeFilteredLightsColor(analysis.Red, analysis.Green, analysis.Blue))
                    {
                        frequent.Fill = new SolidColorBrush(Color.FromArgb(255, analysis.Red, analysis.Green, analysis.Blue));
                        changes++;
                    }
                    fps.Text = $"Frames: {count} Changes: {changes} Values:{analysis.Red},{analysis.Green},{analysis.Blue},{analysis.Alpha}";
                }
                catch (Exception ex)
                {
                    UpdateStatus(ex.Message);
                }
            }


            var stopPreviewTask = _mediaCapture.StopPreviewAsync();
            var turnOffLightsTask = _hue.TurnOffFilteredLights();

            await stopPreviewTask;
            await turnOffLightsTask;

            _mediaCapture.Dispose();
            _mediaCapture = null;
            SetStartupButtonVisibility();
        }

        private async Task InitVideo()
        {
            SetDisabledButtonVisibility();

            try
            {
                if (_mediaCapture != null)
                {
                    // Cleanup MediaCapture object
                    if (_isSyncing)
                    {
                        await _mediaCapture.StopPreviewAsync();
                        _isSyncing = false;
                    }

                    _mediaCapture.Dispose();
                    _mediaCapture = null;
                }

                UpdateStatus("Initializing camera to capture audio and video...");
                // Use default initialization
                _mediaCapture = new MediaCapture();

                var settings = new MediaCaptureInitializationSettings()
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                };

                await _mediaCapture.InitializeAsync(settings);


                // Set callbacks for failure and recording limit exceeded
                UpdateStatus("Device successfully initialized for video recording!");
                _mediaCapture.Failed += mediaCapture_Failed;

                // Start Preview                
                previewElement.Source = _mediaCapture;
                await _mediaCapture.StartPreviewAsync();
                _isSyncing = true;
                UpdateStatus("Camera preview succeeded");
            }
            catch (Exception ex)
            {
                UpdateStatus("Unable to initialize camera for audio/video mode: " + ex.Message);
            }
        }

        private void cleanup_Click(object sender, RoutedEventArgs e)
        {
            SetDisabledButtonVisibility();
            Cleanup();
        }

        /// <summary>
        /// Callback function for any failures in MediaCapture operations
        /// </summary>
        /// <param name="currentCaptureObject"></param>
        /// <param name="currentFailure"></param>
        private async void mediaCapture_Failed(MediaCapture currentCaptureObject, MediaCaptureFailedEventArgs currentFailure)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    UpdateStatus("MediaCaptureFailed: " + currentFailure.Message);

                }
                catch (Exception ex)
                {
                    UpdateStatus($"mediaCapture_Failed ex: {ex.Message}");
                }
                finally
                {
                    SetDisabledButtonVisibility();
                    UpdateStatus("Check if camera is diconnected. Try re-launching the app");
                }
            });
        }

    }
}
