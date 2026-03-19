using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System.Runtime.Versioning;

namespace OccultApi.Services
{
    [SupportedOSPlatform("windows")]
    public class SpiritBoxAudioGeneratorHeterodox : SpiritBoxAudioGenerator 
    {
        private readonly ISpiritBoxAudioGetter _audioGetter;
        private readonly ILogger<SpiritBoxAudioGeneratorHeterodox> _logger;

        public SpiritBoxAudioGeneratorHeterodox(ISpiritBoxAudioGetter audioGetter, ILogger<SpiritBoxAudioGeneratorHeterodox> logger) : base(logger)
        {
            _audioGetter = audioGetter;
            _logger = logger;
        }

        public override async Task<Stream> GenerateAsync(string text, CancellationToken cancellationToken = default)
        {
            var synthStream = await GenerateSourceAudioAsync(text, cancellationToken);
            _logger.LogInformation("Synthesized {Bytes} bytes of source audio", synthStream.Length);

            var segments = SegmentAudio(synthStream);
            _logger.LogInformation("Split audio into {Count} segments", segments.Count);

            var audioStreams = await _audioGetter.GetRandomAudioPathsAsync(segments.Count, cancellationToken);
            _logger.LogInformation("Retrieved {Count} random audio paths", audioStreams.Count);

            var pool = audioStreams.ToList();
            var available = new List<string>();
            var assignedPaths = new string[segments.Count];

            for (var i = 0; i < segments.Count; i++)
            {
                if (available.Count == 0)
                    available.AddRange(pool);

                var index = Random.Shared.Next(available.Count);
                assignedPaths[i] = available[index];
                available.RemoveAt(index);
            }

            _logger.LogInformation("Assigned audio paths to segments");

            synthStream.Position = 0;
            using var waveReader = new WaveFileReader(synthStream);
            var segmentFormat = waveReader.WaveFormat;

            var matchedChunks = new (float[] Samples, WaveFormat Format)[segments.Count];
            for (var i = 0; i < segments.Count; i++)
            {
                var searchSeconds = segments[i].Seconds * 10;
                matchedChunks[i] = FindSimilarSection(segments[i].Data, segmentFormat, assignedPaths[i], searchSeconds);
                _logger.LogInformation("Matched segment {Index}/{Total} ({Seconds}s, searched {SearchSeconds}s window, {SampleCount} samples)",
                    i + 1, segments.Count, segments[i].Seconds, searchSeconds, matchedChunks[i].Samples.Length);
            }

            var outputFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                matchedChunks[0].Format.SampleRate, matchedChunks[0].Format.Channels);

            var tempStream = new MemoryStream();
            using (var writer = new WaveFileWriter(tempStream, outputFormat))
            {
                foreach (var chunk in matchedChunks)
                {
                    writer.WriteSamples(chunk.Samples, 0, chunk.Samples.Length);
                }
            }

            var outputStream = new MemoryStream(tempStream.ToArray());
            _logger.LogInformation("Output stream is {Bytes} bytes", outputStream.Length);

            return outputStream;
        }

        private static (float[] Samples, WaveFormat Format) FindSimilarSection(byte[] segmentData, WaveFormat segmentFormat, string audioFilePath, int searchSeconds)
        {
            var segmentSamples = BytesToSamples(segmentData, segmentFormat);
            var segmentDurationSeconds = (double)segmentSamples.Length / (segmentFormat.SampleRate * segmentFormat.Channels);

            using var reader = new MediaFoundationReader(audioFilePath);
            var sampleRate = reader.WaveFormat.SampleRate;
            var channels = reader.WaveFormat.Channels;
            var totalSeconds = reader.TotalTime.TotalSeconds;

            var targetSampleCount = (int)(segmentDurationSeconds * sampleRate * channels);

            var chunkDuration = Math.Min(searchSeconds, totalSeconds);
            var maxStart = Math.Max(0, totalSeconds - chunkDuration);
            var startSeconds = maxStart > 0 ? Random.Shared.NextDouble() * maxStart : 0;

            reader.CurrentTime = TimeSpan.FromSeconds(startSeconds);

            var provider = reader.ToSampleProvider();
            var maxSamples = (int)(sampleRate * channels * chunkDuration);
            var audioSamples = new float[maxSamples];
            var totalRead = 0;

            while (totalRead < maxSamples)
            {
                var read = provider.Read(audioSamples, totalRead, maxSamples - totalRead);
                if (read == 0) break;
                totalRead += read;
            }

            if (totalRead <= targetSampleCount)
                return (audioSamples.AsSpan(0, totalRead).ToArray(), reader.WaveFormat);

            var frameSize = Math.Max(1, sampleRate / 100);
            var segmentEnvelope = ComputeEnergyEnvelope(segmentSamples, frameSize);

            var bestOffset = 0;
            var bestScore = double.MinValue;
            var maxOffset = totalRead - targetSampleCount;

            var envelopeTargetLength = targetSampleCount;
            for (var offset = 0; offset <= maxOffset; offset += frameSize)
            {
                var windowEnvelope = ComputeEnergyEnvelope(
                    audioSamples.AsSpan(offset, envelopeTargetLength), frameSize);

                var score = NormalizedCrossCorrelation(segmentEnvelope, windowEnvelope);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestOffset = offset;
                }
            }

            var matchLength = Math.Min(targetSampleCount, totalRead - bestOffset);
            return (audioSamples.AsSpan(bestOffset, matchLength).ToArray(), reader.WaveFormat);
        }

        private static float[] BytesToSamples(byte[] data, WaveFormat format)
        {
            using var stream = new MemoryStream(data);
            using var reader = new RawSourceWaveStream(stream, format);
            var provider = reader.ToSampleProvider();
            var samples = new float[data.Length / format.BlockAlign * format.Channels];
            var read = provider.Read(samples, 0, samples.Length);
            return samples.AsSpan(0, read).ToArray();
        }

        private static float[] ComputeEnergyEnvelope(ReadOnlySpan<float> samples, int frameSize)
        {
            var frameCount = samples.Length / frameSize;
            var envelope = new float[frameCount];

            for (var i = 0; i < frameCount; i++)
            {
                var sum = 0f;
                var start = i * frameSize;
                for (var j = 0; j < frameSize; j++)
                {
                    var s = samples[start + j];
                    sum += s * s;
                }
                envelope[i] = MathF.Sqrt(sum / frameSize);
            }

            return envelope;
        }

        private static double NormalizedCrossCorrelation(float[] a, float[] b)
        {
            var len = Math.Min(a.Length, b.Length);
            if (len == 0) return 0;

            double sumAB = 0, sumAA = 0, sumBB = 0;
            for (var i = 0; i < len; i++)
            {
                sumAB += a[i] * b[i];
                sumAA += a[i] * a[i];
                sumBB += b[i] * b[i];
            }

            var denom = Math.Sqrt(sumAA * sumBB);
            return denom == 0 ? 0 : sumAB / denom;
        }
    }
}
