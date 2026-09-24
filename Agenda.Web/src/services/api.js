const TOKEN_KEY = 'agenda-online-token'
const USER_KEY = 'agenda-online-user'
const API_BASE_URL = (import.meta.env?.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

export function getSavedSession() {
  try {
    const token = localStorage.getItem(TOKEN_KEY)
    const userText = localStorage.getItem(USER_KEY)
    if (!token || !userText) return null
    const user = JSON.parse(userText)
    if (!user?.id || !user?.name) return null
    return {
      token,
      user
    }
  } catch {
    return null
  }
}

export function saveSession(session) {
  try {
    localStorage.setItem(TOKEN_KEY, session.token)
    localStorage.setItem(USER_KEY, JSON.stringify(session.user))
  } catch { /* Login can still work without persistent storage. */ }
}

export function clearSession() {
  try {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  } catch {}
}

export async function register(payload, signal) {
  return request('/api/auth/register', {
    method: 'POST',
    body: payload,
    signal
  })
}

export async function login(payload, signal) {
  return request('/api/auth/login', {
    method: 'POST',
    body: payload,
    unauthorizedMessage: 'E-mail ou senha invalidos. Confira os dados ou crie uma conta.',
    signal
  })
}

export async function listNotes(token, signal) {
  return request('/api/notes', {
    token,
    signal
  })
}

export async function createNote(token, payload) {
  return request('/api/notes', {
    method: 'POST',
    token,
    body: payload
  })
}

export async function importLocalNote(token, localId, note) {
  return request('/api/notes/import', { method: 'POST', token, body: { localId, note } })
}

export async function updateNote(token, id, payload) {
  return request(`/api/notes/${id}`, {
    method: 'PUT',
    token,
    body: payload
  })
}

export async function deleteNote(token, id) {
  return request(`/api/notes/${id}`, {
    method: 'DELETE',
    token
  })
}

async function request(url, options = {}) {
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), 60000)
  const abort = () => controller.abort()
  if (options.signal?.aborted) controller.abort()
  options.signal?.addEventListener('abort', abort, { once: true })
  try {
    const response = await fetch(API_BASE_URL + url, {
      method: options.method ?? 'GET',
      headers: {
        Accept: 'application/json',
        ...(options.body ? { 'Content-Type': 'application/json' } : {}),
        ...(options.token ? { Authorization: 'Bearer ' + options.token } : {}),
        ...options.headers
      },
      signal: controller.signal,
      body: options.body ? JSON.stringify(options.body) : undefined
    })
    if (response.status === 204) return null
    const data = await response.json().catch(error => {
      if (controller.signal.aborted) throw error
      return null
    })
    if (!response.ok) {
      const error = new Error(response.status === 401
        ? options.unauthorizedMessage ?? 'Sua sessão expirou. Entre novamente.'
        : response.status >= 500 ? 'O serviço está indisponível agora. Tente novamente em instantes.'
          : data?.message ?? 'Não foi possível concluir. Tente novamente.')
      error.status = response.status
      throw error
    }
    if (data === null) throw new Error('Recebemos uma resposta incompleta. Tente novamente.')
    return data
  } catch (error) {
    if (controller.signal.aborted) {
      throw new Error('A conexão demorou mais que o esperado. Confira sua conexão e tente novamente.')
    }
    if (error instanceof TypeError) {
      throw new Error('Não conseguimos conectar agora. Confira sua conexão e tente novamente.')
    }
    throw error
  } finally {
    clearTimeout(timeout)
    options.signal?.removeEventListener('abort', abort)
  }
}
