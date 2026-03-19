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


var aiServicesEndpoint = new Uri(builder.Configuration["AiServicesEndpoint"]
    ?? throw new InvalidOperationException("AiServicesEndpoint is not configured."));

var deploymentName = builder.Configuration["AiDeploymentName"]
    ?? throw new InvalidOperationException("AiDeploymentName is not configured.");

var isDev = builder.Environment.IsDevelopment();

var managedIdentityClientId = isDev ? null : (builder.Configuration["ManagedIdentityClientId"]
    ?? throw new InvalidOperationException("ManagedIdentityClientId is not configured."));

builder
    .Services
    .AddSingleton<ISpiritBoxAudioGetter, SpiritBoxAudioGetter>()
    .AddSingleton<ISpiritBoxAudioGeneratorFactory, SpiritBoxAudioGeneratorFactory>()
    .AddSingleton<ISpiritBoxResponseGenerator, SpiritBoxResponseGenerator>()
    .AddSingleton<ISpiritBoxTextResponseGenerator, SpiritBoxTextResponseGenerator>()
    .AddSingleton<TokenCredential>(_ => isDev
        ? new DefaultAzureCredential()
        : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId!)))
    .AddSingleton(sp =>
    {
        var credential = sp.GetRequiredService<TokenCredential>();

        return new AzureOpenAIClient(aiServicesEndpoint, credential);
    })
    .AddSingleton(sp =>
    {
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
