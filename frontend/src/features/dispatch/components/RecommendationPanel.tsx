import { useCallback, useEffect, useMemo, useState } from "react";
import { CheckCircle2, Clock, SkipForward, Truck } from "lucide-react";
import ToastHost from "@/components/ToastHost";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { api } from "@/lib/api";
import { cn } from "@/lib/utils";
import { useToast } from "@/lib/useToast";
import { useDispatchHub } from "@/hooks/useDispatchHub";

type CompletedTripRecommendation = {
  tripId: string;
  driverName: string;
  truckPlate: string;
  dropoffLocation: string;
  deliveredAt?: string | null;
};

type RecommendedTripRecommendation = {
  tripId: string;
  containerNumber?: string | null;
  pickupLocation: string;
  dropoffLocation: string;
  containerSize?: string | null;
  tripType?: string | null;
  scheduledPickupTime?: string | null;
  agingHours: number;
};

export type DispatchRecommendation = {
  recommendationId: string;
  rank: number;
  totalScore: number;
  completedTrip: CompletedTripRecommendation;
  recommendedTrip: RecommendedTripRecommendation;
  expiresAt: string;
  generatedAt: string;
  wasAccepted: boolean;
  wasIgnored: boolean;
  reviewedBy?: string | null;
  reviewedAt?: string | null;
};

type RecommendationGroup = {
  key: string;
  completedTrip: CompletedTripRecommendation;
  expiresAt: string;
  recommendations: DispatchRecommendation[];
};

type Props = {
  className?: string;
};

