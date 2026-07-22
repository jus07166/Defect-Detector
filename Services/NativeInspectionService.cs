using OpenCvBackend;
using OpenCvStudy.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCvStudy.Services
{
    public sealed class NativeInspectionService : IInspectionService
    {
        private readonly SemaphoreSlim _inspectionGate = new SemaphoreSlim(1, 1);

        public async Task<InspectionResult> InspectAsync(
            string imagePath,
            InspectionOptions options,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                throw new ArgumentException("이미지 경로가 필요합니다.", nameof(imagePath));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            await _inspectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                InspectionResult result = await Task.Run(
                    () => InspectCore(imagePath, options)).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            finally
            {
                _inspectionGate.Release();
            }
        }

        private static InspectionResult InspectCore(string imagePath, InspectionOptions options)
        {
            using (var backend = new BackendProcessor())
            {
                List<byte[]> defectImages;
                byte[] mainImage = backend.GetContourImage(
                    imagePath,
                    out defectImages,
                    options.MinDefectPixels,
                    options.ThresholdSensitivity,
                    options.MinShapeArea,
                    options.Mode == InspectionMode.DeepLearning,
                    options.AnomalyThreshold,
                    options.MaxExpectedScore);

                if (mainImage == null || mainImage.Length == 0)
                {
                    throw new InvalidOperationException("이미지를 로드하거나 처리하지 못했습니다.");
                }

                return new InspectionResult(mainImage, defectImages);
            }
        }
    }
}
