import { useState, useRef } from 'react'

interface Props {
  sessionId: string
  onIngestComplete: () => void
}

interface ProgressEvent {
  stage: string
  message: string
  pagesFound?: number
  pagesProcessed?: number
  chunksStored?: number
  isComplete?: boolean
  error?: string
}

export default function UrlIngest({ sessionId, onIngestComplete }: Props) {
  const [url, setUrl] = useState('')
  const [maxPages, setMaxPages] = useState(10)
  const [isIngesting, setIsIngesting] = useState(false)
  const [progress, setProgress] = useState<ProgressEvent[]>([])
  const [stats, setStats] = useState<{ pages: number; chunks: number } | null>(null)
  const abortRef = useRef<AbortController | null>(null)
  const logEndRef = useRef<HTMLDivElement>(null)

  async function startIngest() {
    if (!url.trim()) return
    setIsIngesting(true)
    setProgress([])
    setStats(null)

    abortRef.current = new AbortController()

    try {
      const res = await fetch('/api/ingest', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ url: url.trim(), sessionId, maxPages }),
        signal: abortRef.current.signal
      })

      const reader = res.body!.getReader()
      const decoder = new TextDecoder()
      let buffer = ''

      while (true) {
        const { done, value } = await reader.read()
        if (done) break

        buffer += decoder.decode(value, { stream: true })
        const lines = buffer.split('\n')
        buffer = lines.pop() ?? ''

        for (const line of lines) {
          if (!line.startsWith('data: ')) continue
          const json = line.slice(6).trim()
          if (!json) continue

          try {
            const evt: ProgressEvent = JSON.parse(json)
            setProgress(prev => [...prev, evt])
            logEndRef.current?.scrollIntoView({ behavior: 'smooth' })

            if (evt.isComplete) {
              if (!evt.error) {
                setStats({ pages: evt.pagesFound ?? 0, chunks: evt.chunksStored ?? 0 })
                onIngestComplete()
              }
              setIsIngesting(false)
            }
          } catch {}
        }
      }
    } catch (err: unknown) {
      if (err instanceof Error && err.name !== 'AbortError') {
        setProgress(prev => [...prev, { stage: 'error', message: String(err), isComplete: true }])
      }
      setIsIngesting(false)
    }
  }

  function stopIngest() {
    abortRef.current?.abort()
    setIsIngesting(false)
  }

  const stageIcon: Record<string, string> = {
    init: '⚙',
    crawling: '🔗',
    chunking: '✂',
    embedding: '🧠',
    storing: '💾',
    complete: '✅',
    error: '❌'
  }

  return (
    <div className="bg-white rounded-xl border border-gray-200 shadow-sm flex flex-col h-full">
      <div className="px-5 py-4 border-b border-gray-100">
        <h2 className="font-semibold text-gray-800">Ingest Website</h2>
        <p className="text-xs text-gray-500 mt-0.5">Crawl a URL and index it for Q&amp;A</p>
      </div>

      <div className="p-5 space-y-3">
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Website URL</label>
          <input
            type="url"
            value={url}
            onChange={e => setUrl(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && !isIngesting && startIngest()}
            placeholder="https://example.com"
            disabled={isIngesting}
            className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent disabled:opacity-50 disabled:bg-gray-50"
          />
        </div>

        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">
            Max pages <span className="font-normal text-gray-400">— {maxPages} page{maxPages !== 1 ? 's' : ''}</span>
          </label>
          <input
            type="range"
            min={1}
            max={50}
            value={maxPages}
            onChange={e => setMaxPages(Number(e.target.value))}
            disabled={isIngesting}
            className="w-full accent-indigo-600 disabled:opacity-50"
          />
          <div className="flex justify-between text-xs text-gray-400 mt-0.5">
            <span>1</span><span>25</span><span>50</span>
          </div>
        </div>

        <div className="flex gap-2">
          <button
            onClick={startIngest}
            disabled={isIngesting || !url.trim()}
            className="flex-1 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
          >
            {isIngesting ? 'Ingesting…' : 'Start Ingest'}
          </button>
          {isIngesting && (
            <button
              onClick={stopIngest}
              className="px-4 py-2 text-sm font-medium border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
            >
              Stop
            </button>
          )}
        </div>

        {stats && (
          <div className="grid grid-cols-2 gap-2">
            <div className="bg-indigo-50 rounded-lg p-3 text-center">
              <div className="text-2xl font-bold text-indigo-700">{stats.pages}</div>
              <div className="text-xs text-indigo-500 mt-0.5">Pages</div>
            </div>
            <div className="bg-emerald-50 rounded-lg p-3 text-center">
              <div className="text-2xl font-bold text-emerald-700">{stats.chunks}</div>
              <div className="text-xs text-emerald-500 mt-0.5">Chunks</div>
            </div>
          </div>
        )}
      </div>

      {progress.length > 0 && (
        <div className="flex-1 overflow-y-auto mx-5 mb-5 bg-gray-50 rounded-lg border border-gray-200 p-3 space-y-1.5 max-h-72 text-xs font-mono">
          {progress.map((p, i) => (
            <div key={i} className={`flex gap-2 ${p.stage === 'error' ? 'text-red-600' : 'text-gray-700'}`}>
              <span>{stageIcon[p.stage] ?? '•'}</span>
              <span className="flex-1 break-all">{p.message}</span>
            </div>
          ))}
          <div ref={logEndRef} />
        </div>
      )}
    </div>
  )
}
