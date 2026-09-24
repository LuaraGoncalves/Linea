const fields = ['title', 'body', 'noteDate', 'noteTime', 'color', 'isCompleted']

export function mergeDraft(current, draft) {
  if (!draft?.base || !draft?.payload) return { ...current }
  const merged = { ...current }
  for (const field of fields) {
    if (draft.payload[field] !== draft.base[field]) merged[field] = draft.payload[field]
  }
  return merged
}

export function createDraftStore(userId) {
  const key = userId ? 'linea-drafts:' + userId : null
  let drafts = {}
  let storage
  let available = Boolean(key)
  try {
    storage = window.localStorage
    const data = key ? JSON.parse(storage.getItem(key) || '{}') : {}
    if (data && typeof data === 'object' && !Array.isArray(data)) drafts = data
  } catch {
    available = false
  }
  function persist() {
    try {
      if (!key || !storage) return false
      storage.setItem(key, JSON.stringify(drafts))
      available = true
      return true
    } catch {
      available = false
      return false
    }
  }
  return {
    get available() { return available },
    get(id) {
      const draft = drafts[id]
      const valid = (data) => data && fields.every((field) =>
        field === 'isCompleted' ? typeof data[field] === 'boolean'
          : field === 'noteDate' || field === 'noteTime' ? data[field] === null || typeof data[field] === 'string'
            : typeof data[field] === 'string')
      return valid(draft?.base) && valid(draft?.payload) ? draft : null
    },
    set(id, base, payload) {
      drafts[id] = { base: { ...base }, payload: { ...payload } }
      return persist()
    },
    remove(id) {
      delete drafts[id]
      return persist()
    },
    has(id) { return Boolean(this.get(id)) }
  }
}

