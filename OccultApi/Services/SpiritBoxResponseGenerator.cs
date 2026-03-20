using OccultApi.Models;

namespace OccultApi.Services
{
    public class SpiritBoxResponseGenerator : ISpiritBoxResponseGenerator
    {
        private readonly ISpiritBoxAudioGeneratorFactory _audioGeneratorFactory;

        public SpiritBoxResponseGenerator(
            ISpiritBoxAudioGeneratorFactory audioGeneratorFactory
        )
        {
            _audioGeneratorFactory = audioGeneratorFactory;
        }

        public async Task<SpiritBoxResponse> GenerateAsync(SpiritBoxRequest request, CancellationToken cancellationToken = default)
        {
            var audioGenerator = _audioGeneratorFactory.Create(request.ResponseType);

            var response = await audioGenerator.GenerateAsync(request.Prompt, cancellationToken);
            using var audioStream = response.AudioStream;

            var memoryStream = new MemoryStream();
            await audioStream.CopyToAsync(memoryStream, cancellationToken);
            var audioBase64 = Convert.ToBase64String(memoryStream.ToArray());

            return new SpiritBoxResponse
            {
                Response = response.TextResponse,
                Audio = audioBase64
            };
        }
    }
}
