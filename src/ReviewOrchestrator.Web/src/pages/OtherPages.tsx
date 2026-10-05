import type { ReactNode } from 'react'
import { useReviewers } from '../api/hooks'
import { Badge } from '../components/Badges'
import { Card, EmptyState, ReviewerAvatar, ReviewerCapacity, Spinner } from '../components/Primitives'

export function TeamPage() {
  const { data, isLoading } = useReviewers()
  if (isLoading || !data) return <Spinner label="Loading team…" />

  return (
    <div className="mx-auto max-w-6xl space-y-5 px-6 py-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight text-slate-900">Team</h1>
        <p className="text-sm text-slate-500">Ownership, specialties and current review load used by the reviewer router</p>
      </div>
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        {data.map((r) => (
          <Card key={r.id} className={r.isActive ? '' : 'opacity-60'}>
            <div className="flex items-center gap-3">
              <ReviewerAvatar name={r.name} size="lg" />
              <div className="flex-1">
                <p className="text-sm font-semibold text-slate-900">{r.name}</p>
                <ReviewerCapacity active={r.activeReviews} capacity={r.capacity} inactive={!r.isActive} />
              </div>
            </div>
            <div className="mt-3 space-y-2 text-xs">
              <Row label="Owns">{r.components.map((c) => <Badge key={c} tone="indigo">{c}</Badge>)}</Row>
              <Row label="Specialties">{r.specialties.map((s) => <Badge key={s}>{s}</Badge>)}</Row>
              <Row label="Reviewed">{r.repositoryHistory.map((s) => <Badge key={s} tone="blue">{s}</Badge>)}</Row>
            </div>
          </Card>
        ))}
      </div>
    </div>
  )
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-start gap-2">
      <span className="w-20 shrink-0 pt-0.5 text-slate-500">{label}</span>
      <div className="flex flex-wrap gap-1">{children}</div>
    </div>
  )
}

export function PlaceholderPage({ title, message, children }: { title: string; message: string; children?: ReactNode }) {
  return (
    <div className="mx-auto max-w-3xl px-6 py-6">
      <h1 className="text-xl font-semibold tracking-tight text-slate-900">{title}</h1>
      <Card className="mt-5">
        <EmptyState title="Not part of the proof of concept" message={message} />
        {children}
      </Card>
    </div>
  )
}

export function SettingsPage() {
  const settings = [
    ['ReviewRouting:DefaultLeaseHours', '4'],
    ['ReviewRouting:MaxAuthorOpenReviewsBeforeWarning', '3'],
    ['Queue:AgingHours / StaleHours / CriticalHours', '8 / 24 / 48'],
    ['ReviewSurface:MeaningfulLineThreshold', '600'],
    ['ReviewSurface:ProductionFileThreshold', '15'],
    ['SourceControl:Provider', 'Mock'],
    ['ReviewBrief:Provider', 'Mock'],
  ]
  return (
    <PlaceholderPage title="Settings" message="Tuning lives in appsettings.json / environment variables for the PoC. Defaults:">
      <dl className="divide-y divide-slate-100 rounded-xl border border-slate-100 text-sm">
        {settings.map(([k, v]) => (
          <div key={k} className="flex justify-between gap-4 px-3 py-2">
            <dt className="font-mono text-xs text-slate-600">{k}</dt>
            <dd className="font-medium text-slate-800">{v}</dd>
          </div>
        ))}
      </dl>
    </PlaceholderPage>
  )
}
