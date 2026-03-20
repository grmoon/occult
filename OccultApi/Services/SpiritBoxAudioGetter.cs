using Azure.Storage.Blobs;

namespace OccultApi.Services
{
    public class SpiritBoxAudioGetter : ISpiritBoxAudioGetter
    {
        private readonly BlobContainerClient _containerClient;

        public SpiritBoxAudioGetter(BlobContainerClient containerClient)
        {
            _containerClient = containerClient;
        }

        public async Task<IReadOnlySet<Stream>> GetRandomAudioAsync(int maxCount, CancellationToken cancellationToken = default)
        {
            var blobNames = await GetRandomBlobNamesAsync(maxCount, cancellationToken);
            var streams = new HashSet<Stream>();

            foreach (var blobName in blobNames)
            {
                var blobClient = _containerClient.GetBlobClient(blobName);
                streams.Add(await blobClient.OpenReadAsync(cancellationToken: cancellationToken));
            }

            return streams;
        }

        public async Task<IReadOnlySet<string>> GetRandomAudioPathsAsync(int maxCount, CancellationToken cancellationToken = default)
        {
            var blobNames = await GetRandomBlobNamesAsync(maxCount, cancellationToken);

            return new HashSet<string>(blobNames.Select(name => _containerClient.GetBlobClient(name).Uri.ToString()));
        }

        private async Task<IReadOnlySet<string>> GetRandomBlobNamesAsync(int maxCount, CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

            var blobNames = new List<string>();
            await foreach (var blob in _containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                blobNames.Add(blob.Name);
            }

            if (blobNames.Count == 0)
            {
                throw new InvalidOperationException("No audio files found in the blob container.");
            }

            var upper = Math.Min(maxCount, blobNames.Count);
            var count = Random.Shared.Next(1, upper + 1);
            var selected = new HashSet<string>();

            while (selected.Count < count)
            {
                selected.Add(blobNames[Random.Shared.Next(blobNames.Count)]);
            }

            return selected.ToHashSet();
        }
    }
}
