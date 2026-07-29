import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent, type MouseEvent } from "react";
import { Link } from "react-router-dom";
import { CheckCircle2, Clock3, ExternalLink, MapPin, RefreshCw, Route, Truck, X, Zap } from "lucide-react";
import { getMe } from "@/features/auth/authStore";
import { api, ApiRequestError } from "@/lib/api";
import { cn } from "@/lib/utils";
import { useToast } from "@/lib/useToast";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

export type CompletedTripRecommendationResponse = {
  tripId: string;
  driverId: string;
  truckId: string;
  driverName: string;
  truckPlate: string;
  currentLocation: string;
  availabilityReason: string;
  availableAt: string | null;
};

export type RecommendedTripRecommendationResponse = {
  tripId: string;
  containerNumber: string | null;
  pickupLocation: string;
  dropoffLocation: string;
  containerSize: string | null;
  tripType: string | null;
  scheduledPickupTime: string | null;
  agingHours: number;
};

export type DispatchRecommendationResponse = {
  suggestionId: string;
  selectionOrder: number;
  isRecommended: boolean;
  suitabilityReasons: string[];
  warnings: string[];
  currentMovement: CompletedTripRecommendationResponse;
  nextMovement: RecommendedTripRecommendationResponse;
  expiresAt: string;
  generatedAt: string;
  wasAccepted: boolean;
  wasIgnored: boolean;
};

type RecommendationGroup = {
  key: string;
  currentMovement: CompletedTripRecommendationResponse;
  options: DispatchRecommendationResponse[];
};

type RecommendationTime = {
  isExpired: boolean;
  label: string;
};

interface RecommendationPanelProps {
  className?: string;
}

interface RecommendationMetaProps {
  recommendation: DispatchRecommendationResponse;
  now: number;
  isRefreshing?: boolean;
}

const reviewerRoles = ["Dispatcher", "Manager", "Admin", "SuperAdmin"];

function formatTimeRemaining(expiresAt: string, now: number): RecommendationTime {
  const remainingSeconds = Math.max(0, Math.ceil((new Date(expiresAt).getTime() - now) / 1000));
  if (remainingSeconds === 0) return { isExpired: true, label: "Expired" };

  const minutes = Math.ceil(remainingSeconds / 60);
  return { isExpired: false, label: `Expires in ${minutes}m` };
}

function formatAvailability(reason: string, availableAt: string | null) {
  if (!availableAt) return reason;
  return `${reason} ${new Date(availableAt).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" })}`;
}

