import { useState, useRef, useEffect } from 'react'
import ReactMarkdown from 'react-markdown'

interface Props {
  sessionId: string
  isReady: boolean
}

interface Message {
  role: 'user' | 'assistant'
  content: string
  isStreaming?: boolean
}

export default function ChatWindow({ sessionId, isReady }: Props) {
  const [messages, setMessages] = useState<Message[]>([])
  const [input, setInput] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const bottomRef = useRef<HTMLDivElement>(null)
  const abortRef = useRef<AbortController | null>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  async function sendMessage() {
    const question = input.trim()
    if (!question || isLoading) return

    setInput('')
    setMessages(prev => [...prev, { role: 'user', content: question }])
    setIsLoading(true)

    const assistantIndex = messages.length + 1
    setMessages(prev => [...prev, { role: 'assistant', content: '', isStreaming: true }])

    abortRef.current = new AbortController()

    try {
      const res = await fetch('/api/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sessionId, question }),
        signal: abortRef.current.signal
      })

      if (!res.ok) throw new Error(`API error: ${res.status}`)

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
          const data = line.slice(6).trim()
          if (data === '[DONE]') break

          try {
            const evt = JSON.parse(data)
            if (evt.token) {
              setMessages(prev => {
                const updated = [...prev]
                updated[assistantIndex] = {
                  ...updated[assistantIndex],
                  content: (updated[assistantIndex]?.content ?? '') + evt.token
                }
                return updated
              })
            }
          } catch {}
        }
      }

      setMessages(prev => {
        const updated = [...prev]
        if (updated[assistantIndex]) updated[assistantIndex] = { ...updated[assistantIndex], isStreaming: false }
        return updated
      })
    } catch (err: unknown) {
      if (err instanceof Error && err.name !== 'AbortError') {
        setMessages(prev => {
          const updated = [...prev]
          updated[assistantIndex] = {
            role: 'assistant',
            content: `Error: ${err.message}`,
            isStreaming: false
          }
          return updated
        })
      }
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="bg-white rounded-xl border border-gray-200 shadow-sm flex flex-col h-full">
      <div className="px-5 py-4 border-b border-gray-100">
        <h2 className="font-semibold text-gray-800">Chat</h2>
        <p className="text-xs text-gray-500 mt-0.5">
          {isReady ? 'Ask questions about the ingested content' : 'Ingest a website first to start chatting'}
        </p>
      </div>

      <div className="flex-1 overflow-y-auto p-5 space-y-4">
        {messages.length === 0 && (
          <div className="flex flex-col items-center justify-center h-full text-center text-gray-400 py-16">
            <div className="text-4xl mb-3">💬</div>
            <p className="text-sm font-medium">No messages yet</p>
            <p className="text-xs mt-1">
              {isReady ? 'Ask your first question below' : 'Ingest a website to get started'}
            </p>
          </div>
        )}

        {messages.map((msg, i) => (
          <div key={i} className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}>
            <div
              className={`max-w-[80%] rounded-2xl px-4 py-3 text-sm leading-relaxed ${
                msg.role === 'user'
                  ? 'bg-indigo-600 text-white rounded-br-sm'
                  : 'bg-gray-100 text-gray-800 rounded-bl-sm'
              }`}
            >
              {msg.role === 'user' ? (
                <span>{msg.content}</span>
              ) : (
                <div className="prose prose-sm prose-gray max-w-none
                  prose-headings:font-semibold prose-headings:text-gray-900
                  prose-h1:text-base prose-h2:text-base prose-h3:text-sm
                  prose-p:my-1.5 prose-p:leading-relaxed
                  prose-ul:my-1.5 prose-ul:pl-4 prose-li:my-0.5
                  prose-ol:my-1.5 prose-ol:pl-4
                  prose-strong:font-semibold prose-strong:text-gray-900
                  prose-a:text-indigo-600 prose-a:no-underline hover:prose-a:underline
                  prose-code:bg-gray-200 prose-code:px-1 prose-code:py-0.5 prose-code:rounded prose-code:text-xs prose-code:font-mono
                  prose-pre:bg-gray-800 prose-pre:text-gray-100 prose-pre:rounded-lg prose-pre:p-3 prose-pre:overflow-x-auto
                  prose-blockquote:border-l-2 prose-blockquote:border-gray-300 prose-blockquote:pl-3 prose-blockquote:italic prose-blockquote:text-gray-600">
                  <ReactMarkdown>{msg.content}</ReactMarkdown>
                  {msg.isStreaming && (
                    <span className="inline-block w-1 h-4 bg-gray-500 ml-0.5 animate-pulse rounded-sm" />
                  )}
                </div>
              )}
              {msg.role === 'user' && msg.isStreaming && (
                <span className="inline-block w-1 h-4 bg-current ml-0.5 animate-pulse rounded-sm" />
              )}
            </div>
          </div>
        ))}
        <div ref={bottomRef} />
      </div>

      <div className="p-4 border-t border-gray-100">
        <div className="flex gap-2">
          <input
            type="text"
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && !e.shiftKey && sendMessage()}
            placeholder={isReady ? 'Ask a question…' : 'Ingest a website first…'}
            disabled={!isReady || isLoading}
            className="flex-1 px-4 py-2.5 text-sm border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent disabled:opacity-50 disabled:bg-gray-50"
          />
          <button
            onClick={sendMessage}
            disabled={!isReady || isLoading || !input.trim()}
            className="px-4 py-2.5 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-xl transition-colors"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5">
              <path d="M3.478 2.405a.75.75 0 00-.926.94l2.432 7.905H13.5a.75.75 0 010 1.5H4.984l-2.432 7.905a.75.75 0 00.926.94 60.519 60.519 0 0018.445-8.986.75.75 0 000-1.218A60.517 60.517 0 003.478 2.405z" />
            </svg>
          </button>
        </div>
      </div>
    </div>
  )
}
