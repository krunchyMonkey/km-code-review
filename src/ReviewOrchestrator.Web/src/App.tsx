import { Navigate, Route, Routes } from 'react-router'
import { useMe } from './api/hooks'
import { Sidebar } from './components/Sidebar'
import { Spinner } from './components/Primitives'
import { AnalyticsPage } from './pages/AnalyticsPage'
import { PlaceholderPage, SettingsPage, TeamPage } from './pages/OtherPages'
import { ReviewQueuePage } from './pages/ReviewQueuePage'

function MyQueue({ mode }: { mode: 'assigned' | 'in-review' }) {
  const { data: me } = useMe()
  if (!me) return <Spinner />
  return mode === 'assigned' ? (
    <ReviewQueuePage
      key="assigned"
      preset={{ title: 'Assigned to Me', subtitle: `Pull requests routed to ${me.name}`, filters: { reviewerId: me.id } }}
    />
  ) : (
    <ReviewQueuePage
      key="mine"
      preset={{ title: 'My Reviews', subtitle: 'Reviews you have started', filters: { reviewerId: me.id, status: 'InReview' } }}
    />
  )
}

export default function App() {
  return (
    <div className="flex h-full">
      <Sidebar />
      <main className="min-w-0 flex-1 overflow-y-auto xl:overflow-hidden">
        <Routes>
          <Route path="/" element={<ReviewQueuePage key="all" preset={{ title: 'PR Review Queue', subtitle: 'Prioritized pull requests needing review' }} />} />
          <Route path="/reviews" element={<Navigate to="/" replace />} />
          <Route path="/my-reviews" element={<MyQueue mode="in-review" />} />
          <Route path="/assigned" element={<MyQueue mode="assigned" />} />
          <Route
            path="/needs-attention"
            element={
              <ReviewQueuePage
                key="attention"
                preset={{ title: 'Needs Attention', subtitle: 'Aging, stale and critical pull requests', filters: { agingState: 'attention' } }}
              />
            }
          />
          <Route path="/analytics" element={<div className="h-full overflow-y-auto"><AnalyticsPage /></div>} />
          <Route path="/team" element={<div className="h-full overflow-y-auto"><TeamPage /></div>} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route
            path="/integrations/source-control"
            element={
              <PlaceholderPage
                title="Azure DevOps / GitHub"
                message="The PoC runs on the mock source-control provider. Live providers plug in behind ISourceControlProvider."
              />
            }
          />
          <Route
            path="/integrations/slack"
            element={
              <PlaceholderPage
                title="Slack"
                message="Notifications are logged by LoggingReviewNotificationService. A Slack implementation can replace it behind IReviewNotificationService."
              />
            }
          />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  )
}
