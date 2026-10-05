import { CircleCheck, Clock, Hourglass, Inbox, TriangleAlert, UserCheck } from 'lucide-react'
import { useMetrics } from '../api/hooks'
import type { AgingState } from '../api/types'
import { Card, EmptyState, MetricCard, ReviewerAvatar, ReviewerCapacity, Spinner } from '../components/Primitives'
import { formatDuration } from '../lib/format'

const bucketHint: Record<AgingState, string> = {
  Fresh: '< 4h',
  Waiting: '4–8h',
  Aging: '8–24h',
  Stale: '24–48h',
  Critical: '≥ 48h',
}

export function AnalyticsPage() {
  const { data, isLoading, isError, error } = useMetrics()

  if (isLoading) return <Spinner label="Loading metrics…" />
  if (isError || !data) return <EmptyState title="Could not load metrics" message={(error as Error | null)?.message} />

  const maxBucket = Math.max(1, ...data.agingBuckets.map((b) => b.count))

  return (
    <div className="mx-auto max-w-6xl space-y-5 px-6 py-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight text-slate-900">Analytics</h1>
        <p className="text-sm text-slate-500">Where review time is going right now</p>
      </div>

      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        <MetricCard label="Waiting for review" value={data.waitingForReview} icon={<Inbox className="size-4" />} />
        <MetricCard label="In review" value={data.inReview} icon={<UserCheck className="size-4" />} />
        <MetricCard label="Aging PRs" value={data.aging} hint="waiting ≥ 8h" icon={<TriangleAlert className="size-4" />} tone={data.aging > 0 ? 'warning' : 'default'} />
        <MetricCard label="Median review wait" value={formatDuration(data.medianWaitMinutes)} hint={`avg ${formatDuration(data.averageWaitMinutes)}`} icon={<Hourglass className="size-4" />} />
        <MetricCard
          label="Avg time to first review"
          value={data.averageTimeToFirstReviewMinutes === null ? '—' : formatDuration(data.averageTimeToFirstReviewMinutes)}
          icon={<Clock className="size-4" />}
        />
        <MetricCard label="Completed reviews" value={data.completedReviews} icon={<CircleCheck className="size-4" />} />
      </div>

      <div className="grid gap-4 lg:grid-cols-[1.3fr_1fr]">
        <Card title="Reviewer load" subtitle="Active (assigned + in-progress) reviews against each reviewer's limit">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs text-slate-500">
                <th className="pb-2 font-medium">Reviewer</th>
                <th className="pb-2 text-right font-medium">Active</th>
                <th className="pb-2 text-right font-medium">Capacity</th>
                <th className="pb-2 pl-4 font-medium">Load</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data.reviewerUtilization.map((r) => (
                <tr key={r.reviewerId} className={r.isActive ? '' : 'text-slate-400'}>
                  <td className="py-2">
                    <span className="flex items-center gap-2">
                      <ReviewerAvatar name={r.reviewerName} size="sm" /> {r.reviewerName}
                    </span>
                  </td>
                  <td className="py-2 text-right tabular-nums">{r.active}</td>
                  <td className="py-2 text-right tabular-nums">{r.capacity}</td>
                  <td className="py-2 pl-4">
                    <ReviewerCapacity active={r.active} capacity={r.capacity} inactive={!r.isActive} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>

        <Card title="Open PRs by aging bucket" subtitle="Time since ready for review">
          <ul className="space-y-3 pt-1" aria-label="Open PRs by aging bucket">
            {data.agingBuckets.map((b) => (
              <li key={b.state} className="group grid grid-cols-[5.5rem_1fr_2rem] items-center gap-3" title={`${b.state} (${bucketHint[b.state]}): ${b.count} PRs`}>
                <span className="text-sm text-slate-700">
                  {b.state}
                  <span className="block text-[11px] text-slate-400">{bucketHint[b.state]}</span>
                </span>
                <span className="h-3 rounded-r bg-slate-100">
                  <span
                    className="block h-full rounded-r bg-indigo-500 transition-[width] group-hover:bg-indigo-600"
                    style={{ width: `${(b.count / maxBucket) * 100}%`, minWidth: b.count ? 4 : 0 }}
                  />
                </span>
                <span className="text-right text-sm font-medium text-slate-700 tabular-nums">{b.count}</span>
              </li>
            ))}
          </ul>
        </Card>
      </div>
    </div>
  )
}
