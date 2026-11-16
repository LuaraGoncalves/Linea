import test from 'node:test'
import assert from 'node:assert/strict'
import { createDraftStore, mergeDraft } from '../src/services/drafts.js'
import { LOCAL_DRAFT_SCOPE } from '../src/services/localNotes.js'
const base = { title: 'Original', body: 'Texto', noteDate: '2026-09-24', noteTime: null, color: 'paper', isCompleted: false }
function storage() {
  const values = new Map()
  return { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value) }
}
test('restores changed fields without replacing newer server metadata', () => {
  const latest = { ...base, isCompleted: true, noteTime: '09:00' }
  assert.deepEqual(mergeDraft(latest, { base, payload: { ...base, body: 'Rascunho' } }), { ...latest, body: 'Rascunho' })
})
test('drafts are isolated by account and survive reload', () => {
  globalThis.window = { localStorage: storage() }
  createDraftStore('user-1').set('note-1', base, { ...base, body: 'Privado' })
  assert.equal(createDraftStore('user-2').get('note-1'), null)
  assert.equal(createDraftStore('user-1').get('note-1').payload.body, 'Privado')
})
test('keeps text in memory when storage fails', () => {
  globalThis.window = { localStorage: { getItem() { throw new Error('Denied') }, setItem() { throw new Error('Quota') } } }
  const store = createDraftStore('user')
  assert.equal(store.set('new', base, { ...base, title: 'Rascunho' }), false)
  assert.equal(store.available, false)
  assert.equal(store.get('new').payload.title, 'Rascunho')
})
test('ignores malformed cached drafts', () => {
  globalThis.window = { localStorage: { getItem: () => '{"note":{"base":{},"payload":{"title":34}}}', setItem() {} } }
  assert.equal(createDraftStore('user').get('note'), null)
})
test('removing a draft preserves the others', () => {
  globalThis.window = { localStorage: storage() }
  const store = createDraftStore('user')
  store.set('1', base, { ...base, title: 'Um' })
  store.set('2', base, { ...base, title: 'Dois' })
  store.remove('1')
  assert.equal(store.has('1'), false)
  assert.equal(createDraftStore('user').get('2').payload.title, 'Dois')
})

test('local drafts survive reload without leaking account drafts', () => {
  globalThis.window = { localStorage: storage() }
  createDraftStore(LOCAL_DRAFT_SCOPE).set('new', base, { ...base, title: 'Local' })
  createDraftStore('account-id').set('new', base, { ...base, title: 'Conta' })
  assert.equal(createDraftStore(LOCAL_DRAFT_SCOPE).get('new').payload.title, 'Local')
  assert.equal(createDraftStore('account-id').get('new').payload.title, 'Conta')
})
