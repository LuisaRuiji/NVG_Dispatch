export type ToastType = "success" | "error";

export type ToastEventDetail = {
  message: string;
  type?: ToastType;
};

const EVENT_NAME = "nvg:toast";

export function emitToast(message: string, type: ToastType = "error") {
  if (typeof window === "undefined") return;
  window.dispatchEvent(
    new CustomEvent<ToastEventDetail>(EVENT_NAME, { detail: { message, type } })
  );
}

export function onToast(handler: (detail: ToastEventDetail) => void) {
  if (typeof window === "undefined") return () => undefined;
  const listener = (event: Event) => {
    const custom = event as CustomEvent<ToastEventDetail>;
    if (custom.detail) {
      handler(custom.detail);
    }
  };
  window.addEventListener(EVENT_NAME, listener);
  return () => window.removeEventListener(EVENT_NAME, listener);
}
