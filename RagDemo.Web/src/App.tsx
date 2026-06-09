import { useState } from 'react'
import UrlIngest from './components/UrlIngest'
import ChatWindow from './components/ChatWindow'

const SESSION_KEY = 'ragdemo-session-id'
const INGEST_KEY = 'ragdemo-ingest-done'

function getOrCreateSessionId() {
  const stored = localStorage.getItem(SESSION_KEY)
  if (stored) return stored
  const id = crypto.randomUUID()
  localStorage.setItem(SESSION_KEY, id)
  return id
}

function App() {
  const [sessionId, setSessionId] = useState(getOrCreateSessionId)
  const [ingestDone, setIngestDone] = useState(() => localStorage.getItem(INGEST_KEY) === 'true')
  const [isClearing, setIsClearing] = useState(false)

  function handleIngestComplete() {
    localStorage.setItem(INGEST_KEY, 'true')
    setIngestDone(true)
  }

  async function handleClear() {
    setIsClearing(true)
    try {
      await fetch(`/api/status/${sessionId}`, { method: 'DELETE' })
    } catch {}

    // Generate a fresh session
    localStorage.removeItem(INGEST_KEY)
    const newId = crypto.randomUUID()
    localStorage.setItem(SESSION_KEY, newId)
    setSessionId(newId)
    setIngestDone(false)
    setIsClearing(false)
  }

  return (
    <div className="min-h-screen flex flex-col">
      <header className="bg-white border-b border-gray-200 px-6 py-4 shadow-sm">
        <div className="max-w-7xl mx-auto flex items-center gap-3">
          <div className="w-8 h-8 bg-indigo-600 rounded-lg flex items-center justify-center">
            <span className="text-white text-sm font-bold">R</span>
          </div>
          <div>
            <h1 className="text-lg font-semibold text-gray-900">RagDemo</h1>
            <p className="text-xs text-gray-500">Retrieval Augmented Generation</p>
          </div>
          <div className="ml-auto flex items-center gap-3">
            {ingestDone && (
              <button
                onClick={handleClear}
                disabled={isClearing}
                className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-red-600 border border-red-200 rounded-lg hover:bg-red-50 disabled:opacity-50 transition-colors"
              >
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-3.5 h-3.5">
                  <path fillRule="evenodd" d="M8.75 1A2.75 2.75 0 006 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 10.23 1.482l.149-.022.841 10.518A2.75 2.75 0 007.596 19h4.807a2.75 2.75 0 002.742-2.53l.841-10.52.149.023a.75.75 0 00.23-1.482A41.03 41.03 0 0014 4.193V3.75A2.75 2.75 0 0011.25 1h-2.5zM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4zM8.58 7.72a.75.75 0 00-1.5.06l.3 7.5a.75.75 0 101.5-.06l-.3-7.5zm4.34.06a.75.75 0 10-1.5-.06l-.3 7.5a.75.75 0 101.5.06l.3-7.5z" clipRule="evenodd" />
                </svg>
                {isClearing ? 'Clearing…' : 'Clear data'}
              </button>
            )}
            <span className="text-xs text-gray-400 font-mono">session: {sessionId.slice(0, 8)}…</span>
          </div>
        </div>
      </header>

      <main className="flex-1 flex max-w-7xl mx-auto w-full p-4 gap-4">
        <div className="w-96 flex-shrink-0">
          <UrlIngest sessionId={sessionId} onIngestComplete={handleIngestComplete} />
        </div>
        <div className="flex-1">
          <ChatWindow sessionId={sessionId} isReady={ingestDone} />
        </div>
      </main>
    </div>
  )
}

export default App
