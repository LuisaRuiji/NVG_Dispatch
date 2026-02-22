import { ApiRequestError } from "@/lib/api";
import { emitToast } from "@/lib/toastBus";

export type NormalizedApiError = {
  status: number;
  message: string;
  errorCode?: string;
  traceId?: string;
  validationErrors?: Record<string, string[]>;
  raw?: string;
};

export function normalizeApiError(error: unknown): NormalizedApiError {
  if (error instanceof ApiRequestError) {
    return {
      status: error.status,
      message: error.message,
      errorCode: error.errorCode,
      traceId: error.traceId,
      validationErrors: error.validationErrors,
      raw: error.raw
    };
  }

  const fallbackMessage = error instanceof Error ? error.message : "Request failed";
  return {
    status: 0,
    message: fallbackMessage
  };
}

export function getUserMessage(err: NormalizedApiError) {
  if (err.status === 401) return "Session expired";
  if (err.status === 403) return "Not authorized";
  if (err.status === 409) return err.message || "Conflict";
  if (err.status === 400 || err.status === 422) {
    if (err.validationErrors) {
      const first = Object.values(err.validationErrors).flat()[0];
      if (first) return first;
    }
    return err.message || "Fix validation errors";
  }
  if (err.status >= 500) return "Something went wrong";
  return err.message || "Request failed";
}

export function handleApiError(
  error: unknown,
  show?: (message: string, type?: "success" | "error") => void
) {
  const normalized = normalizeApiError(error);
  const message = getUserMessage(normalized);

  if (show) {
    show(message, "error");
  } else {
    emitToast(message, "error");
  }

  if (import.meta.env.DEV) {
    console.error("API error:", normalized);
  }

  return normalized;
}
