import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

type Props = {
  status?: string | null;
};

const statusMap: Record<string, string> = {
  draft: "bg-slate-100 text-slate-600 border-slate-200",
  submitted: "bg-blue-100 text-blue-700 border-blue-200",
  pending_io: "bg-amber-100 text-amber-700 border-amber-200",
  pending_manager: "bg-amber-100 text-amber-700 border-amber-200",
  pending_finance: "bg-amber-100 text-amber-700 border-amber-200",
  pending_ceo: "bg-amber-100 text-amber-700 border-amber-200",
  approved: "bg-emerald-100 text-emerald-700 border-emerald-200",
  issued: "bg-blue-100 text-blue-700 border-blue-200",
  rejected: "bg-red-100 text-red-700 border-red-200",
  closed: "bg-slate-100 text-slate-600 border-slate-200",
  open: "bg-blue-100 text-blue-700 border-blue-200",
  partially_received: "bg-amber-100 text-amber-700 border-amber-200",
  partially_returned: "bg-amber-100 text-amber-700 border-amber-200",
  dispatched: "bg-blue-100 text-blue-700 border-blue-200",
  enroute_pickup: "bg-blue-100 text-blue-700 border-blue-200",
  at_pickup: "bg-indigo-100 text-indigo-700 border-indigo-200",
  loaded: "bg-indigo-100 text-indigo-700 border-indigo-200",
  enroute_dropoff: "bg-blue-100 text-blue-700 border-blue-200",
  at_dropoff: "bg-indigo-100 text-indigo-700 border-indigo-200",
  delivered: "bg-emerald-100 text-emerald-700 border-emerald-200",
  cancelled: "bg-red-100 text-red-700 border-red-200",
  on_hold: "bg-amber-100 text-amber-700 border-amber-200",
  failed_attempt: "bg-rose-100 text-rose-700 border-rose-200"
};

export default function StatusBadge({ status }: Props) {
  const raw = status ?? "Unknown";
  const key = raw.toLowerCase().replace(/\s+/g, "_");
  const klass =
    statusMap[key] ??
    (key.includes("pending") ? statusMap.pending : undefined) ??
    (key.includes("approved") ? statusMap.approved : undefined) ??
    (key.includes("rejected") ? statusMap.rejected : undefined) ??
    (key.includes("closed") ? statusMap.closed : undefined) ??
    "bg-muted text-muted-foreground border-border";

  return (
    <Badge className={cn("border px-2 py-0.5 text-xs font-semibold uppercase tracking-wide", klass)}>
      {raw}
    </Badge>
  );
}
