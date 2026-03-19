namespace OccultApi.Services
{
    public interface ISpiritBoxTextResponseGenerator
    {
        public Task<string> RespondAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