export default function RecommendationPanel({ className }: Props) {
  const { toasts, show } = useToast();
  const [recommendations, setRecommendations] = useState<DispatchRecommendation[]>([]);
  const [loadingIds, setLoadingIds] = useState<Set<string>>(() => new Set());
  const [now, setNow] = useState(() => Date.now());

  const pruneExpired = useCallback((items: DispatchRecommendation[]) => {
    const current = Date.now();
    return items.filter((item) => new Date(item.expiresAt).getTime() > current);
  }, []);

  const loadPending = useCallback(async () => {
    try {
      const result = await api<DispatchRecommendation[]>("/api/dispatch/recommendations/pending", { method: "GET" });
      setRecommendations(pruneExpired(result ?? []));
    } catch (e: any) {
      console.error(e);
    }
  }, [pruneExpired]);

  useDispatchHub({
    onRecommendationGenerated: () => {
      void loadPending();
    }
  });

  useEffect(() => {
    void loadPending();
    const pollId = window.setInterval(() => void loadPending(), 60000);
    return () => window.clearInterval(pollId);
  }, [loadPending]);

  useEffect(() => {
    const refresh = () => void loadPending();
    window.addEventListener("nvg:recommendations-refresh", refresh);
    return () => window.removeEventListener("nvg:recommendations-refresh", refresh);
  }, [loadPending]);

  useEffect(() => {
    const tickId = window.setInterval(() => {
      setNow(Date.now());
      setRecommendations((prev) => pruneExpired(prev));
    }, 1000);
    return () => window.clearInterval(tickId);
  }, [pruneExpired]);

  const groups = useMemo<RecommendationGroup[]>(() => {
    const grouped = new Map<string, RecommendationGroup>();
    for (const recommendation of recommendations) {
      const key = recommendation.completedTrip.tripId;
      const current = grouped.get(key);
      if (current) {
        current.recommendations.push(recommendation);
      } else {
        grouped.set(key, {
          key,
          completedTrip: recommendation.completedTrip,
          expiresAt: recommendation.expiresAt,
          recommendations: [recommendation]
        });
      }
    }

    return Array.from(grouped.values())
      .map((group) => ({
        ...group,
        recommendations: group.recommendations.sort((a, b) => a.rank - b.rank),
        expiresAt: group.recommendations.reduce(
          (earliest, item) =>
            new Date(item.expiresAt).getTime() < new Date(earliest).getTime() ? item.expiresAt : earliest,
          group.expiresAt
        )
      }))
      .sort((a, b) => new Date(b.recommendations[0]?.generatedAt ?? 0).getTime() - new Date(a.recommendations[0]?.generatedAt ?? 0).getTime());
  }, [recommendations]);

  const setLoading = (id: string, value: boolean) => {
    setLoadingIds((prev) => {
      const next = new Set(prev);
      if (value) next.add(id);
      else next.delete(id);
      return next;
    });
  };

  const acceptRecommendation = async (recommendation: DispatchRecommendation) => {
    try {
      setLoading(recommendation.recommendationId, true);
      await api<DispatchRecommendation>(`/api/dispatch/recommendations/${recommendation.recommendationId}/accept`, {
        method: "POST"
      });
      setRecommendations((prev) =>
        prev.filter((item) => item.completedTrip.tripId !== recommendation.completedTrip.tripId)
      );
      show("Review and assign the recommended trip in the dispatch queue.", "success");
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to accept recommendation.", "error");
    } finally {
      setLoading(recommendation.recommendationId, false);
    }
  };

  const ignoreRecommendation = async (recommendation: DispatchRecommendation) => {
    try {
      setLoading(recommendation.recommendationId, true);
      await api(`/api/dispatch/recommendations/${recommendation.recommendationId}/ignore`, {
        method: "POST"
      });
      setRecommendations((prev) => prev.filter((item) => item.recommendationId !== recommendation.recommendationId));
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to skip recommendation.", "error");
    } finally {
      setLoading(recommendation.recommendationId, false);
    }
  };

  if (groups.length === 0) {
    return null;
  }

  return (
    <section className={cn("rounded-2xl border border-amber-300 bg-amber-50 p-4 shadow-card", className)}>
      <ToastHost toasts={toasts} />
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex items-start gap-3">
          <div className="mt-0.5 flex h-9 w-9 items-center justify-center rounded-full bg-amber-200 text-amber-900">
            <Truck className="h-5 w-5" />
          </div>
          <div>
            <h2 className="text-base font-semibold text-amber-950">Post-Delivery Recommendations</h2>
            <p className="text-sm text-amber-800">Drivers available for nearby jobs</p>
          </div>
        </div>
        <Badge variant="outline" className="w-fit border-amber-300 bg-card text-amber-900">
          {recommendations.length} active
        </Badge>
      </div>

      <div className="mt-4 space-y-4">
        {groups.map((group) => (
          <div key={group.key} className="rounded-2xl border border-amber-200 bg-card p-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <p className="text-sm font-medium text-foreground">
                {group.completedTrip.driverName} ({group.completedTrip.truckPlate}) just delivered to{" "}
                {group.completedTrip.dropoffLocation || "the dropoff point"}
              </p>
              <span className="inline-flex min-h-11 items-center gap-2 rounded-full border border-amber-300 bg-amber-50 px-3 py-2 text-sm font-semibold text-amber-900">
                <Clock className="h-4 w-4" />
                Expires in {formatCountdown(group.expiresAt, now)}
              </span>
            </div>

            <div className="mt-3 grid gap-3 lg:grid-cols-2 xl:grid-cols-3">
              {group.recommendations.map((recommendation) => {
                const isLoading = loadingIds.has(recommendation.recommendationId);
                return (
                  <article
                    key={recommendation.recommendationId}
                    className={cn(
                      "rounded-2xl border bg-card p-4 shadow-sm",
                      recommendation.rank === 1 ? "border-amber-400 bg-amber-50/50" : "border-border"
                    )}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <Badge
                        variant={recommendation.rank === 1 ? "default" : "outline"}
                        className={recommendation.rank === 1 ? "bg-amber-600 text-white" : "text-foreground"}
                      >
                        {formatRank(recommendation.rank)}
                      </Badge>
                      <span className="text-xs font-semibold text-muted-foreground">
                        Score {recommendation.totalScore.toFixed(2)}
                      </span>
                    </div>
                    <div className="mt-3 h-2 rounded-full bg-muted">
                      <div
                        className="h-2 rounded-full bg-amber-500"
                        style={{ width: `${Math.min(100, Math.max(0, recommendation.totalScore * 100))}%` }}
                      />
                    </div>
                    <dl className="mt-3 space-y-2 text-xs text-muted-foreground">
                      <RecommendationField label="Container#" value={recommendation.recommendedTrip.containerNumber ?? "Not set"} />
                      <RecommendationField label="From" value={recommendation.recommendedTrip.pickupLocation || "-"} />
                      <RecommendationField label="To" value={recommendation.recommendedTrip.dropoffLocation || "-"} />
                      <RecommendationField label="Container size" value={formatContainerSize(recommendation.recommendedTrip.containerSize)} />
                      <RecommendationField label="Waiting" value={formatWaiting(recommendation.recommendedTrip.agingHours)} />
                    </dl>
                    <div className="mt-4 grid gap-2 sm:grid-cols-2">
                      <Button
                        onClick={() => acceptRecommendation(recommendation)}
                        disabled={isLoading}
                        className="h-12 w-full gap-2"
                      >
                        <CheckCircle2 className="h-4 w-4" />
                        Accept for Review
                      </Button>
                      <Button
                        variant="outline"
                        onClick={() => ignoreRecommendation(recommendation)}
                        disabled={isLoading}
                        className="h-12 w-full gap-2"
                      >
                        <SkipForward className="h-4 w-4" />
                        Ignore
                      </Button>
                    </div>
                    <p className="mt-2 text-xs text-muted-foreground">
                      Accepting flags this job for dispatcher review; it does not auto-assign the trip.
                    </p>
                  </article>
                );
              })}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

function RecommendationField({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="font-semibold uppercase tracking-wide text-muted-foreground/70">{label}</dt>
      <dd className="mt-0.5 font-medium text-foreground">{value}</dd>
    </div>
  );
}

function formatRank(rank: number) {
  if (rank === 1) return "1st";
  if (rank === 2) return "2nd";
  if (rank === 3) return "3rd";
  return `${rank}th`;
}

function formatContainerSize(value?: string | null) {
  if (!value) return "Not set";
  if (value === "TwentyFt") return "20ft";
  if (value === "FortyFt") return "40ft";
  if (value === "FortyHC") return "40HC";
  return value;
}

function formatWaiting(hours: number) {
  const totalMinutes = Math.max(0, Math.round(hours * 60));
  const h = Math.floor(totalMinutes / 60);
  const m = totalMinutes % 60;
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

function formatCountdown(expiresAt: string, now: number) {
  const ms = Math.max(0, new Date(expiresAt).getTime() - now);
  const totalSeconds = Math.floor(ms / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}
