import { test, afterEach } from 'node:test'
import assert from 'node:assert/strict'
import { listNotes, login, updateNote, getSavedSession, importLocalNote } from '../src/services/api.js'

const originalFetch = globalThis.fetch
const originalStorage = globalThis.localStorage
afterEach(() => { globalThis.fetch = originalFetch; globalThis.localStorage = originalStorage })

test('updates with authorization and a cancellable request, without automatic retries', async () => {
  let calls = 0
  globalThis.fetch = async (url, options) => {
    calls++
    assert.equal(url, '/api/notes/note-1')
    assert.equal(options.headers.Authorization, 'Bearer token')
    assert.equal(options.method, 'PUT')
    assert.ok(options.signal instanceof AbortSignal)
    assert.deepEqual(JSON.parse(options.body), { title: 'Novo' })
    return new Response(JSON.stringify({ id: 'note-1', title: 'Novo' }))
  }
  assert.equal((await updateNote('token', 'note-1', { title: 'Novo' })).title, 'Novo')
  assert.equal(calls, 1)
})

test('reports unauthorized sessions with a status code', async () => {
  globalThis.fetch = async () => new Response('{}', { status: 401 })
  await assert.rejects(listNotes('old-token'), error => error.status === 401 && /sessão expirou/.test(error.message))
})

test('does not expose internal server errors', async () => {
  globalThis.fetch = async () => new Response('{"message":"Database internal details"}', { status: 500 })
  await assert.rejects(listNotes('token'), /serviço está indisponível/)
})

test('reports network and incomplete response failures', async () => {
  globalThis.fetch = async () => { throw new TypeError('Failed to fetch') }
  await assert.rejects(listNotes('token'), /Não conseguimos conectar/)
  globalThis.fetch = async () => new Response('<html>Unexpected response</html>')
  await assert.rejects(listNotes('token'), /resposta incompleta/)
})

test('external cancellation aborts fetch and settles the request', async () => {
  globalThis.fetch = (_, { signal }) => new Promise((resolve, reject) => {
    signal.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')))
  })
  const controller = new AbortController()
  const pending = listNotes('token', controller.signal)
  controller.abort()
  await assert.rejects(pending, /conexão demorou/)
})

test('login uses the same 60-second limit and does not retry', async () => {
  const originalTimeout = globalThis.setTimeout
  const originalClear = globalThis.clearTimeout
  let expire
  let calls = 0
  try {
    globalThis.setTimeout = (callback, ms) => { assert.equal(ms, 60000); expire = callback; return 1 }
    globalThis.clearTimeout = () => {}
    globalThis.fetch = (_, { signal }) => new Promise((resolve, reject) => {
      calls++
      signal.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')))
    })
    const pending = login({ email: 'test@example.test', password: 'Teste1' })
    expire()
    await assert.rejects(pending, /conexão demorou/)
    assert.equal(calls, 1)
  } finally {
    globalThis.setTimeout = originalTimeout
    globalThis.clearTimeout = originalClear
  }
})

test('malformed or inaccessible saved sessions return to signed-out state', () => {
  globalThis.localStorage = { getItem: key => key.endsWith('token') ? 'token' : '{"name":"Missing id"}' }
  assert.equal(getSavedSession(), null)
  globalThis.localStorage = { getItem() { throw new Error('Denied') } }
  assert.equal(getSavedSession(), null)
})

test('import sends a stable local id and explicit account authorization', async () => {
  const note = { title: 'Local', body: 'Texto' }
  let calls = 0
  globalThis.fetch = async (url, options) => {
    calls++
    assert.equal(url, '/api/notes/import')
    assert.equal(options.method, 'POST')
    assert.equal(options.headers.Authorization, 'Bearer account-token')
    assert.deepEqual(JSON.parse(options.body), { localId: 'local-id', note })
    return new Response(JSON.stringify({ ...note, id: 'cloud-id' }))
  }
  assert.equal((await importLocalNote('account-token', 'local-id', note)).id, 'cloud-id')
  assert.equal(calls, 1)
})

test('optional login can be cancelled before switching back to local notes', async () => {
  globalThis.fetch = (_, { signal }) => new Promise((resolve, reject) => {
    signal.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')))
  })
  const controller = new AbortController()
  const pending = login({ email: 'test@example.test', password: 'Teste1' }, controller.signal)
  controller.abort()
  await assert.rejects(pending, /conexão demorou/)
})
