using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OccultApi.Models;
using OccultApi.Services;

namespace OccultApi.Triggers;

public class SpiritBoxTrigger
{
    private readonly ILogger<SpiritBoxTrigger> _logger;
    private readonly ISpiritBoxResponseGenerator _responseGenerator;

    public SpiritBoxTrigger(ILogger<SpiritBoxTrigger> logger, ISpiritBoxResponseGenerator responseGenerator)
    {
        _logger = logger;
        _responseGenerator = responseGenerator;
    }

    [Function(nameof(SpiritBoxTrigger))]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        var request = await req.ReadFromJsonAsync<SpiritBoxRequest>();

        if (request is null)
        {
            return new BadRequestObjectResult("Invalid request body.");
        }

        _logger.LogInformation("Spirit box received prompt: {Prompt}", request.Prompt);

        var response = await _responseGenerator.GenerateAsync(request.Prompt);

        return new OkObjectResult(response);
    }
}