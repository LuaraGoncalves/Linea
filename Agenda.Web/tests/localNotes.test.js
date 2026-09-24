import test from 'node:test'
import assert from 'node:assert/strict'
import { createLocalNoteStore, LOCAL_NOTES_KEY, normalizeLocalPayload } from '../src/services/localNotes.js'

const payload = { title: ' Tarefa ', body: ' Texto ', noteDate: '2026-09-24', noteTime: '09:30', color: 'paper', isCompleted: false }
function storage() {
  const data = new Map()
  return { getItem: key => data.get(key) ?? null, setItem: (key, value) => data.set(key, value) }
}
test('local CRUD persists across store instances without an account', () => {
  const data = storage()
  const store = createLocalNoteStore(() => data)
  assert.deepEqual(store.list(), [])
  const first = store.save(null, payload)
  assert.equal(first.title, 'Tarefa')
  const reopened = createLocalNoteStore(() => data)
  assert.equal(reopened.list()[0].id, first.id)
  const updated = reopened.save(first.id, { ...payload, body: 'Editado', isCompleted: true }, first.revision)
  assert.equal(store.list()[0].isCompleted, true)
  assert.notEqual(updated.revision, first.revision)
  store.remove(updated.id, updated.revision)
  assert.deepEqual(reopened.list(), [])
})
test('mutations keep unrelated notes written by another tab', () => {
  const data = storage()
  const a = createLocalNoteStore(() => data)
  const b = createLocalNoteStore(() => data)
  const first = a.save(null, payload)
  const second = b.save(null, { ...payload, title: 'Outra aba' })
  a.remove(first.id, first.revision)
  assert.equal(b.list()[0].id, second.id)
})
test('stale writes and transfers cannot remove a newer local revision', () => {
  const data = storage()
  const store = createLocalNoteStore(() => data)
  const first = store.save(null, payload)
  store.save(first.id, { ...payload, body: 'Novo' }, first.revision)
  assert.throws(() => store.save(first.id, payload, first.revision), /outra aba/)
  assert.throws(() => store.remove(first.id, first.revision), /outra aba/)
  assert.equal(store.list()[0].body, 'Novo')
})
test('corrupt or unknown storage is never overwritten', () => {
  for (const original of ['invalid', '{"version":2,"notes":[]}', '{"version":1,"notes":[{}]}']) {
    const data = storage()
    data.setItem(LOCAL_NOTES_KEY, original)
    const store = createLocalNoteStore(() => data)
    assert.throws(() => store.list(), /preservados/)
    assert.throws(() => store.save(null, payload), /preservados/)
    assert.equal(data.getItem(LOCAL_NOTES_KEY), original)
  }
})
test('quota failures keep the previous saved note', () => {
  const data = storage()
  const store = createLocalNoteStore(() => data)
  const note = store.save(null, payload)
  data.setItem = () => { throw new Error('quota') }
  assert.throws(() => store.save(note.id, { ...payload, body: 'Novo' }, note.revision), /Não foi possível salvar/)
  assert.equal(store.list()[0].body, 'Texto')
})
test('blocked storage and invalid payloads do not report success', () => {
  const blocked = createLocalNoteStore(() => { throw new Error('blocked') })
  assert.throws(() => blocked.list(), /bloqueou/)
  const store = createLocalNoteStore(() => storage())
  assert.throws(() => store.save(null, { ...payload, title: 'a'.repeat(101) }), /Confira/)
})
test('normalization matches server title fallback and time precision', () => {
  assert.deepEqual(normalizeLocalPayload({ ...payload, title: '', noteTime: '09:30:00' }), {
    ...payload, title: 'Sem titulo', body: 'Texto'
  })
})
