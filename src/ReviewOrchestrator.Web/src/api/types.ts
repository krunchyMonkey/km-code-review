// Mirrors the DTOs in ReviewOrchestrator.Infrastructure/Application/Dtos.cs.

export type PriorityBand = 'High' | 'Medium' | 'Low'
export type ReviewRisk = 'High' | 'Medium' | 'Low'
export type AgingState = 'Fresh' | 'Waiting' | 'Aging' | 'Stale' | 'Critical'
export type PullRequestStatus =
  | 'Draft'
  | 'WaitingForReview'
  | 'InReview'
  | 'ChangesRequested'
  | 'Approved'
  | 'Merged'
  | 'Closed'
export type AssignmentStatus = 'Assigned' | 'Acknowledged' | 'Released' | 'Completed' | 'Expired'

export interface ReviewerRef {
  id: string
  name: string
  avatarUrl: string
}

export interface QueueItem {
  id: string
  externalId: string
  title: string
  repositoryName: string
  authorId: string
  authorName: string
  readyForReviewAt: string
  waitMinutes: number
  priorityScore: number
  priorityBand: PriorityBand
  priorityReasons: string[]
  risk: ReviewRisk
  agingState: AgingState
  status: PullRequestStatus
  labels: string[]
  additions: number
  deletions: number
  changedFiles: number
  assignedReviewer: ReviewerRef | null
  assignmentStatus: AssignmentStatus | null
  hasReviewSurfaceWarning: boolean
  isReleaseBlocker: boolean
  isProductionFix: boolean
  blocksOtherWork: boolean
}

export interface QueueResponse {
  items: QueueItem[]
  summary: { total: number; high: number; medium: number; low: number; aging: number }
  repositories: string[]
}

export interface QueueFilters {
  repository?: string
  priority?: string
  risk?: string
  status?: string
  reviewerId?: string
  authorId?: string
  agingState?: string
  search?: string
  sort?: string
}

export interface Assignment {
  id: string
  reviewerId: string
  reviewerName: string
  status: AssignmentStatus
  assignedAt: string
  acknowledgedAt: string | null
  completedAt: string | null
  leaseExpiresAt: string
  leaseRemainingMinutes: number | null
  reviewerActiveReviews: number
  reviewerCapacity: number
  whyReasons: string[]
}

export interface Candidate {
  reviewerId: string
  name: string
  avatarUrl: string
  score: number
  eligible: boolean
  qualifiedForRisk: boolean
  activeReviews: number
  capacity: number
  isAssigned: boolean
  reasons: string[]
}

export interface Recommendation {
  rank: number
  path: string
  reason: string
  additions: number | null
  deletions: number | null
}

export interface Brief {
  generatedAt: string
  whatChanged: string[]
  humanAttentionAreas: string[]
  mostlyMechanical: string[]
  recommendedReviewOrder: Recommendation[]
}

export interface PrFile {
  path: string
  additions: number
  deletions: number
  isGenerated: boolean
  isTestFile: boolean
  isSecuritySensitive: boolean
  component: string | null
  category: string
}

export interface PullRequestDetail {
  id: string
  externalId: string
  title: string
  description: string
  repositoryName: string
  authorId: string
  authorName: string
  createdAt: string
  readyForReviewAt: string
  updatedAt: string
  waitMinutes: number
  status: PullRequestStatus
  agingState: AgingState
  sourceBranch: string
  targetBranch: string
  externalUrl: string
  linkedWorkItem: string | null
  labels: string[]
  additions: number
  deletions: number
  changedFiles: number
  meaningfulChangedLines: number
  productionFiles: number
  isReleaseBlocker: boolean
  isProductionFix: boolean
  blocksOtherWork: boolean
  isSecuritySensitive: boolean
  priority: { score: number; band: PriorityBand; reasons: string[] }
  risk: {
    level: ReviewRisk
    reasons: string[]
    areasTouched: string[]
    signals: { isWarning: boolean; text: string }[]
  }
  activeAssignment: Assignment | null
  assignmentHistory: Assignment[]
  candidates: Candidate[]
  routingNote: string | null
  brief: Brief | null
  files: PrFile[]
  wipWarning: { authorId: string; authorName: string; openReviewCount: number; message: string } | null
  reviewSurfaceWarning: { meaningfulChangedLines: number; productionFiles: number; title: string; message: string } | null
}

export interface Reviewer {
  id: string
  externalId: string
  name: string
  avatarUrl: string
  isActive: boolean
  activeReviews: number
  capacity: number
  components: string[]
  specialties: string[]
  repositoryHistory: string[]
}

export interface MetricsSummary {
  waitingForReview: number
  inReview: number
  changesRequested: number
  aging: number
  averageWaitMinutes: number
  medianWaitMinutes: number
  averageTimeToFirstReviewMinutes: number | null
  completedReviews: number
  reviewerUtilization: { reviewerId: string; reviewerName: string; active: number; capacity: number; isActive: boolean }[]
  agingBuckets: { state: AgingState; count: number }[]
}
