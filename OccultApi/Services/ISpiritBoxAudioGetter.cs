namespace OccultApi.Services
{
    public interface ISpiritBoxAudioGetter
    {
        Task<IReadOnlySet<Stream>> GetRandomAudioAsync(int maxCount, CancellationToken cancellationToken = default);
    }
}
