using OccultApi.Models;

namespace OccultApi.Services
{
    public interface ISpiritBoxAudioGeneratorFactory
    {
        public ISpiritBoxAudioGenerator Create(SpiritBoxResponseType responseType);
    }
}
