import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

type Props = {
  children: ReactNode;
  className?: string;
  tableClassName?: string;
};

export default function DataTable({ children, className, tableClassName }: Props) {
  return (
    <div className={cn("data-table-scroll rounded-xl border border-border bg-card", className)}>
      <table className={cn("w-full border-collapse text-sm table-smooth", tableClassName)}>{children}</table>
    </div>
  );
}
