type Props = {
  rows?: number;
};

export default function LoadingSkeleton({ rows = 4 }: Props) {
  return (
    <div className="space-y-3">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="h-4 w-full rounded-full bg-muted/80" />
      ))}
    </div>
  );
}
