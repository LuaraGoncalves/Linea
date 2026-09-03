<script setup>
import { computed, reactive, ref } from 'vue'
import {
  AlertTriangle,
  CalendarDays,
  Check,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Clock,
  ListChecks,
  LogOut,
  NotebookPen,
  Pencil,
  Plus,
  Save,
  Trash2,
  UserRound
} from '@lucide/vue'
import {
  clearSession,
  createNote,
  deleteNote,
  getSavedSession,
  listNotes,
  login,
  register,
  saveSession,
  updateNote
} from './services/api'

const savedSession = getSavedSession()

const session = ref(savedSession)
const notes = ref([])
const selectedNoteId = ref(null)
const authMode = ref('login')
const loading = ref(false)
const saving = ref(false)
const deletingId = ref(null)
const message = ref('')
const readingNoteId = ref(null)
const pageDirection = ref('next')
const isTurningPage = ref(false)
const isEditorOpen = ref(true)

const authForm = reactive({
  name: '',
  email: '',
  password: ''
})

const noteForm = reactive({
  title: '',
  noteDate: getLocalDateValue(),
  noteTime: '',
  color: 'paper',
  body: '',
  isCompleted: false
})

const selectedNote = computed(() => {
  return notes.value.find((note) => note.id === selectedNoteId.value) ?? null
})

const readingNote = computed(() => {
  return upcomingNotes.value.find((note) => note.id === readingNoteId.value) ?? null
})

const readingNoteIndex = computed(() => {
  return upcomingNotes.value.findIndex((note) => note.id === readingNoteId.value)
})

const todayNotes = computed(() => {
  const today = getLocalDateValue()
  return notes.value.filter((note) => normalizeNoteDate(note.noteDate) === today)
})

const pendingNotes = computed(() => notes.value.filter((note) => !note.isCompleted))
const completedNotes = computed(() => notes.value.filter((note) => note.isCompleted))
const overdueNotes = computed(() => notes.value.filter((note) => isOverdue(note)))

const upcomingNotes = computed(() => {
  return [...notes.value].sort((first, second) => {
    const firstDate = `${normalizeNoteDate(first.noteDate) ?? '9999-12-31'} ${first.noteTime ?? '99:99'}`
    const secondDate = `${normalizeNoteDate(second.noteDate) ?? '9999-12-31'} ${second.noteTime ?? '99:99'}`
    return firstDate.localeCompare(secondDate)
  })
})

const passwordValidationMessage = computed(() => {
  if (authMode.value !== 'register') {
    return ''
  }

  return validatePassword(authForm.password)
})

if (session.value) {
  loadNotes()
}

async function submitAuth() {
  message.value = ''

  if (authMode.value === 'register') {
    const passwordMessage = validatePassword(authForm.password)

    if (passwordMessage) {
      return
    }
  }

  loading.value = true

  try {
    const response = authMode.value === 'register'
      ? await register(createAuthPayload())
      : await login(createLoginPayload())
    session.value = response
    saveSession(response)
    resetAuthForm()
    await loadNotes()
  } catch (error) {
    handleRequestError(error)
  } finally {
    loading.value = false
  }
}

async function loadNotes() {
  if (!session.value?.token) {
    return
  }

  loading.value = true
  message.value = ''

  try {
    notes.value = await listNotes(session.value.token)
  } catch (error) {
    handleRequestError(error)
  } finally {
    loading.value = false
  }
}

async function saveNote() {
  if (!session.value?.token) {
    return
  }

  saving.value = true
  message.value = ''

  try {
    if (selectedNote.value) {
      const updated = await updateNote(session.value.token, selectedNote.value.id, createNotePayload())
      replaceNote(updated)
      selectedNoteId.value = updated.id
    } else {
      const created = await createNote(session.value.token, createNotePayload())
      notes.value = [...notes.value, created]
      selectedNoteId.value = created.id
    }

    resetNoteForm()
  } catch (error) {
    handleRequestError(error)
  } finally {
    saving.value = false
  }
}

