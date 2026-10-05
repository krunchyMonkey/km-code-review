import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './client'
import type { PullRequestDetail, QueueFilters } from './types'
import { useToast } from '../components/Toast'

// Poll gently so lease expirations done by the background service show up without a manual refresh.
const REFRESH_MS = 30_000

export const useReviewQueue = (filters: QueueFilters) =>
  useQuery({
    queryKey: ['review-queue', filters],
    queryFn: () => api.queue(filters),
    refetchInterval: REFRESH_MS,
    placeholderData: (previous) => previous,
  })

export const usePullRequest = (id: string | null) =>
  useQuery({
    queryKey: ['pull-request', id],
    queryFn: () => api.pullRequest(id!),
    enabled: !!id,
    refetchInterval: REFRESH_MS,
  })

export const useReviewers = () => useQuery({ queryKey: ['reviewers'], queryFn: api.reviewers, refetchInterval: REFRESH_MS })

export const useMe = () => useQuery({ queryKey: ['me'], queryFn: api.me, refetchInterval: REFRESH_MS })

export const useMetrics = () => useQuery({ queryKey: ['metrics-summary'], queryFn: api.metrics, refetchInterval: REFRESH_MS })

type ReviewAction =
  | { kind: 'start'; reviewerId: string }
  | { kind: 'release' }
  | { kind: 'approve' }
  | { kind: 'request-changes' }
  | { kind: 'recalculate' }
  | { kind: 'simulate-lease-expiry' }

const successMessage: Record<ReviewAction['kind'], (pr: PullRequestDetail) => string> = {
  start: (pr) => `Review started on #${pr.externalId}`,
  release: (pr) =>
    pr.activeAssignment
      ? `Released #${pr.externalId} — reassigned to ${pr.activeAssignment.reviewerName}`
      : `Released #${pr.externalId} — no reviewer has capacity right now`,
  approve: (pr) => `#${pr.externalId} approved`,
  'request-changes': (pr) => `Changes requested on #${pr.externalId}`,
  recalculate: (pr) => `Recalculated #${pr.externalId}`,
  'simulate-lease-expiry': (pr) =>
    pr.activeAssignment
      ? `Lease expired — #${pr.externalId} rerouted to ${pr.activeAssignment.reviewerName}`
      : `Lease expired — no eligible reviewer for #${pr.externalId}`,
}

/** All review mutations share one hook so cache invalidation stays consistent. */
export function useReviewAction(prId: string) {
  const queryClient = useQueryClient()
  const toast = useToast()

  return useMutation({
    mutationFn: (action: ReviewAction) => {
      switch (action.kind) {
        case 'start':
          return api.startReview(prId, action.reviewerId)
        case 'release':
          return api.releaseReview(prId)
        case 'approve':
          return api.completeReview(prId, 'Approved')
        case 'request-changes':
          return api.completeReview(prId, 'ChangesRequested')
        case 'recalculate':
          return api.recalculate(prId)
        case 'simulate-lease-expiry':
          return api.simulateLeaseExpiry(prId)
      }
    },
    onSuccess: (detail, action) => {
      queryClient.setQueryData(['pull-request', prId], detail)
      queryClient.invalidateQueries({ queryKey: ['pull-request', prId] })
      queryClient.invalidateQueries({ queryKey: ['review-queue'] })
      queryClient.invalidateQueries({ queryKey: ['metrics-summary'] })
      queryClient.invalidateQueries({ queryKey: ['reviewers'] })
      queryClient.invalidateQueries({ queryKey: ['me'] })
      toast.success(successMessage[action.kind](detail))
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : 'Something went wrong'),
  })
}
