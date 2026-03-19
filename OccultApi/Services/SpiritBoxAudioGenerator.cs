using System.Speech.Synthesis;
using Microsoft.Extensions.Logging;

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
    }
}
