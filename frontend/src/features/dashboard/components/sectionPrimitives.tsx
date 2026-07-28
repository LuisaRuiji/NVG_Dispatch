import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import type { LucideIcon } from "lucide-react";
import { ArrowRight, Zap } from "lucide-react";
import EmptyState from "@/components/EmptyState";
import LoadingSkeleton from "@/components/LoadingSkeleton";

export type QuickAction = {
  label: string;
  to: string;
  icon: LucideIcon;
};

type DashboardSectionProps = {
  title: string;
  description: string;
  loading: boolean;
  empty: boolean;
  emptyTitle?: string;
  children: ReactNode;
  actions: QuickAction[];
};

export function DashboardSection({
  title,
  description,
  loading,
  empty,
  emptyTitle = "No dashboard data available.",
  children,
  actions
}: DashboardSectionProps) {
  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold text-foreground">{title}</h2>
          <p className="mt-1 text-sm text-muted-foreground">{description}</p>
        </div>
      </div>

      {loading ? (
        <div className="surface-card p-6">
          <LoadingSkeleton rows={6} />
        </div>
      ) : empty ? (
        <EmptyState title={emptyTitle} description="Refresh or check whether this role has active work." />
      ) : (
        <>
          {children}
          {actions.length > 0 ? <QuickActionList actions={actions} /> : null}
        </>
      )}
    </section>
  );
}

export function KpiGrid({ children }: { children: ReactNode }) {
  return <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">{children}</div>;
}

export function QuickActionList({ actions }: { actions: QuickAction[] }) {
  return (
    <div className="surface-card p-6">
      <div className="mb-4 flex items-center gap-2">
        <Zap className="h-4 w-4 text-primary" />
        <h3 className="text-sm font-semibold text-foreground">Quick Actions</h3>
      </div>
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        {actions.length === 0 ? (
          <EmptyState title="No quick actions" description="Your role has no quick actions." />
        ) : (
          actions.map((action) => (
            <Link
              key={`${action.to}:${action.label}`}
              to={action.to}
              className="group/action flex min-h-14 items-center justify-between rounded-lg border border-border/50 bg-card p-4 text-sm font-medium text-foreground transition-all duration-200 hover:border-primary/30 hover:bg-muted/50 hover:shadow-md hover:shadow-primary/5 active:scale-[0.99]"
            >
              <span className="flex min-w-0 items-center gap-3">
                <action.icon className="h-4 w-4 shrink-0 text-primary/70 transition-colors group-hover/action:text-primary" />
                <span className="truncate">{action.label}</span>
              </span>
              <ArrowRight className="h-4 w-4 shrink-0 text-muted-foreground transition-transform duration-200 group-hover/action:translate-x-1 group-hover/action:text-primary" />
            </Link>
          ))
        )}
      </div>
    </div>
  );
}

export function formatNumber(value?: number | null) {
  return value == null ? null : value.toLocaleString();
}

export function formatCurrency(value?: number | null) {
  if (value == null) {
    return null;
  }

  return `PHP ${value.toLocaleString(undefined, {
    maximumFractionDigits: 0
  })}`;
}

export function formatPercent(value?: number | null) {
  return value == null ? null : `${value.toLocaleString(undefined, { maximumFractionDigits: 1 })}%`;
}
