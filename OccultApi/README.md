# OccultApi

An Azure Functions API that simulates a paranormal **spirit box** — an audio device purported to channel fragmented voices from beyond. Given a text prompt, the API generates eerie, spliced audio responses using AI text generation, text-to-speech synthesis, and MP3 audio manipulation.

## Architecture

```
POST /api/SpiritBoxTrigger
        │
        ▼
SpiritBoxResponseGenerator
        │
        ├─ SpiritBoxAudioGeneratorFactory
        │       │
        │       ├─ Heterodox ── AI text response ─► TTS ─► segment ─► similarity-match against MP3s ─► WAV
        │       │
        │       └─ Orthodox ─── random duration ─► segment ─► random MP3 chunks ─► WAV
        │
        └─ SpiritBoxResponse { Response, Audio (base64 WAV) }
```

### Response Types

| Type | Behaviour |
|---|---|
| **Heterodox** | Sends the prompt to an AI agent for a cryptic text response, synthesizes it to speech via Azure Speech, segments the PCM audio, then finds similar-sounding sections in random MP3 files using energy-envelope cross-correlation. Returns both the text response and the stitched WAV. |
| **Orthodox** | Picks a random total duration (10–20 s), splits it into 1–5 s segments, and extracts random chunks from MP3 files in blob storage. Returns only the stitched WAV (no text response). |

## API

### `POST /api/SpiritBoxTrigger`

**Request body:**

```json
{
  "prompt": "Is anyone there?",
  "responseType": "heterodox"
}
```

`responseType` is case-insensitive. Accepted values: `heterodox`, `orthodox`.

**Response body:**

```json
{
  "response": "You already know the answer. The cold knows it too.",
  "audio": "<base64-encoded WAV>"
}
```

`response` is `null` for the `orthodox` type.

## Configuration

All custom settings go in the `Values` section of `local.settings.json` (locally) or as Application Settings in the Azure Function App.

| Setting | Required | Description |
|---|---|---|
| `AiServicesEndpoint` | Yes | Endpoint URL for the Azure AI Services multi-service resource (e.g. `https://<name>.services.ai.azure.com/`). Used for both OpenAI chat completions and Speech synthesis. |
| `AiDeploymentName` | Yes | The model deployment name for chat completions (e.g. `gpt-4.1`). |
| `AiSpeechRegion` | Yes | Azure region of the AI Services resource (e.g. `westus`). Required by the Speech SDK for token-based auth. |
| `AudioStorageUri` | Yes | Full URI to the blob container holding MP3 audio files (e.g. `https://<account>.blob.core.windows.net/<container>`). |
| `ManagedIdentityClientId` | Prod only | Client ID of the user-assigned managed identity. Not required in development (the app uses Azure CLI credentials instead). |

**Example `local.settings.json`:**

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AiServicesEndpoint": "https://<name>.services.ai.azure.com/",
    "AiDeploymentName": "gpt-4.1",
    "AiSpeechRegion": "westus",
    "AudioStorageUri": "https://<account>.blob.core.windows.net/<container>"
  },
  "Host": {
    "CORS": "http://localhost:4280,http://localhost:3000,http://localhost:5173",
    "CORSCredentials": true
  }
}
```

## Authentication

| Environment | Credential |
|---|---|
| Development | `AzureCliCredential` — uses your `az login` session |
| Production | `ManagedIdentityCredential` — user-assigned managed identity specified by `ManagedIdentityClientId` |

## Required Azure Resources & RBAC Roles

The identity (your Azure CLI user for dev, the managed identity for prod) must be granted the following **minimum** roles:

| Azure Resource | Role | Reason |
|---|---|---|
| **Azure AI Services** (multi-service) | `Cognitive Services OpenAI User` | Chat completions via Azure OpenAI |
| **Azure AI Services** (multi-service) | `Cognitive Services Speech User` | Text-to-speech synthesis via the Speech SDK |
| **Storage Account** (blob) | `Storage Blob Data Reader` | List and read MP3 audio files from the blob container |

### Resource Summary

| Resource | Purpose |
|---|---|
| Azure AI Services (AI Foundry) | Hosts the OpenAI model deployment and Speech service behind a single endpoint |
| Azure Storage Account | Stores MP3 audio files used as source material for spirit box audio generation |
| Azure Function App (.NET 10, isolated worker) | Hosts the API |
| Application Insights | Telemetry and logging |

## Local Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (for local `AzureWebJobsStorage`)
- Azure CLI (`az login` for credential-based access to Azure resources)

### Run

```bash
cd OccultApi
func start
```

The MP3 audio files must already exist in the blob container referenced by `AudioStorageUri`. Your Azure CLI identity must have the RBAC roles listed above on the corresponding resources.
