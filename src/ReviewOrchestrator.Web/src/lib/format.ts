/** 47m · 3h 12m · 1d 4h */
export function formatDuration(minutes: number): string {
  const m = Math.max(0, Math.round(minutes))
  if (m < 60) return `${m}m`
  const hours = Math.floor(m / 60)
  if (hours < 24) {
    const rest = m % 60
    return rest ? `${hours}h ${rest}m` : `${hours}h`
  }
  const days = Math.floor(hours / 24)
  const restHours = hours % 24
  return restHours ? `${days}d ${restHours}h` : `${days}d`
}

export function minutesSince(iso: string): number {
  return (Date.now() - new Date(iso).getTime()) / 60_000
}

export function timeAgo(iso: string): string {
  const minutes = minutesSince(iso)
  if (minutes < 1) return 'just now'
  return `${formatDuration(minutes)} ago`
}

export const statusLabel: Record<string, string> = {
  Draft: 'draft',
  WaitingForReview: 'waiting for review',
  InReview: 'in review',
  ChangesRequested: 'changes requested',
  Approved: 'approved',
  Merged: 'merged',
  Closed: 'closed',
}

export function fileName(path: string): string {
  return path.split('/').pop() ?? path
}

export function initials(name: string): string {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]!.toUpperCase())
    .join('')
}

export const formatNumber = (n: number) => n.toLocaleString('en-US')