function formatPickupWindow(scheduledPickupTime: string | null) {
  if (!scheduledPickupTime) return "Pickup time not set";
  return new Date(scheduledPickupTime).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

function getTripReference(trip: RecommendedTripRecommendationResponse) {
  return trip.containerNumber ?? `Trip ${trip.tripId.slice(0, 8).toUpperCase()}`;
}

function RecommendationMeta({ recommendation, now, isRefreshing = false }: RecommendationMetaProps) {
  const time = formatTimeRemaining(recommendation.expiresAt, now);

  return (
    <div className="flex min-w-0 flex-wrap items-center gap-2 text-xs">
      <Badge variant="outline" className="border-border bg-card text-foreground">
        {recommendation.isRecommended ? "Recommended" : "Alternative"}
      </Badge>
      <span
        className={cn(
          "inline-flex items-center gap-1.5 font-medium",
          time.isExpired ? "text-amber-800" : "text-muted-foreground"
        )}
        aria-label={time.isExpired ? "Suggestion expired; refresh required" : time.label}
      >
        <Clock3 className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
        {time.label}
      </span>
      {isRefreshing ? (
        <span className="inline-flex items-center gap-1.5 text-muted-foreground" aria-live="polite">
          <RefreshCw className="h-3.5 w-3.5 animate-spin motion-reduce:animate-none" aria-hidden="true" />
          Updating
        </span>
      ) : null}
    </div>
  );
}

function QueueSkeleton() {
  return (
    <Card aria-label="Loading Trip Chaining suggestions">
      <CardContent className="p-4 sm:p-5">
        <div className="mb-4 h-5 w-48 animate-pulse rounded bg-muted" />
        <div className="space-y-2">
          {["first", "second", "third", "fourth"].map((key) => (
            <div key={key} className="h-20 animate-pulse rounded-xl bg-muted/70" />
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

export default function RecommendationPanel({ className }: RecommendationPanelProps) {
  const me = getMe();
  const canReview = reviewerRoles.some((role) => me?.roles.some((currentRole) => currentRole === role));
  const { show } = useToast();
  const [recommendations, setRecommendations] = useState<DispatchRecommendationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [fetchError, setFetchError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionLoadingId, setActionLoadingId] = useState<string | null>(null);
  const [selectedByGroup, setSelectedByGroup] = useState<Record<string, string>>({});
  const [reviewingGroupKey, setReviewingGroupKey] = useState<string | null>(null);
  const [now, setNow] = useState(() => Date.now());
  const drawerRef = useRef<HTMLDivElement | null>(null);
  const closeButtonRef = useRef<HTMLButtonElement | null>(null);
  const reviewTriggerRef = useRef<HTMLButtonElement | null>(null);

  const loadRecommendations = useCallback(async () => {
    const hasPreviousData = recommendations.length > 0;
    if (hasPreviousData) setIsRefreshing(true);
    else setLoading(true);

    try {
      const data = await api<DispatchRecommendationResponse[]>("/api/dispatch/trip-chaining/pending", { method: "GET" });
      setRecommendations(data ?? []);
      setFetchError(null);
    } catch (error: unknown) {
      const message = error instanceof ApiRequestError && error.status === 403
        ? "You do not have permission to review Trip Chaining suggestions."
        : "Could not update this queue. The last available suggestions are still shown.";
      setFetchError(message);
    } finally {
      setLoading(false);
      setIsRefreshing(false);
    }
  }, [recommendations.length]);

  useEffect(() => {
    if (!canReview) return;
    void loadRecommendations();
    const refreshInterval = window.setInterval(() => void loadRecommendations(), 30_000);
    const refreshHandler = () => void loadRecommendations();
    window.addEventListener("nvg:recommendations-refresh", refreshHandler);
    return () => {
      window.clearInterval(refreshInterval);
      window.removeEventListener("nvg:recommendations-refresh", refreshHandler);
    };
  }, [canReview, loadRecommendations]);

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1_000);
    return () => window.clearInterval(timer);
  }, []);

  const groups = useMemo<RecommendationGroup[]>(() => {
    const grouped = new Map<string, DispatchRecommendationResponse[]>();
    for (const recommendation of recommendations) {
      if (recommendation.wasAccepted || recommendation.wasIgnored) continue;
      const key = recommendation.currentMovement.tripId;
      grouped.set(key, [...(grouped.get(key) ?? []), recommendation]);
    }

    return Array.from(grouped.entries())
      .map(([key, options]) => ({
        key,
        currentMovement: options[0].currentMovement,
        options: [...options].sort((a, b) => a.selectionOrder - b.selectionOrder)
      }))
      .sort((a, b) => new Date(b.options[0].generatedAt).getTime() - new Date(a.options[0].generatedAt).getTime());
  }, [recommendations]);

  const reviewingGroup = groups.find((group) => group.key === reviewingGroupKey) ?? null;
  const selectedRecommendation = reviewingGroup
    ? reviewingGroup.options.find((option) => option.suggestionId === selectedByGroup[reviewingGroup.key]) ?? reviewingGroup.options[0]
    : null;
  const selectedTime = selectedRecommendation ? formatTimeRemaining(selectedRecommendation.expiresAt, now) : null;

  const closeReview = useCallback(() => {
    setReviewingGroupKey(null);
    setActionError(null);
    window.setTimeout(() => reviewTriggerRef.current?.focus(), 0);
  }, []);

  useEffect(() => {
    if (!reviewingGroup) return;
    closeButtonRef.current?.focus();
  }, [reviewingGroup]);

  useEffect(() => {
    if (reviewingGroupKey && !reviewingGroup) closeReview();
  }, [closeReview, reviewingGroup, reviewingGroupKey]);

  const handleDrawerKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === "Escape") {
      event.preventDefault();
      closeReview();
      return;
    }

    if (event.key !== "Tab" || !drawerRef.current) return;
    const focusable = Array.from(
      drawerRef.current.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])'
      )
    );
    if (focusable.length === 0) return;

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  const handleIgnore = async (recommendationId: string) => {
    setActionLoadingId(recommendationId);
    setActionError(null);
    try {
      await api(`/api/dispatch/trip-chaining/suggestions/${recommendationId}/dismiss`, { method: "POST" });
      closeReview();
      show("Next-movement suggestion dismissed.", "success");
      await loadRecommendations();
    } catch (error: unknown) {
      setActionError(error instanceof ApiRequestError && error.status === 409
        ? "This suggestion changed. Refresh the queue before reviewing it again."
        : "Could not dismiss this suggestion. Try again.");
      await loadRecommendations();
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleAccept = async (recommendationId: string) => {
    setActionLoadingId(recommendationId);
    setActionError(null);
    try {
      await api(`/api/dispatch/trip-chaining/suggestions/${recommendationId}/confirm`, { method: "POST" });
      closeReview();
      show("Next movement confirmed and dispatched.", "success");
      await loadRecommendations();
    } catch (error: unknown) {
      setActionError(error instanceof ApiRequestError && error.status === 409
        ? "This next-movement suggestion is no longer available. Refresh the queue before trying again."
        : "Could not confirm this assignment. Try again after refreshing the queue.");
      await loadRecommendations();
    } finally {
      setActionLoadingId(null);
    }
  };

  const openReview = (group: RecommendationGroup, event: MouseEvent<HTMLButtonElement>) => {
    reviewTriggerRef.current = event.currentTarget;
    setSelectedByGroup((current) => current[group.key] ? current : { ...current, [group.key]: group.options[0].suggestionId });
    setReviewingGroupKey(group.key);
    setActionError(null);
  };

  if (!canReview) return null;

  if (loading && recommendations.length === 0) return <QueueSkeleton />;

  if (groups.length === 0) {
    return (
      <Card className={cn(className)}>
        <CardContent className="flex items-start gap-3 p-5 sm:p-6">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-muted text-muted-foreground">
            <Zap className="h-5 w-5" aria-hidden="true" />
          </div>
          <div>
            <h2 className="font-semibold text-foreground">No Trip Chaining opportunities need review.</h2>
            <p className="mt-1 text-sm text-muted-foreground">
              Suggested next movements appear when a truck becomes available and a suitable unassigned trip exists.
            </p>
            {fetchError ? (
              <div className="mt-3 flex flex-wrap items-center gap-3">
                <p className="text-sm text-destructive">{fetchError}</p>
                <Button variant="outline" size="sm" onClick={() => void loadRecommendations()} disabled={isRefreshing}>
                  <RefreshCw className={cn("h-4 w-4", isRefreshing && "animate-spin motion-reduce:animate-none")} aria-hidden="true" />
                  Retry
                </Button>
              </div>
            ) : null}
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <section className={cn("min-w-0 space-y-4", className)} aria-labelledby="trip-chaining-queue-title">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <h2 id="trip-chaining-queue-title" className="text-xl font-semibold tracking-tight text-foreground">
            Trip Chaining
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Review a recommended next movement before the truck begins an empty return trip.
          </p>
        </div>
        <Badge className="w-fit border-amber-200 bg-amber-100 text-amber-900 dark:border-amber-900 dark:bg-amber-950/50 dark:text-amber-300">
          {groups.length} waiting
        </Badge>
      </div>

      {fetchError ? (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-destructive/30 bg-destructive/5 px-4 py-3 text-sm text-foreground" role="status">
          <span>{fetchError}</span>
          <Button variant="outline" size="sm" onClick={() => void loadRecommendations()} disabled={isRefreshing}>
            <RefreshCw className={cn("h-4 w-4", isRefreshing && "animate-spin motion-reduce:animate-none")} aria-hidden="true" />
            Retry
          </Button>
        </div>
      ) : null}

      <div className="overflow-hidden rounded-2xl border border-border bg-card shadow-card">
        <div className="hidden border-b border-border bg-muted/50 px-4 py-3 lg:grid lg:grid-cols-[minmax(9rem,1fr)_minmax(13rem,1.35fr)_minmax(8rem,.8fr)_minmax(14rem,1.45fr)_minmax(9rem,.85fr)_minmax(14rem,max-content)] lg:items-center lg:gap-4">
          <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Driver / truck</span>
          <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Current movement</span>
          <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Recommended next movement</span>
          <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Route</span>
          <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Suggestion / expiry</span>
          <span className="text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Action</span>
        </div>

        <div className="divide-y divide-border">
          {groups.map((group) => {
            const best = group.options[0];
            const time = formatTimeRemaining(best.expiresAt, now);
            return (
              <article
                key={group.key}
                className="grid min-w-0 gap-3 p-4 transition-colors duration-200 hover:bg-muted/30 motion-reduce:transition-none lg:grid-cols-[minmax(9rem,1fr)_minmax(13rem,1.35fr)_minmax(8rem,.8fr)_minmax(14rem,1.45fr)_minmax(9rem,.85fr)_minmax(14rem,max-content)] lg:items-center lg:gap-4"
              >
                <div className="flex min-w-0 items-center gap-3">
                  <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary text-primary-foreground">
                    <Truck className="h-4 w-4" aria-hidden="true" />
                  </div>
                  <div className="min-w-0">
                    <p className="truncate font-semibold text-foreground" title={group.currentMovement.driverName}>{group.currentMovement.driverName}</p>
                    <p className="truncate text-sm text-muted-foreground" title={`Truck ${group.currentMovement.truckPlate}`}>Truck {group.currentMovement.truckPlate}</p>
                  </div>
                </div>

                <div className="min-w-0 border-t border-border pt-3 lg:border-0 lg:pt-0">
                  <p className="text-xs font-medium text-muted-foreground">{formatAvailability(group.currentMovement.availabilityReason, group.currentMovement.availableAt)}</p>
                  <p className="mt-1 flex min-w-0 items-start gap-1.5 text-sm text-foreground" title={group.currentMovement.currentLocation}>
                    <MapPin className="mt-0.5 h-3.5 w-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />
                    <span className="line-clamp-2">{group.currentMovement.currentLocation || "Current location unavailable"}</span>
                  </p>
                </div>

                <div className="min-w-0">
                  <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground lg:hidden">Recommended next movement</p>
                  <Link
                    to={`/dispatch/trips/${best.nextMovement.tripId}`}
                    className="mt-1 inline-flex max-w-full items-center gap-1 truncate font-semibold text-primary hover:text-primary/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                    title={`Open ${getTripReference(best.nextMovement)}`}
                  >
                    <span className="truncate">{getTripReference(best.nextMovement)}</span>
                    <ExternalLink className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                  </Link>
                </div>

                <div className="min-w-0">
                  <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground lg:hidden">Route</p>
                  <p className="mt-1 line-clamp-2 text-sm text-foreground" title={`${best.nextMovement.pickupLocation || "Location pending"} to ${best.nextMovement.dropoffLocation || "Location pending"}`}>
                    {best.nextMovement.pickupLocation || "Location pending"}
                    <span className="mx-1 text-muted-foreground" aria-hidden="true">→</span>
                    {best.nextMovement.dropoffLocation || "Location pending"}
                  </p>
                </div>

                <div className="min-w-0">
                  <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground lg:hidden">Suggestion and expiry</p>
                  <div className="mt-1">
                    <RecommendationMeta recommendation={best} now={now} isRefreshing={isRefreshing} />
                  </div>
                  {time.isExpired ? <p className="mt-1 text-xs text-amber-800 dark:text-amber-300">Refresh before assigning.</p> : null}
                </div>

                <Button
                  size="sm"
                  variant="outline"
                  className="h-12 w-full shrink-0 lg:h-10 lg:justify-self-end"
                  onClick={(event) => openReview(group, event)}
                >
                  Review next movement
                </Button>
              </article>
            );
          })}
        </div>
      </div>

      {reviewingGroup && selectedRecommendation && selectedTime ? (
        <div className="fixed inset-0 z-50 bg-foreground/25" role="presentation" onMouseDown={closeReview}>
          <div
            ref={drawerRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby="trip-chaining-review-title"
            className="ml-auto flex h-full w-full max-w-xl flex-col border-l border-border bg-card shadow-2xl motion-reduce:transition-none"
            onMouseDown={(event) => event.stopPropagation()}
            onKeyDown={handleDrawerKeyDown}
          >
            <div className="flex items-start justify-between gap-4 border-b border-border p-5 sm:p-6">
              <div className="min-w-0">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">Trip Chaining</p>
                <h3 id="trip-chaining-review-title" className="mt-1 text-xl font-semibold text-foreground">
                  Recommended next movement
                </h3>
                <p className="mt-1 text-sm text-muted-foreground">{reviewingGroup.currentMovement.driverName} · Truck {reviewingGroup.currentMovement.truckPlate}</p>
              </div>
              <Button ref={closeButtonRef} variant="ghost" size="icon" onClick={closeReview} aria-label="Close Trip Chaining review">
                <X className="h-5 w-5" aria-hidden="true" />
              </Button>
            </div>

            <div className="min-h-0 flex-1 space-y-5 overflow-y-auto p-5 sm:p-6">
              <div className="rounded-xl border border-border bg-muted/40 p-4">
                <p className="text-sm font-medium text-foreground">This is an operational suggestion, not an automatic assignment.</p>
                <p className="mt-1 text-sm text-muted-foreground">Confirming assigns the driver and truck to the selected next movement. Availability and dispatch readiness are checked again first.</p>
              </div>

              <div>
                <h4 className="text-sm font-semibold text-foreground">Available options</h4>
                <div className="mt-3 divide-y divide-border overflow-hidden rounded-xl border border-border">
                  {reviewingGroup.options.map((option) => {
                    const optionTime = formatTimeRemaining(option.expiresAt, now);
                    const isSelected = option.suggestionId === selectedRecommendation.suggestionId;
                    return (
                      <button
                        key={option.suggestionId}
                        type="button"
                        aria-pressed={isSelected}
                        className={cn(
                          "flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition-colors duration-200 hover:bg-muted/70 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring motion-reduce:transition-none",
                          isSelected && "bg-muted"
                        )}
                        onClick={() => {
                          setSelectedByGroup((current) => ({ ...current, [reviewingGroup.key]: option.suggestionId }));
                          setActionError(null);
                        }}
                      >
                        <span className="min-w-0">
                          <span className="block truncate font-semibold text-foreground">{option.isRecommended ? "Recommended" : "Alternative"}: {getTripReference(option.nextMovement)}</span>
                          <span className="mt-1 block truncate text-xs text-muted-foreground">{option.nextMovement.pickupLocation || "Location pending"} to {option.nextMovement.dropoffLocation || "Location pending"}</span>
                        </span>
                        <span className="shrink-0 text-xs font-medium text-muted-foreground">{optionTime.label}</span>
                      </button>
                    );
                  })}
                </div>
              </div>

              <div className="space-y-4 rounded-xl border border-border p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">Selected next movement</p>
                    <Link to={`/dispatch/trips/${selectedRecommendation.nextMovement.tripId}`} className="mt-1 inline-flex items-center gap-1 font-semibold text-primary hover:text-primary/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                      {getTripReference(selectedRecommendation.nextMovement)}
                      <ExternalLink className="h-4 w-4" aria-hidden="true" />
                    </Link>
                  </div>
                  <RecommendationMeta recommendation={selectedRecommendation} now={now} />
                </div>

                <div className="grid gap-3 border-y border-border py-4 sm:grid-cols-[1fr_auto_1fr] sm:items-center">
                  <div className="min-w-0">
                    <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Pickup</p>
                    <p className="mt-1 break-words text-sm font-medium text-foreground">{selectedRecommendation.nextMovement.pickupLocation || "Location pending"}</p>
                  </div>
                  <Route className="hidden h-4 w-4 text-muted-foreground sm:block" aria-hidden="true" />
                  <div className="min-w-0">
                    <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Dropoff</p>
                    <p className="mt-1 break-words text-sm font-medium text-foreground">{selectedRecommendation.nextMovement.dropoffLocation || "Location pending"}</p>
                  </div>
                </div>
                <p className="text-sm text-muted-foreground">Pickup: {formatPickupWindow(selectedRecommendation.nextMovement.scheduledPickupTime)}</p>
                <div className="space-y-2 border-t border-border pt-4">
                  <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">Assignment check</p>
                  {selectedRecommendation.suitabilityReasons.map((reason) => <p key={reason} className="flex items-start gap-2 text-sm text-foreground"><CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-success" aria-hidden="true" />{reason}</p>)}
                  {selectedRecommendation.warnings.map((warning) => <p key={warning} className="text-sm text-warning-foreground">Warning: {warning}</p>)}
                </div>
              </div>

              {selectedTime.isExpired ? (
                <p className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/50 dark:text-amber-300" role="status">
                  This suggestion has expired. Refresh the queue before assigning.
                </p>
              ) : null}
              {actionError ? <p className="rounded-xl border border-destructive/30 bg-destructive/5 px-4 py-3 text-sm text-destructive" role="status">{actionError}</p> : null}
            </div>

            <div className="flex flex-col-reverse gap-2 border-t border-border p-5 sm:flex-row sm:justify-between sm:p-6">
              <Button
                variant="ghost"
                className="h-12 justify-start text-muted-foreground hover:text-foreground"
                onClick={() => void handleIgnore(selectedRecommendation.suggestionId)}
                disabled={actionLoadingId !== null || selectedTime.isExpired}
              >
                Dismiss suggestion
              </Button>
              <Button
                className="h-12"
                onClick={() => void handleAccept(selectedRecommendation.suggestionId)}
                disabled={actionLoadingId !== null || selectedTime.isExpired}
              >
                <CheckCircle2 className={cn("h-4 w-4", actionLoadingId === selectedRecommendation.suggestionId && "animate-spin motion-reduce:animate-none")} aria-hidden="true" />
                {actionLoadingId === selectedRecommendation.suggestionId ? "Confirming…" : "Confirm next movement"}
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </section>
  );
}
