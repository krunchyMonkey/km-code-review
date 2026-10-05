import type { ReactNode } from 'react'
import { Clock, Flame, TriangleAlert } from 'lucide-react'
import type { AgingState, PriorityBand, PullRequestStatus, ReviewRisk } from '../api/types'
import { statusLabel } from '../lib/format'

type Tone = 'red' | 'amber' | 'blue' | 'green' | 'slate' | 'indigo' | 'orange'

const tones: Record<Tone, string> = {
  red: 'bg-red-50 text-red-700 ring-red-600/15',
  amber: 'bg-amber-50 text-amber-800 ring-amber-600/20',
  blue: 'bg-sky-50 text-sky-700 ring-sky-600/15',
  green: 'bg-emerald-50 text-emerald-700 ring-emerald-600/15',
  slate: 'bg-slate-100 text-slate-600 ring-slate-500/10',
  indigo: 'bg-indigo-50 text-indigo-700 ring-indigo-600/15',
  orange: 'bg-orange-50 text-orange-700 ring-orange-600/20',
}

export function Badge({ tone = 'slate', children, className = '' }: { tone?: Tone; children: ReactNode; className?: string }) {
  return (
    <span className={`inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-[11px] font-semibold ring-1 ring-inset ${tones[tone]} ${className}`}>
      {children}
    </span>
  )
}

const bandTone: Record<PriorityBand, Tone> = { High: 'red', Medium: 'amber', Low: 'blue' }

export function PriorityBadge({ band, size = 'sm' }: { band: PriorityBand; size?: 'sm' | 'md' }) {
  return (
    <Badge tone={bandTone[band]} className={`uppercase tracking-wide ${size === 'md' ? 'px-2 py-1 text-xs' : ''}`}>
      {band}
    </Badge>
  )
}

export function RiskBadge({ risk }: { risk: ReviewRisk }) {
  return (
    <Badge tone={bandTone[risk]} className="uppercase tracking-wide">
      {risk} risk
    </Badge>
  )
}

/** Fresh/Waiting stay quiet; Aging, Stale and Critical are deliberately loud. */
export function AgingBadge({ state, hideQuiet = false }: { state: AgingState; hideQuiet?: boolean }) {
  if (hideQuiet && (state === 'Fresh' || state === 'Waiting')) return null
  switch (state) {
    case 'Critical':
      return (
        <Badge tone="red">
          <Flame className="size-3" /> Critical
        </Badge>
      )
    case 'Stale':
      return (
        <Badge tone="orange">
          <TriangleAlert className="size-3" /> Stale
        </Badge>
      )
    case 'Aging':
      return (
        <Badge tone="amber">
          <Clock className="size-3" /> Aging
        </Badge>
      )
    default:
      return <Badge tone="slate">{state}</Badge>
  }
}

const statusTone: Partial<Record<PullRequestStatus, Tone>> = {
  WaitingForReview: 'slate',
  InReview: 'indigo',
  ChangesRequested: 'amber',
  Approved: 'green',
}

export function StatusBadge({ status }: { status: PullRequestStatus }) {
  return <Badge tone={statusTone[status] ?? 'slate'}>{statusLabel[status] ?? status}</Badge>
}

export function LabelChip({ label }: { label: string }) {
  return <span className="rounded-md bg-slate-100 px-1.5 py-0.5 text-[11px] font-medium text-slate-600">{label}</span>
}
