import { useRef, useState } from 'react'
import './App.css'

const API_URL = `${import.meta.env.VITE_API_HOST ?? ''}/api/SpiritBoxTrigger`
const isDev = import.meta.env.DEV

type ResponseType = 'Orthodox' | 'Heterodox'

function App() {
  const [question, setQuestion] = useState('')
  const [response, setResponse] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [responseType, setResponseType] = useState<ResponseType>('Orthodox')
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [audioUnlocked, setAudioUnlocked] = useState(false)
  const audioRef = useRef<HTMLAudioElement | null>(null)

  function unlockAudio() {
    // Create an AudioContext during a user gesture to unlock audio playback
    const ctx = new AudioContext()
    const buffer = ctx.createBuffer(1, 1, 22050)
    const source = ctx.createBufferSource()
    source.buffer = buffer
    source.connect(ctx.destination)
    source.start()
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

    setLoading(true)
    setResponse(null)
    setError(null)

    try {
      const res = await fetch(API_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ prompt: question.trim(), responseType }),
      })

      if (!res.ok) throw new Error()
      const data = await res.json()
      setResponse(data.response)

      if (data.audio) {
        const audio = new Audio(`data:audio/wav;base64,${data.audio}`)
        audioRef.current = audio
        audio.play()
      }
    } catch {
      setError('The connection falters… the spirits have withdrawn.')
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
            <button className="spirit-button" onClick={unlockAudio}>
              Open the Channel
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

        <div className={`spirit-response${response || error ? ' visible' : ''}${loading ? ' channeling' : ''}`}>
          {loading && <p className="spirit-static">⦁ ⦁ ⦁</p>}
          {error && <p className="spirit-error">{error}</p>}
          {isDev && response && <p className="spirit-message">{response}</p>}
        </div>
      </div>}
    </>
  )
}

export default App
