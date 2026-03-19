using System.Speech.Synthesis;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace OccultApi.Services
{
    internal record AudioSegment {
        public required int Offset { get; init; }
        public required int Length { get; init; }
        public required Stream Source { get; init; }
        public required int Seconds { get; init; }
    };

    public class SpiritBoxAudioGeneratorHeterodox : SpiritBoxAudioGenerator 
    {
        private readonly ISpiritBoxAudioGetter _audioGetter;
        private readonly ILogger<SpiritBoxAudioGeneratorHeterodox> _logger;

        public SpiritBoxAudioGeneratorHeterodox(ISpiritBoxAudioGetter audioGetter, ILogger<SpiritBoxAudioGeneratorHeterodox> logger) : base(logger)
        {
            _audioGetter = audioGetter;
            _logger = logger;
        }

        //public async Task<Stream> GenerateAsync(string text, CancellationToken cancellationToken = default)
        //{
        //    _logger.LogInformation("Synthesizing audio for text of length {Length}", text.Length);

        //    using var synthesizer = new SpeechSynthesizer();
        //    var synthStream = new MemoryStream();

        //    var tempText = "You called to see if the quiet answers; it did, with something that knows your name. The light behind you stutters and a cold pressure settles at your shoulders.";

        //    synthesizer.SetOutputToWaveStream(synthStream);
        //    synthesizer.Speak(tempText);

        //    synthStream.Position = 0;

        //    var segments = Segment(synthStream);

        //    _logger.LogInformation("Synthesized {Bytes} bytes of audio", synthStream.Length);

        //    return synthStream;
        //}

        //private List<AudioSegment> Segment(Stream inputStream)
        //{
        //    var reader = new WaveFileReader(inputStream);
        //    var lengthInSeconds = reader.TotalTime.TotalSeconds;

        //    reader.GetChunkData

        //    _logger.LogInformation("Audio length is {Seconds} seconds", lengthInSeconds);
        //    var segments = new List<AudioSegment>();

        //    return segments;
        //}


        public override async Task<Stream> GenerateAsync(string text, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Synthesizing audio for text of length {Length}", text.Length);

            var synthStream = await GenerateSourceAudioAsync(text, cancellationToken);

            _logger.LogInformation("Synthesized {Bytes} bytes of audio", synthStream.Length);

            var segments = SegmentAudio(synthStream);
            _logger.LogInformation("Split audio into {Count} segments", segments.Count);

            var randomAudioStreams = await _audioGetter.GetRandomAudioAsync(segments.Count, cancellationToken);
            _logger.LogInformation("Retrieved {Count} random audio streams", randomAudioStreams.Count);

            var streamList = randomAudioStreams.ToList();
            var available = new List<Stream>();
            var segmentAudioMap = new Dictionary<AudioSegment, Stream>();

            foreach (var segment in segments)
            {
                if (available.Count == 0)
                    available.AddRange(streamList);

                var index = Random.Shared.Next(available.Count);
                segmentAudioMap[segment] = available[index];
                available.RemoveAt(index);
                _logger.LogInformation("Assigned stream {StreamIndex} to segment at offset {Offset} length {Length}",
                    index, segment.Offset, segment.Length);
            }

            var synthSamples = ReadAllSamples(synthStream);

            var matchedSections = new List<AudioSegment>();

            foreach (var segment in segments)
            {
                var audioStream = segmentAudioMap[segment];
                var segmentSamples = synthSamples.AsSpan(segment.Offset, segment.Length).ToArray();
                var matchOffset = FindSimilarSection(segmentSamples, audioStream, segment.Seconds);
                _logger.LogInformation("Segment at offset {SegmentOffset} length {Length} matched at offset {MatchOffset}",
                    segment.Offset, segment.Length, matchOffset);
                matchedSections.Add(new AudioSegment
                {
                    Offset = matchOffset,
                    Length = segment.Length,
                    Source = audioStream,
                    Seconds = segment.Seconds
                });
            }

            synthStream.Position = 0;
            using var waveReader = new WaveFileReader(synthStream);
            var waveFormat = waveReader.WaveFormat;

            _logger.LogInformation("Writing {Count} matched sections to output stream", matchedSections.Count);

            var tempStream = new MemoryStream();
            using (var writer = new WaveFileWriter(tempStream, waveFormat))
            {
                foreach (var section in matchedSections)
                {
                    var samples = ReadAllSamples(section.Source);
                    var length = Math.Min(section.Length, samples.Length - section.Offset);
                    if (length > 0)
                        writer.WriteSamples(samples, section.Offset, length);
                }
            }

            var outputStream = new MemoryStream(tempStream.ToArray());

            _logger.LogInformation("Output stream is {Bytes} bytes", outputStream.Length);

            outputStream.Position = 0;
            return outputStream;
        }

        private static float[] ReadAllSamples(Stream stream)
        {
            stream.Position = 0;
            using var reader = CreateWaveStream(stream);
            var provider = reader.ToSampleProvider();
            var samples = new List<float>();
            var buffer = new float[4096];
            int read;

            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
                samples.AddRange(buffer.AsSpan(0, read).ToArray());

            return samples.ToArray();
        }

        private static WaveStream CreateWaveStream(Stream stream)
        {
            stream.Position = 0;
            var header = new byte[4];
            _ = stream.Read(header, 0, 4);
            stream.Position = 0;

            if (header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F')
                return new WaveFileReader(stream);

            return new Mp3FileReader(stream);
        }

        private static (float[] Samples, int StartSample) ReadSampleChunk(Stream stream, int chunkSeconds)
        {
            stream.Position = 0;
            using var reader = CreateWaveStream(stream);
            var sampleRate = reader.WaveFormat.SampleRate;
            var totalDuration = reader.TotalTime.TotalSeconds;

            var chunkDuration = Math.Min(chunkSeconds, totalDuration);
            var maxStart = Math.Max(0, totalDuration - chunkDuration);
            var startSeconds = maxStart > 0 ? Random.Shared.NextDouble() * maxStart : 0;

            reader.CurrentTime = TimeSpan.FromSeconds(startSeconds);

            var provider = reader.ToSampleProvider();
            var maxSamples = (int)(sampleRate * chunkDuration);
            var samples = new List<float>(maxSamples);
            var buffer = new float[4096];
            int read;

            while (samples.Count < maxSamples && (read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                var toTake = Math.Min(read, maxSamples - samples.Count);
                for (var i = 0; i < toTake; i++)
                    samples.Add(buffer[i]);
            }

            var startSample = (int)(startSeconds * sampleRate);
            return (samples.ToArray(), startSample);
        }

        private static int FindSimilarSection(float[] segmentSamples, Stream audioStream, int segmentSeconds)
        {
            var (audioSamples, chunkStartSample) = ReadSampleChunk(audioStream, segmentSeconds * 10);

            if (audioSamples.Length <= segmentSamples.Length)
                return chunkStartSample;

            var frameSize = 441;
            var segmentEnvelope = ComputeEnergyEnvelope(segmentSamples, frameSize);

            var bestOffset = 0;
            var bestScore = double.MinValue;
            var maxOffset = audioSamples.Length - segmentSamples.Length;

            for (var offset = 0; offset <= maxOffset; offset += frameSize)
            {
                var windowEnvelope = ComputeEnergyEnvelope(
                    audioSamples.AsSpan(offset, segmentSamples.Length).ToArray(), frameSize);

                var score = NormalizedCrossCorrelation(segmentEnvelope, windowEnvelope);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestOffset = offset;
                }
            }

            return chunkStartSample + bestOffset;
        }

        private static float[] ComputeEnergyEnvelope(float[] samples, int frameSize)
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

        private static List<AudioSegment> SegmentAudio(Stream wavStream)
        {
            wavStream.Position = 0;

            using var waveReader = new WaveFileReader(wavStream);
            var sampleRate = waveReader.WaveFormat.SampleRate;
            var totalSamples = (int)(waveReader.SampleCount);

            var segments = new List<AudioSegment>();
            var offset = 0;
            var minSeconds = 1;
            var maxSeconds = 10;

            while (offset < totalSamples)
            {
                var remaining = totalSamples - offset;
                var minLen = sampleRate * minSeconds;

                if (remaining <= minLen)
                {
                    segments.Add(new AudioSegment
                    {
                        Offset = offset,
                        Length = remaining,
                        Source = wavStream,
                        Seconds = (int)Math.Ceiling((double)remaining / sampleRate)
                    });
                    break;
                }

                var maxLen = Math.Min(remaining, sampleRate * maxSeconds);
                var length = Random.Shared.Next(minLen, maxLen);

                segments.Add(new AudioSegment {
                    Offset = offset,
                    Length = length,
                    Source = wavStream,
                    Seconds = (int)Math.Ceiling((double)length / sampleRate)
                });
                offset += length;
            }

            return segments;
        }
    }
}
