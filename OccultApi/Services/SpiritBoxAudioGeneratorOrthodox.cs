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

        public override async Task<SpiritboxAudioGeneratorResult> GenerateAsync(string text, CancellationToken cancellationToken = default)
        {
            var totalSeconds = Random.Shared.Next(10, 21);
            var segmentList = new List<int>();
            var remaining = totalSeconds;

            while (remaining > 0)
            {
                var seconds = Math.Min(Random.Shared.Next(1, MaxSegmentDurationSeconds + 1), remaining);
                segmentList.Add(seconds);
                remaining -= seconds;
            }

            var segments = segmentList.ToArray();
            _logger.LogInformation("Generated {Count} segments totalling {Total}s", segments.Length, totalSeconds);

            var audioStreams = await _audioGetter.GetRandomAudioAsync(segments.Length, cancellationToken);
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
            var assignedBuffers = new byte[segments.Length][];

            for (var i = 0; i < segments.Length; i++)
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
            for (var i = 0; i < segments.Length; i++)
            {
                var (samples, sr, ch) = GetRandomMp3Chunk(new MemoryStream(assignedBuffers[i]), segments[i]);
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

            return new SpiritboxAudioGeneratorResult { AudioStream = outputStream, TextResponse = null };
        }

        private static (float[] Samples, int SampleRate, int Channels) GetRandomMp3Chunk(Stream audioStream, int seconds)
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
