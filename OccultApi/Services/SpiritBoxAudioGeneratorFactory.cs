using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using OccultApi.Models;

namespace OccultApi.Services
{
    public class SpiritBoxAudioGeneratorFactory : ISpiritBoxAudioGeneratorFactory
    {
        private readonly ISpiritBoxAudioGetter _audioGetter;
        private readonly SpeechConfig _speechConfig;
        private readonly ILogger<SpiritBoxAudioGeneratorFactory> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ISpiritBoxTextResponseGenerator _textResponseGenerator;
        private readonly float _orthodoxMinSeconds;
        private readonly float _orthodoxMaxSeconds;

        public SpiritBoxAudioGeneratorFactory(
            ISpiritBoxAudioGetter audioGetter,
            SpeechConfig speechConfig,
            ILoggerFactory loggerFactory,
            ISpiritBoxTextResponseGenerator textResponseGenerator,
            float orthodoxMinSeconds,
            float orthodoxMaxSeconds
        )
        {
            _loggerFactory = loggerFactory;
            _audioGetter = audioGetter;
            _speechConfig = speechConfig;
            _logger = loggerFactory.CreateLogger<SpiritBoxAudioGeneratorFactory>();
            _textResponseGenerator = textResponseGenerator;
            _orthodoxMinSeconds = orthodoxMinSeconds;
            _orthodoxMaxSeconds = orthodoxMaxSeconds;
        }

        public ISpiritBoxAudioGenerator Create(SpiritBoxResponseType responseType)
        {
            switch (responseType)
            {
                case SpiritBoxResponseType.Heterodox:
                    return new SpiritBoxAudioGeneratorHeterodox(
                        audioGetter: _audioGetter,
                        speechConfig: _speechConfig,
                        logger: _loggerFactory.CreateLogger<SpiritBoxAudioGeneratorHeterodox>(),
                        textResponseGenerator: _textResponseGenerator
                    );
                case SpiritBoxResponseType.Orthodox:
                    return new SpiritBoxAudioGeneratorOrthodox(
                        audioGetter: _audioGetter,
                        speechConfig: _speechConfig,
                        logger: _loggerFactory.CreateLogger<SpiritBoxAudioGeneratorOrthodox>(),
                        minSeconds: _orthodoxMinSeconds,
                        maxSeconds: _orthodoxMaxSeconds
                    );
                default:
                    throw new ArgumentException($"Unsupported response type: {responseType}");
            }
        }
    }
}
