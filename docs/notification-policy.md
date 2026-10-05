# Notification policy (future)

Status: **documented, partially implemented.** The PoC logs notifications through `LoggingReviewNotificationService` instead of sending them.

The goal is to escalate aging work on purpose instead of re-notifying people on every polling interval.

| Age since ready for review | Event | Audience | PoC behaviour |
|---|---|---|---|
| 0h | Assigned normally | Assigned reviewer | `NotifyAssignedAsync` logged on assignment |
| 4h | Quiet reminder / lease expiration | Assigned reviewer → next reviewer | Lease expires, PR is rerouted, `NotifyReassignedAsync` logged |
| 8h | PR enters **Aging** | Assigned reviewer | `NotifyAgingAsync` logged once |
| 24h | PR enters **Stale**, may be rerouted | Reviewer + component owners | `NotifyAgingAsync` logged once |
| 48h | PR enters **Critical**, team escalation | Team channel / lead | `NotifyAgingAsync` logged once |

## Anti-spam rules

- At most one notification per **state transition**. `PullRequest.LastNotifiedAgingState` records the last state notified, so the 60-second lease check never repeats a notification.
- Lease reminders go to one person, never to a channel.
- Team escalation (Critical) is batched into a digest, never one message per PR. *Not implemented.*
- Escalations stop as soon as a review is acknowledged. *Not implemented.*
