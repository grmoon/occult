using System.Speech.Synthesis;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace OccultApi.Services
{

    public abstract class SpiritBoxAudioGenerator : ISpiritBoxAudioGenerator
    {
        protected const int MaxSegmentDurationSeconds = 5;
        private readonly ILogger<SpiritBoxAudioGenerator> _logger;

        public SpiritBoxAudioGenerator(ILogger<SpiritBoxAudioGenerator> logger)
        {
            _logger = logger;
        }

        public abstract Task<Stream> GenerateAsync(string text, CancellationToken cancellationToken = default);

        protected async Task<Stream> GenerateSourceAudioAsync(string text, CancellationToken cancellationToken)
        {
            using var synthesizer = new SpeechSynthesizer();
            var synthStream = new MemoryStream();

            synthesizer.SetOutputToWaveStream(synthStream);
            synthesizer.Speak(text);

            synthStream.Position = 0;

            return synthStream;
        }

        protected static List<(int Seconds, byte[] Data)> SegmentAudio(Stream audioStream)
        {
            audioStream.Position = 0;
            using var reader = new WaveFileReader(audioStream);
            var bytesPerSecond = reader.WaveFormat.AverageBytesPerSecond;
            var blockAlign = reader.WaveFormat.BlockAlign;
            var totalBytes = reader.Length;
            var segments = new List<(int Seconds, byte[] Data)>();
            var offset = 0L;

            while (offset < totalBytes)
            {
                var seconds = Random.Shared.Next(1, MaxSegmentDurationSeconds + 1);
                var length = (int)((long)seconds * bytesPerSecond);
                length -= length % blockAlign;
                length = (int)Math.Min(length, totalBytes - offset);

                reader.Position = offset;
                var buffer = new byte[length];
                var totalRead = 0;

                while (totalRead < length)
                {
                    var read = reader.Read(buffer, totalRead, length - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }

                segments.Add((seconds, buffer.AsSpan(0, totalRead).ToArray()));
                offset += length;
            }

            return segments;
        }
    }
}
