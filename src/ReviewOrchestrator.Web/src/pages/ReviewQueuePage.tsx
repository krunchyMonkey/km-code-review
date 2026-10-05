import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router'
import { Search } from 'lucide-react'
import { useReviewQueue } from '../api/hooks'
import type { QueueFilters } from '../api/types'
import { QueueItem } from '../components/QueueItem'
import { EmptyState, FilterDropdown, Spinner } from '../components/Primitives'
import { PullRequestPanel } from './PullRequestPanel'

type Tab = 'all' | 'High' | 'Medium' | 'Low' | 'aging'

export interface QueuePreset {
  title: string
  subtitle: string
  filters?: QueueFilters
}

const statusOptions = [
  { value: '', label: 'All open' },
  { value: 'WaitingForReview', label: 'Waiting for review' },
  { value: 'InReview', label: 'In review' },
  { value: 'ChangesRequested', label: 'Changes requested' },
  { value: 'Approved', label: 'Approved' },
  { value: 'all', label: 'Any status' },
]

const sortOptions = [
  { value: 'priority-desc', label: 'Sort: Priority' },
  { value: 'oldest', label: 'Sort: Oldest first' },
  { value: 'newest', label: 'Sort: Newest first' },
  { value: 'smallest', label: 'Sort: Smallest first' },
  { value: 'largest', label: 'Sort: Largest first' },
  { value: 'risk-desc', label: 'Sort: Risk' },
]

export function ReviewQueuePage({ preset }: { preset: QueuePreset }) {
  const [searchParams, setSearchParams] = useSearchParams()
  const selectedId = searchParams.get('pr')

  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [repository, setRepository] = useState('')
  const [status, setStatus] = useState(preset.filters?.status ?? '')
  const [sort, setSort] = useState('priority-desc')
  const [tab, setTab] = useState<Tab>(preset.filters?.agingState ? 'aging' : 'all')

  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(search.trim()), 250)
    return () => clearTimeout(t)
  }, [search])

  const filters: QueueFilters = useMemo(
    () => ({
      ...preset.filters,
      repository: repository || undefined,
      status: status || undefined,
      sort,
      search: debouncedSearch || undefined,
      priority: tab === 'High' || tab === 'Medium' || tab === 'Low' ? tab : undefined,
      agingState: tab === 'aging' ? 'attention' : undefined,
    }),
    [preset.filters, repository, status, sort, debouncedSearch, tab],
  )

  const { data, isLoading, isError, error } = useReviewQueue(filters)

  // Keep a PR selected: default to the top of the queue so the detail pane is never empty.
  useEffect(() => {
    if (!data || selectedId) return
    const first = data.items[0]
    if (first) setSearchParams({ pr: first.id }, { replace: true })
  }, [data, selectedId, setSearchParams])

  const select = (id: string) => setSearchParams({ pr: id })

  const tabs: { id: Tab; label: string; count?: number }[] = [
    { id: 'all', label: 'All', count: data?.summary.total },
    { id: 'High', label: 'High', count: data?.summary.high },
    { id: 'Medium', label: 'Medium', count: data?.summary.medium },
    { id: 'Low', label: 'Low', count: data?.summary.low },
    { id: 'aging', label: 'Aging', count: data?.summary.aging },
  ]

  return (
    <div className="grid h-full min-h-0 xl:grid-cols-[minmax(400px,460px)_1fr]">
      <div className="flex min-h-0 flex-col border-slate-200 xl:border-r">
        <header className="space-y-3 px-5 pt-5 pb-3">
          <div>
            <h1 className="text-xl font-semibold tracking-tight text-slate-900">{preset.title}</h1>
            <p className="text-sm text-slate-500">{preset.subtitle}</p>
          </div>

          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-slate-400" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search title, #number, author, label…"
              className="h-9 w-full rounded-lg border border-slate-200 bg-white pr-3 pl-9 text-sm shadow-sm outline-none placeholder:text-slate-400 focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100"
            />
          </div>

          <div className="flex flex-wrap gap-2">
            <FilterDropdown
              label="Repository"
              value={repository}
              onChange={setRepository}
              options={[{ value: '', label: 'All repositories' }, ...(data?.repositories ?? []).map((r) => ({ value: r, label: r }))]}
            />
            <FilterDropdown
              label="Priority"
              value={tab === 'High' || tab === 'Medium' || tab === 'Low' ? tab : ''}
              onChange={(v) => setTab((v || 'all') as Tab)}
              options={[
                { value: '', label: 'Any priority' },
                { value: 'High', label: 'High' },
                { value: 'Medium', label: 'Medium' },
                { value: 'Low', label: 'Low' },
              ]}
            />
            {!preset.filters?.status && <FilterDropdown label="Status" value={status} onChange={setStatus} options={statusOptions} />}
            <FilterDropdown label="Sort" value={sort} onChange={setSort} options={sortOptions} />
          </div>

          <div className="flex gap-1 border-b border-slate-200" role="tablist">
            {tabs.map((t) => (
              <button
                key={t.id}
                role="tab"
                aria-selected={tab === t.id}
                onClick={() => setTab(t.id)}
                className={`-mb-px flex items-center gap-1.5 border-b-2 px-2.5 pb-2 text-sm font-medium transition-colors ${
                  tab === t.id ? 'border-indigo-600 text-indigo-700' : 'border-transparent text-slate-500 hover:text-slate-800'
                }`}
              >
                {t.label}
                <span
                  className={`rounded-full px-1.5 text-[11px] tabular-nums ${
                    tab === t.id ? 'bg-indigo-100 text-indigo-700' : t.id === 'aging' && t.count ? 'bg-orange-100 text-orange-700' : 'bg-slate-100 text-slate-500'
                  }`}
                >
                  {t.count ?? '–'}
                </span>
              </button>
            ))}
          </div>
        </header>

        <div className="min-h-0 flex-1 space-y-2 overflow-y-auto px-5 pb-5 max-xl:max-h-[60vh]">
          {isLoading && <Spinner label="Loading queue…" />}
          {isError && <EmptyState title="Could not load the queue" message={(error as Error).message} />}
          {data && data.items.length === 0 && (
            <EmptyState title="Nothing here" message="No pull requests match these filters. The queue is clear — nice." />
          )}
          {data?.items.map((item) => (
            <QueueItem key={item.id} item={item} selected={item.id === selectedId} onSelect={() => select(item.id)} />
          ))}
        </div>
      </div>

      <div className="min-h-0 overflow-y-auto">
        {selectedId ? (
          <PullRequestPanel id={selectedId} />
        ) : (
          !isLoading && <EmptyState title="Select a pull request" message="Pick an item from the queue to see its review brief." />
        )}
      </div>
    </div>
  )
}
