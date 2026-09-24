<script setup>
import { computed, nextTick, onMounted, onBeforeUnmount, reactive, ref, watch } from 'vue'
import {
  AlertTriangle, ArrowLeft, Check, CheckCircle2, ChevronLeft, CloudUpload,
  ChevronRight, Circle, Clock, Eye, EyeOff, ListChecks, LogOut,
  NotebookPen, Pencil, Plus, Save, Search, Trash2, UserRound, Volume2, VolumeX, X
} from '@lucide/vue'
import {
  clearSession, createNote, deleteNote, getSavedSession, listNotes,
  importLocalNote, login, register, saveSession, updateNote
} from './services/api'
import { createDraftStore, mergeDraft } from './services/drafts'
import { createLocalNoteStore, LOCAL_DRAFT_SCOPE, LOCAL_NOTES_KEY, normalizeLocalPayload } from './services/localNotes'

const session = ref(getSavedSession())
const notes = ref([])
const selectedNoteId = ref(null)
const readingNoteId = ref(null)
const authMode = ref('login')
const authOpen = ref(false)
const transferring = ref(false)
const localNoteCount = ref(0)
const transferFeedback = ref('')
const localNotes = createLocalNoteStore()
const loading = ref(false)
const opening = ref(true)
const openingError = ref('')
const slowRequest = ref(false)
const saving = ref(false)
const deletingId = ref(null)
const completingId = ref(null)
const message = ref('')
const saveFeedback = ref('')
const isEditorOpen = ref(true)
const isTurningPage = ref(false)
const activeFilter = ref('pending')
const searchQuery = ref('')
const mobileView = ref('list')
const editorHeading = ref(null)
const listHeading = ref(null)
const titleField = ref(null)
const authError = ref(null)
const showPassword = ref(false)
const authSubmitted = ref(false)
const soundEnabled = ref(false)
const draftRevision = ref(0)
const draftStorageAvailable = ref(true)
const authForm = reactive({ name: '', email: '', password: '' })
const noteForm = reactive(emptyPayload())
const baseline = ref(createNotePayload())
let drafts = createDraftStore(session.value?.user.id ?? LOCAL_DRAFT_SCOPE)
let authController
let loadController
let formReady = false
let slowTimer
let turnTimer
let listScroll = 0

const selectedNote = computed(() => notes.value.find(note => note.id === selectedNoteId.value) ?? null)
const readingNote = computed(() => notes.value.find(note => note.id === readingNoteId.value) ?? null)
const dirty = computed(() => JSON.stringify(createNotePayload()) !== JSON.stringify(baseline.value))
const busy = computed(() => saving.value || transferring.value || Boolean(deletingId.value) || Boolean(completingId.value))
const todayNotes = computed(() => notes.value.filter(note => normalizeNoteDate(note.noteDate) === getLocalDateValue() && !note.isCompleted))
const pendingNotes = computed(() => notes.value.filter(note => !note.isCompleted))
const completedNotes = computed(() => notes.value.filter(note => note.isCompleted))
const overdueNotes = computed(() => notes.value.filter(isOverdue))
const sortedNotes = computed(() => [...notes.value].sort((a, b) => {
  if (a.isCompleted !== b.isCompleted) return Number(a.isCompleted) - Number(b.isCompleted)
  return ((normalizeNoteDate(a.noteDate) || '9999-12-31') + (a.noteTime || '99:99'))
    .localeCompare((normalizeNoteDate(b.noteDate) || '9999-12-31') + (b.noteTime || '99:99'))
}))
const upcomingNotes = computed(() => {
  const query = normalizeSearch(searchQuery.value.trim())
  return sortedNotes.value.filter(note => {
    const matches = activeFilter.value === 'all'
      || (activeFilter.value === 'pending' && !note.isCompleted)
      || (activeFilter.value === 'today' && !note.isCompleted && normalizeNoteDate(note.noteDate) === getLocalDateValue())
      || (activeFilter.value === 'overdue' && isOverdue(note))
      || (activeFilter.value === 'completed' && note.isCompleted)
    return matches && (!query || normalizeSearch(note.title + ' ' + note.body).includes(query))
  })
})
const noteGroups = computed(() => {
  const groups = new Map()
  for (const note of upcomingNotes.value) {
    const key = note.isCompleted ? 'Concluídas' : !note.noteDate ? 'Sem data'
      : isOverdue(note) ? 'Atrasadas' : normalizeNoteDate(note.noteDate) === getLocalDateValue() ? 'Hoje' : 'Próximas'
    if (!groups.has(key)) groups.set(key, [])
    groups.get(key).push(note)
  }
  return [...groups].map(([label, items]) => ({ label, items }))
})
const readingNoteIndex = computed(() => upcomingNotes.value.findIndex(note => note.id === readingNoteId.value))
const passwordRules = computed(() => [
  { label: 'De 5 a 80 caracteres', valid: authForm.password.length >= 5 && authForm.password.length <= 80 },
  { label: 'Uma letra maiúscula', valid: /\p{Lu}/u.test(authForm.password) },
  { label: 'Uma letra minúscula', valid: /\p{Ll}/u.test(authForm.password) },
  { label: 'Um número', valid: /\p{Nd}/u.test(authForm.password) }
])
const passwordInvalid = computed(() => authMode.value === 'register' && !passwordRules.value.every(rule => rule.valid))
const passwordValidationMessage = computed(() => passwordInvalid.value && authSubmitted.value ? 'Confira os requisitos da senha.' : '')
const saveStatus = computed(() => saving.value ? slowRequest.value ? 'Salvando... aguarde mais um instante.' : 'Salvando...'
  : dirty.value ? draftStorageAvailable.value ? 'Rascunho neste navegador' : 'Alterações não salvas'
    : saveFeedback.value || (selectedNoteId.value ? session.value ? 'Salvo na conta' : 'Salvo neste navegador' : 'Sem alterações'))
