using OccultApi.Models;

namespace OccultApi.Services
{
    public interface ISpiritBoxResponseGenerator
    {
        Task<SpiritBoxResponse> GenerateAsync(SpiritBoxRequest request, CancellationToken cancellationToken = default);
    }
}
