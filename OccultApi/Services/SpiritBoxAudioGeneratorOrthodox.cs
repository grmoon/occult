using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using NLayer;

namespace OccultApi.Services
{
    public class SpiritBoxAudioGeneratorOrthodox : SpiritBoxAudioGenerator
    {
        private readonly ISpiritBoxAudioGetter _audioGetter;
        private readonly ILogger<SpiritBoxAudioGeneratorOrthodox> _logger;
        private readonly float _minSeconds;
        private readonly float _maxSeconds;

        public SpiritBoxAudioGeneratorOrthodox(ISpiritBoxAudioGetter audioGetter, SpeechConfig speechConfig, ILogger<SpiritBoxAudioGeneratorOrthodox> logger, float minSeconds = 0.5f, float maxSeconds = 1f) : base(speechConfig, logger)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minSeconds, 0);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxSeconds, minSeconds);

            _audioGetter = audioGetter;
            _logger = logger;
            _minSeconds = minSeconds;
            _maxSeconds = maxSeconds;
        }

        public override async Task<SpiritboxAudioGeneratorResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var totalDuration = _minSeconds + (float)Random.Shared.NextDouble() * (_maxSeconds - _minSeconds);

            var segments = new List<double>();
            var remaining = (double)totalDuration;

            while (remaining >= 1)
            {
                var seconds = Math.Min(Random.Shared.Next(1, MaxSegmentDurationSeconds + 1), remaining);
                segments.Add(seconds);
                remaining -= seconds;
            }

            if (remaining > 0)
                segments.Add(remaining);

            _logger.LogInformation("Generated {Count} segments totalling {Total:F2}s", segments.Count, totalDuration);

            var audioStreams = await _audioGetter.GetRandomAudioAsync(segments.Count, cancellationToken);
            _logger.LogInformation("Retrieved {Count} random audio streams", audioStreams.Count);

            var audioBuffers = new List<byte[]>();
            foreach (var stream in audioStreams)
            {
                using (stream)
                {
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms, cancellationToken);
                    audioBuffers.Add(ms.ToArray());
                }
            }

            var available = new List<byte[]>();
            var assignedBuffers = new byte[segments.Count][];

            for (var i = 0; i < segments.Count; i++)
            {
                if (available.Count == 0)
                    available.AddRange(audioBuffers);

                var index = Random.Shared.Next(available.Count);
                assignedBuffers[i] = available[index];
                available.RemoveAt(index);
            }

            _logger.LogInformation("Assigned audio buffers to segments");

            var chunks = new List<float[]>();
            int sampleRate = 0, channels = 0;
            for (var i = 0; i < segments.Count; i++)
            {
                var (samples, sr, ch) = GetRandomMp3Chunk(new MemoryStream(assignedBuffers[i]), segments[i]);
                chunks.Add(samples);
                sampleRate = sr;
                channels = ch;
                _logger.LogInformation("Extracted {Seconds:F2}s chunk ({Samples} samples) for segment {Index}/{Total}",
                    segments[i], samples.Length, i + 1, segments.Count);
            }

            var allSamples = chunks.SelectMany(c => c).ToArray();
            var outputBytes = WriteWav(allSamples, sampleRate, channels);

            var outputStream = new MemoryStream(outputBytes);
            _logger.LogInformation("Output stream is {Bytes} bytes", outputStream.Length);

            return new SpiritboxAudioGeneratorResult { AudioStream = outputStream, TextResponse = null };
        }

        private static (float[] Samples, int SampleRate, int Channels) GetRandomMp3Chunk(Stream audioStream, double seconds)
        {
            using var mpegFile = new MpegFile(audioStream);
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