const filters = [
  { value: 'pending', label: 'Pendentes' }, { value: 'today', label: 'Hoje' },
  { value: 'completed', label: 'Concluídas' }, { value: 'all', label: 'Todas' }
]
const colors = [
  { value: 'paper', label: 'Papel' }, { value: 'rose', label: 'Rosa' },
  { value: 'sage', label: 'Verde' }, { value: 'blue', label: 'Azul' }
]

watch(noteForm, () => {
  if (formReady) persistDraft()
}, { deep: true, flush: 'sync' })
watch(() => loading.value || busy.value, value => {
  clearTimeout(slowTimer)
  slowRequest.value = false
  if (value) slowTimer = setTimeout(() => { slowRequest.value = true }, 8000)
})
watch(authMode, () => {
  message.value = ''
  authSubmitted.value = false
  showPassword.value = false
})
watch([activeFilter, searchQuery], () => {
  if (!busy.value && !upcomingNotes.value.some(note => note.id === selectedNoteId.value)) {
    openFirstNoteReader(false)
  }
})

onMounted(() => {
  try { soundEnabled.value = localStorage.getItem('linea-page-sound') === 'true' } catch {}
  window.addEventListener('beforeunload', protectDraft)
  window.addEventListener('storage', handleStorageChange)
  refreshLocalCount()
  if (session.value) loadNotes()
  else loadLocalNotes()
})
onBeforeUnmount(() => {
  clearTimeout(slowTimer)
  clearTimeout(turnTimer)
  window.removeEventListener('beforeunload', protectDraft)
  window.removeEventListener('storage', handleStorageChange)
  authController?.abort()
  loadController?.abort()
})

