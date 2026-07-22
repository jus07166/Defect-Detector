using OpenCvStudy.Models;
using System;

namespace OpenCvStudy.ViewModels
{
    public sealed class InspectionSettingsViewModel : ObservableObject
    {
        private InspectionMode _mode = InspectionMode.RuleBased;
        private int _minDefectPixels = 3;
        private int _thresholdSensitivity = 5;
        private double _minShapeArea = 100.0;
        private double _anomalyThreshold = 0.5;
        private double _maxExpectedScore = 2.0;

        public event EventHandler Changed;

        public int MinDefectPixels
        {
            get { return _minDefectPixels; }
            set { SetSetting(ref _minDefectPixels, value); }
        }

        public int ThresholdSensitivity
        {
            get { return _thresholdSensitivity; }
            set { SetSetting(ref _thresholdSensitivity, value); }
        }

        public double MinShapeArea
        {
            get { return _minShapeArea; }
            set { SetSetting(ref _minShapeArea, value); }
        }

        public bool IsRuleBasedMode
        {
            get { return _mode == InspectionMode.RuleBased; }
            set
            {
                if (value)
                {
                    ChangeMode(InspectionMode.RuleBased);
                }
            }
        }

        public bool IsDeepLearningMode
        {
            get { return _mode == InspectionMode.DeepLearning; }
            set
            {
                if (value)
                {
                    ChangeMode(InspectionMode.DeepLearning);
                }
            }
        }

        public double AnomalyThreshold
        {
            get { return _anomalyThreshold; }
            set { SetSetting(ref _anomalyThreshold, value); }
        }

        public double MaxExpectedScore
        {
            get { return _maxExpectedScore; }
            set { SetSetting(ref _maxExpectedScore, value); }
        }

        public InspectionOptions CreateSnapshot()
        {
            return new InspectionOptions(
                MinDefectPixels,
                ThresholdSensitivity,
                MinShapeArea,
                _mode,
                AnomalyThreshold,
                MaxExpectedScore);
        }

        private void ChangeMode(InspectionMode mode)
        {
            if (_mode == mode)
            {
                return;
            }

            _mode = mode;
            OnPropertyChanged(nameof(IsRuleBasedMode));
            OnPropertyChanged(nameof(IsDeepLearningMode));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void SetSetting<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (SetProperty(ref field, value, propertyName))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