function editNote(note) {
  isEditorOpen.value = true
  fillNoteForm(note)
}

function fillNoteForm(note) {
  selectedNoteId.value = note.id
  readingNoteId.value = note.id
  noteForm.title = note.title
  noteForm.noteDate = normalizeNoteDate(note.noteDate) ?? ''
  noteForm.noteTime = note.noteTime ?? ''
  noteForm.color = note.color ?? 'paper'
  noteForm.body = note.body
  noteForm.isCompleted = Boolean(note.isCompleted)
}

function openNoteReader(note) {
  fillNoteForm(note)
  pageDirection.value = 'next'
  isEditorOpen.value = false
}

function closeNoteReader() {
  readingNoteId.value = null
  isTurningPage.value = false
  isEditorOpen.value = true
}

function openNewNoteForm() {
  resetNoteForm()
  isEditorOpen.value = true
}

async function saveReadingNote() {
  if (!session.value?.token || !selectedNoteId.value || isEditorOpen.value) {
    return
  }

  saving.value = true
  message.value = ''

  try {
    const updated = await updateNote(session.value.token, selectedNoteId.value, createNotePayload())
    replaceNote(updated)
    selectedNoteId.value = updated.id
    readingNoteId.value = updated.id
    noteForm.title = updated.title
    noteForm.body = updated.body
  } catch (error) {
    handleRequestError(error)
  } finally {
    saving.value = false
  }
}

async function moveReadingPage(step) {
  if (!upcomingNotes.value.length || isTurningPage.value) {
    return
  }

  await saveReadingNote()

  const currentIndex = readingNoteIndex.value < 0 ? 0 : readingNoteIndex.value
  const nextIndex = (currentIndex + step + upcomingNotes.value.length) % upcomingNotes.value.length
  pageDirection.value = step > 0 ? 'next' : 'previous'
  isTurningPage.value = true
  playPageTurnSound()

  window.setTimeout(() => {
    fillNoteForm(upcomingNotes.value[nextIndex])

    window.setTimeout(() => {
      isTurningPage.value = false
    }, 310)
  }, 520)
}

function playPageTurnSound() {
  const AudioContext = window.AudioContext || window.webkitAudioContext

  if (!AudioContext) {
    return
  }

  const audio = new AudioContext()
  const noiseLength = Math.floor(audio.sampleRate * 0.16)
  const buffer = audio.createBuffer(1, noiseLength, audio.sampleRate)
  const data = buffer.getChannelData(0)

  for (let i = 0; i < noiseLength; i += 1) {
    data[i] = (Math.random() * 2 - 1) * (1 - i / noiseLength)
  }

  const source = audio.createBufferSource()
  const filter = audio.createBiquadFilter()
  const gain = audio.createGain()

  source.buffer = buffer
  filter.type = 'bandpass'
  filter.frequency.setValueAtTime(1200, audio.currentTime)
  filter.frequency.exponentialRampToValueAtTime(2600, audio.currentTime + 0.12)
  gain.gain.setValueAtTime(0.0001, audio.currentTime)
  gain.gain.exponentialRampToValueAtTime(0.18, audio.currentTime + 0.02)
  gain.gain.exponentialRampToValueAtTime(0.0001, audio.currentTime + 0.16)

  source.connect(filter)
  filter.connect(gain)
  gain.connect(audio.destination)
  source.start()
  source.stop(audio.currentTime + 0.17)
  source.onended = () => audio.close()
}

async function removeNote(id) {
  if (!session.value?.token) {
    return
  }

  deletingId.value = id
  message.value = ''

  try {
    await deleteNote(session.value.token, id)
    notes.value = notes.value.filter((note) => note.id !== id)

    if (selectedNoteId.value === id) {
      resetNoteForm()
    }

    if (readingNoteId.value === id) {
      closeNoteReader()
    }
  } catch (error) {
    handleRequestError(error)
  } finally {
    deletingId.value = null
  }
}

