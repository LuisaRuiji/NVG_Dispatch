import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

type Props = {
  children: ReactNode;
  className?: string;
};

export default function DataTable({ children, className }: Props) {
  return (
    <div className={cn("overflow-auto rounded-xl border border-border bg-white fade-in", className)}>
      <table className="w-full border-collapse text-sm table-smooth">{children}</table>
    </div>
  );
}
