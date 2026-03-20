using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using NLayer;

namespace OccultApi.Services
{
    public class SpiritBoxAudioGeneratorHeterodox : SpiritBoxAudioGenerator 
    {
        private readonly ISpiritBoxAudioGetter _audioGetter;
        private readonly ILogger<SpiritBoxAudioGeneratorHeterodox> _logger;

        public SpiritBoxAudioGeneratorHeterodox(ISpiritBoxAudioGetter audioGetter, SpeechConfig speechConfig, ILogger<SpiritBoxAudioGeneratorHeterodox> logger) : base(speechConfig, logger)
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

            var matchedChunks = new (float[] Samples, int SampleRate, int Channels)[segments.Count];
            for (var i = 0; i < segments.Count; i++)
            {
                var searchSeconds = segments[i].Seconds * 10;
                matchedChunks[i] = FindSimilarSection(segments[i].Data, assignedPaths[i], searchSeconds);
                _logger.LogInformation("Matched segment {Index}/{Total} ({Seconds}s, searched {SearchSeconds}s window, {SampleCount} samples)",
                    i + 1, segments.Count, segments[i].Seconds, searchSeconds, matchedChunks[i].Samples.Length);
            }

            var allSamples = matchedChunks.SelectMany(c => c.Samples).ToArray();
            var outputBytes = WriteWav(allSamples, matchedChunks[0].SampleRate, matchedChunks[0].Channels);

            var outputStream = new MemoryStream(outputBytes);
            _logger.LogInformation("Output stream is {Bytes} bytes", outputStream.Length);

            return outputStream;
        }

        private static (float[] Samples, int SampleRate, int Channels) FindSimilarSection(byte[] segmentData, string audioFilePath, int searchSeconds)
        {
            var segmentSamples = PcmBytesToSamples(segmentData);
            var segmentDurationSeconds = (double)segmentSamples.Length / (SynthSampleRate * SynthChannels);

            using var mpegFile = new MpegFile(audioFilePath);
            var sampleRate = mpegFile.SampleRate;
            var channels = mpegFile.Channels;
            var totalSeconds = mpegFile.Duration.TotalSeconds;

            var targetSampleCount = (int)(segmentDurationSeconds * sampleRate * channels);

            var chunkDuration = Math.Min(searchSeconds, totalSeconds);
            var maxStart = Math.Max(0, totalSeconds - chunkDuration);
            var startSeconds = maxStart > 0 ? Random.Shared.NextDouble() * maxStart : 0;

            mpegFile.Time = TimeSpan.FromSeconds(startSeconds);

            var maxSamples = (int)(sampleRate * channels * chunkDuration);
            var audioSamples = new float[maxSamples];
            var totalRead = mpegFile.ReadSamples(audioSamples, 0, maxSamples);

            if (totalRead <= targetSampleCount)
                return (audioSamples.AsSpan(0, totalRead).ToArray(), sampleRate, channels);

            var frameSize = Math.Max(1, sampleRate / 100);
            var segmentEnvelope = ComputeEnergyEnvelope(segmentSamples, frameSize);

            var bestOffset = 0;
            var bestScore = double.MinValue;
            var maxOffset = totalRead - targetSampleCount;

            for (var offset = 0; offset <= maxOffset; offset += frameSize)
            {
                var windowEnvelope = ComputeEnergyEnvelope(
                    audioSamples.AsSpan(offset, targetSampleCount), frameSize);

                var score = NormalizedCrossCorrelation(segmentEnvelope, windowEnvelope);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestOffset = offset;
                }
            }

            var matchLength = Math.Min(targetSampleCount, totalRead - bestOffset);
            return (audioSamples.AsSpan(bestOffset, matchLength).ToArray(), sampleRate, channels);
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