function resetNoteForm() {
  selectedNoteId.value = null
  noteForm.title = ''
  noteForm.noteDate = getLocalDateValue()
  noteForm.noteTime = ''
  noteForm.color = 'paper'
  noteForm.body = ''
  noteForm.isCompleted = false
  isEditorOpen.value = true
}

function resetAuthForm() {
  authForm.name = ''
  authForm.email = ''
  authForm.password = ''
}

function logout() {
  clearSession()
  session.value = null
  notes.value = []
  resetNoteForm()
}

async function toggleCompleted(note) {
  if (!session.value?.token) {
    return
  }

  try {
    const updated = await updateNote(session.value.token, note.id, createNotePayload(note, !note.isCompleted))
    replaceNote(updated)
    if (selectedNoteId.value === updated.id) {
      noteForm.isCompleted = updated.isCompleted
    }
  } catch (error) {
    handleRequestError(error)
  }
}

function createAuthPayload() {
  return {
    name: authForm.name,
    email: authForm.email,
    password: authForm.password
  }
}

function createLoginPayload() {
  return {
    email: authForm.email,
    password: authForm.password
  }
}

function createNotePayload(source = noteForm, isCompleted = source.isCompleted) {
  return {
    title: source.title,
    body: source.body,
    noteDate: source.noteDate || null,
    noteTime: source.noteTime || null,
    color: source.color,
    isCompleted
  }
}

function replaceNote(updatedNote) {
  notes.value = notes.value.map((note) => (note.id === updatedNote.id ? updatedNote : note))
}

function handleRequestError(error) {
  message.value = error.message

  if (error.message.includes('sessao expirou')) {
    logout()
  }
}

function isOverdue(note) {
  if (note.isCompleted || !note.noteDate) {
    return false
  }

  const noteDate = normalizeNoteDate(note.noteDate)
  const today = getLocalDateValue()

  if (!noteDate) {
    return false
  }

  if (noteDate < today) {
    return true
  }

  if (noteDate > today || !note.noteTime) {
    return false
  }

  const [hour, minute] = note.noteTime.split(':').map(Number)

  if (Number.isNaN(hour) || Number.isNaN(minute)) {
    return false
  }

  const now = new Date()
  return hour < now.getHours() || (hour === now.getHours() && minute < now.getMinutes())
}

function formatDate(date) {
  const noteDate = normalizeNoteDate(date)

  if (!noteDate) {
    return 'Sem data'
  }

  return new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit',
    month: 'short',
    weekday: 'short'
  }).format(new Date(`${noteDate}T00:00:00`))
}

function getLocalDateValue(date = new Date()) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function normalizeNoteDate(date) {
  if (!date) {
    return null
  }

  return String(date).slice(0, 10)
}

function validatePassword(password) {
  if (!password) {
    return 'A senha precisa ter pelo menos 5 caracteres, uma letra maiúscula, uma letra minúscula e um número.'
  }

  if (password.length < 5) {
    return 'A senha precisa ter pelo menos 5 caracteres.'
  }

  if (!/[A-Z]/.test(password)) {
    return 'A senha precisa ter pelo menos uma letra maiúscula.'
  }

  if (!/[a-z]/.test(password)) {
    return 'A senha precisa ter pelo menos uma letra minúscula.'
  }

  if (!/\d/.test(password)) {
    return 'A senha precisa ter pelo menos um número.'
  }

  return ''
}
</script>

