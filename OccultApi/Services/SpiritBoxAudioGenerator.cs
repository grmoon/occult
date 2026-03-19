using System.Collections.Concurrent;
using System.Diagnostics;
using System.Speech.Synthesis;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace OccultApi.Services
{
    public class SpiritBoxAudioGenerator : ISpiritBoxAudioGenerator
    {
        private readonly ISpiritBoxAudioGetter _audioGetter;
        private readonly ILogger<SpiritBoxAudioGenerator> _logger;

        public SpiritBoxAudioGenerator(ISpiritBoxAudioGetter audioGetter, ILogger<SpiritBoxAudioGenerator> logger)
        {
            _audioGetter = audioGetter;
            _logger = logger;
        }

        private const int SampleRate = 22050;

        public async Task<Stream> GenerateAsync(string text, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Starting audio generation for text of length {Length}", text.Length);

            var segments = SegmentText(text);
            _logger.LogInformation("Text segmented into {Count} segments", segments.Count);

            var audioStreams = await _audioGetter.GetRandomAudioAsync(segments.Count, cancellationToken);
            _logger.LogInformation("Fetched {Count} audio streams", audioStreams.Count);

            var segmentStreamMap = MapSegmentsToStreams(segments, audioStreams);

            // Synthesize reference waveforms and read audio streams in parallel
            Dictionary<string, float[]> referenceWaveforms = null!;
            var streamSamples = new ConcurrentDictionary<Stream, float[]>();

            var uniqueStreams = segmentStreamMap.Select(x => x.Stream).Distinct().ToList();

            _logger.LogInformation("Synthesizing waveforms and reading {Count} unique streams in parallel", uniqueStreams.Count);

            await Task.WhenAll(
                Task.Run(() =>
                {
                    referenceWaveforms = SynthesizeSegmentWaveforms(segments);
                    _logger.LogInformation("Synthesized {Count} unique reference waveforms", referenceWaveforms.Count);
                }, cancellationToken),
                Task.Run(() =>
                {
                    Parallel.ForEach(uniqueStreams, stream =>
                    {
                        streamSamples[stream] = ReadStreamAsSamples(stream);
                    });
                    _logger.LogInformation("Read all audio stream samples");
                }, cancellationToken)
            );

            // Match segments in parallel, preserving order
            _logger.LogInformation("Matching {Count} segments to audio", segmentStreamMap.Count);
            var matchResults = new float[segmentStreamMap.Count][];

            Parallel.For(0, segmentStreamMap.Count, i =>
            {
                var (segment, stream) = segmentStreamMap[i];
                var sourceSamples = streamSamples[stream];
                var reference = referenceWaveforms[segment];
                var (offset, length) = FindBestMatch(sourceSamples, reference);

                matchResults[i] = sourceSamples[offset..(offset + length)];
            });

            _logger.LogInformation("All segments matched");

            foreach (var stream in audioStreams)
            {
                stream.Dispose();
            }

            var totalSamples = matchResults.Sum(r => r.Length);
            var matchedSamples = new float[totalSamples];
            var pos = 0;

            foreach (var result in matchResults)
            {
                result.CopyTo(matchedSamples, pos);
                pos += result.Length;
            }

            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);
            var tempStream = new MemoryStream();

            using (var writer = new WaveFileWriter(tempStream, waveFormat))
            {
                writer.WriteSamples(matchedSamples, 0, matchedSamples.Length);
            }

            stopwatch.Stop();
            _logger.LogInformation("Audio generation completed in {Elapsed}ms with {Samples} total samples",
                stopwatch.ElapsedMilliseconds, totalSamples);

            return new MemoryStream(tempStream.ToArray());
        }

        private static List<(string Segment, Stream Stream)> MapSegmentsToStreams(List<string> segments, IReadOnlySet<Stream> streams)
        {
            var streamList = streams.ToList();
            var remaining = new List<Stream>(streamList);
            var result = new List<(string, Stream)>(segments.Count);

            foreach (var segment in segments)
            {
                if (remaining.Count == 0)
                {
                    remaining.AddRange(streamList);
                }

                var index = Random.Shared.Next(remaining.Count);
                result.Add((segment, remaining[index]));
                remaining.RemoveAt(index);
            }

            return result;
        }

        private static List<string> SegmentText(string text)
        {
            var segments = new List<string>();
            var remaining = text.AsSpan();

            while (remaining.Length > 0)
            {
                var maxLen = Math.Min(remaining.Length, 5);
                var len = Random.Shared.Next(1, maxLen + 1);
                segments.Add(remaining[..len].ToString());
                remaining = remaining[len..];
            }

            return segments;
        }

        private static Dictionary<string, float[]> SynthesizeSegmentWaveforms(List<string> segments)
        {
            var waveforms = new Dictionary<string, float[]>();

            using var synthesizer = new SpeechSynthesizer();

            foreach (var segment in segments)
            {
                if (waveforms.ContainsKey(segment))
                {
                    continue;
                }

                using var stream = new MemoryStream();
                synthesizer.SetOutputToWaveStream(stream);
                synthesizer.Speak(segment);
                stream.Position = 0;

                using var waveReader = new NAudio.Wave.WaveFileReader(stream);
                var samples = new List<float>();
                var buffer = new float[waveReader.WaveFormat.SampleRate];
                var sampleProvider = waveReader.ToSampleProvider();
                int read;

                while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (var i = 0; i < read; i++)
                    {
                        samples.Add(buffer[i]);
                    }
                }

                waveforms[segment] = [.. samples];
            }

            synthesizer.SetOutputToNull();
            return waveforms;
        }

        private const int ChunkDurationSeconds = 10;

        private static float[] ReadStreamAsSamples(Stream audioStream)
        {
            audioStream.Position = 0;

            using var mp3Reader = new Mp3FileReader(audioStream);
            ISampleProvider sampleProvider = mp3Reader.ToSampleProvider();

            if (mp3Reader.WaveFormat.Channels > 1)
            {
                sampleProvider = new StereoToMonoSampleProvider(sampleProvider);
            }

            if (mp3Reader.WaveFormat.SampleRate != SampleRate)
            {
                sampleProvider = new WdlResamplingSampleProvider(sampleProvider, SampleRate);
            }

            var totalSamples = (int)(mp3Reader.TotalTime.TotalSeconds * SampleRate);
            var chunkSize = Math.Min(SampleRate * ChunkDurationSeconds, totalSamples);

            var startSample = totalSamples > chunkSize
                ? Random.Shared.Next(totalSamples - chunkSize)
                : 0;

            // Skip to the random start position
            var skipBuffer = new float[4096];
            var samplesToSkip = startSample;

            while (samplesToSkip > 0)
            {
                var toRead = Math.Min(samplesToSkip, skipBuffer.Length);
                var skipped = sampleProvider.Read(skipBuffer, 0, toRead);

                if (skipped == 0)
                {
                    break;
                }

                samplesToSkip -= skipped;
            }

            // Read the chunk
            var samples = new List<float>(chunkSize);
            var buffer = new float[4096];
            var remaining = chunkSize;

            while (remaining > 0)
            {
                var toRead = Math.Min(remaining, buffer.Length);
                var read = sampleProvider.Read(buffer, 0, toRead);

                if (read == 0)
                {
                    break;
                }

                for (var i = 0; i < read; i++)
                {
                    samples.Add(buffer[i]);
                }

                remaining -= read;
            }

            return [.. samples];
        }

        private static (int Offset, int Length) FindBestMatch(float[] source, float[] reference)
        {
            var windowSize = reference.Length;

            if (windowSize >= source.Length)
            {
                return (0, source.Length);
            }

            var step = Math.Max(1, windowSize / 4);
            var totalSteps = (source.Length - windowSize) / step + 1;

            var bestOffset = 0;
            var bestScore = float.MinValue;
            var lockObj = new object();

            Parallel.For(0, totalSteps, () => (Offset: 0, Score: float.MinValue), (stepIndex, _, localBest) =>
            {
                var i = stepIndex * step;
                var score = NormalizedCrossCorrelation(
                    source.AsSpan(i, windowSize),
                    reference.AsSpan());

                return score > localBest.Score ? (i, score) : localBest;
            },
            localBest =>
            {
                lock (lockObj)
                {
                    if (localBest.Score > bestScore)
                    {
                        bestScore = localBest.Score;
                        bestOffset = localBest.Offset;
                    }
                }
            });

            return (bestOffset, windowSize);
        }

        private static float NormalizedCrossCorrelation(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            var length = Math.Min(a.Length, b.Length);
            float sumAB = 0, sumAA = 0, sumBB = 0;

            for (var i = 0; i < length; i++)
            {
                sumAB += a[i] * b[i];
                sumAA += a[i] * a[i];
                sumBB += b[i] * b[i];
            }

            var denominator = MathF.Sqrt(sumAA * sumBB);
            return denominator == 0 ? 0 : sumAB / denominator;
        }

    }
}
