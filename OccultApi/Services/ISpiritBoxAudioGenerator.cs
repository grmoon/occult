using System;
namespace OccultApi.Services
{
    public interface ISpiritBoxAudioGenerator
    {
        public Task<Stream> GenerateAsync(string text, CancellationToken cancellationToken = default);
    }
}
