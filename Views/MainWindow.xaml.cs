using OpenCvStudy.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace OpenCvStudy
{
    public partial class MainWindow : Window
    {
        private const double ZoomStep = 0.2;
        private const double MinZoom = 0.2;
        private const double MaxZoom = 10.0;

        private double _currentZoom = 1.0;
        private bool _isDragging;
        private Point _startMousePoint;
        private Point _startTranslatePoint;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Image_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point mousePosition = e.GetPosition(DisplayImage);
            double previousZoom = _currentZoom;

            _currentZoom += e.Delta > 0 ? ZoomStep : -ZoomStep;
            _currentZoom = Math.Max(MinZoom, Math.Min(MaxZoom, _currentZoom));

            imageScale.ScaleX = _currentZoom;
            imageScale.ScaleY = _currentZoom;
            imageTranslate.X -= mousePosition.X * (_currentZoom - previousZoom);
            imageTranslate.Y -= mousePosition.Y * (_currentZoom - previousZoom);

            e.Handled = true;
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;

            if (element == null)
            {
                return;
            }

            element.CaptureMouse();
            _isDragging = true;
            _startMousePoint = e.GetPosition(this);
            _startTranslatePoint = new Point(imageTranslate.X, imageTranslate.Y);
        }

        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;

            if (element == null)
            {
                return;
            }

            element.ReleaseMouseCapture();
            _isDragging = false;
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            Point currentMousePoint = e.GetPosition(this);
            double deltaX = currentMousePoint.X - _startMousePoint.X;
            double deltaY = currentMousePoint.Y - _startMousePoint.Y;

            imageTranslate.X = _startTranslatePoint.X + deltaX;
            imageTranslate.Y = _startTranslatePoint.Y + deltaY;
        }

        private void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            ResetImageTransform();
        }

        private void MainImage_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as MainViewModel;
            viewModel?.ShowOriginalImage();
        }

        private void MainImage_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as MainViewModel;
            viewModel?.ShowResultImage();
        }

        private void ResetImageTransform()
        {
            _currentZoom = 1.0;
            imageScale.ScaleX = 1.0;
            imageScale.ScaleY = 1.0;
            imageTranslate.X = 0;
            imageTranslate.Y = 0;
            _isDragging = false;
        }
    }
}
