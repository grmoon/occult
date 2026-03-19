namespace OccultApi.Services
{
    public class SpiritBoxAudioGetter : ISpiritBoxAudioGetter
    {
        private static readonly string AudioDirectory = Path.Combine(AppContext.BaseDirectory, "Audio");

        public async Task<IReadOnlySet<Stream>> GetRandomAudioAsync(int maxCount, CancellationToken cancellationToken = default)
        {
            var paths = await GetRandomAudioPathsAsync(maxCount, cancellationToken);
            return new HashSet<Stream>(paths.Select(file => (Stream)File.OpenRead(file)));
        }

        public Task<IReadOnlySet<string>> GetRandomAudioPathsAsync(int maxCount, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

            var files = Directory.GetFiles(AudioDirectory);

            if (files.Length == 0)
            {
                throw new InvalidOperationException("No audio files found in the Audio directory.");
            }

            var upper = Math.Min(maxCount, files.Length);
            var count = Random.Shared.Next(1, upper + 1);
            var selected = new HashSet<string>();

            while (selected.Count < count)
            {
                selected.Add(files[Random.Shared.Next(files.Length)]);
            }

            return Task.FromResult<IReadOnlySet<string>>(selected);
        }
    }
}
