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
        private readonly float _orthodoxSegmentMinSeconds;
        private readonly float _orthodoxSegmentMaxSeconds;

        public SpiritBoxAudioGeneratorFactory(
            ISpiritBoxAudioGetter audioGetter,
            SpeechConfig speechConfig,
            ILoggerFactory loggerFactory,
            ISpiritBoxTextResponseGenerator textResponseGenerator,
            float orthodoxMinSeconds,
            float orthodoxMaxSeconds,
            float orthodoxSegmentMinSeconds,
            float orthodoxSegmentMaxSeconds
        )
        {
            _loggerFactory = loggerFactory;
            _audioGetter = audioGetter;
            _speechConfig = speechConfig;
            _logger = loggerFactory.CreateLogger<SpiritBoxAudioGeneratorFactory>();
            _textResponseGenerator = textResponseGenerator;
            _orthodoxMinSeconds = orthodoxMinSeconds;
            _orthodoxMaxSeconds = orthodoxMaxSeconds;
            _orthodoxSegmentMinSeconds = orthodoxSegmentMinSeconds;
            _orthodoxSegmentMaxSeconds = orthodoxSegmentMaxSeconds;
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
                        maxSeconds: _orthodoxMaxSeconds,
                        segmentMinSeconds: _orthodoxSegmentMinSeconds,
                        segmentMaxSeconds: _orthodoxSegmentMaxSeconds
                    );
                default:
                    throw new ArgumentException($"Unsupported response type: {responseType}");
            }
        }
    }
}
