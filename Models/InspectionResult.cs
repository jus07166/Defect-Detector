using System;
using System.Collections.Generic;

namespace OpenCvStudy.Models
{
    public sealed class InspectionResult
    {
        public InspectionResult(byte[] mainImage, IEnumerable<byte[]> defectImages)
        {
            MainImage = mainImage ?? throw new ArgumentNullException(nameof(mainImage));
            DefectImages = new List<byte[]>(defectImages ?? Array.Empty<byte[]>()).AsReadOnly();
        }

        public byte[] MainImage { get; }

        public IReadOnlyList<byte[]> DefectImages { get; }
    }
}