<template>
  <main class="app-shell">
    <section v-if="!session" class="auth-board">
      <div class="auth-paper">
        <p class="date-stamp">{{ new Date().toLocaleDateString('pt-BR') }}</p>
        <h1>Linea</h1>
        <p class="subtitle">Seu caderno particular para compromissos, ideias e lembretes.</p>

        <div class="mode-switch" aria-label="Escolha o modo de acesso">
          <button :class="{ active: authMode === 'login' }" type="button" @click="authMode = 'login'">
            Entrar
          </button>
          <button :class="{ active: authMode === 'register' }" type="button" @click="authMode = 'register'">
            Criar conta
          </button>
        </div>

        <form class="auth-form" @submit.prevent="submitAuth">
          <label v-if="authMode === 'register'">
            Nome
            <input v-model="authForm.name" autocomplete="name" maxlength="80" required type="text" />
          </label>

          <label>
            E-mail
            <input v-model="authForm.email" autocomplete="email" maxlength="120" required type="email" />
          </label>

          <label>
            Senha
            <input
              v-model="authForm.password"
              :autocomplete="authMode === 'register' ? 'new-password' : 'current-password'"
              maxlength="80"
              minlength="5"
              required
              type="password"
            />
            <span v-if="passwordValidationMessage" class="field-hint">{{ passwordValidationMessage }}</span>
          </label>

          <p v-if="message" class="message" role="alert">{{ message }}</p>

          <button class="primary-action" :disabled="loading" type="submit">
            <UserRound :size="18" />
            {{ loading ? 'Aguarde...' : authMode === 'login' ? 'Entrar na agenda' : 'Criar minha agenda' }}
          </button>
        </form>
      </div>
    </section>

    <section v-else class="desk">
      <header class="topbar">
        <div class="topbar-identity">
          <span>Linea</span>
          <strong>{{ session.user.name }}</strong>
        </div>

        <div class="topbar-status" aria-label="Resumo da agenda">
          <span class="status-chip">
            <ListChecks :size="15" />
            {{ pendingNotes.length }} pendentes
          </span>
          <span class="status-chip warning">
            <AlertTriangle :size="15" />
            {{ overdueNotes.length }} atrasadas
          </span>
          <span class="status-chip success">
            <CheckCircle2 :size="15" />
            {{ completedNotes.length }} concluídas
          </span>
        </div>

        <button class="icon-button" type="button" title="Sair" @click="logout">
          <LogOut :size="18" />
        </button>
      </header>

      <div class="notebook">
        <section class="page page-left list-page">
          <div class="page-heading">
            <div>
              <p>{{ todayNotes.length === 1 ? '1 item para hoje' : `${todayNotes.length} itens para hoje` }}</p>
              <h2>Próximos registros</h2>
            </div>
            <CalendarDays :size="30" />
          </div>

          <div class="pinned-note">
            <strong>Resumo do dia</strong>
            <span>{{ todayNotes.length ? 'Você tem registros para conferir hoje.' : 'Hoje ainda está livre.' }}</span>
          </div>

          <div class="notes-list">
            <article
              v-for="note in upcomingNotes"
              :key="note.id"
              class="note-card"
              :class="[`tone-${note.color}`, { selected: note.id === selectedNoteId, completed: note.isCompleted, overdue: isOverdue(note) }]"
            >
              <button class="note-content" type="button" @click="openNoteReader(note)">
                <span class="note-date">
                  <Clock :size="15" />
                  {{ formatDate(note.noteDate) }} {{ note.noteTime ? `às ${note.noteTime}` : '' }}
                </span>
                <div class="note-badges">
                  <span v-if="isOverdue(note)" class="late-badge">Atrasada</span>
                  <span v-if="note.isCompleted" class="done-badge">Concluída</span>
                </div>
                <strong>{{ note.title }}</strong>
                <p>{{ note.body || 'Sem detalhes adicionais.' }}</p>
              </button>
              <button class="complete-button" :class="{ active: note.isCompleted }" type="button" :title="note.isCompleted ? 'Marcar como pendente' : 'Marcar como concluída'" @click="toggleCompleted(note)">
                <CheckCircle2 :size="16" />
              </button>
              <button class="icon-button danger" :disabled="deletingId === note.id" type="button" title="Apagar anotação" @click="removeNote(note.id)">
                <Trash2 :size="16" />
              </button>
            </article>

            <div v-if="!notes.length" class="empty-state">
              <Check :size="34" />
              <strong>Nenhuma anotação ainda</strong>
              <span>Escreva a primeira página da sua agenda.</span>
            </div>
          </div>
        </section>

        <section class="page page-right editor-page" :class="{ 'reader-page': readingNote && !isEditorOpen }">
          <div class="page-heading">
            <div>
              <p>{{ new Date().toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' }) }}</p>
              <h1>{{ readingNote && !isEditorOpen ? 'Folha da tarefa' : 'Minhas anotações' }}</h1>
            </div>
            <button v-if="readingNote && !isEditorOpen" class="icon-button" type="button" title="Editar anotação" @click="editNote(readingNote)">
              <Pencil :size="17" />
            </button>
            <NotebookPen v-else :size="32" />
          </div>

          <article v-if="readingNote && !isEditorOpen" class="reader-sheet" :class="[`turn-${pageDirection}`, { turning: isTurningPage }]">
            <span class="page-turn-leaf" aria-hidden="true"></span>
            <span class="page-turn-shadow" aria-hidden="true"></span>

            <p class="reader-date">{{ formatDate(readingNote.noteDate) }} {{ readingNote.noteTime ? `às ${readingNote.noteTime}` : '' }}</p>
            <input
              v-model="noteForm.title"
              class="reader-title-input"
              maxlength="100"
              placeholder="Titulo da tarefa"
              type="text"
              @blur="saveReadingNote"
            />
            <textarea
              v-model="noteForm.body"
              class="reader-task-input"
              maxlength="3000"
              placeholder="Escreva os detalhes da tarefa..."
              @blur="saveReadingNote"
            ></textarea>

            <footer class="reader-controls">
              <button type="button" title="Anotação anterior" @click="moveReadingPage(-1)">
                <ChevronLeft :size="24" />
              </button>
              <span>{{ readingNoteIndex + 1 }} de {{ upcomingNotes.length }}</span>
              <button type="button" title="Próxima anotação" @click="moveReadingPage(1)">
                <ChevronRight :size="24" />
              </button>
            </footer>
          </article>

          <form v-else class="note-form" @submit.prevent="saveNote">
            <div v-if="selectedNote" class="editing-ribbon">Editando anotação</div>
            <label>
              Título
              <input v-model="noteForm.title" maxlength="100" placeholder="Consulta, reunião, tarefa..." type="text" />
            </label>

            <div class="form-row">
              <label>
                Data
                <input v-model="noteForm.noteDate" type="date" />
              </label>
              <label>
                Hora
                <input v-model="noteForm.noteTime" type="time" />
              </label>
            </div>

            <label>
              Cor da ficha
              <select v-model="noteForm.color">
                <option value="paper">Papel</option>
                <option value="rose">Rosa</option>
                <option value="sage">Verde</option>
                <option value="blue">Azul</option>
              </select>
            </label>

            <label class="check-row">
              <input v-model="noteForm.isCompleted" type="checkbox" />
              <span>Marcar como concluída</span>
            </label>

            <label>
              Detalhes
              <textarea v-model="noteForm.body" maxlength="3000" rows="8" placeholder="Escreva como se fosse seu bloco de notas..."></textarea>
            </label>

            <div class="form-actions">
              <button class="ghost-action" type="button" @click="openNewNoteForm">
                <Plus :size="17" />
                Nova
              </button>
              <button class="primary-action compact" :disabled="saving" type="submit">
                <Save :size="17" />
                {{ saving ? 'Salvando...' : selectedNote ? 'Atualizar' : 'Salvar' }}
              </button>
            </div>
          </form>

          <p v-if="message" class="message" role="alert">{{ message }}</p>
        </section>
      </div>

    </section>
  </main>
</template>

