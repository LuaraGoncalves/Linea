const TOKEN_KEY = 'agenda-online-token'
const USER_KEY = 'agenda-online-user'
const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

export function getSavedSession() {
  const token = localStorage.getItem(TOKEN_KEY)
  const userText = localStorage.getItem(USER_KEY)

  if (!token || !userText) {
    return null
  }

  try {
    return {
      token,
      user: JSON.parse(userText)
    }
  } catch {
    clearSession()
    return null
  }
}

export function saveSession(session) {
  localStorage.setItem(TOKEN_KEY, session.token)
  localStorage.setItem(USER_KEY, JSON.stringify(session.user))
}

export function clearSession() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(USER_KEY)
}

export async function register(payload) {
  return request('/api/auth/register', {
    method: 'POST',
    body: payload
  })
}

export async function login(payload) {
  return request('/api/auth/login', {
    method: 'POST',
    body: payload
  })
}

export async function listNotes(token) {
  return request('/api/notes', {
    token
  })
}

export async function createNote(token, payload) {
  return request('/api/notes', {
    method: 'POST',
    token,
    body: payload
  })
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
  const headers = {
    Accept: 'application/json',
    ...options.headers
  }

  if (options.body) {
    headers['Content-Type'] = 'application/json'
  }

  if (options.token) {
    headers.Authorization = `Bearer ${options.token}`
  }

  let response

  try {
    response = await fetch(`${API_BASE_URL}${url}`, {
      method: options.method ?? 'GET',
      headers,
      body: options.body ? JSON.stringify(options.body) : undefined
    })
  } catch {
    throw new Error('Nao foi possivel conectar com a API. Verifique se o backend esta ligado.')
  }

  if (response.status === 204) {
    return null
  }

  const data = await response.json().catch(() => null)

  if (!response.ok) {
    if (response.status === 401) {
      clearSession()
      throw new Error('Sua sessao expirou. Entre novamente.')
    }

    throw new Error(data?.message ?? 'Algo deu errado. Tente novamente.')
  }

  return data
}
