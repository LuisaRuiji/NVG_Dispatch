# Dispatch Data Retention and Archive Policy

## Decision

Completed trips and audit logs are retained as operational evidence. They are not
deleted automatically when a trip is completed, because the system must preserve
proof-of-delivery, document-verification decisions, corrections, and accountability.

The live **Trip records** view contains current operations only. `Closed` and
`Cancelled` trips are available on the dedicated **Archives** page, with the same
server-side paging and operational filters. This keeps the dispatch workspace
focused while preserving finalized records for lookup and review.

Audit Logs are already role-scoped and server-paginated. They must remain
append-only during their retention period.

## Recommended retention schedule

| Data | Live operational database | Archive | Disposal |
| --- | --- | --- | --- |
| Closed or cancelled trip, its stops, status history, and documents | 12 months | 6 additional years in a read-only archive | Purge only after company/legal approval |
| Audit log | 12 months | 6 additional years in a read-only archive | Purge only after company/legal approval |
| High-volume driver location pings | 90 days | Optional monthly route summary, if required | Purge after 90 days |

The exact duration must be confirmed by the company because customer contracts,
tax obligations, insurance claims, and local record-keeping rules can require a
longer period.

## Archive implementation requirements

1. A nightly job selects only `Closed` or `Cancelled` trips older than the approved
   live-retention period. `Delivered` trips with unresolved documents are never
   archived.
2. It copies a complete, immutable snapshot of the trip, stops, documents,
   document versions, status history, and related audit events to a separate
   archive database or storage tier. Document files remain under a company-scoped
   archive storage key.
3. It verifies the copied record count and checksum before removing the live copy.
   Every archive and purge action creates its own audit event.
4. Historical search reads from the archive only when the user explicitly selects
   an archived date range. Dashboards, availability checks, and dispatch conflict
   detection always use the live operational data only.
5. Restore is manager/admin controlled, logged, and performed as a correction
   workflow rather than silently changing history.

## Why this is safe

Separating the screen view from physical storage prevents completed work from
overwhelming dispatchers immediately. Server-side pagination and the indexes on
trip status/update time and audit creation time keep the live queries bounded.
The later archive job addresses long-term database growth without losing the
traceability that trips and audit logs provide.
