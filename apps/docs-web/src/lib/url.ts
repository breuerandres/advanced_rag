export function removeHandoffFromUrl(): void {
  const url = new URL(window.location.href)
  url.searchParams.delete('handoff')
  window.history.replaceState(null, '', `${url.pathname}${url.search}${url.hash}`)
}
