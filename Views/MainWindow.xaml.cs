using OpenCvStudy.ViewModel;
using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OpenCvStudy
{
    public partial class MainWindow : Window
    {
        // 현재 확대 비율 (기본 1.0)
        private double currentZoom = 1.0;

        // 휠 1틱당 변경할 배율 (20%)
        private readonly double zoomStep = 0.2;

        // 최소/최대 확대 제한 (20% ~ 1000%)
        private readonly double minZoom = 0.2;
        private readonly double maxZoom = 10.0;

        // 드래그 관련 변수
        private bool isDragging = false;       // 드래그 중인지 여부
        private Point startMousePoint;         // 클릭한 마우스의 시작 위치
        private Point startTranslatePoint;     // 이미지 이동(Translate) 시작 위치

        //ROI 상태 변수
        private bool _isDrawingRoi = false;
        private Point _roiStartPoint;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = new ViewModel.MainViewModel();
        }

        // 마우스 휠 이벤트 핸들러
        private void Image_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 마우스 위치를 기준으로 확대/축소를 적용하기 위해 현재 마우스 위치를 가져옴
            Point mousePos = e.GetPosition(DisplayImage);

            // 이전 확대 비율을 저장
            double oldZoom = currentZoom;

            // e.Delta가 양수면 휠을 위로(확대), 음수면 아래로(축소)
            if (e.Delta > 0)
            {
                currentZoom += zoomStep;
            }
            else
            {
                currentZoom -= zoomStep;
            }

            // 배율이 최소/최대 범위를 벗어나지 않도록 강제 제한
            currentZoom = Math.Max(minZoom, Math.Min(maxZoom, currentZoom));

            // XAML에서 정의한 ScaleTransform에 적용
            imageScale.ScaleX = currentZoom;
            imageScale.ScaleY = currentZoom;

            // 마우스 위치를 기준으로 이미지가 확대/축소되도록 Translate 값을 조정
            // 확대된 비율(currentZoom - oldZoom)만큼 마우스 위치(mousePos)를 곱해 반대로 빼기
            imageTranslate.X -= mousePos.X * (currentZoom - oldZoom);
            imageTranslate.Y -= mousePos.Y * (currentZoom - oldZoom);

            // 이벤트 처리를 완료했음을 알림 (부모 컨트롤로 이벤트가 전파되는 것 방지)
            e.Handled = true;
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            if (element == null) return;

            // 마우스 커서가 밖으로 나가도 이벤트를 놓치지 않도록 캡처
            element.CaptureMouse();
            isDragging = true;

            // 현재 마우스 위치와, 현재 이미지의 이동(X, Y) 값을 기억
            startMousePoint = e.GetPosition(this);
            startTranslatePoint = new Point(imageTranslate.X, imageTranslate.Y);
        }
        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            if (element == null) return;

            // 마우스 캡처 해제 및 드래그 상태 종료
            element.ReleaseMouseCapture();
            isDragging = false;
        }
        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                // 현재 마우스 위치를 구함
                Point currentMousePoint = e.GetPosition(this);

                // 마우스가 시작점에서 얼마나 이동했는지 계산 (Delta)
                double deltaX = currentMousePoint.X - startMousePoint.X;
                double deltaY = currentMousePoint.Y - startMousePoint.Y;

                // 기존 Translate 값에 Delta를 더하여 실시간으로 위치 업데이트
                imageTranslate.X = startTranslatePoint.X + deltaX;
                imageTranslate.Y = startTranslatePoint.Y + deltaY;
            }
        }
        //Pos 초기화
        private void ResetImageTransform()
        {
            currentZoom = 1.0;
            imageScale.ScaleX = 1.0;
            imageScale.ScaleY = 1.0;

            imageTranslate.X = 0;
            imageTranslate.Y = 0;

            isDragging = false;
        }
        private void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            ResetImageTransform();
        }
        private void MainImage_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 마우스를 누르고 있을 때 원본 이미지 출력
            if (DataContext is MainViewModel vm)
            {
                vm.ShowOriginalImage();
            }
        }

        private void MainImage_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 마우스를 떼면 다시 결과 이미지 출력
            if (DataContext is MainViewModel vm)
            {
                vm.ShowResultImage();
            }
        }
    }
}