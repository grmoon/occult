# Occult — Spirit Box

A paranormal-themed web app that simulates a spirit box. Users ask questions and receive cryptic, AI-generated audio responses from "the other side."

## Architecture

The project is a two-part solution:

| Project | Tech | Hosted On |
|---|---|---|
| **[OccultApi](OccultApi/)** | .NET 10 Azure Functions (isolated worker) | Azure Function App |
| **[OccultFrontend](OccultFrontend/)** | React 19 + TypeScript + Vite | Azure Static Web Apps |

### How It Works

1. The frontend presents a spirit box interface and prompts the user to enable audio.
2. The user types a question and submits it.
3. The backend receives the prompt and uses **Azure OpenAI** (GPT-4.1) to generate a short, cryptic text response guided by a system prompt that enforces an eerie spirit-box persona.
4. The backend generates or retrieves an audio clip:
   - **Orthodox mode** — A random pre-recorded audio clip is pulled from **Azure Blob Storage**.
   - **Heterodox mode** — **Azure AI Speech** synthesizes audio matching the text response.
5. The response (optional text + base64-encoded audio) is returned to the frontend, which auto-plays the audio.

### Azure Services

- **Azure AI Foundry** — Hosts the OpenAI model deployment and Speech service
- **Azure Blob Storage** — Stores pre-recorded audio clips for Orthodox mode
- **Azure Static Web Apps** — Hosts the frontend
- **Azure Function App** — Hosts the API
- **Application Insights** — Telemetry and logging
- **Managed Identity** — Passwordless authentication to all Azure services in production

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-tools)
- [Node.js](https://nodejs.org/) (LTS)
- [Azure CLI](https://learn.microsoft.com/cli/azure/) (for deployment and local credential auth)

## Getting Started

### Backend (OccultApi)

1. Copy the example settings file:

   ```sh
   cp OccultApi/local.settings.example.json OccultApi/local.settings.json
   ```

2. Authenticate with Azure (used for local credential-based access to AI services and storage):

   ```sh
   az login
   ```

3. Start the function app:

   ```sh
   cd OccultApi
   func start
   ```

   The API will be available at `http://localhost:7071`.

### Frontend (OccultFrontend)

1. Install dependencies:

   ```sh
   cd OccultFrontend
   npm install
   ```

2. Create a `.env.local` file pointing at the local API:

   ```
   VITE_API_HOST=http://localhost:7071
   ```

3. Start the dev server:

   ```sh
   npm run dev
   ```

   The app will be available at `http://localhost:5173`.

## Deployment

Each project has a PowerShell deployment script:

- **API:** [`OccultApi/Scripts/Deploy.ps1`](OccultApi/Scripts/Deploy.ps1) — Publishes to the Azure Function App and configures app settings and CORS.
- **Frontend:** [`OccultFrontend/scripts/Deploy.ps1`](OccultFrontend/scripts/Deploy.ps1) — Builds with the production API host and deploys to Azure Static Web Apps via the SWA CLI.

## Project Structure

```
Occult/
├── Occult.slnx                  # Solution file
├── OccultApi/                   # Azure Functions backend
│   ├── Models/                  # Request/response records
│   ├── Services/                # Response generation, audio, and speech services
│   ├── Triggers/                # HTTP trigger (SpiritBoxTrigger)
│   ├── Prompts/                 # System prompt for the AI spirit persona
│   ├── Audio/                   # Local audio assets
│   ├── Scripts/                 # Deployment script
│   └── Program.cs               # Host builder and DI configuration
└── OccultFrontend/              # React + Vite frontend
    ├── src/                     # Application source
    ├── public/                  # Static assets
    ├── scripts/                 # Deployment script
    └── vite.config.ts           # Vite configuration
```
