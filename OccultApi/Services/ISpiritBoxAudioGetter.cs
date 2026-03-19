namespace OccultApi.Services
{
    public interface ISpiritBoxAudioGetter
    {
        Task<IReadOnlySet<Stream>> GetRandomAudioAsync(int maxCount, CancellationToken cancellationToken = default);
        Task<IReadOnlySet<string>> GetRandomAudioPathsAsync(int maxCount, CancellationToken cancellationToken = default);
    }
}
