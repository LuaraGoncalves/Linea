<script setup>
import { computed, reactive, ref } from 'vue'
import {
  AlertTriangle,
  CalendarDays,
  Check,
  CheckCircle2,
  Clock,
  ListChecks,
  LogOut,
  NotebookPen,
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

const authForm = reactive({
  name: '',
  email: '',
  password: ''
})

const noteForm = reactive({
  title: '',
  noteDate: new Date().toISOString().slice(0, 10),
  noteTime: '',
  color: 'paper',
  body: '',
  isCompleted: false
})

const selectedNote = computed(() => {
  return notes.value.find((note) => note.id === selectedNoteId.value) ?? null
})

const todayNotes = computed(() => {
  const today = new Date().toISOString().slice(0, 10)
  return notes.value.filter((note) => note.noteDate === today)
})

const pendingNotes = computed(() => notes.value.filter((note) => !note.isCompleted))
const completedNotes = computed(() => notes.value.filter((note) => note.isCompleted))
const overdueNotes = computed(() => notes.value.filter((note) => isOverdue(note)))

const upcomingNotes = computed(() => {
  return [...notes.value].sort((first, second) => {
    const firstDate = `${first.noteDate ?? '9999-12-31'} ${first.noteTime ?? '99:99'}`
    const secondDate = `${second.noteDate ?? '9999-12-31'} ${second.noteTime ?? '99:99'}`
    return firstDate.localeCompare(secondDate)
  })
})

if (session.value) {
  loadNotes()
}

async function submitAuth() {
  loading.value = true
  message.value = ''

  try {
    const payload =
      authMode.value === 'register'
        ? authForm
        : {
            email: authForm.email,
            password: authForm.password
          }

    const response = authMode.value === 'register' ? await register(payload) : await login(payload)
    session.value = response
    saveSession(response)
    resetAuthForm()
    await loadNotes()
  } catch (error) {
    message.value = error.message
    if (error.message.includes('sessao expirou')) {
      logout()
    }
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
    message.value = error.message
    if (error.message.includes('sessao expirou')) {
      logout()
    }
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

  const payload = {
    title: noteForm.title,
    body: noteForm.body,
    noteDate: noteForm.noteDate || null,
    noteTime: noteForm.noteTime || null,
    color: noteForm.color,
    isCompleted: noteForm.isCompleted
  }

  try {
    if (selectedNote.value) {
      const updated = await updateNote(session.value.token, selectedNote.value.id, payload)
      notes.value = notes.value.map((note) => (note.id === updated.id ? updated : note))
      selectedNoteId.value = updated.id
    } else {
      const created = await createNote(session.value.token, payload)
      notes.value = [...notes.value, created]
      selectedNoteId.value = created.id
    }

    resetNoteForm()
  } catch (error) {
    message.value = error.message
    if (error.message.includes('sessao expirou')) {
      logout()
    }
  } finally {
    saving.value = false
  }
}

function editNote(note) {
  selectedNoteId.value = note.id
  noteForm.title = note.title
  noteForm.noteDate = note.noteDate ?? ''
  noteForm.noteTime = note.noteTime ?? ''
  noteForm.color = note.color ?? 'paper'
  noteForm.body = note.body
  noteForm.isCompleted = Boolean(note.isCompleted)
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
  } catch (error) {
    message.value = error.message
    if (error.message.includes('sessao expirou')) {
      logout()
    }
  } finally {
    deletingId.value = null
  }
}

function resetNoteForm() {
  selectedNoteId.value = null
  noteForm.title = ''
  noteForm.noteDate = new Date().toISOString().slice(0, 10)
  noteForm.noteTime = ''
  noteForm.color = 'paper'
  noteForm.body = ''
  noteForm.isCompleted = false
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

  const payload = {
    title: note.title,
    body: note.body,
    noteDate: note.noteDate,
    noteTime: note.noteTime,
    color: note.color,
    isCompleted: !note.isCompleted
  }

  try {
    const updated = await updateNote(session.value.token, note.id, payload)
    notes.value = notes.value.map((item) => (item.id === updated.id ? updated : item))
    if (selectedNoteId.value === updated.id) {
      noteForm.isCompleted = updated.isCompleted
    }
  } catch (error) {
    message.value = error.message
    if (error.message.includes('sessao expirou')) {
      logout()
    }
  }
}

function isOverdue(note) {
  if (note.isCompleted || !note.noteDate) {
    return false
  }

  const limit = note.noteTime ? `${note.noteDate}T${note.noteTime}:00` : `${note.noteDate}T23:59:59` 
  return new Date(limit) < new Date()
}

function formatDate(date) {
  if (!date) {
    return 'Sem data'
  }

  return new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit',
    month: 'short',
    weekday: 'short'
  }).format(new Date(`${date}T00:00:00`))
}
</script>

<template>
  <main class="app-shell">
    <section v-if="!session" class="auth-board">
      <div class="auth-paper">
        <p class="date-stamp">{{ new Date().toLocaleDateString('pt-BR') }}</p>
        <h1>Agenda Online</h1>
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
            <input v-model="authForm.password" autocomplete="current-password" maxlength="80" minlength="6" required type="password" />
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
          <span>Agenda Online</span>
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
        <section class="page page-left">
          <div class="page-heading">
            <div>
              <p>{{ new Date().toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' }) }}</p>
              <h1>Minhas anotações</h1>
            </div>
            <NotebookPen :size="32" />
          </div>

          <form class="note-form" @submit.prevent="saveNote">
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
              <button class="ghost-action" type="button" @click="resetNoteForm">
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

        <section class="page page-right">
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
              <button class="note-content" type="button" @click="editNote(note)">
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
      </div>
    </section>
  </main>
</template>

