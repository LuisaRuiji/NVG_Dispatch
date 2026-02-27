import { emitToast } from "@/lib/toastBus";
import { emitMaintenance } from "@/lib/maintenanceBus";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

let accessToken: string | null = null;
let onUnauthorized: (() => void) | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler;
}

type RequestInitEx = RequestInit & { auth?: boolean };

type ApiErrorPayload = {
  errorCode?: string;
  message?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
};

export class ApiRequestError extends Error {
  status: number;
  errorCode?: string;
  traceId?: string;
  validationErrors?: Record<string, string[]>;
  raw?: string;

  constructor(status: number, message: string, payload?: ApiErrorPayload, raw?: string) {
    super(message);
    this.name = "ApiRequestError";
    this.status = status;
    this.errorCode = payload?.errorCode;
    this.traceId = payload?.traceId;
    this.validationErrors = payload?.errors;
    this.raw = raw;
  }
}

function normalizePayload(raw: string): ApiErrorPayload | null {
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as ApiErrorPayload | string;
    if (typeof parsed === "string") {
      return { message: parsed };
    }
    if (parsed && typeof parsed === "object") {
      const asAny = parsed as Record<string, any>;
      return {
        errorCode: parsed.errorCode ?? asAny.ErrorCode,
        message: parsed.message ?? asAny.Message,
        traceId: parsed.traceId ?? asAny.TraceId,
        errors: parsed.errors ?? asAny.Errors
      };
    }
  } catch {
    return null;
  }
  return null;
}

export async function api<T>(path: string, init: RequestInitEx = {}): Promise<T> {
  const headers = new Headers(init.headers || {});
  headers.set("Content-Type", "application/json");

  if (init.auth !== false && accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }

  const res = await fetch(`${API_BASE_URL}${path}`, { ...init, headers });

  if (!res.ok) {
    const raw = await res.text().catch(() => "");
    const apiError = normalizePayload(raw);
    let message =
      apiError?.message?.trim() ||
      raw.trim() ||
      `${res.status} ${res.statusText}`.trim();

    if (res.status === 401 && init.auth !== false) {
      emitToast("Session expired", "error");
      onUnauthorized?.();
    }

    if (apiError?.errorCode === "MODULE_DISABLED") {
      const maintenanceMessage =
        apiError?.message?.trim() ||
        raw.trim() ||
        "Module is under maintenance.";
      emitMaintenance(maintenanceMessage);
    }

    throw new ApiRequestError(res.status, message || "Request failed", apiError ?? undefined, raw);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export async function downloadFile(path: string, filename: string) {
  const headers = new Headers();
  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }

  const res = await fetch(`${API_BASE_URL}${path}`, { headers });

  if (!res.ok) {
    const raw = await res.text().catch(() => "");
    const apiError = normalizePayload(raw);
    let message =
      apiError?.message?.trim() ||
      raw.trim() ||
      `${res.status} ${res.statusText}`.trim();

    if (res.status === 401) {
      emitToast("Session expired", "error");
      onUnauthorized?.();
    }

    throw new ApiRequestError(res.status, message || "Request failed", apiError ?? undefined, raw);
  }

  const blob = await res.blob();
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(url);
}
