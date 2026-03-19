using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System.Runtime.Versioning;

namespace OccultApi.Services
{
    [SupportedOSPlatform("windows")]
    public class SpiritBoxAudioGeneratorOrthodox : SpiritBoxAudioGenerator
    {
        private readonly ISpiritBoxAudioGetter _audioGetter;
        private readonly ILogger<SpiritBoxAudioGeneratorOrthodox> _logger;

        public SpiritBoxAudioGeneratorOrthodox(ISpiritBoxAudioGetter audioGetter, ILogger<SpiritBoxAudioGeneratorOrthodox> logger) : base(logger)
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

            var audioStreams = await _audioGetter.GetRandomAudioAsync(segments.Length, cancellationToken);
            _logger.LogInformation("Retrieved {Count} random audio streams", audioStreams.Count);

            var pool = audioStreams.ToList();
            var available = new List<Stream>();
            var assignedStreams = new Stream[segments.Length];

            for (var i = 0; i < segments.Length; i++)
            {
                if (available.Count == 0)
                    available.AddRange(pool);

                var index = Random.Shared.Next(available.Count);
                assignedStreams[i] = available[index];
                available.RemoveAt(index);
            }

            _logger.LogInformation("Assigned audio streams to segments");

            var chunks = new byte[segments.Length][];
            WaveFormat? outputFormat = null;
            for (var i = 0; i < segments.Length; i++)
            {
                chunks[i] = GetRandomMp3Chunk(assignedStreams[i], segments[i], out var chunkFormat);
                outputFormat ??= chunkFormat;
                _logger.LogInformation("Extracted {Seconds}s chunk ({Bytes} bytes) for segment {Index}/{Total}",
                    segments[i], chunks[i].Length, i + 1, segments.Length);
            }

            var tempStream = new MemoryStream();
            using (var writer = new WaveFileWriter(tempStream, outputFormat!))
            {
                foreach (var chunk in chunks)
                {
                    writer.Write(chunk, 0, chunk.Length);
                }
            }

            var outputStream = new MemoryStream(tempStream.ToArray());
            _logger.LogInformation("Output stream is {Bytes} bytes", outputStream.Length);

            return outputStream;
        }


        private static byte[] GetRandomMp3Chunk(Stream mp3Stream, int seconds, out WaveFormat waveFormat)
        {
            mp3Stream.Position = 0;
            using var reader = new Mp3FileReader(mp3Stream);
            waveFormat = reader.WaveFormat;
            var totalSeconds = reader.TotalTime.TotalSeconds;

            var chunkSeconds = Math.Min(seconds, totalSeconds);
            var maxStart = Math.Max(0, totalSeconds - chunkSeconds);
            var startSeconds = maxStart > 0 ? Random.Shared.NextDouble() * maxStart : 0;

            reader.CurrentTime = TimeSpan.FromSeconds(startSeconds);

            var bytesPerSecond = reader.WaveFormat.AverageBytesPerSecond;
            var bytesToRead = (int)(chunkSeconds * bytesPerSecond);
            var buffer = new byte[bytesToRead];
            var totalRead = 0;

            while (totalRead < bytesToRead)
            {
                var read = reader.Read(buffer, totalRead, bytesToRead - totalRead);
                if (read == 0) break;
                totalRead += read;
            }

            return buffer.AsSpan(0, totalRead).ToArray();
        }

    }
}
