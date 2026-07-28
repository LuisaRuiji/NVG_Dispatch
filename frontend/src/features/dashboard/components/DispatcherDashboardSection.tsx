import { useCallback, useEffect, useRef, useState } from "react";
import type { DashboardKpis } from "../kpis";
import { DashboardSection } from "./sectionPrimitives";
import RecommendationPanel from "@/features/dispatch/components/RecommendationPanel";
import { useDispatchHub } from "@/hooks/useDispatchHub";
import { clearDashboardKpiCache } from "../kpis";
import {
  fetchDispatcherOperations,
  type DispatcherOperationsSnapshot
} from "../dispatcherOperations";
import DispatcherOperationalDashboard from "./dispatcher/DispatcherOperationalDashboard";

type Props = {
  kpis: DashboardKpis;
  loading: boolean;
};

export default function DispatcherDashboardSection({ kpis, loading }: Props) {
  const dispatch = kpis.dispatchKpis;
  const [snapshot, setSnapshot] = useState<DispatcherOperationsSnapshot | null>(null);
  const [operationsLoading, setOperationsLoading] = useState(true);
  const [operationsError, setOperationsError] = useState<string | null>(null);
  const operationsRequestId = useRef(0);

  const loadOperations = useCallback(async () => {
    const requestId = ++operationsRequestId.current;
    setOperationsLoading(true);
    try {
      const result = await fetchDispatcherOperations();
      if (requestId !== operationsRequestId.current) return;
      setSnapshot(result);
      setOperationsError(null);
    } catch (error) {
      if (requestId !== operationsRequestId.current) return;
      console.error(error);
      setOperationsError("Could not update the live operations view. Retry to load the latest trips and driver availability.");
    } finally {
      if (requestId === operationsRequestId.current) {
        setOperationsLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    void loadOperations();
    const refreshHandler = () => void loadOperations();
    window.addEventListener("nvg:dispatcher-dashboard-refresh", refreshHandler);
    return () => {
      operationsRequestId.current += 1;
      window.removeEventListener("nvg:dispatcher-dashboard-refresh", refreshHandler);
    };
  }, [loadOperations]);

  useDispatchHub({
    onRecommendationGenerated: () => {
      window.dispatchEvent(new Event("nvg:recommendations-refresh"));
    },
    onTripStatusChanged: () => {
      clearDashboardKpiCache();
    },
    onDocumentUploaded: clearDashboardKpiCache,
    onDocumentVerified: clearDashboardKpiCache,
    onShipmentRequestSubmitted: clearDashboardKpiCache
  });

  return (
    <DashboardSection
      title="Dispatch operations"
      description="Review active trips, resolve assignment blockers, and confirm next-job recommendations."
      loading={loading && dispatch === null}
      empty={dispatch === null}
      actions={[]}
    >
      {dispatch ? (
        <DispatcherOperationalDashboard
          kpis={dispatch}
          snapshot={snapshot}
          loading={operationsLoading}
          error={operationsError}
          onRetry={() => void loadOperations()}
        />
      ) : null}
      <RecommendationPanel />
    </DashboardSection>
  );
}
