import { cn } from "@/lib/utils";

type Props = {
  className?: string;
};

export default function Spinner({ className }: Props) {
  return (
    <span
      className={cn(
        "inline-block h-4 w-4 animate-spin rounded-full border-2 border-white/60 border-t-white",
        className
      )}
      aria-hidden="true"
    />
  );
}
