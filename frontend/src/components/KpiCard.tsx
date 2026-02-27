import { Card, CardContent } from "@/components/ui/card";

type Props = {
  title: string;
  value: string | number | null;
  subtitle?: string;
};

export default function KpiCard({ title, value, subtitle }: Props) {
  const display = value === null ? "—" : value;
  return (
    <Card className="surface-card hover-lift fade-up border-transparent bg-gradient-to-br from-card to-muted/20">
      <CardContent className="p-6">
        <p className="text-xs font-bold uppercase tracking-widest text-muted-foreground/80">{title}</p>
        <p className="mt-2 text-3xl font-bold tracking-tight text-foreground">{display}</p>
        {subtitle ? <p className="mt-1.5 text-xs text-muted-foreground/70">{subtitle}</p> : null}
      </CardContent>
    </Card>
  );
}
