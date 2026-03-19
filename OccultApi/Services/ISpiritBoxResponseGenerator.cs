using OccultApi.Models;

namespace OccultApi.Services
{
    public interface ISpiritBoxResponseGenerator
    {
        Task<SpiritBoxResponse> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
