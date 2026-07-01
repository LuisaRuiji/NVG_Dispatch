import type { ReactNode } from "react";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";

export const chartColors = {
  primary: "hsl(var(--primary))",
  accent: "hsl(var(--accent))",
  muted: "hsl(var(--muted-foreground))",
  border: "hsl(var(--border))",
  danger: "hsl(var(--destructive))"
};

export const donutPalette = [
  chartColors.primary,
  chartColors.accent,
  "#22c55e",
  "#f59e0b",
  "#06b6d4",
  "#8b5cf6",
  "#ef4444",
  "#64748b"
];

type ChartCardProps = {
  title: string;
  loading: boolean;
  empty: boolean;
  emptyDescription: string;
  children: ReactNode;
};

export function ChartCard({ title, loading, empty, emptyDescription, children }: ChartCardProps) {
  return (
    <div className="surface-card p-5">
      <h3 className="mb-4 text-sm font-semibold text-foreground">{title}</h3>
      {loading ? (
        <LoadingSkeleton rows={5} />
      ) : empty ? (
        <EmptyState title="No chart data" description={emptyDescription} />
      ) : (
        <div className="h-64 min-h-64 w-full min-w-[1px]">{children}</div>
      )}
    </div>
  );
}

export function ChartGrid({ children }: { children: ReactNode }) {
  return <div className="grid gap-4 xl:grid-cols-2">{children}</div>;
}
