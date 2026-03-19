using Microsoft.Extensions.Logging;
using OccultApi.Models;

namespace OccultApi.Services
{
    public class SpiritBoxAudioGeneratorFactory : ISpiritBoxAudioGeneratorFactory
    {
        private readonly ISpiritBoxAudioGetter _audioGetter;
        private readonly ILogger<SpiritBoxAudioGeneratorFactory> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public SpiritBoxAudioGeneratorFactory(
            ISpiritBoxAudioGetter audioGetter,
            ILoggerFactory loggerFactory
        )
        {
            _loggerFactory = loggerFactory;
            _audioGetter = audioGetter;
            _logger = loggerFactory.CreateLogger<SpiritBoxAudioGeneratorFactory>();
        }

        public ISpiritBoxAudioGenerator Create(SpiritBoxResponseType responseType)
        {
            switch (responseType)
            {
                case SpiritBoxResponseType.Heterodox:
                    return new SpiritBoxAudioGeneratorHeterodox(
                        audioGetter: _audioGetter,
                        logger: _loggerFactory.CreateLogger<SpiritBoxAudioGeneratorHeterodox>()
                    );
                case SpiritBoxResponseType.Orthodox:
                    return new SpiritBoxAudioGeneratorOrthodox(
                        audioGetter: _audioGetter,
                        logger: _loggerFactory.CreateLogger<SpiritBoxAudioGeneratorOrthodox>()
                    );
                default:
                    throw new ArgumentException($"Unsupported response type: {responseType}");
            }
        }
    }
}
