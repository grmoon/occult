using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using NLayer;

namespace OccultApi.Services
{
    public class SpiritBoxAudioGeneratorOrthodox : SpiritBoxAudioGenerator
    {
        private readonly ISpiritBoxAudioGetter _audioGetter;
        private readonly ILogger<SpiritBoxAudioGeneratorOrthodox> _logger;

        public SpiritBoxAudioGeneratorOrthodox(ISpiritBoxAudioGetter audioGetter, SpeechConfig speechConfig, ILogger<SpiritBoxAudioGeneratorOrthodox> logger) : base(speechConfig, logger)
        {
            _audioGetter = audioGetter;
            _logger = logger;
        }

        public override async Task<Stream> GenerateAsync(string text, CancellationToken cancellationToken = default)
        {
            var synthStream = await GenerateSourceAudioAsync(text, cancellationToken);
            _logger.LogInformation("Synthesized {Bytes} bytes of source audio", synthStream.Length);

            var segments = SegmentAudio(synthStream).Select(val => val.Seconds).ToArray();
            _logger.LogInformation("Split audio into {Count} segments", segments.Length);

            var audioStreams = await _audioGetter.GetRandomAudioPathsAsync(segments.Length, cancellationToken);
            _logger.LogInformation("Retrieved {Count} random audio paths", audioStreams.Count);

            var pool = audioStreams.ToList();
            var available = new List<string>();
            var assignedPaths = new string[segments.Length];

            for (var i = 0; i < segments.Length; i++)
            {
                if (available.Count == 0)
                    available.AddRange(pool);

                var index = Random.Shared.Next(available.Count);
                assignedPaths[i] = available[index];
                available.RemoveAt(index);
            }

            _logger.LogInformation("Assigned audio paths to segments");

            var chunks = new List<float[]>();
            int sampleRate = 0, channels = 0;
            for (var i = 0; i < segments.Length; i++)
            {
                var (samples, sr, ch) = GetRandomMp3Chunk(assignedPaths[i], segments[i]);
                chunks.Add(samples);
                sampleRate = sr;
                channels = ch;
                _logger.LogInformation("Extracted {Seconds}s chunk ({Samples} samples) for segment {Index}/{Total}",
                    segments[i], samples.Length, i + 1, segments.Length);
            }

            var allSamples = chunks.SelectMany(c => c).ToArray();
            var outputBytes = WriteWav(allSamples, sampleRate, channels);

            var outputStream = new MemoryStream(outputBytes);
            _logger.LogInformation("Output stream is {Bytes} bytes", outputStream.Length);

            return outputStream;
        }

        private static (float[] Samples, int SampleRate, int Channels) GetRandomMp3Chunk(string filePath, int seconds)
        {
            using var mpegFile = new MpegFile(filePath);
            var sampleRate = mpegFile.SampleRate;
            var channels = mpegFile.Channels;
            var totalSeconds = mpegFile.Duration.TotalSeconds;

            var chunkSeconds = Math.Min(seconds, totalSeconds);
            var maxStart = Math.Max(0, totalSeconds - chunkSeconds);
            var startSeconds = maxStart > 0 ? Random.Shared.NextDouble() * maxStart : 0;

            mpegFile.Time = TimeSpan.FromSeconds(startSeconds);

            var maxSamples = (int)(chunkSeconds * sampleRate * channels);
            var buffer = new float[maxSamples];
            var totalRead = mpegFile.ReadSamples(buffer, 0, maxSamples);

            return (buffer.AsSpan(0, totalRead).ToArray(), sampleRate, channels);
        }
    }
}
