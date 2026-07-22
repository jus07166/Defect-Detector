using OpenCvBackend;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCvBackendImageTests
{
    internal sealed class CategoryStatistics
    {
        public CategoryStatistics(string name, bool expectedDefect)
        {
            Name = name;
            ExpectedDefect = expectedDefect;
        }

        public string Name { get; }
        public bool ExpectedDefect { get; }
        public int Total { get; set; }
        public int Correct { get; set; }
    }

    internal static class Program
    {
        private const int MinDefectPixels = 3;
        private const int ThresholdSensitivity = 5;
        private const double MinShapeArea = 100.0;
        private const double AnomalyThreshold = 0.4;
        private const double MaxExpectedScore = 2.0;

        private static int Main(string[] args)
        {
            if (args.Length != 5)
            {
                Console.Error.WriteLine(
                    "Usage: OpenCvBackend.ImageTests.exe " +
                    "<pill-dataset-root> <max-images-per-category> " +
                    "<minimum-recall> <minimum-specificity> <report-csv>");
                return 2;
            }

            try
            {
                string datasetRoot = Path.GetFullPath(args[0]);
                int maxImagesPerCategory = ParseNonNegativeInt(args[1], "max-images-per-category");
                double minimumRecall = ParseRatio(args[2], "minimum-recall");
                double minimumSpecificity = ParseRatio(args[3], "minimum-specificity");
                string reportPath = Path.GetFullPath(args[4]);

                return EvaluateDataset(
                    datasetRoot,
                    maxImagesPerCategory,
                    minimumRecall,
                    minimumSpecificity,
                    reportPath);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"[ERROR] {exception.Message}");
                return 2;
            }
        }

        private static int EvaluateDataset(
            string datasetRoot,
            int maxImagesPerCategory,
            double minimumRecall,
            double minimumSpecificity,
            string reportPath)
        {
            string testRoot = Path.Combine(datasetRoot, "test");
            string groundTruthRoot = Path.Combine(datasetRoot, "ground_truth");

            Require(Directory.Exists(testRoot), $"Dataset test directory was not found: {testRoot}");
            Require(
                Directory.Exists(groundTruthRoot),
                $"Dataset ground-truth directory was not found: {groundTruthRoot}");

            string reportDirectory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            var categories = Directory
                .GetDirectories(testRoot)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Require(categories.Length > 1, "The pill test dataset does not contain enough categories.");
            Require(
                categories.Any(path => string.Equals(
                    Path.GetFileName(path),
                    "good",
                    StringComparison.OrdinalIgnoreCase)),
                "The pill test dataset does not contain the 'good' category.");

            var categoryStatistics = new List<CategoryStatistics>();

            int truePositives = 0;
            int trueNegatives = 0;
            int falsePositives = 0;
            int falseNegatives = 0;
            int processingFailures = 0;

            using (var backend = new BackendProcessor())
            using (var report = new StreamWriter(
                reportPath,
                false,
                new UTF8Encoding(false)))
            {
                report.WriteLine(
                    "category,image,expected,predicted,defect_count,correct,error");

                foreach (string categoryPath in categories)
                {
                    string categoryName = Path.GetFileName(categoryPath);
                    bool expectedDefect = !string.Equals(
                        categoryName,
                        "good",
                        StringComparison.OrdinalIgnoreCase);
                    var statistics = new CategoryStatistics(categoryName, expectedDefect);
                    categoryStatistics.Add(statistics);

                    IEnumerable<string> imagePaths = Directory
                        .GetFiles(categoryPath, "*.png")
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

                    if (maxImagesPerCategory > 0)
                    {
                        imagePaths = imagePaths.Take(maxImagesPerCategory);
                    }

                    foreach (string imagePath in imagePaths)
                    {
                        statistics.Total++;
                        string imageName = Path.GetFileName(imagePath);

                        try
                        {
                            ValidateGroundTruth(
                                groundTruthRoot,
                                categoryName,
                                imagePath,
                                expectedDefect);

                            List<byte[]> defects;
                            byte[] result = backend.GetContourImage(
                                imagePath,
                                out defects,
                                MinDefectPixels,
                                ThresholdSensitivity,
                                MinShapeArea,
                                true,
                                AnomalyThreshold,
                                MaxExpectedScore);

                            ValidateBmp(result, "result image");
                            Require(defects != null, "Backend returned a null defect list.");

                            foreach (byte[] defectImage in defects)
                            {
                                ValidateBmp(defectImage, "defect image");
                            }

                            bool predictedDefect = defects.Count > 0;
                            bool correct = predictedDefect == expectedDefect;

                            if (correct)
                            {
                                statistics.Correct++;
                            }

                            if (expectedDefect && predictedDefect)
                            {
                                truePositives++;
                            }
                            else if (!expectedDefect && !predictedDefect)
                            {
                                trueNegatives++;
                            }
                            else if (!expectedDefect)
                            {
                                falsePositives++;
                            }
                            else
                            {
                                falseNegatives++;
                            }

                            string status = correct ? "PASS" : "MISS";
                            Console.WriteLine(
                                $"[{status}] {categoryName}/{imageName} " +
                                $"expected={Label(expectedDefect)} " +
                                $"predicted={Label(predictedDefect)} " +
                                $"regions={defects.Count}");

                            WriteReportRow(
                                report,
                                categoryName,
                                imageName,
                                expectedDefect,
                                predictedDefect,
                                defects.Count,
                                correct,
                                string.Empty);
                        }
                        catch (Exception exception)
                        {
                            processingFailures++;
                            Console.Error.WriteLine(
                                $"[ERROR] {categoryName}/{imageName}: {exception.Message}");
                            WriteReportRow(
                                report,
                                categoryName,
                                imageName,
                                expectedDefect,
                                false,
                                0,
                                false,
                                exception.Message);
                        }
                    }
                }
            }

            int evaluated = truePositives + trueNegatives + falsePositives + falseNegatives;
            Require(evaluated > 0, "No pill test images were evaluated.");

            double accuracy = Ratio(truePositives + trueNegatives, evaluated);
            double precision = Ratio(truePositives, truePositives + falsePositives);
            double recall = Ratio(truePositives, truePositives + falseNegatives);
            double specificity = Ratio(trueNegatives, trueNegatives + falsePositives);
            double f1 = precision + recall == 0.0
                ? 0.0
                : 2.0 * precision * recall / (precision + recall);

            Console.WriteLine();
            Console.WriteLine("Category summary");
            foreach (CategoryStatistics statistics in categoryStatistics)
            {
                Console.WriteLine(
                    $"  {statistics.Name,-16} " +
                    $"expected={Label(statistics.ExpectedDefect),-6} " +
                    $"correct={statistics.Correct}/{statistics.Total}");
            }

            Console.WriteLine();
            Console.WriteLine(
                $"TP={truePositives} TN={trueNegatives} " +
                $"FP={falsePositives} FN={falseNegatives} " +
                $"ERROR={processingFailures}");
            Console.WriteLine(
                $"Accuracy={accuracy:P2} Precision={precision:P2} " +
                $"Recall={recall:P2} Specificity={specificity:P2} F1={f1:P2}");
            Console.WriteLine($"CSV report: {reportPath}");

            bool passed = processingFailures == 0
                && recall >= minimumRecall
                && specificity >= minimumSpecificity;

            if (!passed)
            {
                Console.Error.WriteLine(
                    $"[FAIL] Required recall >= {minimumRecall:P2}, " +
                    $"specificity >= {minimumSpecificity:P2}, and no processing errors.");
                return 1;
            }

            Console.WriteLine(
                $"[PASS] Recall >= {minimumRecall:P2}, " +
                $"specificity >= {minimumSpecificity:P2}, no processing errors.");
            return 0;
        }

        private static void ValidateGroundTruth(
            string groundTruthRoot,
            string categoryName,
            string imagePath,
            bool expectedDefect)
        {
            if (!expectedDefect)
            {
                return;
            }

            string maskName = Path.GetFileNameWithoutExtension(imagePath) + "_mask.png";
            string maskPath = Path.Combine(groundTruthRoot, categoryName, maskName);
            Require(File.Exists(maskPath), $"Ground-truth mask was not found: {maskPath}");
        }

        private static void ValidateBmp(byte[] image, string description)
        {
            Require(image != null && image.Length > 2, $"Backend returned no {description}.");
            Require(
                image[0] == (byte)'B' && image[1] == (byte)'M',
                $"Backend {description} is not BMP data.");
        }

        private static void WriteReportRow(
            TextWriter report,
            string category,
            string image,
            bool expectedDefect,
            bool predictedDefect,
            int defectCount,
            bool correct,
            string error)
        {
            report.WriteLine(string.Join(",", new[]
            {
                Csv(category),
                Csv(image),
                Csv(Label(expectedDefect)),
                Csv(Label(predictedDefect)),
                defectCount.ToString(CultureInfo.InvariantCulture),
                correct ? "true" : "false",
                Csv(error)
            }));
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private static string Label(bool defect)
        {
            return defect ? "defect" : "good";
        }

        private static double Ratio(int numerator, int denominator)
        {
            return denominator == 0 ? 0.0 : (double)numerator / denominator;
        }

        private static int ParseNonNegativeInt(string value, string name)
        {
            int parsed;
            Require(
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                && parsed >= 0,
                $"{name} must be a non-negative integer.");
            return parsed;
        }

        private static double ParseRatio(string value, string name)
        {
            double parsed;
            Require(
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                && parsed >= 0.0
                && parsed <= 1.0,
                $"{name} must be between 0 and 1.");
            return parsed;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
