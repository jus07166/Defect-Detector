using Microsoft.Win32;
using OpenCvBackend; //C++ CLI 백엔드 프로젝트 네임스페이스
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace OpenCvStudy.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        //C++ 백엔드 처리기 인스턴스 생성
        private readonly BackendProcessor _backend = new BackendProcessor();
        // 현재 로드된 이미지 경로 저장
        private string _currentImagePath = string.Empty;

        // >>통합된 메인 화면 이미지 프로퍼티 
        private BitmapSource _displayImage;
        public BitmapSource DisplayImage
        {
            get => _displayImage;
            set
            {
                _displayImage = value;
                OnPropertyChanged(nameof(DisplayImage));
            }
        }

        private BitmapSource _originalImage; // 검사 전 원본 이미지 저장용
        private BitmapSource _resultImage;   // C++에서 넘어온 결과 이미지 저장용
        // <<

        private DefectItem _selectedDefect;
        public DefectItem SelectedDefect
        {
            get { return _selectedDefect; }
            set
            {
                _selectedDefect = value;
                OnPropertyChanged(nameof(SelectedDefect));

                // 리스트에서 불량 항목을 클릭했을 때 해당 이미지를 화면에 띄움
                if (_selectedDefect != null)
                {
                    this.DisplayImage = (BitmapSource)_selectedDefect.DefectImage;
                }
            }
        }

        //>>파라미터 바인딩용 속성
        private int _minDefectPixels = 3;
        public int MinDefectPixels
        {
            get { return _minDefectPixels; }
            set
            {
                if (_minDefectPixels != value)
                {
                    _minDefectPixels = value;
                    OnPropertyChanged();
                    RunInspection(); // 값이 변경될 때마다 즉시 검사 실행
                }
            }
        }

        private int _thresholdSensitivity = 5;
        public int ThresholdSensitivity
        {
            get { return _thresholdSensitivity; }
            set
            {
                _thresholdSensitivity = value;
                OnPropertyChanged();
                RunInspection();
            }
        }

        private double _minShapeArea = 100.0;
        public double MinShapeArea
        {
            get { return _minShapeArea; }
            set
            {
                _minShapeArea = value;
                OnPropertyChanged();
                RunInspection();
            }
        }

        //>> 모드 전환용 속성
        private bool _isRuleBasedMode = true;
        public bool IsRuleBasedMode
        {
            get { return _isRuleBasedMode; }
            set
            {
                _isRuleBasedMode = value;
                OnPropertyChanged();
                if (value) RunInspection();
            }
        }

        private bool _isDeepLearningMode = false;
        public bool IsDeepLearningMode
        {
            get { return _isDeepLearningMode; }
            set
            {
                _isDeepLearningMode = value;
                OnPropertyChanged();
                if (value) RunInspection();
            }
        }

        //>>알약 검사 파라미터
        private double _anomalyThreshold = 0.5;
        public double AnomalyThreshold
        {
            get => _anomalyThreshold;
            set
            {
                _anomalyThreshold = value;
                OnPropertyChanged();
                RunInspection();
            }
        }

        private double _maxExpectedScore = 2.0;
        public double MaxExpectedScore
        {
            get => _maxExpectedScore;
            set
            {
                _maxExpectedScore = value;
                OnPropertyChanged();
                RunInspection();
            }
        }

        public ObservableCollection<DefectItem> DefectList { get; set; } = new ObservableCollection<DefectItem>();

        public ICommand UploadImageCommand { get; private set; }
        public ICommand ClearImageCommand { get; private set; }
        public ICommand ExitCommand { get; private set; }

        public MainViewModel()
        {
            UploadImageCommand = new RelayCommand(UploadImage);
            ClearImageCommand = new RelayCommand(ClearImage);
            ExitCommand = new RelayCommand(ExitApplication);
        }

        private void ExitApplication()
        {
            Application.Current.Shutdown();
        }

        private void UploadImage()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.bmp) | *.jpg; *.jpeg; *.png; *.bmp";

            if (openFileDialog.ShowDialog() == true)
            {
                _currentImagePath = openFileDialog.FileName;

                //파일 경로에서 원본 이미지를 로드하여 _originalImage 변수에 미리 저장해 둡니다.
                _originalImage = LoadBitmapFromPath(_currentImagePath);

                RunInspection();
            }
        }

        private void RunInspection()
        {
            if (string.IsNullOrEmpty(_currentImagePath)) return;

            List<byte[]> defectBytesList;
            try
            {
                byte[] resultBytes = _backend.GetContourImage(
                    _currentImagePath,
                    out defectBytesList,
                    MinDefectPixels,
                    ThresholdSensitivity,
                    MinShapeArea,
                    IsDeepLearningMode,
                    AnomalyThreshold,
                    MaxExpectedScore
                );

                if (resultBytes != null)
                {
                    //검사가 끝나면 결과 이미지를 _resultImage에 저장하고, 화면(DisplayImage)에 띄웁니다.
                    _resultImage = ConvertBytesToBitmapImage(resultBytes);
                    DisplayImage = _resultImage;
                }
                else
                {
                    MessageBox.Show("이미지를 로드하거나 처리하는 데 실패했습니다.", "오류");
                }

                // 리스트 초기화 및 갱신
                this.DefectList.Clear();
                if (defectBytesList != null)
                {
                    for (int i = 0; i < defectBytesList.Count; i++)
                    {
                        AddDefectToList(defectBytesList[i], $"발견된 이물질 #{i + 1}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지 처리 중 C++ 백엔드 오류가 발생했습니다.\n{ex.Message}\n\n※ 실행 폴더에 opencv_world dll 파일이 있는지 확인하세요.", "에러");
            }
        }

        private void ClearImage()
        {
            //클리어할 때 관련 변수들을 전부 싹 비워줍니다.
            DisplayImage = null;
            _originalImage = null;
            _resultImage = null;
            DefectList.Clear();
        }

        //XAML 마우스 클릭 이벤트에서 호출될 토글 메서드
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 파일 경로를 이용해 원본 이미지를 로드하는 헬퍼 함수입니다.
        /// </summary>
        private BitmapImage LoadBitmapFromPath(string path)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // 파일 락(Lock) 해제
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze(); // 크로스 스레드 에러 방지
            return bitmap;
        }

        /// <summary>
        /// 백엔드에서 넘겨받은 순수 바이트 배열(BMP)을 WPF UI용 BitmapImage로 변환합니다.
        /// </summary>
        private BitmapImage ConvertBytesToBitmapImage(byte[] bytes)
        {
            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream memoryStream = new MemoryStream(bytes))
            {
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.EndInit();
            }
            bitmapImage.Freeze();
            return bitmapImage;
        }

        public void AddDefectToList(byte[] imageData, string infoText)
        {
            if (imageData == null || imageData.Length == 0) return;

            BitmapImage bitmap = ConvertBytesToBitmapImage(imageData);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                DefectList.Add(new DefectItem
                {
                    DefectImage = bitmap,
                    DefectInfo = infoText
                });
            });
        }
    }

    public class DefectItem
    {
        public BitmapSource DefectImage { get; set; }
        public string DefectInfo { get; set; }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute();
    }
}