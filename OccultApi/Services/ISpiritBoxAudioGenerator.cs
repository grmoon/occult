using System;
namespace OccultApi.Services
{
    public record SpiritboxAudioGeneratorResult
    {
        public required Stream AudioStream { get; init; }
        public required string? TextResponse { get; init; }
    }

    public interface ISpiritBoxAudioGenerator
    {
        public Task<SpiritboxAudioGeneratorResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
