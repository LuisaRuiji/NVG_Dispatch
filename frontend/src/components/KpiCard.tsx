import { Card, CardContent } from "@/components/ui/card";

type Props = {
  title: string;
  value: string | number | null;
  subtitle?: string;
};

export default function KpiCard({ title, value, subtitle }: Props) {
  const display = value === null ? "—" : value;
  return (
    <Card className="border-border bg-white shadow-sm">
      <CardContent className="p-5">
        <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">{title}</p>
        <p className="mt-2 text-2xl font-semibold text-foreground">{display}</p>
        {subtitle ? <p className="mt-1 text-xs text-muted-foreground">{subtitle}</p> : null}
      </CardContent>
    </Card>
  );
}
