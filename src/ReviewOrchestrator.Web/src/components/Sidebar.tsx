import type { ReactNode } from 'react'
import { NavLink } from 'react-router'
import { ChartColumn, GitMerge, Inbox, ListTodo, MessageSquare, Settings, TriangleAlert, UserCheck, Users } from 'lucide-react'
import { useMe, useMetrics, useReviewQueue } from '../api/hooks'
import { ReviewerAvatar } from './Primitives'

function Item({ to, icon, children, count, end }: { to: string; icon: ReactNode; children: ReactNode; count?: number; end?: boolean }) {
  return (
    <NavLink
      to={to}
      end={end}
      className={({ isActive }) =>
        `flex items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm transition-colors ${
          isActive ? 'bg-white/10 font-medium text-white' : 'text-slate-400 hover:bg-sidebar-hover hover:text-slate-200'
        }`
      }
    >
      <span className="[&>svg]:size-4">{icon}</span>
      <span className="flex-1">{children}</span>
      {count !== undefined && count > 0 && (
        <span className="rounded-full bg-white/10 px-1.5 text-[11px] font-semibold text-slate-300 tabular-nums">{count}</span>
      )}
    </NavLink>
  )
}

function Section({ title, children }: { title?: string; children: ReactNode }) {
  return (
    <div className="space-y-0.5">
      {title && <p className="px-2.5 pt-5 pb-1.5 text-[11px] font-semibold tracking-wider text-slate-500 uppercase">{title}</p>}
      {children}
    </div>
  )
}

export function Sidebar() {
  const { data: me } = useMe()
  const { data: metrics } = useMetrics()
  const { data: mine } = useReviewQueue({ reviewerId: me?.id })

  return (
    <aside className="flex w-60 shrink-0 flex-col bg-sidebar px-3 py-4 max-md:hidden">
      <div className="flex items-center gap-2.5 px-2 pb-3">
        <span className="flex size-8 items-center justify-center rounded-lg bg-gradient-to-br from-indigo-500 to-violet-600 text-white shadow-lg shadow-indigo-900/40">
          <GitMerge className="size-4" />
        </span>
        <div>
          <p className="text-sm font-semibold text-white">ReviewHub</p>
          <p className="text-[11px] text-slate-500">PR Review Orchestrator</p>
        </div>
      </div>

      <nav className="flex-1 overflow-y-auto">
        <Section>
          <Item to="/" end icon={<Inbox />} count={metrics ? metrics.waitingForReview + metrics.inReview + metrics.changesRequested : undefined}>
            Review Queue
          </Item>
          <Item to="/my-reviews" icon={<ListTodo />}>
            My Reviews
          </Item>
          <Item to="/assigned" icon={<UserCheck />} count={mine?.items.length}>
            Assigned to Me
          </Item>
          <Item to="/needs-attention" icon={<TriangleAlert />} count={metrics?.aging}>
            Needs Attention
          </Item>
        </Section>
        <Section title="Insights">
          <Item to="/analytics" icon={<ChartColumn />}>
            Analytics
          </Item>
          <Item to="/team" icon={<Users />}>
            Team
          </Item>
          <Item to="/settings" icon={<Settings />}>
            Settings
          </Item>
        </Section>
        <Section title="Integrations">
          <Item to="/integrations/source-control" icon={<GitMerge />}>
            Azure DevOps / GitHub
          </Item>
          <Item to="/integrations/slack" icon={<MessageSquare />}>
            Slack
          </Item>
        </Section>
      </nav>

      {me && (
        <div className="mt-3 flex items-center gap-2.5 rounded-xl bg-white/5 px-2.5 py-2.5">
          <ReviewerAvatar name={me.name} />
          <div className="min-w-0">
            <p className="truncate text-sm font-medium text-white">{me.name}</p>
            <p className="text-[11px] text-slate-500">
              {me.activeReviews} / {me.capacity} active reviews
            </p>
          </div>
        </div>
      )}
    </aside>
  )
}
