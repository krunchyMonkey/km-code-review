import { useEffect, useRef, useState } from 'react'
import {
  CircleCheck,
  ExternalLink,
  FileCode,
  GitBranch,
  Info,
  LoaderCircle,
  Maximize2,
  MoreHorizontal,
  Play,
  RefreshCw,
  ShieldCheck,
  TimerOff,
  TriangleAlert,
  Undo2,
  UserCheck,
  Users,
  X,
} from 'lucide-react'
import { useMe, usePullRequest, useReviewAction, useReviewers } from '../api/hooks'
import type { PullRequestDetail } from '../api/types'
import { AgingBadge, Badge, LabelChip, PriorityBadge, RiskBadge, StatusBadge } from '../components/Badges'
import { Button, Card, EmptyState, ReviewerAvatar, ReviewerCapacity, Spinner } from '../components/Primitives'
import { ReviewBriefCard } from '../components/ReviewBriefCard'
import { fileName, formatDuration, formatNumber, timeAgo } from '../lib/format'

type DetailTab = 'overview' | 'files' | 'commits' | 'checks' | 'discussion'

export function PullRequestPanel({ id }: { id: string }) {
  const { data: pr, isLoading, isError, error } = usePullRequest(id)
  const [tab, setTab] = useState<DetailTab>('overview')

  useEffect(() => setTab('overview'), [id])

  if (isLoading) return <Spinner label="Loading pull request…" />
  if (isError || !pr) return <EmptyState title="Could not load this pull request" message={(error as Error | null)?.message} />

  const tabs: { id: DetailTab; label: string; count?: number }[] = [
    { id: 'overview', label: 'Overview' },
    { id: 'files', label: 'Files', count: pr.changedFiles },
    { id: 'commits', label: 'Commits' },
    { id: 'checks', label: 'Checks' },
    { id: 'discussion', label: 'Discussion' },
  ]

  return (
    <div className="mx-auto max-w-6xl px-6 pt-5 pb-10">
      <PrHeader pr={pr} />

      <div className="mt-4 flex gap-1 border-b border-slate-200" role="tablist">
        {tabs.map((t) => (
          <button
            key={t.id}
            role="tab"
            aria-selected={tab === t.id}
            onClick={() => setTab(t.id)}
            className={`-mb-px border-b-2 px-3 pb-2.5 text-sm font-medium ${
              tab === t.id ? 'border-indigo-600 text-indigo-700' : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            {t.label}
            {t.count !== undefined && <span className="ml-1.5 text-xs text-slate-400">{t.count}</span>}
          </button>
        ))}
      </div>

      <div className="mt-5">
        {tab === 'overview' && <Overview pr={pr} />}
        {tab === 'files' && <FilesTab pr={pr} />}
        {tab === 'commits' && <EmptyState title="Commits" message="Commit history comes from the source-control provider. Not part of the PoC." />}
        {tab === 'checks' && <EmptyState title="Checks" message="Build and policy checks will be read from Azure DevOps / GitHub in a later iteration." />}
        {tab === 'discussion' && <EmptyState title="Discussion" message="Review threads stay in your source-control tool; ReviewHub links out instead of duplicating them." />}
      </div>
    </div>
  )
}

// ------------------------------------------------------------------ header

function PrHeader({ pr }: { pr: PullRequestDetail }) {
  const action = useReviewAction(pr.id)
  const defaultReviewer = useDefaultReviewer(pr)
  const active = pr.activeAssignment
  const isOpen = pr.status === 'WaitingForReview' || pr.status === 'InReview' || pr.status === 'ChangesRequested'

  return (
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <PriorityBadge band={pr.priority.band} size="md" />
          <StatusBadge status={pr.status} />
          <AgingBadge state={pr.agingState} hideQuiet />
        </div>
        <h2 className="mt-2 text-xl font-semibold tracking-tight text-slate-900">{pr.title}</h2>
        <p className="mt-1 flex flex-wrap items-center gap-x-2 text-sm text-slate-500">
          <span className="font-medium text-slate-700">#{pr.externalId}</span>
          <span>in {pr.repositoryName}</span>
          <span className="text-slate-300">·</span>
          <span className="inline-flex items-center gap-1 font-mono text-xs">
            <GitBranch className="size-3.5" />
            {pr.sourceBranch} → {pr.targetBranch}
          </span>
        </p>
      </div>

      <div className="flex items-center gap-2">
        {isOpen && active?.status !== 'Acknowledged' && defaultReviewer && (
          <Button variant="primary" disabled={action.isPending} onClick={() => action.mutate({ kind: 'start', reviewerId: defaultReviewer })}>
            {action.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <Play className="size-4" />}
            Start Review
          </Button>
        )}
        {active?.status === 'Acknowledged' && (
          <span className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-indigo-50 px-3 text-sm font-medium text-indigo-700">
            <UserCheck className="size-4" /> In review by {active.reviewerName}
          </span>
        )}
        <OverflowMenu pr={pr} />
      </div>
    </div>
  )
}

function OverflowMenu({ pr }: { pr: PullRequestDetail }) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const action = useReviewAction(pr.id)

  useEffect(() => {
    if (!open) return
    const close = (e: MouseEvent) => !ref.current?.contains(e.target as Node) && setOpen(false)
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [open])

  const item = 'flex w-full items-center gap-2 rounded-md px-2.5 py-2 text-left text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-40'
  return (
    <div className="relative" ref={ref}>
      <Button variant="secondary" aria-label="More actions" onClick={() => setOpen((o) => !o)} className="px-2.5">
        <MoreHorizontal className="size-4" />
      </Button>
      {open && (
        <div className="absolute right-0 z-20 mt-1 w-60 rounded-xl border border-slate-200 bg-white p-1 shadow-lg">
          <button className={item} onClick={() => { action.mutate({ kind: 'recalculate' }); setOpen(false) }}>
            <RefreshCw className="size-4 text-slate-400" /> Recalculate scores & brief
          </button>
          <button
            className={item}
            disabled={pr.activeAssignment?.status !== 'Assigned'}
            onClick={() => { action.mutate({ kind: 'simulate-lease-expiry' }); setOpen(false) }}
          >
            <TimerOff className="size-4 text-slate-400" /> Simulate lease expiry
          </button>
          <a className={item} href={pr.externalUrl} target="_blank" rel="noreferrer noopener">
            <ExternalLink className="size-4 text-slate-400" /> Open in source control
          </a>
        </div>
      )}
    </div>
  )
}

/** The reviewer a "Start Review" click acts as: the assignee, else the top eligible candidate. */
function useDefaultReviewer(pr: PullRequestDetail): string | undefined {
  return pr.activeAssignment?.reviewerId ?? pr.candidates.find((c) => c.eligible)?.reviewerId
}

// ------------------------------------------------------------------ overview

function Overview({ pr }: { pr: PullRequestDetail }) {
  return (
    <div className="space-y-4">
      {(pr.reviewSurfaceWarning || pr.wipWarning) && (
        <div className="grid gap-3 lg:grid-cols-2">
          {pr.reviewSurfaceWarning && (
            <Callout tone="warning" icon={<Maximize2 className="size-4" />} title={pr.reviewSurfaceWarning.title}>
              {pr.reviewSurfaceWarning.message}
            </Callout>
          )}
          {pr.wipWarning && (
            <Callout tone="info" icon={<Users className="size-4" />} title="Review pipeline load">
              {pr.wipWarning.message}
            </Callout>
          )}
        </div>
      )}

      {pr.brief ? (
        <ReviewBriefCard brief={pr.brief} />
      ) : (
        <Card title="AI Review Brief">
          <p className="text-sm text-slate-500">No brief available — the configured brief provider is not enabled.</p>
        </Card>
      )}

      <div className="grid gap-4 lg:grid-cols-[1.2fr_1fr]">
        <DetailsCard pr={pr} />
        <div className="space-y-4">
          <RiskCard pr={pr} />
          <AssignmentCard pr={pr} />
        </div>
      </div>
    </div>
  )
}

function Callout({ tone, icon, title, children }: { tone: 'warning' | 'info'; icon: React.ReactNode; title: string; children: React.ReactNode }) {
  const styles = tone === 'warning' ? 'border-amber-200 bg-amber-50 text-amber-900' : 'border-sky-200 bg-sky-50 text-sky-900'
  return (
    <div className={`flex gap-3 rounded-xl border px-4 py-3 ${styles}`} role="note">
      <span className="mt-0.5">{icon}</span>
      <div>
        <p className="text-sm font-semibold">{title}</p>
        <p className="mt-0.5 text-sm opacity-90">{children}</p>
      </div>
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className="mt-0.5 text-sm text-slate-800">{children}</dd>
    </div>
  )
}

function DetailsCard({ pr }: { pr: PullRequestDetail }) {
  return (
    <Card title="PR Details">
      <dl className="grid grid-cols-2 gap-x-4 gap-y-3 sm:grid-cols-3">
        <Field label="Author">
          <span className="inline-flex items-center gap-1.5">
            <ReviewerAvatar name={pr.authorName} size="sm" /> {pr.authorName}
          </span>
        </Field>
        <Field label="Waiting">
          <span className="tabular-nums">{formatDuration(pr.waitMinutes)}</span>
        </Field>
        <Field label="Risk">
          <RiskBadge risk={pr.risk.level} />
        </Field>
        <Field label="Changes">
          <span className="tabular-nums">
            <span className="text-emerald-600">+{formatNumber(pr.additions)}</span>{' '}
            <span className="text-red-600">−{formatNumber(pr.deletions)}</span>
          </span>
        </Field>
        <Field label="Files">
          {pr.changedFiles} <span className="text-slate-400">({pr.productionFiles} production)</span>
        </Field>
        <Field label="Meaningful lines">{formatNumber(pr.meaningfulChangedLines)}</Field>
        <Field label="Source branch">
          <span className="font-mono text-xs break-all">{pr.sourceBranch}</span>
        </Field>
        <Field label="Work item">{pr.linkedWorkItem ?? <span className="text-slate-400">none linked</span>}</Field>
        <Field label="Opened">{timeAgo(pr.createdAt)}</Field>
      </dl>

      <div className="mt-4">
        <p className="text-xs text-slate-500">Labels</p>
        <div className="mt-1 flex flex-wrap gap-1">
          {pr.labels.map((l) => (
            <LabelChip key={l} label={l} />
          ))}
        </div>
      </div>

      {/* Rendered as plain text: PR descriptions are untrusted input. */}
      <div className="mt-4">
        <p className="text-xs text-slate-500">Description</p>
        <p className="mt-1 text-sm leading-relaxed whitespace-pre-line text-slate-700">{pr.description || 'No description provided.'}</p>
      </div>

      <div className="mt-5 rounded-xl border border-slate-100 bg-slate-50 p-4">
        <div className="flex items-center justify-between">
          <p className="text-sm font-semibold text-slate-800">Why this is ranked {pr.priority.band === 'High' ? 'highly' : 'here'}</p>
          <span className="text-xs text-slate-500 tabular-nums">score {Math.round(pr.priority.score)}</span>
        </div>
        <ul className="mt-2 space-y-1">
          {pr.priority.reasons.map((r) => {
            const [points, ...rest] = r.split(' ')
            const negative = points!.startsWith('-')
            return (
              <li key={r} className="flex items-baseline gap-2 text-sm">
                <span className={`w-9 shrink-0 text-right font-semibold tabular-nums ${negative ? 'text-red-600' : 'text-emerald-700'}`}>
                  {points}
                </span>
                <span className="text-slate-700">{rest.join(' ')}</span>
              </li>
            )
          })}
        </ul>
      </div>
    </Card>
  )
}

function RiskCard({ pr }: { pr: PullRequestDetail }) {
  return (
    <Card title="Risk Assessment" icon={<ShieldCheck className="mt-0.5 size-4 text-slate-400" />} actions={<RiskBadge risk={pr.risk.level} />}>
      <p className="text-xs text-slate-500">Areas touched</p>
      <div className="mt-1.5 flex flex-wrap gap-1">
        {pr.risk.areasTouched.map((a) => (
          <Badge key={a} tone="slate">
            {a}
          </Badge>
        ))}
      </div>

      <p className="mt-4 text-xs text-slate-500">Why {pr.risk.level.toLowerCase()} risk</p>
      <ul className="mt-1.5 space-y-1 text-sm text-slate-700">
        {pr.risk.reasons.map((r) => (
          <li key={r} className="flex gap-2">
            <span className="mt-2 size-1 shrink-0 rounded-full bg-slate-400" />
            {r}
          </li>
        ))}
      </ul>

      <ul className="mt-4 space-y-1.5 border-t border-slate-100 pt-3">
        {pr.risk.signals.map((s) => (
          <li key={s.text} className="flex items-center gap-2 text-sm">
            {s.isWarning ? (
              <TriangleAlert className="size-4 shrink-0 text-amber-500" />
            ) : (
              <CircleCheck className="size-4 shrink-0 text-emerald-600" />
            )}
            <span className={s.isWarning ? 'text-slate-800' : 'text-slate-600'}>{s.text}</span>
          </li>
        ))}
      </ul>
    </Card>
  )
}

function AssignmentCard({ pr }: { pr: PullRequestDetail }) {
  const action = useReviewAction(pr.id)
  const { data: reviewers } = useReviewers()
  const { data: me } = useMe()
  const active = pr.activeAssignment
  const isOpen = pr.status === 'WaitingForReview' || pr.status === 'InReview' || pr.status === 'ChangesRequested'
  const defaultReviewer = useDefaultReviewer(pr)
  const [reviewAs, setReviewAs] = useState<string | undefined>(undefined)
  useEffect(() => setReviewAs(undefined), [pr.id, active?.reviewerId])
  const actingReviewer = reviewAs ?? defaultReviewer

  const others = pr.candidates.filter((c) => c.reviewerId !== active?.reviewerId).slice(0, 2)
  const firstName = active?.reviewerName.split(' ')[0]
  const busy = action.isPending

  return (
    <Card title="Review Assignment" icon={<UserCheck className="mt-0.5 size-4 text-slate-400" />}>
      {active ? (
        <div className="space-y-3">
          <div className="flex items-center gap-3">
            <ReviewerAvatar name={active.reviewerName} size="lg" />
            <div className="min-w-0 flex-1">
              <p className="text-sm font-semibold text-slate-900">
                {active.reviewerName}
                {me?.id === active.reviewerId && <span className="ml-1.5 text-xs font-normal text-slate-400">(you)</span>}
              </p>
              <p className="text-xs text-slate-500">
                {active.status === 'Acknowledged'
                  ? `Reviewing since ${timeAgo(active.acknowledgedAt!)}`
                  : `Assigned ${timeAgo(active.assignedAt)} · not started yet`}
              </p>
            </div>
          </div>

          <dl className="grid grid-cols-2 gap-3 rounded-xl bg-slate-50 p-3">
            <div>
              <dt className="text-xs text-slate-500">Lease remaining</dt>
              <dd className="mt-0.5 text-sm font-medium text-slate-800 tabular-nums">
                {active.leaseRemainingMinutes === null ? (
                  <span className="text-slate-500">n/a — review started</span>
                ) : (
                  <span className={active.leaseRemainingMinutes < 30 ? 'text-orange-700' : ''}>{formatDuration(active.leaseRemainingMinutes)}</span>
                )}
              </dd>
            </div>
            <div>
              <dt className="text-xs text-slate-500">Active reviews</dt>
              <dd className="mt-1">
                <ReviewerCapacity active={active.reviewerActiveReviews} capacity={active.reviewerCapacity} />
              </dd>
            </div>
          </dl>

          {active.whyReasons.length > 0 && (
            <div>
              <p className="text-xs font-semibold text-slate-700">Why {firstName}?</p>
              <ul className="mt-1 space-y-0.5 text-sm text-slate-600">
                {active.whyReasons.map((r) => (
                  <li key={r} className="flex gap-2">
                    <span className="mt-2 size-1 shrink-0 rounded-full bg-indigo-400" /> {r}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      ) : (
        <div className="flex items-start gap-2 rounded-xl bg-slate-50 p-3 text-sm text-slate-600">
          <Info className="mt-0.5 size-4 shrink-0 text-slate-400" />
          {pr.status === 'Approved'
            ? 'Review complete — approved.'
            : pr.status === 'ChangesRequested'
              ? 'Changes were requested. Start another review once the author has pushed updates.'
              : (pr.routingNote ?? 'No reviewer assigned.')}
        </div>
      )}

      {pr.routingNote && active && <p className="mt-3 text-xs text-slate-500">{pr.routingNote}</p>}

      {others.length > 0 && isOpen && (
        <div className="mt-4 border-t border-slate-100 pt-3">
          <p className="text-xs text-slate-500">{active ? 'Next candidates' : 'Candidates'}</p>
          <ul className="mt-2 space-y-2">
            {others.map((c) => (
              <li key={c.reviewerId} className="flex items-center gap-2.5" title={c.reasons.join('\n')}>
                <ReviewerAvatar name={c.name} size="sm" />
                <div className="min-w-0 flex-1">
                  <p className={`text-sm ${c.eligible ? 'text-slate-800' : 'text-slate-400'}`}>
                    {c.name} <span className="text-xs tabular-nums text-slate-400">· score {c.score}</span>
                  </p>
                  <p className="truncate text-xs text-slate-500">{c.reasons.filter((r) => !r.includes('active review')).slice(0, 2).join(' · ') || '—'}</p>
                </div>
                <ReviewerCapacity active={c.activeReviews} capacity={c.capacity} />
              </li>
            ))}
          </ul>
        </div>
      )}

      {isOpen && (
        <div className="mt-4 space-y-2 border-t border-slate-100 pt-4">
          {active?.status !== 'Acknowledged' && reviewers && (
            <label className="flex items-center gap-2 text-xs text-slate-500">
              Review as
              <select
                value={actingReviewer ?? ''}
                onChange={(e) => setReviewAs(e.target.value)}
                className="h-8 flex-1 rounded-lg border border-slate-200 bg-white px-2 text-sm text-slate-700"
              >
                {reviewers
                  .filter((r) => r.isActive && r.externalId !== pr.authorId)
                  .map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.name} ({r.activeReviews}/{r.capacity})
                    </option>
                  ))}
              </select>
            </label>
          )}
          <div className="flex flex-wrap gap-2">
            {active?.status === 'Acknowledged' ? (
              <>
                <Button variant="success" disabled={busy} onClick={() => action.mutate({ kind: 'approve' })}>
                  <CircleCheck className="size-4" /> Approve
                </Button>
                <Button variant="warning" disabled={busy} onClick={() => action.mutate({ kind: 'request-changes' })}>
                  <X className="size-4" /> Request Changes
                </Button>
              </>
            ) : (
              <Button
                variant="primary"
                disabled={busy || !actingReviewer}
                onClick={() => actingReviewer && action.mutate({ kind: 'start', reviewerId: actingReviewer })}
              >
                <Play className="size-4" /> Start Review
              </Button>
            )}
            {active && (
              <Button variant="secondary" disabled={busy} onClick={() => action.mutate({ kind: 'release' })}>
                <Undo2 className="size-4" /> Release
              </Button>
            )}
          </div>
        </div>
      )}
    </Card>
  )
}

// ------------------------------------------------------------------ files

function FilesTab({ pr }: { pr: PullRequestDetail }) {
  return (
    <Card title="Changed files" subtitle={`${pr.changedFiles} files · ${formatNumber(pr.meaningfulChangedLines)} meaningful lines (generated & lock files excluded)`}>
      <ul className="divide-y divide-slate-100">
        {pr.files.map((f) => (
          <li key={f.path} className="flex items-center gap-3 py-2">
            <FileCode className={`size-4 shrink-0 ${f.isSecuritySensitive ? 'text-red-500' : 'text-slate-400'}`} />
            <div className="min-w-0 flex-1">
              <p className="truncate font-mono text-[13px] text-slate-800" title={f.path}>
                {fileName(f.path)} <span className="font-sans text-xs text-slate-400">{f.path}</span>
              </p>
            </div>
            <div className="flex shrink-0 items-center gap-1.5">
              {f.isSecuritySensitive && <Badge tone="red">security</Badge>}
              {f.isGenerated && <Badge tone="slate">generated</Badge>}
              {f.isTestFile && <Badge tone="green">test</Badge>}
              {!f.isGenerated && !f.isTestFile && !f.isSecuritySensitive && <Badge tone="slate">{f.category.toLowerCase()}</Badge>}
              <span className="w-24 text-right text-xs tabular-nums">
                <span className="text-emerald-600">+{f.additions}</span> <span className="text-red-600">−{f.deletions}</span>
              </span>
            </div>
          </li>
        ))}
      </ul>
    </Card>
  )
}
