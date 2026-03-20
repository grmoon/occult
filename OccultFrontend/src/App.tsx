import { useEffect, useRef, useState } from 'react'
import './App.css'

const API_URL = `${import.meta.env.VITE_API_HOST ?? ''}/api/SpiritBoxTrigger`
const isDev = import.meta.env.DEV

type ResponseType = 'Orthodox' | 'Heterodox'

interface LoopingAudio {
  source: AudioBufferSourceNode
  gain: GainNode
}

/** Fetch a raw ArrayBuffer from a URL */
async function fetchAudioData(url: string): Promise<ArrayBuffer> {
  const res = await fetch(url)
  return res.arrayBuffer()
}

/** Start a gapless-looping buffer source at the given volume, optionally at a random offset */
function startLoop(ctx: AudioContext, buffer: AudioBuffer, volume: number, randomOffset = false): LoopingAudio {
  const source = ctx.createBufferSource()
  source.buffer = buffer
  source.loop = true

  const gain = ctx.createGain()
  gain.gain.value = volume
  source.connect(gain).connect(ctx.destination)

  if (randomOffset) {
    source.start(0, Math.random() * buffer.duration)
  } else {
    source.start()
  }

  return { source, gain }
}

function App() {
  const [question, setQuestion] = useState('')
  const [response, setResponse] = useState<string | null>(null)
  const [hasResponse, setHasResponse] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [responseType, setResponseType] = useState<ResponseType>('Orthodox')
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [audioUnlocked, setAudioUnlocked] = useState(false)
  const [unlocking, setUnlocking] = useState(false)
  const audioRef = useRef<HTMLAudioElement | null>(null)
  const audioCtxRef = useRef<AudioContext | null>(null)
  const staticDataRef = useRef<ArrayBuffer | null>(null)
  const corruptedDataRef = useRef<ArrayBuffer | null>(null)
  const staticBufferRef = useRef<AudioBuffer | null>(null)
  const corruptedBufferRef = useRef<AudioBuffer | null>(null)
  const ambientRef = useRef<LoopingAudio | null>(null)
  const channelingRef = useRef<LoopingAudio | null>(null)
  const channelingStartRef = useRef<number>(0)

  // Pre-fetch raw audio data on mount (no user gesture needed)
  useEffect(() => {
    Promise.all([
      fetchAudioData('/static.mp3'),
      fetchAudioData('/corrupted.mp3'),
    ]).then(([staticData, corruptedData]) => {
      staticDataRef.current = staticData
      corruptedDataRef.current = corruptedData
    })
  }, [])

  async function unlockAudio() {
    setUnlocking(true)

    const ctx = new AudioContext()
    audioCtxRef.current = ctx

    // Decode pre-fetched audio data (fast since bytes are already in memory)
    const [staticBuffer, corruptedBuffer] = await Promise.all([
      ctx.decodeAudioData(staticDataRef.current ?? await fetchAudioData('/static.mp3')),
      ctx.decodeAudioData(corruptedDataRef.current ?? await fetchAudioData('/corrupted.mp3')),
      new Promise(r => setTimeout(r, 1000)), // Ensure "Opening…" shows for at least 1s
    ])
    staticBufferRef.current = staticBuffer
    corruptedBufferRef.current = corruptedBuffer

    // Start ambient static loop at a random position
    ambientRef.current = startLoop(ctx, staticBuffer, 0.01, true)

    setAudioUnlocked(true)
  }

  async function handleSubmit(e: React.SubmitEvent<HTMLFormElement>) {
    e.preventDefault()
    if (!question.trim() || loading) return

    // Stop any currently playing audio
    if (audioRef.current) {
      audioRef.current.pause()
      audioRef.current = null
    }

    const ctx = audioCtxRef.current!

    setLoading(true)
    setResponse(null)
    setHasResponse(false)
    setError(null)

    // Mute ambient static while channeling
    if (ambientRef.current) {
      ambientRef.current.gain.gain.value = 0
    }

    // Start gapless corrupted loop
    channelingRef.current = startLoop(ctx, corruptedBufferRef.current!, 0.0025, true)
    channelingStartRef.current = Date.now()

    try {
      const res = await fetch(API_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ prompt: question.trim(), responseType }),
      })

      if (!res.ok) throw new Error()
      const data = await res.json()
      setResponse(data.response ?? null)
      setHasResponse(true)

      // Calculate how long channeling played before the response
      const channelingDuration = Date.now() - channelingStartRef.current

      if (data.audio) {
        const audio = new Audio(`data:audio/wav;base64,${data.audio}`)
        audioRef.current = audio
        audioRef.current.volume = 0.003
        audio.addEventListener('ended', () => {
          // After API audio ends, keep channeling for the same leading duration, then resume static
          setTimeout(() => {
            if (channelingRef.current) {
              channelingRef.current.source.stop()
              channelingRef.current = null
            }
            if (ambientRef.current) {
              ambientRef.current.gain.gain.value = 0.01
            }
          }, channelingDuration)
        })
        audio.play()
      } else {
        // No spirit box audio — stop channeling and resume ambient static
        if (channelingRef.current) {
          channelingRef.current.source.stop()
          channelingRef.current = null
        }
        if (ambientRef.current) {
          ambientRef.current.gain.gain.value = 0.01
        }
      }
    } catch {
      setError('The connection falters… the spirits have withdrawn.')
      // Stop channeling and resume ambient static on error
      if (channelingRef.current) {
        channelingRef.current.source.stop()
        channelingRef.current = null
      }
      if (ambientRef.current) {
        ambientRef.current.gain.gain.value = 0.01
      }
    } finally {
      setLoading(false)
      setQuestion('')
    }
  }

  return (
    <>
      {/* Animated background */}
      <div className="bg-container">
        <div className="bg-gradient" />
      </div>
      <div className="bg-noise" />

      {/* Dev settings */}
      {isDev && (
        <div className={`dev-settings${settingsOpen ? ' open' : ''}`}>
          <button className="dev-settings-toggle" onClick={() => setSettingsOpen(!settingsOpen)}>
            {settingsOpen ? '✕' : '⚙'}
          </button>
          {settingsOpen && (
            <div className="dev-settings-panel">
              <span className="dev-settings-label">Response Type</span>
              <div className="dev-toggle">
                <button
                  className={`dev-toggle-option${responseType === 'Orthodox' ? ' active' : ''}`}
                  onClick={() => setResponseType('Orthodox')}
                >
                  Orthodox
                </button>
                <button
                  className={`dev-toggle-option${responseType === 'Heterodox' ? ' active' : ''}`}
                  onClick={() => setResponseType('Heterodox')}
                >
                  Heterodox
                </button>
              </div>
              <p className="dev-settings-desc">
                {responseType === 'Orthodox'
                  ? 'Audio is randomized — no attempt to match the text.'
                  : 'Audio is matched to the sound of the text output.'}
              </p>
            </div>
          )}
        </div>
      )}

      {/* Audio unlock gate */}
      {!audioUnlocked && (
        <div className="audio-gate">
          <div className="audio-gate-content">
            <h1 className="spirit-title">Spirit Box</h1>
            <p className="audio-gate-text">The spirits communicate through sound.<br />Allow audio to open the channel.</p>
            <button className={`spirit-button${unlocking ? ' opening' : ''}`} onClick={unlockAudio} disabled={unlocking}>
              {unlocking ? 'Opening…' : 'Open the Channel'}
            </button>
          </div>
        </div>
      )}

      {/* Spirit Box */}
      {audioUnlocked && <div className="spirit-box">
        <h1 className="spirit-title">Spirit Box</h1>
        <p className="spirit-subtitle">Speak into the void. Something may answer.</p>

        <form className="spirit-form" onSubmit={handleSubmit}>
          <input
            className="spirit-input"
            type="text"
            placeholder="Ask the spirits…"
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            disabled={loading}
            autoFocus
          />
          <button className="spirit-button" type="submit" disabled={loading || !question.trim()}>
            {loading ? 'Channeling…' : 'Transmit'}
          </button>
        </form>

        <div className={`spirit-response${hasResponse || error ? ' visible' : ''}${loading ? ' channeling' : ''}`}>
          {loading && <p className="spirit-static">⦁ ⦁ ⦁</p>}
          {error && <p className="spirit-error">{error}</p>}
          {isDev && response && <p className="spirit-message">{response}</p>}
        </div>
      </div>}
    </>
  )
}

export default App
