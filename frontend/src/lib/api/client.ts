const defaultBaseUrl = "http://localhost:5000";

export const apiBaseUrl =
  import.meta.env.VITE_API_BASE_URL?.trim() || defaultBaseUrl;

async function request<T>(
  path: string,
  init?: RequestInit,
  token?: string
): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init?.headers ?? {})
    },
    ...init
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `Request failed: ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function apiGet<T>(path: string, token?: string) {
  return request<T>(path, { method: "GET" }, token);
}

export function apiPost<T>(path: string, body?: unknown, token?: string) {
  return request<T>(path, {
    method: "POST",
    body: body ? JSON.stringify(body) : undefined
  }, token);
}

export function apiPut<T>(path: string, body?: unknown, token?: string) {
  return request<T>(path, {
    method: "PUT",
    body: body ? JSON.stringify(body) : undefined
  }, token);
}

export function apiDelete<T>(path: string, token?: string) {
  return request<T>(path, { method: "DELETE" }, token);
}