function emptyPayload() {
  return { title: '', body: '', noteDate: getLocalDateValue(), noteTime: null, color: 'paper', isCompleted: false }
}
function createNotePayload(source = noteForm, isCompleted = source.isCompleted) {
  return {
    title: source.title ?? '', body: source.body ?? '',
    noteDate: normalizeNoteDate(source.noteDate), noteTime: source.noteTime || null,
    color: source.color || 'paper', isCompleted: Boolean(isCompleted)
  }
}
function persistDraft() {
  const key = selectedNoteId.value ?? 'new'
  draftStorageAvailable.value = dirty.value
    ? drafts.set(key, baseline.value, createNotePayload()) : drafts.remove(key)
  draftRevision.value++
}
function hasDraft(id) {
  void draftRevision.value
  return drafts.has(id)
}
function protectDraft(event) {
  if (dirty.value || (!draftStorageAvailable.value && draftRevision.value)) {
    event.preventDefault()
    event.returnValue = ''
  }
}
function fillNoteForm(note = null) {
  formReady = false
  selectedNoteId.value = note?.id ?? null
  readingNoteId.value = note?.id ?? null
  baseline.value = note ? createNotePayload(note) : emptyPayload()
  Object.assign(noteForm, mergeDraft(baseline.value, drafts.get(note?.id ?? 'new')))
  formReady = true
  saveFeedback.value = ''
  message.value = ''
  persistDraft()
}
async function focusEditor(form = false) {
  await nextTick()
  if (window.matchMedia('(max-width: 900px)').matches) window.scrollTo({ top: 0 })
  const target = form ? titleField.value : editorHeading.value
  target?.focus({ preventScroll: true })
}
function openNoteReader(note, focus = true) {
  if (busy.value || !note) return
  if (mobileView.value === 'list') listScroll = window.scrollY
  fillNoteForm(note)
  isEditorOpen.value = false
  mobileView.value = 'detail'
  if (focus) focusEditor()
}
function openFirstNoteReader(showDetail = true) {
  const first = upcomingNotes.value[0]
  if (first) {
    fillNoteForm(first)
    isEditorOpen.value = false
    if (showDetail) mobileView.value = 'detail'
  } else {
    fillNoteForm()
    isEditorOpen.value = true
    mobileView.value = 'list'
  }
}
function openNewNoteForm() {
  if (busy.value) return
  if (mobileView.value === 'list') listScroll = window.scrollY
  fillNoteForm()
  isEditorOpen.value = true
  mobileView.value = 'detail'
  focusEditor(true)
}
function editNote(note) {
  if (busy.value) return
  if (selectedNoteId.value !== note.id) fillNoteForm(note)
  isEditorOpen.value = true
  focusEditor(true)
}
async function backToList() {
  if (busy.value) return
  mobileView.value = 'list'
  await nextTick()
  window.scrollTo({ top: listScroll })
  listHeading.value?.focus({ preventScroll: true })
}
function cancelEditing() {
  if (busy.value) return
  if (dirty.value && !window.confirm('Descartar as alterações desta anotação?')) return
  drafts.remove(selectedNoteId.value ?? 'new')
  draftRevision.value++
  if (selectedNote.value) {
    fillNoteForm(selectedNote.value)
    isEditorOpen.value = false
    focusEditor()
  } else {
    openFirstNoteReader(false)
    backToList()
  }
}
function refreshLocalCount() {
  try { localNoteCount.value = localNotes.list().length }
  catch { localNoteCount.value = 0 }
}
function loadLocalNotes() {
  opening.value = false
  loading.value = false
  openingError.value = ''
  try {
    notes.value = localNotes.list()
    openFirstNoteReader()
    if (drafts.has('new')) openNewNoteForm()
    refreshLocalCount()
  } catch (error) { openingError.value = error.message }
}
function handleStorageChange(event) {
  if (event.key !== LOCAL_NOTES_KEY && event.key !== null) return
  refreshLocalCount()
  if (session.value || authOpen.value) return
  if (dirty.value || busy.value) {
    message.value = 'As anotações locais mudaram em outra aba. Seu texto aberto foi mantido.'
    return
  }
  try {
    notes.value = localNotes.list()
    const current = notes.value.find(note => note.id === selectedNoteId.value)
    if (current) fillNoteForm(current)
    else openFirstNoteReader(false)
  } catch (error) { message.value = error.message }
}
async function openAuth() {
  if (busy.value) return
  if (dirty.value) {
    await saveNote()
    if (dirty.value) return
  }
  openingError.value = ''
  message.value = ''
  authOpen.value = true
  await nextTick()
  document.querySelector('.auth-form input')?.focus()
}
function closeAuth() {
  authController?.abort()
  authController = null
  loading.value = false
  authOpen.value = false
  message.value = ''
  Object.assign(authForm, { name: '', email: '', password: '' })
  loadLocalNotes()
}
async function submitAuth() {
  if (loading.value) return
  message.value = ''
  authSubmitted.value = true
  if (passwordInvalid.value) return
  loading.value = true
  const mode = authMode.value
  const controller = new AbortController()
  authController = controller
  try {
    const response = mode === 'register'
      ? await register({ ...authForm }, controller.signal)
      : await login({ email: authForm.email, password: authForm.password }, controller.signal)
    if (controller.signal.aborted) return
    session.value = response
    saveSession(response)
    drafts = createDraftStore(response.user.id)
    formReady = false
    authOpen.value = false
    notes.value = []
    selectedNoteId.value = null
    readingNoteId.value = null
    activeFilter.value = 'pending'
    searchQuery.value = ''
    Object.assign(authForm, { name: '', email: '', password: '' })
    showPassword.value = false
    await loadNotes()
  } catch (error) {
    if (controller.signal.aborted) return
    message.value = error.message
    await nextTick()
    authError.value?.focus()
  } finally {
    if (authController === controller) {
      loading.value = false
      authController = null
    }
  }
}
async function loadNotes() {
  if (!session.value?.token) return
  const token = session.value.token
  loadController?.abort()
  const controller = new AbortController()
  loadController = controller
  loading.value = true
  opening.value = true
  openingError.value = ''
  message.value = ''
  try {
    const loaded = await listNotes(token, controller.signal)
    if (controller.signal.aborted || session.value?.token !== token) return
    notes.value = loaded
    openFirstNoteReader()
    if (drafts.has('new')) openNewNoteForm()
  } catch (error) {
    if (controller.signal.aborted || session.value?.token !== token) return
    handleRequestError(error)
    if (session.value) openingError.value = error.message
  } finally {
    if (loadController === controller) {
      opening.value = false
      loading.value = false
      loadController = null
    }
  }
}
async function saveNote() {
  if (busy.value || !dirty.value) return
  const token = session.value?.token ?? null
  const id = selectedNoteId.value
  const payload = createNotePayload()
  const sent = JSON.stringify(payload)
  saving.value = true
  message.value = ''
  try {
    const updated = token
      ? id ? await updateNote(token, id, payload) : await createNote(token, payload)
      : localNotes.save(id, payload, selectedNote.value?.revision)
    if ((session.value?.token ?? null) !== token) return
    if (id) replaceNote(updated)
    else notes.value = [...notes.value, updated]
    // Keep anything typed after the request started; only the sent snapshot was saved.
    formReady = false
    drafts.remove(id ?? 'new')
    selectedNoteId.value = updated.id
    readingNoteId.value = updated.id
    baseline.value = createNotePayload(updated)
    if (JSON.stringify(createNotePayload()) === sent) Object.assign(noteForm, baseline.value)
    formReady = true
    persistDraft()
    saveFeedback.value = token ? 'Salvo na conta' : 'Salvo neste navegador'
    refreshLocalCount()
    if (!dirty.value) {
      isEditorOpen.value = false
      focusEditor()
    }
  } catch (error) {
    saveFeedback.value = 'Não foi possível salvar'
    handleRequestError(error)
  } finally { saving.value = false }
}
async function removeNote(id) {
  if (busy.value) return
  const note = notes.value.find(item => item.id === id)
  if (!note || !window.confirm('Apagar "' + note.title + '"? A anotação e seu rascunho serão excluídos.')) return
  const token = session.value?.token ?? null
  deletingId.value = id
  const wasList = mobileView.value === 'list'
  const wasSelected = selectedNoteId.value === id
  message.value = ''
  try {
    if (token) await deleteNote(token, id)
    else localNotes.remove(id, note.revision)
    if ((session.value?.token ?? null) !== token) return
    notes.value = notes.value.filter(item => item.id !== id)
    drafts.remove(id)
    draftRevision.value++
    if (wasSelected) openFirstNoteReader(!wasList)
    saveFeedback.value = 'Anotação excluída'
    refreshLocalCount()
  } catch (error) { handleRequestError(error) }
  finally {
    deletingId.value = null
    await nextTick()
    if (!authOpen.value) {
      if (wasList) listHeading.value?.focus({ preventScroll: true })
      else if (wasSelected) editorHeading.value?.focus({ preventScroll: true })
    }
  }
}
async function toggleCompleted(note) {
  if (busy.value) return
  const token = session.value?.token ?? null
  completingId.value = note.id
  message.value = ''
  try {
    const payload = createNotePayload(note, !note.isCompleted)
    const updated = token ? await updateNote(token, note.id, payload)
      : localNotes.save(note.id, payload, note.revision)
    if ((session.value?.token ?? null) !== token) return
    replaceNote(updated)
    // Completion must not replace a separate, unsaved title or body.
    if (selectedNoteId.value === updated.id) {
      formReady = false
      baseline.value = { ...baseline.value, isCompleted: updated.isCompleted }
      noteForm.isCompleted = updated.isCompleted
      formReady = true
      persistDraft()
    }
    saveFeedback.value = updated.isCompleted ? 'Anotação concluída' : 'Anotação reaberta'
  } catch (error) { handleRequestError(error) }
  finally { completingId.value = null }
}
function replaceNote(note) {
  notes.value = notes.value.map(item => item.id === note.id ? note : item)
}
function logout(force = false) {
  if (!force && busy.value) return
  if (!force && dirty.value && !draftStorageAvailable.value
    && !window.confirm('Não foi possível guardar o rascunho neste navegador. Sair e descartar?')) return
  formReady = false
  loadController?.abort()
  loadController = null
  authController?.abort()
  authController = null
  loading.value = false
  authOpen.value = false
  clearSession()
  session.value = null
  notes.value = []
  selectedNoteId.value = null
  readingNoteId.value = null
  openingError.value = ''
  opening.value = false
  saveFeedback.value = ''
  mobileView.value = 'list'
  authMode.value = 'login'
  activeFilter.value = 'pending'
  searchQuery.value = ''
  transferFeedback.value = ''
  Object.assign(noteForm, emptyPayload())
  Object.assign(authForm, { name: '', email: '', password: '' })
  drafts = createDraftStore(LOCAL_DRAFT_SCOPE)
  loadLocalNotes()
}
function handleRequestError(error) {
  message.value = error.message
  if (error.status === 401) {
    logout(true)
    message.value = 'Sua sessão expirou. Você está no modo local; entre novamente para acessar as anotações da conta.'
  }
}
async function transferLocalNotes() {
  if (!session.value?.token || busy.value) return
  const token = session.value.token
  transferring.value = true
  message.value = ''
  transferFeedback.value = ''
  let transferred = 0
  try {
    const local = localNotes.list()
    for (const note of local) {
      const localDraft = createDraftStore(LOCAL_DRAFT_SCOPE).get(note.id)
      const payload = normalizeLocalPayload(mergeDraft(note, localDraft))
      const imported = await importLocalNote(token, note.id, payload)
      if (session.value?.token !== token) return
      if (!notes.value.some(item => item.id === imported.id)) notes.value.push(imported)
      if (JSON.stringify(normalizeLocalPayload(imported)) !== JSON.stringify(payload)) {
        throw new Error('Esta anotação já está na conta com outro conteúdo. A versão local foi mantida para você conferir.')
      }
      // Only remove the exact local snapshot acknowledged by the server.
      const currentDrafts = createDraftStore(LOCAL_DRAFT_SCOPE)
      if (JSON.stringify(currentDrafts.get(note.id)) !== JSON.stringify(localDraft)) {
        throw new Error('Um rascunho mudou durante a transferência. A versão local foi mantida.')
      }
      localNotes.remove(note.id, note.revision)
      currentDrafts.remove(note.id)
      transferred++
    }
    if (!dirty.value && !selectedNoteId.value) openFirstNoteReader()
  } catch (error) { handleRequestError(error) }
  finally {
    transferring.value = false
    refreshLocalCount()
    if (session.value?.token === token && transferred) {
      transferFeedback.value = transferred === 1 ? '1 anotação salva na conta.' : transferred + ' anotações salvas na conta.'
    }
  }
}
function moveReadingPage(step) {
  if (busy.value || isTurningPage.value) return
  const next = upcomingNotes.value[readingNoteIndex.value + step]
  if (!next || readingNoteIndex.value < 0) return
  openNoteReader(next)
  if (!window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    isTurningPage.value = true
    clearTimeout(turnTimer)
    turnTimer = setTimeout(() => { isTurningPage.value = false }, 180)
  }
  if (soundEnabled.value) playPageTurnSound()
}
function toggleSound() {
  soundEnabled.value = !soundEnabled.value
  try { localStorage.setItem('linea-page-sound', String(soundEnabled.value)) } catch {}
}
function playPageTurnSound() {
  try {
    const Audio = window.AudioContext || window.webkitAudioContext
    if (!Audio) return
    const audio = new Audio()
    const buffer = audio.createBuffer(1, Math.floor(audio.sampleRate * 0.12), audio.sampleRate)
    const values = buffer.getChannelData(0)
    for (let i = 0; i < values.length; i++) values[i] = (Math.random() * 2 - 1) * (1 - i / values.length)
    const source = audio.createBufferSource()
    const gain = audio.createGain()
    source.buffer = buffer
    gain.gain.value = 0.035
    source.connect(gain)
    gain.connect(audio.destination)
    source.onended = () => audio.close()
    source.start()
  } catch { /* Sound is optional; navigation must still work. */ }
}
function normalizeSearch(value) {
  return value.normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase('pt-BR')
}
function isOverdue(note) {
  if (note.isCompleted || !note.noteDate) return false
  const date = normalizeNoteDate(note.noteDate)
  const today = getLocalDateValue()
  if (date !== today) return date < today
  if (!note.noteTime) return false
  const now = new Date()
  return note.noteTime.slice(0, 5) < String(now.getHours()).padStart(2, '0') + ':' + String(now.getMinutes()).padStart(2, '0')
}
function formatDate(value) {
  const date = normalizeNoteDate(value)
  if (!date) return 'Sem data'
  return new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short', weekday: 'short' }).format(new Date(date + 'T00:00:00'))
}
function getLocalDateValue(date = new Date()) {
  return date.getFullYear() + '-' + String(date.getMonth() + 1).padStart(2, '0') + '-' + String(date.getDate()).padStart(2, '0')
}
function normalizeNoteDate(date) { return date ? String(date).slice(0, 10) : null }
</script>

