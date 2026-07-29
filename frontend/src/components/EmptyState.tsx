type Props = {
  title: string;
  description?: string;
};

export default function EmptyState({ title, description }: Props) {
  return (
    <div className="flex flex-col items-center gap-2 py-16 text-center fade-in">
      <div className="mb-2 h-12 w-12 rounded-lg bg-muted flex items-center justify-center">
        <div className="h-6 w-6 rounded-md border-2 border-muted-foreground/30" />
      </div>
      <span className="text-base font-semibold text-foreground">{title}</span>
      {description ? <span className="max-w-[280px] text-sm text-muted-foreground/80">{description}</span> : null}
    </div>
  );
}
