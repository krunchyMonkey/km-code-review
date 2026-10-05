import type { MetricsSummary, PullRequestDetail, QueueFilters, QueueResponse, Reviewer } from './types'

/** Error carrying the API's RFC 7807 Problem Details message. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message)
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    let message = `Request failed (${response.status})`
    try {
      const problem = await response.json()
      message = problem.detail ?? problem.title ?? message
    } catch {
      // Non-JSON error body; keep the generic message.
    }
    throw new ApiError(response.status, message)
  }
  return response.json() as Promise<T>
}

function toQueryString(filters: QueueFilters): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(filters)) {
    if (value) params.set(key, value)
  }
  const qs = params.toString()
  return qs ? `?${qs}` : ''
}

export const api = {
  queue: (filters: QueueFilters) => request<QueueResponse>(`/api/review-queue${toQueryString(filters)}`),
  pullRequest: (id: string) => request<PullRequestDetail>(`/api/pull-requests/${id}`),
  startReview: (id: string, reviewerId: string) =>
    request<PullRequestDetail>(`/api/pull-requests/${id}/start-review`, {
      method: 'POST',
      body: JSON.stringify({ reviewerId }),
    }),
  releaseReview: (id: string) => request<PullRequestDetail>(`/api/pull-requests/${id}/release-review`, { method: 'POST' }),
  completeReview: (id: string, outcome: 'Approved' | 'ChangesRequested') =>
    request<PullRequestDetail>(`/api/pull-requests/${id}/complete-review`, {
      method: 'POST',
      body: JSON.stringify({ outcome }),
    }),
  recalculate: (id: string) => request<PullRequestDetail>(`/api/pull-requests/${id}/recalculate`, { method: 'POST' }),
  simulateLeaseExpiry: (id: string) =>
    request<PullRequestDetail>(`/api/pull-requests/${id}/simulate-lease-expiry`, { method: 'POST' }),
  reviewers: () => request<Reviewer[]>('/api/reviewers'),
  me: () => request<Reviewer>('/api/me'),
  metrics: () => request<MetricsSummary>('/api/metrics/summary'),
}
