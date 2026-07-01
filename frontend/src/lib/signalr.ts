import * as signalR from "@microsoft/signalr";
import { getAccessToken } from "@/lib/api";

let connection: signalR.HubConnection | null = null;
let startPromise: Promise<void> | null = null;
let activeConsumers = 0;

function getHubUrl() {
  const baseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim();
  return baseUrl ? `${baseUrl.replace(/\/$/, "")}/hubs/dispatch` : "/hubs/dispatch";
}

export function getDispatchHubConnection(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(getHubUrl(), {
        withCredentials: true,
        accessTokenFactory: () => getAccessToken() ?? ""
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();
  }
  return connection;
}

export async function startDispatchHub(): Promise<void> {
  activeConsumers += 1;
  const hub = getDispatchHubConnection();
  if (hub.state === signalR.HubConnectionState.Disconnected) {
    startPromise ??= hub.start().finally(() => {
      startPromise = null;
    });
    await startPromise;
  }
}

export async function stopDispatchHub(): Promise<void> {
  activeConsumers = Math.max(0, activeConsumers - 1);
  if (activeConsumers > 0) {
    return;
  }

  if (connection?.state === signalR.HubConnectionState.Connected) {
    await connection.stop();
  }
}
