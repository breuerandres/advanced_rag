const STATE_KEYS: Record<string, string> = {
  Published: 'states.published',
  Draft: 'states.draft',
  'In Review': 'states.in_review',
  Archived: 'states.archived',
}

export function documentStateKey(state: string): string | null {
  return STATE_KEYS[state] ?? null
}
