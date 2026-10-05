import { Eye, ListChecks, Sparkles, Wrench } from 'lucide-react'
import type { ReactNode } from 'react'
import type { Brief } from '../api/types'
import { fileName, timeAgo } from '../lib/format'

function Column({ title, icon, items, tone }: { title: string; icon: ReactNode; items: string[]; tone: 'indigo' | 'amber' | 'slate' }) {
  const styles = {
    indigo: 'bg-indigo-50/50 border-indigo-100 [&_h4]:text-indigo-900 [&_.dot]:bg-indigo-400',
    amber: 'bg-amber-50/50 border-amber-100 [&_h4]:text-amber-900 [&_.dot]:bg-amber-500',
    slate: 'bg-slate-50 border-slate-100 [&_h4]:text-slate-700 [&_.dot]:bg-slate-400',
  }
  return (
    <div className={`rounded-xl border p-4 ${styles[tone]}`}>
      <h4 className="mb-2.5 flex items-center gap-1.5 text-xs font-semibold tracking-wide uppercase">
        {icon}
        {title}
      </h4>
      <ul className="space-y-2">
        {items.map((item) => (
          <li key={item} className="flex gap-2 text-[13px] leading-snug text-slate-700">
            <span className="dot mt-1.5 size-1.5 shrink-0 rounded-full" />
            <span>{item}</span>
          </li>
        ))}
      </ul>
    </div>
  )
}

export function ReviewBriefCard({ brief }: { brief: Brief }) {
  return (
    <section className="rounded-2xl border border-slate-200/80 bg-white shadow-sm shadow-slate-900/[0.03]">
      <header className="flex items-start justify-between gap-3 px-5 pt-4">
        <div className="flex items-start gap-2.5">
          <span className="rounded-lg bg-gradient-to-br from-indigo-500 to-violet-500 p-1.5 text-white">
            <Sparkles className="size-4" />
          </span>
          <div>
            <h3 className="text-sm font-semibold text-slate-900">AI Review Brief</h3>
            <p className="mt-0.5 text-xs text-slate-500">A concise summary to help you review faster</p>
          </div>
        </div>
        <span className="text-xs whitespace-nowrap text-slate-400">Generated {timeAgo(brief.generatedAt)}</span>
      </header>

      <div className="grid gap-3 px-5 pt-4 lg:grid-cols-3">
        <Column title="What changed" icon={<ListChecks className="size-3.5" />} items={brief.whatChanged} tone="indigo" />
        <Column title="Worth human attention" icon={<Eye className="size-3.5" />} items={brief.humanAttentionAreas} tone="amber" />
        <Column title="Mostly mechanical" icon={<Wrench className="size-3.5" />} items={brief.mostlyMechanical} tone="slate" />
      </div>

      <div className="px-5 pt-5 pb-5">
        <h4 className="mb-2 text-xs font-semibold tracking-wide text-slate-500 uppercase">Recommended review order</h4>
        <ol className="divide-y divide-slate-100 rounded-xl border border-slate-100">
          {brief.recommendedReviewOrder.map((r) => (
            <li key={r.path} className="flex items-center gap-3 px-3 py-2.5">
              <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-indigo-600 text-xs font-semibold text-white">
                {r.rank}
              </span>
              <div className="min-w-0 flex-1">
                <p className="truncate font-mono text-[13px] font-medium text-slate-800" title={r.path}>
                  {fileName(r.path)}
                  <span className="ml-2 font-sans text-xs font-normal text-slate-400">{r.path.replace(fileName(r.path), '')}</span>
                </p>
                <p className="text-xs text-slate-500">{r.reason}</p>
              </div>
              {r.additions !== null && (
                <span className="shrink-0 text-xs tabular-nums">
                  <span className="text-emerald-600">+{r.additions}</span> <span className="text-red-600">−{r.deletions}</span>
                </span>
              )}
            </li>
          ))}
        </ol>
      </div>
    </section>
  )
}
