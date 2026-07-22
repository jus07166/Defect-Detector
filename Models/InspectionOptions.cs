namespace OpenCvStudy.Models
{
    public sealed class InspectionOptions
    {
        public InspectionOptions(
            int minDefectPixels,
            int thresholdSensitivity,
            double minShapeArea,
            InspectionMode mode,
            double anomalyThreshold,
            double maxExpectedScore)
        {
            MinDefectPixels = minDefectPixels;
            ThresholdSensitivity = thresholdSensitivity;
            MinShapeArea = minShapeArea;
            Mode = mode;
            AnomalyThreshold = anomalyThreshold;
            MaxExpectedScore = maxExpectedScore;
        }

        public int MinDefectPixels { get; }

        public int ThresholdSensitivity { get; }

        public double MinShapeArea { get; }

        public InspectionMode Mode { get; }

        public double AnomalyThreshold { get; }

        public double MaxExpectedScore { get; }
    }
}
