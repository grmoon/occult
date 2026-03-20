# Occult Frontend

The frontend for **Spirit Box** — a web app that lets users ask questions to an AI-powered spirit box and receive cryptic audio responses from the other side.

Built with React, TypeScript, and Vite. Deployed as an Azure Static Web App.

## Tech Stack

- **React 19** with TypeScript
- **Vite 8** for dev server and builds
- **React Compiler** via Babel plugin for optimized rendering
- **Fluent UI React Components** for UI primitives

## Prerequisites

- [Node.js](https://nodejs.org/) (LTS recommended)
- The [OccultApi](../OccultApi/) backend running locally (or a deployed instance)

## Getting Started

1. **Install dependencies:**

   ```sh
   npm install
   ```

2. **Configure the API host (optional):**

   During local development the app defaults to the current origin for API calls. To point at a different backend, create a `.env.local` file:

   ```
   VITE_API_HOST=http://localhost:7071
   ```

3. **Start the dev server:**

   ```sh
   npm run dev
   ```

   The app will be available at `http://localhost:5173`.

## Scripts

| Script | Description |
|---|---|
| `npm run dev` | Start the Vite dev server with HMR |
| `npm run build` | Type-check and build for production |
| `npm run preview` | Preview the production build locally |
| `npm run lint` | Run ESLint |

## How It Works

1. The user is prompted to unlock audio (required by browsers for autoplay).
2. Once unlocked, the user types a question and submits it.
3. The frontend POSTs the question to the `/api/SpiritBoxTrigger` endpoint on the backend.
4. The backend generates a cryptic text response (via Azure OpenAI) and an audio clip (via Azure AI Speech), returning both in the response.
5. The frontend auto-plays the audio. In dev mode, the text response is also displayed on screen.

### Response Types (Dev Only)

A settings panel is available in development mode to toggle between two response types:

- **Orthodox** — Audio is selected randomly from a pool of pre-recorded clips; no attempt to match the text.
- **Heterodox** — Audio is synthesized to match the text of the response.

## Deployment

The included PowerShell script deploys the built app to Azure Static Web Apps:

```powershell
.\scripts\Deploy.ps1
```

This will install dependencies, build the project with the production `VITE_API_HOST`, and deploy the `dist/` output using the SWA CLI.

## Project Structure

```
OccultFrontend/
├── public/              # Static assets
├── scripts/
│   └── Deploy.ps1       # Azure SWA deployment script
├── src/
│   ├── App.tsx          # Main application component
│   ├── App.css          # Application styles
│   ├── main.tsx         # Entry point
│   └── assets/          # Bundled assets
├── index.html           # HTML entry point
├── vite.config.ts       # Vite configuration
├── tsconfig.json        # TypeScript configuration
└── package.json
```
