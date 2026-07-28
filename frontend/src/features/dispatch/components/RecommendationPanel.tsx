import { useEffect, useState, useMemo } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/lib/useToast";
import { Button } from "@/components/ui/button";
import { Truck, MapPin, CheckCircle, Clock, Zap, ExternalLink } from "lucide-react";
import { Link } from "react-router-dom";

export type CompletedTripRecommendationResponse = {
  tripId: string;
  driverId: string;
  truckId: string;
  driverName: string;
  truckPlate: string;
  dropoffLocation: string;
  deliveredAt: string | null;
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
  recommendationId: string;
  rank: number;
  totalScore: number;
  completedTrip: CompletedTripRecommendationResponse;
  recommendedTrip: RecommendedTripRecommendationResponse;
  expiresAt: string;
  generatedAt: string;
  wasAccepted: boolean;
  wasIgnored: boolean;
};

interface RecommendationPanelProps {
  filterByDriverId?: string;
  className?: string;
}

export default function RecommendationPanel({ filterByDriverId, className = "" }: RecommendationPanelProps) {
  const [recommendations, setRecommendations] = useState<DispatchRecommendationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoadingId, setActionLoadingId] = useState<string | null>(null);
  const { show } = useToast();

  const loadRecommendations = async () => {
    try {
      const data = await api<DispatchRecommendationResponse[]>("/api/dispatch/recommendations/pending", {
        method: "GET"
      });
      setRecommendations(data ?? []);
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load post-delivery recommendations.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadRecommendations();
    const interval = setInterval(loadRecommendations, 30000);
    return () => clearInterval(interval);
  }, []);

  const handleAccept = async (recommendationId: string) => {
    setActionLoadingId(recommendationId);
    try {
      await api(`/api/dispatch/recommendations/${recommendationId}/accept`, {
        method: "POST"
      });
      show("Trip assignment accepted successfully.", "success");
      await loadRecommendations();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to accept assignment.", "error");
    } finally {
      setActionLoadingId(null);
    }
  };

  const displayData = useMemo(() => {
    let filtered = recommendations.filter(r => !r.wasAccepted && !r.wasIgnored);
    
    if (filterByDriverId) {
      filtered = filtered.filter(r => r.completedTrip.driverId === filterByDriverId);
    }

    // Group by Truck/Driver
    const grouped = new Map<string, DispatchRecommendationResponse[]>();
    for (const r of filtered) {
      const key = `${r.completedTrip.truckId}_${r.completedTrip.driverId}`;
      if (!grouped.has(key)) {
        grouped.set(key, []);
      }
      grouped.get(key)!.push(r);
    }
    
    // Sort groups by completed trip delivery time and sort candidates within group by rank (1, 2, 3)
    return Array.from(grouped.values()).map(items => {
      return items.sort((a, b) => a.rank - b.rank);
    }).sort((a, b) => {
      const aTime = new Date(a[0]?.completedTrip?.deliveredAt || a[0]?.generatedAt).getTime();
      const bTime = new Date(b[0]?.completedTrip?.deliveredAt || b[0]?.generatedAt).getTime();
      return bTime - aTime;
    });
  }, [recommendations, filterByDriverId]);

  if (loading && recommendations.length === 0) {
    return (
      <div className={`surface-card p-6 border-l-4 border-l-indigo-500 animate-pulse ${className}`}>
        <div className="h-6 w-48 bg-slate-200 rounded mb-4" />
        <div className="h-24 bg-slate-100 rounded" />
      </div>
    );
  }

  if (displayData.length === 0) {
    // Only show nothing if filtering for driver and no recs found, else show empty state
    if (filterByDriverId) return null;
    
    return (
      <div className={`surface-card p-6 border-l-4 border-l-slate-300 ${className}`}>
        <h3 className="font-semibold text-slate-800 flex items-center gap-2">
          <Zap className="h-5 w-5 text-slate-400" />
          Post-Delivery Recommendations
        </h3>
        <p className="text-sm text-slate-500 mt-2">
          No trucks are currently pending trip assignments from the CSP-TOPSIS engine.
        </p>
      </div>
    );
  }

  return (
    <div className={`bg-gradient-to-br from-indigo-50 to-blue-50 border border-indigo-100 rounded-2xl shadow-sm p-5 md:p-6 mb-6 ${className}`}>
      <div className="flex items-center justify-between mb-5">
        <div>
          <h3 className="text-lg font-bold text-indigo-900 flex items-center gap-2">
            <Zap className="h-5 w-5 text-amber-500 fill-amber-500" />
            {filterByDriverId ? "Suggested Next Trip" : "Post-Delivery Recommendations"}
          </h3>
          <p className="text-xs text-indigo-700/70 mt-1">
            CSP-TOPSIS Engine generated these assignments for recently completed trips.
          </p>
        </div>
      </div>

      <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
        {displayData.map((group) => {
          const completed = group[0].completedTrip;
          return (
            <div key={`${completed.truckId}-${completed.driverId}`} className="bg-white rounded-xl border shadow-sm overflow-hidden flex flex-col">
              {/* Truck Header */}
              <div className="bg-slate-900 px-4 py-3 text-white flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Truck className="h-4 w-4 text-indigo-300" />
                  <span className="font-bold text-sm tracking-wider">{completed.truckPlate}</span>
                </div>
                <div className="text-xs text-slate-300 font-medium">
                  {completed.driverName}
                </div>
              </div>
              
              <div className="p-3 bg-slate-50 border-b flex items-center gap-2 text-xs text-slate-600">
                <MapPin className="h-3 w-3 text-emerald-600 shrink-0" />
                <span className="truncate" title={completed.dropoffLocation}>
                  Finished at: <strong className="text-slate-800">{completed.dropoffLocation}</strong>
                </span>
              </div>

              {/* Recommendations List (Top 3) */}
              <div className="p-3 flex-1 flex flex-col gap-3">
                {group.slice(0, 3).map((rec) => {
                  const target = rec.recommendedTrip;
                  const isTopRank = rec.rank === 1;
                  
                  return (
                    <div 
                      key={rec.recommendationId} 
                      className={`relative rounded-lg border p-3 flex flex-col gap-2 transition-all ${
                        isTopRank ? 'border-indigo-200 bg-indigo-50/40 shadow-sm' : 'border-slate-100 bg-slate-50/50 opacity-90'
                      }`}
                    >
                      {/* Rank Badge */}
                      <div className={`absolute -left-2 -top-2 h-5 w-5 rounded-full flex items-center justify-center text-[10px] font-bold shadow-sm ${
                        isTopRank ? 'bg-indigo-600 text-white' : 'bg-slate-300 text-slate-700'
                      }`}>
                        #{rec.rank}
                      </div>

                      <div className="flex justify-between items-start pl-2">
                        <div className="space-y-0.5">
                          <Link to={`/dispatch/trips/${target.tripId}`} className="text-xs font-semibold text-indigo-700 hover:underline flex items-center gap-1">
                            {target.containerNumber || 'No Container'} <ExternalLink className="h-3 w-3" />
                          </Link>
                          <div className="text-[10px] text-slate-500 uppercase tracking-wide font-medium">
                            {target.tripType ?? 'Standard'} • Score: <span className={isTopRank ? 'text-indigo-700 font-bold' : ''}>{Math.round(rec.totalScore)}%</span>
                          </div>
                        </div>
                        
                        <Button 
                          size="sm"
                          disabled={actionLoadingId !== null}
                          onClick={() => handleAccept(rec.recommendationId)}
                          className={`h-7 px-3 text-[11px] font-bold gap-1 rounded-md ${
                            isTopRank 
                              ? 'bg-indigo-600 hover:bg-indigo-700 text-white' 
                              : 'bg-white border-slate-200 text-slate-600 hover:bg-slate-50 hover:text-indigo-600'
                          }`}
                          variant={isTopRank ? "default" : "outline"}
                        >
                          {actionLoadingId === rec.recommendationId ? (
                            <Clock className="h-3.5 w-3.5 animate-spin" />
                          ) : (
                            <CheckCircle className="h-3.5 w-3.5" />
                          )}
                          Accept
                        </Button>
                      </div>

                      <div className="pl-2 space-y-1.5 mt-1">
                        <div className="flex items-start gap-1.5 text-[11px] text-slate-600">
                          <MapPin className="h-3 w-3 shrink-0 text-slate-400 mt-0.5" />
                          <span className="truncate" title={target.pickupLocation}>{target.pickupLocation}</span>
                        </div>
                        <div className="flex items-start gap-1.5 text-[11px] text-slate-600">
                          <MapPin className="h-3 w-3 shrink-0 text-slate-400 mt-0.5" />
                          <span className="truncate" title={target.dropoffLocation}>{target.dropoffLocation}</span>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
