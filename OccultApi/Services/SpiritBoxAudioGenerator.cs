using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;

namespace OccultApi.Services
{

    public abstract class SpiritBoxAudioGenerator : ISpiritBoxAudioGenerator
    {
        protected const int MaxSegmentDurationSeconds = 5;
        protected const int SynthSampleRate = 16000;
        protected const int SynthBitsPerSample = 16;
        protected const int SynthChannels = 1;
        protected const int SynthBytesPerSecond = SynthSampleRate * SynthBitsPerSample / 8 * SynthChannels;
        protected const int SynthBlockAlign = SynthBitsPerSample / 8 * SynthChannels;
        private const int WavHeaderSize = 44;
        private readonly SpeechConfig _speechConfig;
        private readonly ILogger<SpiritBoxAudioGenerator> _logger;

        public SpiritBoxAudioGenerator(SpeechConfig speechConfig, ILogger<SpiritBoxAudioGenerator> logger)
        {
            _speechConfig = speechConfig;
            _logger = logger;
        }

        public abstract Task<Stream> GenerateAsync(string text, CancellationToken cancellationToken = default);

        protected async Task<Stream> GenerateSourceAudioAsync(string text, CancellationToken cancellationToken)
        {
            _speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff16Khz16BitMonoPcm);

            using var synthesizer = new SpeechSynthesizer(_speechConfig, null);
            var result = await synthesizer.SpeakTextAsync(text);

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                throw new InvalidOperationException($"Speech synthesis canceled: {cancellation.Reason}, {cancellation.ErrorDetails}");
            }

            var synthStream = new MemoryStream(result.AudioData);
            return synthStream;
        }

        protected static List<(int Seconds, byte[] Data)> SegmentAudio(Stream audioStream)
        {
            audioStream.Position = WavHeaderSize;

            var totalBytes = audioStream.Length - WavHeaderSize;
            var segments = new List<(int Seconds, byte[] Data)>();
            var offset = 0L;

            while (offset < totalBytes)
            {
                var seconds = Random.Shared.Next(1, MaxSegmentDurationSeconds + 1);
                var length = seconds * SynthBytesPerSecond;
                length -= length % SynthBlockAlign;
                length = (int)Math.Min(length, totalBytes - offset);

                audioStream.Position = WavHeaderSize + offset;
                var buffer = new byte[length];
                var totalRead = 0;

                while (totalRead < length)
                {
                    var read = audioStream.Read(buffer, totalRead, length - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }

                segments.Add((seconds, buffer.AsSpan(0, totalRead).ToArray()));
                offset += length;
            }

            return segments;
        }

        protected static float[] PcmBytesToSamples(byte[] data)
        {
            var sampleCount = data.Length / 2;
            var samples = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var sample16 = (short)(data[i * 2] | (data[i * 2 + 1] << 8));
                samples[i] = sample16 / 32768f;
            }

            return samples;
        }

        protected static byte[] WriteWav(float[] samples, int sampleRate, int channels)
        {
            var bitsPerSample = 32;
            var bytesPerSample = bitsPerSample / 8;
            var dataSize = samples.Length * bytesPerSample;
            var fileSize = WavHeaderSize + dataSize;

            using var stream = new MemoryStream(fileSize);
            using var writer = new BinaryWriter(stream);

            // RIFF header
            writer.Write("RIFF"u8);
            writer.Write(fileSize - 8);
            writer.Write("WAVE"u8);

            // fmt chunk
            writer.Write("fmt "u8);
            writer.Write(16); // chunk size
            writer.Write((short)3); // IEEE float
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * bytesPerSample); // byte rate
            writer.Write((short)(channels * bytesPerSample)); // block align
            writer.Write((short)bitsPerSample);

            // data chunk
            writer.Write("data"u8);
            writer.Write(dataSize);

            foreach (var sample in samples)
            {
                writer.Write(sample);
            }

            return stream.ToArray();
        }
    }
}
