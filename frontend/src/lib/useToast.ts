import { useCallback, useRef, useState } from "react";

export type Toast = {
  id: number;
  message: string;
  type: "success" | "error";
};

export function useToast() {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const lastRef = useRef<{ message: string; ts: number } | null>(null);

  const show = useCallback((message: string, type: Toast["type"] = "success") => {
    const now = Date.now();
    if (lastRef.current && lastRef.current.message === message && now - lastRef.current.ts < 1500) {
      return;
    }
    if (lastRef.current) {
      lastRef.current.message = message;
      lastRef.current.ts = now;
    } else {
      lastRef.current = { message, ts: now };
    }
    const id = Date.now() + Math.floor(Math.random() * 1000);
    setToasts((prev) => [...prev, { id, message, type }]);
    window.setTimeout(() => {
      setToasts((prev) => prev.filter((toast) => toast.id !== id));
    }, 2500);
  }, []);

  return { toasts, show };
}
