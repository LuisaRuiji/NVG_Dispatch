type Props = {
  title: string;
  description?: string;
};

export default function EmptyState({ title, description }: Props) {
  return (
    <div className="flex flex-col items-center gap-1 py-10 text-center text-sm text-muted-foreground">
      <span className="text-base font-semibold text-foreground">{title}</span>
      {description ? <span>{description}</span> : null}
    </div>
  );
}
