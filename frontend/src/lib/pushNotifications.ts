import { PushNotifications } from "@capacitor/push-notifications";
import { Capacitor } from "@capacitor/core";
import { api } from "@/lib/api";

export async function registerPushNotifications(): Promise<void> {
  if (!Capacitor.isNativePlatform()) {
    return;
  }

  const permission = await PushNotifications.requestPermissions();
  if (permission.receive !== "granted") {
    return;
  }

  await PushNotifications.register();

  void PushNotifications.addListener("registration", (token) => {
    void savePushToken(token.value);
  });

  void PushNotifications.addListener("pushNotificationReceived", (notification) => {
    console.log("Push received:", notification);
  });

  void PushNotifications.addListener("pushNotificationActionPerformed", (action) => {
    handleNotificationTap(normalizeNotificationData(action.notification.data));
  });
}

async function savePushToken(token: string): Promise<void> {
  await api<void>("/api/notifications/push-token", {
    method: "POST",
    body: JSON.stringify({ token, platform: Capacitor.getPlatform() })
  });
}

function handleNotificationTap(data: Record<string, string>): void {
  if (data.tripId) {
    window.location.href = `/dispatch/trips/${data.tripId}`;
  }
  if (data.requestId) {
    window.location.href = `/shipment-requests/${data.requestId}`;
  }
}

function normalizeNotificationData(data: unknown): Record<string, string> {
  if (!data || typeof data !== "object") {
    return {};
  }

  return Object.entries(data as Record<string, unknown>).reduce<Record<string, string>>(
    (acc, [key, value]) => {
      if (value !== undefined && value !== null) {
        acc[key] = String(value);
      }
      return acc;
    },
    {}
  );
}
