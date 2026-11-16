export const LOCAL_NOTES_KEY = 'linea-local-notes:v1'
export const LOCAL_DRAFT_SCOPE = 'device'
const colors = ['paper', 'rose', 'sage', 'blue']
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

export function normalizeLocalPayload(note) {
  return {
    title: note.title?.trim() || 'Sem titulo', body: note.body?.trim() || '',
    noteDate: note.noteDate || null, noteTime: note.noteTime ? note.noteTime.slice(0, 5) : null,
    color: note.color || 'paper', isCompleted: Boolean(note.isCompleted)
  }
}

export function createLocalNoteStore(getStorage = () => window.localStorage) {
  function read() {
    let text
    try { text = getStorage().getItem(LOCAL_NOTES_KEY) }
    catch { throw new Error('O navegador bloqueou o armazenamento. Libere os dados deste site ou entre em uma conta.') }
    if (text === null) return []
    try {
      const data = JSON.parse(text)
      const valid = note => note && uuid.test(note.id) && typeof note.revision === 'string'
        && typeof note.title === 'string' && typeof note.body === 'string'
        && (note.noteDate === null || /^\d{4}-\d{2}-\d{2}$/.test(note.noteDate))
        && (note.noteTime === null || /^\d{2}:\d{2}$/.test(note.noteTime))
        && colors.includes(note.color) && typeof note.isCompleted === 'boolean'
      if (data.version !== 1 || !Array.isArray(data.notes) || !data.notes.every(valid)
        || new Set(data.notes.map(note => note.id)).size !== data.notes.length) throw new Error()
      return data.notes
    } catch { throw new Error('Não foi possível ler as anotações locais. Os dados originais foram preservados; não limpe os dados do navegador.') }
  }
  function write(notes) {
    try { getStorage().setItem(LOCAL_NOTES_KEY, JSON.stringify({ version: 1, notes })) }
    catch { throw new Error('Não foi possível salvar neste navegador. Verifique o espaço e as permissões; seu texto continua aberto.') }
  }
  function find(notes, id, revision) {
    const index = notes.findIndex(note => note.id === id)
    if (index < 0 || notes[index].revision !== revision) {
      throw new Error('Esta anotação mudou em outra aba. Recarregue a agenda antes de salvar; seu rascunho foi mantido.')
    }
    return index
  }
  return {
    list: read,
    save(id, payload, revision) {
      if (payload.title.length > 100 || payload.body.length > 3000 || !colors.includes(payload.color)) {
        throw new Error('Confira o título, os detalhes e a cor da anotação.')
      }
      const notes = read()
      const index = id ? find(notes, id, revision) : -1
      const now = new Date().toISOString()
      const note = { ...normalizeLocalPayload(payload), id: id || crypto.randomUUID(),
        revision: crypto.randomUUID(), createdAt: index < 0 ? now : notes[index].createdAt, updatedAt: now }
      if (index < 0) notes.push(note)
      else notes[index] = note
      write(notes)
      return note
    },
    remove(id, revision) {
      const notes = read()
      notes.splice(find(notes, id, revision), 1)
      write(notes)
    }
  }
}
