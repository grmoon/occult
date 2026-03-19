using System.Speech.Synthesis;
using Microsoft.Extensions.Logging;

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

        public async Task<Stream> GenerateAsync(string text, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Synthesizing audio for text of length {Length}", text.Length);

            using var synthesizer = new SpeechSynthesizer();
            var stream = new MemoryStream();

            synthesizer.SetOutputToWaveStream(stream);
            synthesizer.Speak(text);

            stream.Position = 0;

            _logger.LogInformation("Synthesized {Bytes} bytes of audio", stream.Length);

            return stream;
        }
    }
}
