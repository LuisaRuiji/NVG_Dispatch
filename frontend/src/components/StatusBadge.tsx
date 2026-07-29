import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { formatStatusLabel, normalizeStatusKey } from "@/lib/statusFormatters";

type Props = {
  status?: string | null;
};

const statusMap: Record<string, string> = {
  draft: "border-[#E2E8F0] bg-[#F1F5F9] text-[#475569]",
  ready_for_dispatch: "border-[#A7F3D0] bg-[#D1FAE5] text-[#065F46]",
  submitted: "border-[#BFDBFE] bg-[#EAF3FF] text-[#1D4ED8]",
  needs_revision: "border-[#FDE68A] bg-[#FEF3C7] text-[#92400E]",
  pending_io: "border-[#FDE68A] bg-[#FEF3C7] text-[#92400E]",
  pending_manager: "border-[#FDE68A] bg-[#FEF3C7] text-[#92400E]",
  pending_finance: "border-[#FDE68A] bg-[#FEF3C7] text-[#92400E]",
  pending_ceo: "border-[#FDE68A] bg-[#FEF3C7] text-[#92400E]",
  approved: "border-[#A7F3D0] bg-[#D1FAE5] text-[#065F46]",
  issued: "border-[#BFDBFE] bg-[#EAF3FF] text-[#1D4ED8]",
  rejected: "border-[#FECACA] bg-[#FEE2E2] text-[#991B1B]",
  converted_to_trip: "border-[#A7F3D0] bg-[#D1FAE5] text-[#065F46]",
  closed: "border-[#E2E8F0] bg-[#F1F5F9] text-[#475569]",
  open: "border-[#BFDBFE] bg-[#EAF3FF] text-[#1D4ED8]",
  partially_received: "border-[#FDE68A] bg-[#FEF3C7] text-[#92400E]",
  partially_returned: "border-[#FDE68A] bg-[#FEF3C7] text-[#92400E]",
  dispatched: "border-[#BFDBFE] bg-[#EAF3FF] text-[#1D4ED8]",
  enroute_pickup: "border-[#BFDBFE] bg-[#EAF3FF] text-[#1D4ED8]",
  at_pickup: "border-[#C7D2FE] bg-[#EEF2FF] text-[#4338CA]",
  loaded: "border-[#C7D2FE] bg-[#EEF2FF] text-[#4338CA]",
  enroute_dropoff: "border-[#BFDBFE] bg-[#EAF3FF] text-[#1D4ED8]",
  at_dropoff: "border-[#C7D2FE] bg-[#EEF2FF] text-[#4338CA]",
  delivered: "border-[#A7F3D0] bg-[#D1FAE5] text-[#065F46]",
  cancelled: "border-[#FECACA] bg-[#FEE2E2] text-[#991B1B]",
  on_hold: "border-[#FDE68A] bg-[#FEF3C7] text-[#92400E]",
  failed_attempt: "border-[#FECACA] bg-[#FEE2E2] text-[#991B1B]"
};

export default function StatusBadge({ status }: Props) {
  const raw = status ?? "Unknown";
  const key = normalizeStatusKey(raw);
  const klass =
    statusMap[key] ??
    (key.includes("pending") ? "bg-amber-100 text-amber-700 border-amber-200" : undefined) ??
    (key.includes("approved") ? statusMap.approved : undefined) ??
    (key.includes("rejected") ? statusMap.rejected : undefined) ??
    (key.includes("closed") ? statusMap.closed : undefined) ??
    "bg-muted text-muted-foreground border-border";

  return (
    <Badge className={cn("border px-2 py-0.5 text-xs font-semibold normal-case tracking-normal", klass)}>
      {formatStatusLabel(raw)}
    </Badge>
  );
}
