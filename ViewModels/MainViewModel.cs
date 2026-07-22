using OpenCvStudy.Commands;
using OpenCvStudy.Models;
using OpenCvStudy.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace OpenCvStudy.ViewModels
{
    public sealed class MainViewModel : ObservableObject, IDisposable
    {
        private static readonly TimeSpan InspectionDebounceDelay = TimeSpan.FromMilliseconds(250);

        private readonly IInspectionService _inspectionService;
        private readonly IDialogService _dialogService;
        private readonly IImageSourceService _imageSourceService;
        private readonly IApplicationLifetime _applicationLifetime;

        private CancellationTokenSource _inspectionCancellation;
        private int _inspectionVersion;
        private bool _isDisposed;
        private string _currentImagePath = string.Empty;
        private BitmapSource _displayImage;
        private BitmapSource _originalImage;
        private BitmapSource _resultImage;
        private DefectItem _selectedDefect;
        private bool _isBusy;

        public MainViewModel(
            IInspectionService inspectionService,
            IDialogService dialogService,
            IImageSourceService imageSourceService,
            IApplicationLifetime applicationLifetime)
        {
            _inspectionService = inspectionService ?? throw new ArgumentNullException(nameof(inspectionService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _imageSourceService = imageSourceService ?? throw new ArgumentNullException(nameof(imageSourceService));
            _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));

            Settings = new InspectionSettingsViewModel();
            Settings.Changed += OnInspectionSettingsChanged;

            UploadImageCommand = new AsyncRelayCommand(UploadImageAsync);
            ClearImageCommand = new RelayCommand(ClearImage);
            ExitCommand = new RelayCommand(_applicationLifetime.Shutdown);
        }

        public ObservableCollection<DefectItem> DefectList { get; } = new ObservableCollection<DefectItem>();

        public InspectionSettingsViewModel Settings { get; }

        public ICommand UploadImageCommand { get; }

        public ICommand ClearImageCommand { get; }

        public ICommand ExitCommand { get; }

        public BitmapSource DisplayImage
        {
            get { return _displayImage; }
            private set { SetProperty(ref _displayImage, value); }
        }

        public DefectItem SelectedDefect
        {
            get { return _selectedDefect; }
            set
            {
                if (!SetProperty(ref _selectedDefect, value))
                {
                    return;
                }

                if (value != null)
                {
                    DisplayImage = value.DefectImage;
                }
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            private set { SetProperty(ref _isBusy, value); }
        }

        public void ShowOriginalImage()
        {
            if (_originalImage != null)
            {
                DisplayImage = _originalImage;
            }
        }

        public void ShowResultImage()
        {
            if (_resultImage != null)
            {
                DisplayImage = _resultImage;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Settings.Changed -= OnInspectionSettingsChanged;
            CancelPendingInspection();
            IsBusy = false;
        }

        private async Task UploadImageAsync()
        {
            string selectedPath = _dialogService.SelectImageFile();

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            try
            {
                BitmapSource originalImage = _imageSourceService.LoadFromFile(selectedPath);

                CancelPendingInspection();
                _currentImagePath = selectedPath;
                _originalImage = originalImage;
                _resultImage = null;
                SelectedDefect = null;
                DefectList.Clear();
                DisplayImage = originalImage;

                await QueueInspectionAsync(TimeSpan.Zero);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(
                    $"이미지를 여는 중 오류가 발생했습니다.\n{ex.Message}",
                    "이미지 열기 오류");
            }
        }

        private void ClearImage()
        {
            CancelPendingInspection();
            _currentImagePath = string.Empty;
            _originalImage = null;
            _resultImage = null;
            SelectedDefect = null;
            DefectList.Clear();
            DisplayImage = null;
            IsBusy = false;
        }

        private void ScheduleInspection()
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(_currentImagePath))
            {
                return;
            }

            _ = QueueInspectionAsync(InspectionDebounceDelay);
        }

        private Task QueueInspectionAsync(TimeSpan delay)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(_currentImagePath))
            {
                return Task.CompletedTask;
            }

            CancellationTokenSource previousCancellation = _inspectionCancellation;
            var currentCancellation = new CancellationTokenSource();
            int requestVersion = ++_inspectionVersion;

            _inspectionCancellation = currentCancellation;
            previousCancellation?.Cancel();

            string imagePath = _currentImagePath;
            InspectionOptions options = Settings.CreateSnapshot();

            return ExecuteInspectionRequestAsync(
                imagePath,
                options,
                delay,
                currentCancellation,
                requestVersion);
        }

        private async Task ExecuteInspectionRequestAsync(
            string imagePath,
            InspectionOptions options,
            TimeSpan delay,
            CancellationTokenSource cancellation,
            int requestVersion)
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellation.Token);
                }

                cancellation.Token.ThrowIfCancellationRequested();

                if (requestVersion == _inspectionVersion)
                {
                    IsBusy = true;
                }

                InspectionResult result = await _inspectionService.InspectAsync(
                    imagePath,
                    options,
                    cancellation.Token);

                cancellation.Token.ThrowIfCancellationRequested();

                if (_isDisposed || requestVersion != _inspectionVersion)
                {
                    return;
                }

                ApplyInspectionResult(result);
            }
            catch (OperationCanceledException)
            {
                // 새 요청이나 Clear 동작으로 취소된 이전 검사 결과는 무시합니다.
            }
            catch (Exception ex)
            {
                if (!_isDisposed &&
                    !cancellation.IsCancellationRequested &&
                    requestVersion == _inspectionVersion)
                {
                    _dialogService.ShowError(
                        $"이미지 처리 중 오류가 발생했습니다.\n{ex.Message}\n\n" +
                        "실행 폴더의 모델 및 OpenCV/CUDA DLL 구성을 확인하세요.",
                        "검사 오류");
                }
            }
            finally
            {
                if (requestVersion == _inspectionVersion)
                {
                    IsBusy = false;
                }

                if (ReferenceEquals(_inspectionCancellation, cancellation))
                {
                    _inspectionCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private void ApplyInspectionResult(InspectionResult result)
        {
            _resultImage = _imageSourceService.LoadFromBytes(result.MainImage);
            DisplayImage = _resultImage;

            SelectedDefect = null;
            DefectList.Clear();

            for (int index = 0; index < result.DefectImages.Count; index++)
            {
                byte[] imageData = result.DefectImages[index];

                if (imageData == null || imageData.Length == 0)
                {
                    continue;
                }

                DefectList.Add(new DefectItem
                {
                    DefectImage = _imageSourceService.LoadFromBytes(imageData),
                    DefectInfo = $"발견된 이물질 #{index + 1}"
                });
            }
        }

        private void CancelPendingInspection()
        {
            _inspectionVersion++;
            _inspectionCancellation?.Cancel();
        }

        private void OnInspectionSettingsChanged(object sender, EventArgs e)
        {
            ScheduleInspection();
        }
    }
}
