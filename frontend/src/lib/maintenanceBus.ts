export type MaintenanceEventDetail = {
  message: string;
};

const EVENT_NAME = "nvg:maintenance";

export function emitMaintenance(message: string) {
  if (typeof window === "undefined") return;
  window.dispatchEvent(
    new CustomEvent<MaintenanceEventDetail>(EVENT_NAME, { detail: { message } })
  );
}

export function onMaintenance(handler: (detail: MaintenanceEventDetail) => void) {
  if (typeof window === "undefined") return () => undefined;
  const listener = (event: Event) => {
    const custom = event as CustomEvent<MaintenanceEventDetail>;
    if (custom.detail) {
      handler(custom.detail);
    }
  };
  window.addEventListener(EVENT_NAME, listener);
  return () => window.removeEventListener(EVENT_NAME, listener);
}
