import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import PageHeader from "@/components/PageHeader";
import ToastHost from "@/components/ToastHost";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useToast } from "@/lib/useToast";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import {
  Sliders,
  Save,
  Info,
  AlertCircle,
  TrendingUp,
  MapPin,
  Clock,
  AlertTriangle,
  Truck,
  Box
} from "lucide-react";

type OptimizationWeightSettingsResponse = {
  id: string;
  deadheadDistanceWeight: number;
  cleaningTimeWeight: number;
  waitingTimeWeight: number;
  jobUrgencyWeight: number;
  cargoCompatibilityWeight: number;
  assetUtilizationWeight: number;
  isActive: boolean;
  createdByUserId?: string | null;
  createdAt: string;
};

export default function OptimizationSettingsPage() {
  const nav = useNavigate();
  const { toasts, show } = useToast();
  const me = getMe();
  const isManager = me?.roles.includes("Manager") ?? false;
  const isOwner = me?.roles.includes("Owner") ?? false;

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  // Form values (stored as percentages 0-100 for user friendliness)
  const [deadheadDistance, setDeadheadDistance] = useState(30);
  const [cleaningTime, setCleaningTime] = useState(15);
  const [waitingTime, setWaitingTime] = useState(15);
  const [jobUrgency, setJobUrgency] = useState(20);
  const [cargoCompatibility, setCargoCompatibility] = useState(10);
  const [assetUtilization, setAssetUtilization] = useState(10);

  // Access check
  useEffect(() => {
    if (!me || (!isManager && !isOwner)) {
      show("Access Denied: You do not have permissions to view this page.", "error");
      nav("/dashboard");
    }
  }, [me, isManager, isOwner, nav, show]);

  // Load current settings
  const loadSettings = async () => {
    setLoading(true);
    try {
      const response = await api<OptimizationWeightSettingsResponse>("/api/dispatch/optimization/settings", {
        method: "GET"
      });
      // Map decimals (0.25) to percentages (25)
      setDeadheadDistance(Math.round(response.deadheadDistanceWeight * 100));
      setCleaningTime(Math.round(response.cleaningTimeWeight * 100));
      setWaitingTime(Math.round(response.waitingTimeWeight * 100));
      setJobUrgency(Math.round(response.jobUrgencyWeight * 100));
      setCargoCompatibility(Math.round(response.cargoCompatibilityWeight * 100));
      setAssetUtilization(Math.round(response.assetUtilizationWeight * 100));
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to load active weight settings.", "error");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (me && (isManager || isOwner)) {
      loadSettings();
    }
  }, [me]);

  // Calculations
  const total = deadheadDistance + cleaningTime + waitingTime + jobUrgency + cargoCompatibility + assetUtilization;
  const isValid = total === 100;
  const isReadOnly = !isManager; // Only Managers can edit

  const handleSaveSettings = async () => {
    if (!isValid || isReadOnly) return;
    setSaving(true);
    try {
      await api<OptimizationWeightSettingsResponse>("/api/dispatch/optimization/settings", {
        method: "PUT",
        body: JSON.stringify({
          deadheadDistanceWeight: deadheadDistance / 100,
          cleaningTimeWeight: cleaningTime / 100,
          waitingTimeWeight: waitingTime / 100,
          jobUrgencyWeight: jobUrgency / 100,
          cargoCompatibilityWeight: cargoCompatibility / 100,
          assetUtilizationWeight: assetUtilization / 100
        })
      });
      show("Optimization weights updated successfully.", "success");
      void loadSettings();
    } catch (e: any) {
      console.error(e);
      show(e?.message ?? "Failed to save weight settings.", "error");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Sliders className="h-8 w-8 animate-spin text-primary" />
      </div>
    );
  }

  return (
    <div className="space-y-6 max-w-5xl mx-auto">
      <ToastHost toasts={toasts} />
      <PageHeader
        title="Optimization Settings"
        description="Configure how the CSP-TOPSIS framework ranks recommended route plans."
        breadcrumbs={
          <nav className="flex items-center gap-2" aria-label="Breadcrumb">
            <Link to="/dispatch/board" className="text-muted-foreground hover:text-foreground">
              Dispatch
            </Link>
            <span className="text-muted-foreground">/</span>
            <span className="text-foreground">Optimization Settings</span>
          </nav>
        }
      />

      {isReadOnly && (
        <Card className="rounded-2xl border-blue-200 bg-blue-50/40 text-blue-900 shadow-sm">
          <CardContent className="p-4 flex items-start gap-3 text-sm">
            <Info className="h-5 w-5 text-blue-600 shrink-0 mt-0.5" />
            <div className="space-y-1">
              <p className="font-semibold">Read-Only Mode</p>
              <p className="text-xs text-blue-800">
                You are currently viewing weight parameters as an Owner. Only Managers have permissions to save changes to the dispatch optimizer settings.
              </p>
            </div>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-6 md:grid-cols-[1fr_320px]">
        {/* Main Inputs Card */}
        <Card className="rounded-2xl border bg-card shadow-sm">
          <CardHeader>
            <CardTitle className="text-base font-semibold">Cost Function Weight Configuration</CardTitle>
            <CardDescription>
              Adjust weights to shift optimization priorities. The sum of all weights must equal exactly 100%.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            
            {/* Visual Stacked Bar Chart Breakdown */}
            <div className="space-y-2">
              <div className="flex justify-between text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                <span>Visual Allocation Breakdown</span>
                <span className={isValid ? "text-emerald-600 font-bold" : "text-rose-600 font-bold"}>
                  Total: {total}% {isValid ? "✔" : "❌"}
                </span>
              </div>
              
              <div className="h-5 w-full rounded-full bg-slate-100 overflow-hidden flex border shadow-inner">
                {deadheadDistance > 0 && (
                  <div style={{ width: `${deadheadDistance}%` }} className="bg-blue-500 h-full transition-all duration-300" title={`Deadhead Distance: ${deadheadDistance}%`} />
                )}
                {cleaningTime > 0 && (
                  <div style={{ width: `${cleaningTime}%` }} className="bg-indigo-500 h-full transition-all duration-300" title={`Cleaning Time: ${cleaningTime}%`} />
                )}
                {waitingTime > 0 && (
                  <div style={{ width: `${waitingTime}%` }} className="bg-rose-500 h-full transition-all duration-300" title={`Waiting Time: ${waitingTime}%`} />
                )}
                {jobUrgency > 0 && (
                  <div style={{ width: `${jobUrgency}%` }} className="bg-amber-500 h-full transition-all duration-300" title={`Job Urgency: ${jobUrgency}%`} />
                )}
                {cargoCompatibility > 0 && (
                  <div style={{ width: `${cargoCompatibility}%` }} className="bg-emerald-500 h-full transition-all duration-300" title={`Cargo Compatibility: ${cargoCompatibility}%`} />
                )}
                {assetUtilization > 0 && (
                  <div style={{ width: `${assetUtilization}%` }} className="bg-purple-500 h-full transition-all duration-300" title={`Asset Utilization: ${assetUtilization}%`} />
                )}
              </div>

              {!isValid && (
                <div className="flex items-center gap-1.5 text-xs text-rose-600 font-medium">
                  <AlertCircle className="h-3.5 w-3.5" />
                  Weights add up to {total}%. Adjust values to sum exactly to 100% to enable saving.
                </div>
              )}
            </div>

            {/* Individual sliders and inputs */}
            <div className="space-y-5 pt-2">
              
              {/* Deadhead Distance */}
              <div className="space-y-2 p-3.5 rounded-xl border bg-slate-50/50">
                <div className="flex items-center justify-between">
                  <label className="text-sm font-semibold text-slate-800 flex items-center gap-1.5">
                    <span className="h-3 w-3 rounded-full bg-blue-500" />
                    Deadhead Distance Weight
                  </label>
                  <span className="text-sm font-bold text-slate-700">{deadheadDistance}%</span>
                </div>
                <div className="flex gap-4 items-center">
                  <input
                    type="range" min="0" max="100" value={deadheadDistance}
                    onChange={(e) => setDeadheadDistance(parseInt(e.target.value) || 0)}
                    disabled={isReadOnly || saving}
                    className="w-full accent-blue-500 h-1.5 bg-slate-200 rounded-lg appearance-none cursor-pointer"
                  />
                  <input
                    type="number" min="0" max="100" value={deadheadDistance}
                    onChange={(e) => setDeadheadDistance(Math.min(100, Math.max(0, parseInt(e.target.value) || 0)))}
                    disabled={isReadOnly || saving}
                    className="w-16 h-8 text-center rounded border bg-background text-sm font-medium focus:outline-none focus:ring-1 focus:ring-primary"
                  />
                </div>
              </div>

              {/* Cleaning Time */}
              <div className="space-y-2 p-3.5 rounded-xl border bg-slate-50/50">
                <div className="flex items-center justify-between">
                  <label className="text-sm font-semibold text-slate-800 flex items-center gap-1.5">
                    <span className="h-3 w-3 rounded-full bg-indigo-500" />
                    Cleaning Time Weight
                  </label>
                  <span className="text-sm font-bold text-slate-700">{cleaningTime}%</span>
                </div>
                <div className="flex gap-4 items-center">
                  <input
                    type="range" min="0" max="100" value={cleaningTime}
                    onChange={(e) => setCleaningTime(parseInt(e.target.value) || 0)}
                    disabled={isReadOnly || saving}
                    className="w-full accent-indigo-500 h-1.5 bg-slate-200 rounded-lg appearance-none cursor-pointer"
                  />
                  <input
                    type="number" min="0" max="100" value={cleaningTime}
                    onChange={(e) => setCleaningTime(Math.min(100, Math.max(0, parseInt(e.target.value) || 0)))}
                    disabled={isReadOnly || saving}
                    className="w-16 h-8 text-center rounded border bg-background text-sm font-medium focus:outline-none focus:ring-1 focus:ring-primary"
                  />
                </div>
              </div>

              {/* Waiting Time */}
              <div className="space-y-2 p-3.5 rounded-xl border bg-slate-50/50">
                <div className="flex items-center justify-between">
                  <label className="text-sm font-semibold text-slate-800 flex items-center gap-1.5">
                    <span className="h-3 w-3 rounded-full bg-rose-500" />
                    Waiting Time Weight
                  </label>
                  <span className="text-sm font-bold text-slate-700">{waitingTime}%</span>
                </div>
                <div className="flex gap-4 items-center">
                  <input
                    type="range" min="0" max="100" value={waitingTime}
                    onChange={(e) => setWaitingTime(parseInt(e.target.value) || 0)}
                    disabled={isReadOnly || saving}
                    className="w-full accent-rose-500 h-1.5 bg-slate-200 rounded-lg appearance-none cursor-pointer"
                  />
                  <input
                    type="number" min="0" max="100" value={waitingTime}
                    onChange={(e) => setWaitingTime(Math.min(100, Math.max(0, parseInt(e.target.value) || 0)))}
                    disabled={isReadOnly || saving}
                    className="w-16 h-8 text-center rounded border bg-background text-sm font-medium focus:outline-none focus:ring-1 focus:ring-primary"
                  />
                </div>
              </div>
              
              {/* Job Urgency */}
              <div className="space-y-2 p-3.5 rounded-xl border bg-slate-50/50">
                <div className="flex items-center justify-between">
                  <label className="text-sm font-semibold text-slate-800 flex items-center gap-1.5">
                    <span className="h-3 w-3 rounded-full bg-amber-500" />
                    Job Urgency Weight
                  </label>
                  <span className="text-sm font-bold text-slate-700">{jobUrgency}%</span>
                </div>
                <div className="flex gap-4 items-center">
                  <input
                    type="range" min="0" max="100" value={jobUrgency}
                    onChange={(e) => setJobUrgency(parseInt(e.target.value) || 0)}
                    disabled={isReadOnly || saving}
                    className="w-full accent-amber-500 h-1.5 bg-slate-200 rounded-lg appearance-none cursor-pointer"
                  />
                  <input
                    type="number" min="0" max="100" value={jobUrgency}
                    onChange={(e) => setJobUrgency(Math.min(100, Math.max(0, parseInt(e.target.value) || 0)))}
                    disabled={isReadOnly || saving}
                    className="w-16 h-8 text-center rounded border bg-background text-sm font-medium focus:outline-none focus:ring-1 focus:ring-primary"
                  />
                </div>
              </div>

              {/* Cargo Compatibility */}
              <div className="space-y-2 p-3.5 rounded-xl border bg-slate-50/50">
                <div className="flex items-center justify-between">
                  <label className="text-sm font-semibold text-slate-800 flex items-center gap-1.5">
                    <span className="h-3 w-3 rounded-full bg-emerald-500" />
                    Cargo Compatibility Weight
                  </label>
                  <span className="text-sm font-bold text-slate-700">{cargoCompatibility}%</span>
                </div>
                <div className="flex gap-4 items-center">
                  <input
                    type="range" min="0" max="100" value={cargoCompatibility}
                    onChange={(e) => setCargoCompatibility(parseInt(e.target.value) || 0)}
                    disabled={isReadOnly || saving}
                    className="w-full accent-emerald-500 h-1.5 bg-slate-200 rounded-lg appearance-none cursor-pointer"
                  />
                  <input
                    type="number" min="0" max="100" value={cargoCompatibility}
                    onChange={(e) => setCargoCompatibility(Math.min(100, Math.max(0, parseInt(e.target.value) || 0)))}
                    disabled={isReadOnly || saving}
                    className="w-16 h-8 text-center rounded border bg-background text-sm font-medium focus:outline-none focus:ring-1 focus:ring-primary"
                  />
                </div>
              </div>

              {/* Asset Utilization */}
              <div className="space-y-2 p-3.5 rounded-xl border bg-slate-50/50">
                <div className="flex items-center justify-between">
                  <label className="text-sm font-semibold text-slate-800 flex items-center gap-1.5">
                    <span className="h-3 w-3 rounded-full bg-purple-500" />
                    Asset Utilization Weight
                  </label>
                  <span className="text-sm font-bold text-slate-700">{assetUtilization}%</span>
                </div>
                <div className="flex gap-4 items-center">
                  <input
                    type="range" min="0" max="100" value={assetUtilization}
                    onChange={(e) => setAssetUtilization(parseInt(e.target.value) || 0)}
                    disabled={isReadOnly || saving}
                    className="w-full accent-purple-500 h-1.5 bg-slate-200 rounded-lg appearance-none cursor-pointer"
                  />
                  <input
                    type="number" min="0" max="100" value={assetUtilization}
                    onChange={(e) => setAssetUtilization(Math.min(100, Math.max(0, parseInt(e.target.value) || 0)))}
                    disabled={isReadOnly || saving}
                    className="w-16 h-8 text-center rounded border bg-background text-sm font-medium focus:outline-none focus:ring-1 focus:ring-primary"
                  />
                </div>
              </div>

            </div>

            {/* Save Action Block */}
            {!isReadOnly && (
              <div className="flex justify-end pt-4 border-t">
                <Button
                  onClick={handleSaveSettings}
                  disabled={saving || !isValid}
                  className="gap-2 font-medium bg-primary hover:bg-primary/95 text-primary-foreground shadow-md shadow-primary/10"
                >
                  {saving ? (
                    <Sliders className="h-4 w-4 animate-spin" />
                  ) : (
                    <Save className="h-4 w-4" />
                  )}
                  Save Active Weights
                </Button>
              </div>
            )}

          </CardContent>
        </Card>

        {/* Sidebar Cost Function Explanation */}
        <div className="space-y-6">
          <Card className="rounded-2xl border bg-card shadow-sm">
            <CardHeader className="pb-3 border-b">
              <CardTitle className="text-sm font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
                <TrendingUp className="h-4 w-4 text-primary" />
                Algorithm Scoring
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-4 text-xs text-muted-foreground space-y-4">
              <p>
                The optimizer schedules routes using a combination of **Constraint Satisfaction Problem (CSP)** and **TOPSIS**. 
                The CSP stage filters out trips that violate hard constraints (e.g., driver hours, maintenance status, maximum weight).
              </p>
              <p>
                The TOPSIS stage then calculates the Closeness Coefficient of each valid trip based on its geometric distance to the positive-ideal and negative-ideal solutions.
              </p>

              <div className="space-y-3.5 pt-2">
                <div className="space-y-1">
                  <p className="font-semibold text-slate-800 flex items-center gap-1">
                    <MapPin className="h-3.5 w-3.5 text-blue-500" />
                    Deadhead Distance
                  </p>
                  <p>Minimizes the empty miles between the drop-off of the completed trip and the pick-up of the candidate trip.</p>
                </div>

                <div className="space-y-1">
                  <p className="font-semibold text-slate-800 flex items-center gap-1">
                    <Clock className="h-3.5 w-3.5 text-indigo-500" />
                    Cleaning Time
                  </p>
                  <p>Accounts for the duration required for sanitation and washing between incompatible cargo types.</p>
                </div>

                <div className="space-y-1">
                  <p className="font-semibold text-slate-800 flex items-center gap-1">
                    <AlertTriangle className="h-3.5 w-3.5 text-rose-500" />
                    Waiting Time
                  </p>
                  <p>Minimizes driver idle time before the next scheduled pick-up window opens.</p>
                </div>
                
                <div className="space-y-1">
                  <p className="font-semibold text-slate-800 flex items-center gap-1">
                    <Info className="h-3.5 w-3.5 text-amber-500" />
                    Job Urgency
                  </p>
                  <p>Prioritizes requests with impending deadlines or high-value client SLA requirements.</p>
                </div>

                <div className="space-y-1">
                  <p className="font-semibold text-slate-800 flex items-center gap-1">
                    <Box className="h-3.5 w-3.5 text-emerald-500" />
                    Cargo Compatibility
                  </p>
                  <p>Rewards chaining trips with similar cargo to eliminate cleaning overhead and reduce structural damage risk.</p>
                </div>
                
                <div className="space-y-1">
                  <p className="font-semibold text-slate-800 flex items-center gap-1">
                    <Truck className="h-3.5 w-3.5 text-purple-500" />
                    Asset Utilization
                  </p>
                  <p>Maximizes the overall active operational time of the vehicle fleet to ensure better ROI on trucks.</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
