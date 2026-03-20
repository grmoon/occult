using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Agents.AI;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OccultApi.Services;


var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();


var aiServicesEndpoint = new Uri(builder.Configuration["AiServicesEndpoint"]
    ?? throw new InvalidOperationException("AiServicesEndpoint is not configured."));

var aiSpeechRegion = builder.Configuration["AiSpeechRegion"]
    ?? throw new InvalidOperationException("AiSpeechRegion is not configured.");

var deploymentName = builder.Configuration["AiDeploymentName"]
    ?? throw new InvalidOperationException("AiDeploymentName is not configured.");

var audioStorageUri = new Uri(builder.Configuration["AudioStorageUri"]
    ?? throw new InvalidOperationException("AudioStorageUri is not configured."));

var orthodoxMinSeconds = float.Parse(builder.Configuration["OrthodoxMinSeconds"]
    ?? throw new InvalidOperationException("OrthodoxMinSeconds is not configured."));

var orthodoxMaxSeconds = float.Parse(builder.Configuration["OrthodoxMaxSeconds"]
    ?? throw new InvalidOperationException("OrthodoxMaxSeconds is not configured."));

var isDev = builder.Environment.IsDevelopment();

var managedIdentityClientId = isDev ? null : (builder.Configuration["ManagedIdentityClientId"]
    ?? throw new InvalidOperationException("ManagedIdentityClientId is not configured."));

builder
    .Services
    .AddSingleton(sp =>
    {
        var credential = sp.GetRequiredService<TokenCredential>();
        return new BlobContainerClient(audioStorageUri, credential);
    })
    .AddSingleton<ISpiritBoxAudioGetter, SpiritBoxAudioGetter>()
    .AddSingleton<ISpiritBoxAudioGeneratorFactory>(sp => new SpiritBoxAudioGeneratorFactory(
        audioGetter: sp.GetRequiredService<ISpiritBoxAudioGetter>(),
        speechConfig: sp.GetRequiredService<SpeechConfig>(),
        loggerFactory: sp.GetRequiredService<ILoggerFactory>(),
        textResponseGenerator: sp.GetRequiredService<ISpiritBoxTextResponseGenerator>(),
        orthodoxMinSeconds: orthodoxMinSeconds,
        orthodoxMaxSeconds: orthodoxMaxSeconds
    ))
    .AddSingleton<ISpiritBoxResponseGenerator, SpiritBoxResponseGenerator>()
    .AddSingleton<ISpiritBoxTextResponseGenerator, SpiritBoxTextResponseGenerator>()
    .AddSingleton(sp =>
    {
        var credential = sp.GetRequiredService<TokenCredential>();

        return SpeechConfig.FromEndpoint(aiServicesEndpoint, credential);
    })
    .AddSingleton<TokenCredential>(_ => isDev
        ? new AzureCliCredential()
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
