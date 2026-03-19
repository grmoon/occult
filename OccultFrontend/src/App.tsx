import { useRef, useState } from 'react'
import './App.css'

const API_URL = `${import.meta.env.VITE_API_HOST ?? ''}/api/SpiritBoxTrigger`

function App() {
  const [question, setQuestion] = useState('')
  const [response, setResponse] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const audioRef = useRef<HTMLAudioElement | null>(null)

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
        body: JSON.stringify({ prompt: question.trim() }),
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

      {/* Spirit Box */}
      <div className="spirit-box">
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
          {response && <p className="spirit-message">{response}</p>}
        </div>
      </div>
    </>
  )
}

export default App
