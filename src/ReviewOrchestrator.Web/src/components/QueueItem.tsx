import { Ban, Flame, Link2, Maximize2 } from 'lucide-react'
import type { QueueItem as QueueItemData } from '../api/types'
import { formatDuration, statusLabel } from '../lib/format'
import { AgingBadge, LabelChip, PriorityBadge } from './Badges'
import { ReviewerAvatar } from './Primitives'

export function QueueItem({ item, selected, onSelect }: { item: QueueItemData; selected: boolean; onSelect: () => void }) {
  const loud = item.agingState === 'Aging' || item.agingState === 'Stale' || item.agingState === 'Critical'
  return (
    <button
      onClick={onSelect}
      aria-current={selected}
      className={`group relative w-full rounded-xl border px-4 py-3.5 text-left transition-all ${
        selected
          ? 'border-indigo-300 bg-indigo-50/60 shadow-sm ring-1 ring-indigo-200'
          : 'border-slate-200/80 bg-white hover:border-slate-300 hover:shadow-sm'
      }`}
    >
      {selected && <span className="absolute inset-y-3 left-0 w-1 rounded-r-full bg-indigo-500" />}
      <div className="flex items-start gap-3">
        <div className="flex w-16 shrink-0 flex-col items-start gap-1 pt-0.5">
          <PriorityBadge band={item.priorityBand} />
          <span className="text-[11px] text-slate-400 tabular-nums" title={item.priorityReasons.join('\n')}>
            score {Math.round(item.priorityScore)}
          </span>
        </div>

        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold text-slate-900">{item.title}</p>
          <p className="mt-0.5 truncate text-xs text-slate-500">
            #{item.externalId} in <span className="font-medium text-slate-600">{item.repositoryName}</span> · opened by {item.authorName}
          </p>

          <div className="mt-2 flex flex-wrap items-center gap-1">
            {item.isReleaseBlocker && (
              <span className="inline-flex items-center gap-1 rounded-md bg-red-600 px-1.5 py-0.5 text-[11px] font-semibold text-white">
                <Ban className="size-3" /> release blocker
              </span>
            )}
            {item.isProductionFix && (
              <span className="inline-flex items-center gap-1 rounded-md bg-red-50 px-1.5 py-0.5 text-[11px] font-semibold text-red-700">
                <Flame className="size-3" /> prod fix
              </span>
            )}
            {item.blocksOtherWork && (
              <span className="inline-flex items-center gap-1 rounded-md bg-violet-50 px-1.5 py-0.5 text-[11px] font-semibold text-violet-700">
                <Link2 className="size-3" /> blocks work
              </span>
            )}
            {item.labels.slice(0, 3).map((l) => (
              <LabelChip key={l} label={l} />
            ))}
          </div>

          <div className="mt-2 flex items-center gap-3 text-xs tabular-nums">
            <span className="font-medium text-emerald-600">+{item.additions}</span>
            <span className="font-medium text-red-600">−{item.deletions}</span>
            <span className="text-slate-500">{item.changedFiles} files</span>
            {item.hasReviewSurfaceWarning && (
              <span className="inline-flex items-center gap-1 text-amber-700" title="Large review surface">
                <Maximize2 className="size-3" /> large
              </span>
            )}
          </div>
        </div>

        <div className="flex shrink-0 flex-col items-end gap-1.5 text-right">
          <span className={`text-sm font-semibold tabular-nums ${loud ? 'text-orange-700' : 'text-slate-700'}`}>
            {formatDuration(item.waitMinutes)}
          </span>
          <span className={`text-[11px] ${item.status === 'InReview' ? 'font-medium text-indigo-600' : 'text-slate-500'}`}>
            {statusLabel[item.status]}
          </span>
          <AgingBadge state={item.agingState} hideQuiet />
          {item.assignedReviewer && (
            <span className="mt-0.5 flex items-center gap-1 text-[11px] text-slate-500" title={`Assigned to ${item.assignedReviewer.name}`}>
              <ReviewerAvatar name={item.assignedReviewer.name} size="sm" />
            </span>
          )}
        </div>
      </div>
    </button>
  )
}
