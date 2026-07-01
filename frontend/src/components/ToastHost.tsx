import type { Toast } from "@/lib/useToast";

type Props = {
  toasts: Toast[];
};

export default function ToastHost({ toasts }: Props) {
  if (toasts.length === 0) return null;

  return (
    <div className="fixed right-6 top-6 z-[80] grid gap-2">
      {toasts.map((toast) => (
        <div
          key={toast.id}
          className={`min-w-[240px] rounded-lg border px-3 py-2 text-sm shadow-sm ${
            toast.type === "error"
              ? "border-red-200 bg-red-50 text-red-700"
              : "border-emerald-200 bg-emerald-50 text-emerald-700"
          }`}
        >
          {toast.message}
        </div>
      ))}
    </div>
  );
}
