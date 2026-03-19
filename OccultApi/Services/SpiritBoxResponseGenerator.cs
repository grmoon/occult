using OccultApi.Models;

namespace OccultApi.Services
{
    public class SpiritBoxResponseGenerator : ISpiritBoxResponseGenerator
    {
        private readonly ISpiritBoxTextResponseGenerator _textGenerator;
        private readonly ISpiritBoxAudioGenerator _audioGenerator;

        public SpiritBoxResponseGenerator(ISpiritBoxTextResponseGenerator textGenerator, ISpiritBoxAudioGenerator audioGenerator)
        {
            _textGenerator = textGenerator;
            _audioGenerator = audioGenerator;
        }

        public async Task<SpiritBoxResponse> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var textResponse = await _textGenerator.RespondAsync(prompt, cancellationToken);

            using var audioStream = await _audioGenerator.GenerateAsync(textResponse, cancellationToken);
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
