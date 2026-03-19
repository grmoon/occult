using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OccultApi.Services;


var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();


builder
    .Services
    .AddSingleton<ISpiritBoxAudioGetter, SpiritBoxAudioGetter>()
    .AddSingleton<ISpiritBoxAudioGenerator, SpiritBoxAudioGenerator>()
    .AddSingleton<ISpiritBoxResponseGenerator, SpiritBoxResponseGenerator>()
    .AddSingleton<ISpiritBoxTextResponseGenerator, SpiritBoxTextResponseGenerator>()
    .AddSingleton<TokenCredential, DefaultAzureCredential>()
    .AddSingleton(sp =>
    {
        var aiServicesEndpoint = new Uri("https://qc-agent-gm-ai-foundry-eastus2.services.ai.azure.com/");
        var credential = sp.GetRequiredService<TokenCredential>();

        return new AzureOpenAIClient(aiServicesEndpoint, credential);
    })
    .AddSingleton(sp =>
    {
        var deploymentName = "gpt-5-mini";
        var client = sp.GetRequiredService<AzureOpenAIClient>();

        return client.GetChatClient(deploymentName).AsIChatClient();
    })
    .AddSingleton<AIAgent>(sp =>
    {
        var chatClient = sp.GetRequiredService<IChatClient>();
        var instructions = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Prompts", "SpiritBoxAnswerInstructions.txt"));

        return chatClient.AsAIAgent(instructions: instructions);
    });

builder.Build().Run();