<template>
  <main class="app-shell">
    <section v-if="opening || openingError" class="opening-screen" :aria-busy="opening" aria-labelledby="opening-title">
      <h1 id="opening-title">Linea</h1>
      <p class="opening-tagline">coloque suas anotações em dia</p>
      <div v-if="opening" class="opening-progress" role="status">
        <span class="opening-orbit" aria-hidden="true"></span>
        <span class="opening-status">{{ slowRequest ? 'Está demorando um pouco mais. Continuamos conectando...' : 'Abrindo sua agenda...' }}</span>
      </div>
      <div v-else class="opening-recovery">
        <p class="message" role="alert">{{ openingError }}</p>
        <button class="primary-action" type="button" @click="session ? loadNotes() : loadLocalNotes()">Tentar novamente</button>
        <button v-if="!session" class="ghost-action" type="button" @click="openAuth">Entrar na conta</button>
      </div>
      <button v-if="session" class="ghost-action" type="button" @click="logout()"><ArrowLeft :size="18" />Usar neste navegador</button>
    </section>

    <section v-else-if="authOpen" class="auth-board">
      <div class="auth-paper">
        <button class="ghost-action auth-back" type="button" @click="closeAuth"><ArrowLeft :size="18" />Continuar sem conta</button>
        <h1>Linea</h1>
        <p class="subtitle">Coloque suas anotações em dia.</p>
        <div class="mode-switch" role="group" aria-label="Acesso à agenda">
          <button :aria-pressed="authMode === 'login'" :class="{ active: authMode === 'login' }" :disabled="loading" type="button" @click="authMode = 'login'">Entrar</button>
          <button :aria-pressed="authMode === 'register'" :class="{ active: authMode === 'register' }" :disabled="loading" type="button" @click="authMode = 'register'">Criar conta</button>
        </div>
        <form class="auth-form" :aria-busy="loading" @submit.prevent="submitAuth">
          <label v-if="authMode === 'register'">
            Nome
            <input v-model="authForm.name" :disabled="loading" autocomplete="name" maxlength="80" required type="text" />
          </label>
          <label>
            E-mail
            <input v-model="authForm.email" :disabled="loading" autocomplete="email" autocapitalize="none" :spellcheck="false" maxlength="120" required type="email" />
          </label>
          <div class="password-field">
            <label for="auth-password">Senha</label>
            <div class="password-input">
              <input id="auth-password" v-model="authForm.password" :disabled="loading"
                :autocomplete="authMode === 'register' ? 'new-password' : 'current-password'"
                :aria-describedby="authMode === 'register' ? 'password-rules' : undefined"
                :aria-invalid="authSubmitted && passwordInvalid ? true : undefined"
                maxlength="80" minlength="5" required :type="showPassword ? 'text' : 'password'" />
              <button class="icon-button" type="button" :disabled="loading" :title="showPassword ? 'Ocultar senha' : 'Mostrar senha'" :aria-label="showPassword ? 'Ocultar senha' : 'Mostrar senha'" :aria-pressed="showPassword" @click="showPassword = !showPassword">
                <EyeOff v-if="showPassword" :size="18" /><Eye v-else :size="18" />
              </button>
            </div>
            <ul v-if="authMode === 'register'" id="password-rules" class="password-rules" :class="{ invalid: authSubmitted && passwordInvalid }">
              <li v-for="rule in passwordRules" :key="rule.label" :class="{ satisfied: rule.valid }">
                <Check v-if="rule.valid" :size="14" aria-hidden="true" /><Circle v-else :size="14" aria-hidden="true" />
                <span>{{ rule.label }}<span class="sr-only">{{ rule.valid ? ': atendido' : ': pendente' }}</span></span>
              </li>
            </ul>
          </div>
          <p v-if="passwordValidationMessage" class="message" role="alert">{{ passwordValidationMessage }}</p>
          <p v-if="message" ref="authError" tabindex="-1" class="message" role="alert">{{ message }}</p>
          <button class="primary-action" :disabled="loading" type="submit">
            <UserRound :size="18" />
            {{ loading ? 'Conectando...' : authMode === 'login' ? 'Entrar na agenda' : 'Criar minha agenda' }}
          </button>
          <p v-if="loading" class="request-status" role="status">{{ slowRequest ? 'Está demorando um pouco mais. Aguarde a conexão.' : 'Conectando à sua agenda...' }}</p>
        </form>
      </div>
    </section>

    <section v-else class="desk">
      <header class="topbar">
        <div class="topbar-identity"><span>Linea</span><strong>{{ session ? session.user.name : 'Neste navegador' }}</strong></div>
        <div class="topbar-status" role="group" aria-label="Filtrar anotações">
          <button class="status-chip" :disabled="busy" :aria-pressed="activeFilter === 'pending'" type="button" @click="activeFilter = 'pending'; backToList()"><ListChecks :size="15" />{{ pendingNotes.length }} {{ pendingNotes.length === 1 ? 'pendente' : 'pendentes' }}</button>
          <button class="status-chip warning" :disabled="busy" :aria-pressed="activeFilter === 'overdue'" type="button" @click="activeFilter = 'overdue'; backToList()"><AlertTriangle :size="15" />{{ overdueNotes.length }} {{ overdueNotes.length === 1 ? 'atrasada' : 'atrasadas' }}</button>
          <button class="status-chip success" :disabled="busy" :aria-pressed="activeFilter === 'completed'" type="button" @click="activeFilter = 'completed'; backToList()"><CheckCircle2 :size="15" />{{ completedNotes.length }} {{ completedNotes.length === 1 ? 'concluída' : 'concluídas' }}</button>
        </div>
        <button v-if="session" class="icon-button" type="button" :disabled="busy" title="Sair da conta e usar neste navegador" aria-label="Sair da conta" @click="logout()"><LogOut :size="18" /></button>
        <button v-else class="ghost-action account-action" type="button" :disabled="busy" @click="openAuth"><UserRound :size="18" />Entrar</button>
      </header>

      <p v-if="!session" class="storage-notice">Anotações locais. Limpar os dados deste site apaga essas anotações.</p>
      <div v-if="session && localNoteCount" class="transfer-bar" :aria-busy="transferring">
        <span>{{ localNoteCount }} {{ localNoteCount === 1 ? 'anotação neste navegador' : 'anotações neste navegador' }}</span>
        <button class="ghost-action" type="button" :disabled="busy" @click="transferLocalNotes"><CloudUpload :size="18" />{{ transferring ? 'Transferindo...' : 'Levar anotações para minha conta' }}</button>
      </div>
      <p v-if="transferFeedback" class="transfer-feedback" role="status">{{ transferFeedback }}</p>
      <p v-if="transferring && slowRequest" class="request-status" role="status">Aguardando a conta. Suas anotações locais estão preservadas.</p>
      <p v-if="message" class="message workspace-message" role="alert">{{ message }}</p>

      <div class="notebook" :data-mobile-view="mobileView">
        <section class="page page-left list-page" aria-labelledby="list-title">
          <div class="page-heading">
            <div>
              <p>{{ todayNotes.length === 1 ? '1 pendente para hoje' : todayNotes.length + ' pendentes para hoje' }}</p>
              <h2 id="list-title" ref="listHeading" tabindex="-1">Anotações</h2>
            </div>
            <button class="icon-button new-note-button" type="button" :disabled="busy" title="Nova anotação" aria-label="Nova anotação" @click="openNewNoteForm"><Plus :size="20" /></button>
          </div>
          <div class="list-toolbar">
            <label class="search-field">
              <span class="sr-only">Buscar anotações</span>
              <Search :size="17" aria-hidden="true" />
              <input v-model="searchQuery" :disabled="busy" type="search" placeholder="Buscar anotações" />
            </label>
            <div class="note-filters" role="group" aria-label="Exibir anotações">
              <button v-for="filter in filters" :key="filter.value" :aria-pressed="activeFilter === filter.value" :disabled="busy" type="button" @click="activeFilter = filter.value">{{ filter.label }}</button>
              <button v-if="activeFilter === 'overdue'" type="button" aria-pressed="true" :disabled="busy">Atrasadas</button>
            </div>
            <p class="results-count" role="status">{{ upcomingNotes.length }} {{ upcomingNotes.length === 1 ? 'anotação' : 'anotações' }}<span v-if="hasDraft('new')" class="new-draft"><button type="button" @click="openNewNoteForm" :disabled="busy">Retomar rascunho</button></span></p>
          </div>
          <div class="notes-list">
            <section v-for="group in noteGroups" :key="group.label" class="note-group" :aria-label="group.label">
              <h3>{{ group.label }}</h3>
              <article v-for="note in group.items" :key="note.id" class="note-card" :class="['tone-' + note.color, { selected: note.id === selectedNoteId, completed: note.isCompleted }]">
                <button class="note-content" :disabled="busy" :aria-current="note.id === selectedNoteId ? 'true' : undefined" type="button" @click="openNoteReader(note)">
                  <span class="note-date"><Clock :size="14" aria-hidden="true" />{{ formatDate(note.noteDate) }}{{ note.noteTime ? ' às ' + note.noteTime.slice(0, 5) : '' }}</span>
                  <strong>{{ note.title }}</strong>
                  <p>{{ note.body || 'Sem detalhes.' }}</p>
                  <span v-if="hasDraft(note.id)" class="draft-badge">Rascunho</span>
                </button>
                <div class="note-actions">
                  <button class="complete-button" :class="{ active: note.isCompleted }" :disabled="busy" :aria-pressed="note.isCompleted" type="button" :title="note.isCompleted ? 'Reabrir anotação' : 'Concluir anotação'" :aria-label="(note.isCompleted ? 'Reabrir: ' : 'Concluir: ') + note.title" @click="toggleCompleted(note)"><CheckCircle2 :size="18" /></button>
                  <button class="icon-button danger" :disabled="busy" type="button" title="Apagar anotação" :aria-label="'Apagar: ' + note.title" @click="removeNote(note.id)"><Trash2 :size="17" /></button>
                </div>
              </article>
            </section>
            <div v-if="!upcomingNotes.length" class="empty-state">
              <NotebookPen :size="28" aria-hidden="true" />
              <strong>{{ searchQuery ? 'Nenhuma anotação encontrada' : !notes.length ? 'Sua primeira anotação' : activeFilter === 'completed' ? 'Nenhuma concluída ainda' : activeFilter === 'today' ? 'Nada pendente para hoje' : 'Nenhuma anotação neste filtro' }}</strong>
              <button v-if="searchQuery" class="ghost-action" type="button" @click="searchQuery = ''"><X :size="16" />Limpar busca</button>
              <button v-else class="primary-action compact" :disabled="busy" type="button" @click="openNewNoteForm"><Plus :size="17" />Nova anotação</button>
            </div>
          </div>
        </section>

        <section class="page page-right editor-page" aria-labelledby="editor-title" :class="{ 'reader-page': readingNote && !isEditorOpen }">
          <button class="back-to-list ghost-action" type="button" :disabled="busy" @click="backToList"><ArrowLeft :size="18" />Anotações</button>
          <div class="page-heading">
            <div>
              <p v-if="readingNote && !isEditorOpen">{{ readingNote.isCompleted ? 'Concluída' : 'Pendente' }}</p>
              <h1 id="editor-title" ref="editorHeading" tabindex="-1">{{ readingNote && !isEditorOpen ? 'Minha anotação' : selectedNote ? 'Editar anotação' : 'Nova anotação' }}</h1>
            </div>
            <div class="reader-heading-actions">
              <button class="icon-button" type="button" :disabled="busy" title="Nova anotação" aria-label="Nova anotação" @click="openNewNoteForm"><Plus :size="18" /></button>
              <button v-if="readingNote && !isEditorOpen" class="icon-button" type="button" :disabled="busy" title="Editar data e detalhes" aria-label="Editar data e detalhes" @click="editNote(readingNote)"><Pencil :size="18" /></button>
            </div>
          </div>

          <form v-if="readingNote && !isEditorOpen" class="reader-sheet" :class="{ turning: isTurningPage }" :aria-busy="saving" @submit.prevent="saveNote">
            <p class="reader-date">{{ formatDate(noteForm.noteDate) }}{{ noteForm.noteTime ? ' às ' + noteForm.noteTime.slice(0, 5) : '' }}</p>
            <label class="reader-field reader-title-field">
              <span>Título</span>
              <textarea v-model="noteForm.title" class="reader-title-input" maxlength="100" rows="2" placeholder="Título da anotação"></textarea>
            </label>
            <label class="reader-field reader-body-field">
              <span>Detalhes</span>
              <textarea v-model="noteForm.body" class="reader-task-input" maxlength="3000" placeholder="Escreva os detalhes da anotação..."></textarea>
            </label>
            <div class="save-bar">
              <span class="save-status" :class="{ pending: dirty }" role="status" aria-live="polite">{{ saveStatus }}</span>
              <button class="primary-action compact" :disabled="busy || !dirty" type="submit"><Save :size="17" />{{ saving ? 'Salvando...' : 'Salvar' }}</button>
            </div>
            <footer class="reader-controls">
              <button class="icon-button" type="button" :disabled="busy || isTurningPage || readingNoteIndex <= 0" title="Anotação anterior" aria-label="Anotação anterior" @click="moveReadingPage(-1)"><ChevronLeft :size="21" /></button>
              <span>{{ readingNoteIndex >= 0 ? (readingNoteIndex + 1) + ' de ' + upcomingNotes.length : 'Fora do filtro atual' }}</span>
              <button class="icon-button" type="button" :disabled="busy || isTurningPage || readingNoteIndex < 0 || readingNoteIndex >= upcomingNotes.length - 1" title="Próxima anotação" aria-label="Próxima anotação" @click="moveReadingPage(1)"><ChevronRight :size="21" /></button>
              <button class="icon-button sound-toggle" type="button" :aria-pressed="soundEnabled" :title="soundEnabled ? 'Desativar som das folhas' : 'Ativar som das folhas'" :aria-label="soundEnabled ? 'Desativar som das folhas' : 'Ativar som das folhas'" @click="toggleSound"><Volume2 v-if="soundEnabled" :size="17" /><VolumeX v-else :size="17" /></button>
            </footer>
          </form>

          <form v-else class="note-form" :aria-busy="saving" @submit.prevent="saveNote">
            <label>Título<input ref="titleField" v-model="noteForm.title" maxlength="100" placeholder="Título da anotação" type="text" /></label>
            <div class="form-row">
              <label>Data<input v-model="noteForm.noteDate" type="date" /></label>
              <label>Hora<input v-model="noteForm.noteTime" type="time" /></label>
            </div>
            <fieldset class="color-options">
              <legend>Cor da anotação</legend>
              <label v-for="color in colors" :key="color.value" class="color-option" :class="'swatch-' + color.value">
                <input v-model="noteForm.color" type="radio" name="note-color" :value="color.value" />
                <span class="color-swatch" aria-hidden="true"><Check v-if="noteForm.color === color.value" :size="16" /></span>
                <span>{{ color.label }}</span>
              </label>
            </fieldset>
            <label class="check-row"><input v-model="noteForm.isCompleted" type="checkbox" /><span>Concluída</span></label>
            <label>Detalhes<textarea v-model="noteForm.body" maxlength="3000" rows="5" placeholder="Escreva os detalhes da anotação..."></textarea></label>
            <div class="form-actions">
              <span class="save-status" :class="{ pending: dirty }" role="status">{{ saveStatus }}</span>
              <button class="ghost-action" :disabled="busy" type="button" @click="cancelEditing">Cancelar</button>
              <button class="primary-action compact" :disabled="busy || !dirty" type="submit"><Save :size="17" />{{ saving ? 'Salvando...' : 'Salvar' }}</button>
            </div>
          </form>
        </section>
      </div>
    </section>
  </main>
</template>

