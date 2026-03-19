using OccultApi.Models;

namespace OccultApi.Services
{
    public class SpiritBoxResponseGenerator : ISpiritBoxResponseGenerator
    {
        private readonly ISpiritBoxTextResponseGenerator _textGenerator;
        private readonly ISpiritBoxAudioGeneratorFactory _audioGeneratorFactory;

        public SpiritBoxResponseGenerator(
            ISpiritBoxTextResponseGenerator textGenerator, 
            ISpiritBoxAudioGeneratorFactory audioGeneratorFactory
        )
        {
            _textGenerator = textGenerator;
            _audioGeneratorFactory = audioGeneratorFactory;
        }

        public async Task<SpiritBoxResponse> GenerateAsync(SpiritBoxRequest request, CancellationToken cancellationToken = default)
        {
            var audioGenerator = _audioGeneratorFactory.Create(request.ResponseType);
            var textResponse = await _textGenerator.RespondAsync(request.Prompt, cancellationToken);

            using var audioStream = await audioGenerator.GenerateAsync(textResponse, cancellationToken);
            var memoryStream = new MemoryStream();
            await audioStream.CopyToAsync(memoryStream, cancellationToken);
            var audioBase64 = Convert.ToBase64String(memoryStream.ToArray());

            return new SpiritBoxResponse
            {
                Response = textResponse,
                Audio = audioBase64
            };
        }
    }
}
