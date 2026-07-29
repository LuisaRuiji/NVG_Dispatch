import { Card, CardContent } from "@/components/ui/card";

type Props = {
  title: string;
  value: string | number | null;
  subtitle?: string;
};

export default function KpiCard({ title, value, subtitle }: Props) {
  const display = value === null ? "—" : value;
  return (
    <Card className="surface-card">
      <CardContent className="p-4">
        <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">{title}</p>
        <p className="mt-1 text-[28px] font-bold leading-none tracking-tight text-foreground">{display}</p>
        {subtitle ? <p className="mt-2 text-xs text-muted-foreground">{subtitle}</p> : null}
      </CardContent>
    </Card>
  );
}
