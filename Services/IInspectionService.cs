using OpenCvStudy.Models;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCvStudy.Services
{
    public interface IInspectionService
    {
        Task<InspectionResult> InspectAsync(
            string imagePath,
            InspectionOptions options,
            CancellationToken cancellationToken);
    }
}
