import { setAccessToken, api } from "@/lib/api";
import type {
  LoginRequest,
  LoginResponse,
  MeResponse,
  MfaRequiredResponse,
  MfaSetupResponse,
  MfaStatusResponse,
  MfaVerifyRequest,
  StepUpResponse
} from "./types";

const LEGACY_ACCESS_TOKEN_STORAGE_KEY = "nvg_access_token";
const LEGACY_REFRESH_TOKEN_STORAGE_KEY = "nvg_refresh_token";
const CSRF_HEADER = { "X-NVG-CSRF": "1" };

let token: string | null = null;
let me: MeResponse | null = null;

export function getToken() {
  return token;
}

export function getMe() {
  return me;
}

export function getDefaultRoute(current: MeResponse | null = me) {
  if (!current) {
    return "/login";
  }
  const roles = current.roles ?? [];
  if (roles.includes("Admin") || roles.includes("SuperAdmin")) {
    return "/admin/users";
  }
  if (roles.includes("Driver")) {
    return "/dispatch/my-trips";
  }
  if (roles.includes("Dispatcher") || roles.includes("Manager") || roles.includes("HeadOfFinance") || roles.includes("CEO")) {
    return "/dispatch";
  }
  if (roles.includes("Customer")) {
    return "/portal/dashboard";
  }
  return "/dashboard";
}

export function isMfaRequiredResponse(
  value: LoginResponse | MeResponse | MfaRequiredResponse
): value is MfaRequiredResponse {
  return "mfaRequired" in value && value.mfaRequired === true;
}

export async function login(payload: LoginRequest): Promise<MeResponse | MfaRequiredResponse> {
  const resp = await api<LoginResponse | MfaRequiredResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(payload),
    auth: false
  });

  if (isMfaRequiredResponse(resp)) {
    return resp;
  }

  applyAccessToken(resp);
  me = await api<MeResponse>("/api/auth/me", { method: "GET" });
  return me;
}

export async function verifyMfaLogin(payload: MfaVerifyRequest) {
  const resp = await api<LoginResponse>("/api/auth/mfa/verify", {
    method: "POST",
    body: JSON.stringify(payload),
    auth: false
  });
  applyAccessToken(resp);
  me = await api<MeResponse>("/api/auth/me", { method: "GET" });
  return me;
}

export function getMfaStatus() {
  return api<MfaStatusResponse>("/api/auth/mfa", { method: "GET" });
}

export function setupMfa() {
  return api<MfaSetupResponse>("/api/auth/mfa/setup", { method: "POST", body: JSON.stringify({}) });
}

export async function confirmMfa(code: string) {
  const status = await api<MfaStatusResponse>("/api/auth/mfa/confirm", {
    method: "POST",
    body: JSON.stringify({ code })
  });
  if (me) {
    me = { ...me, mfaEnabled: status.enabled };
  }
  return status;
}

export async function disableMfa(code: string) {
  const status = await api<MfaStatusResponse>("/api/auth/mfa/disable", {
    method: "POST",
    body: JSON.stringify({ code })
  });
  if (me) {
    me = { ...me, mfaEnabled: status.enabled };
  }
  return status;
}

export async function stepUp(code: string) {
  const resp = await api<StepUpResponse>("/api/auth/step-up", {
    method: "POST",
    body: JSON.stringify({ code })
  });
  token = resp.accessToken;
  setAccessToken(token);
  return resp;
}

export async function loadMeIfTokenExists() {
  clearLegacyTokenStorage();

  if (token) {
    try {
      me = await api<MeResponse>("/api/auth/me", { method: "GET" });
      return me;
    } catch {
      token = null;
      setAccessToken(null);
    }
  }

  if (await refreshSession()) {
    me = await api<MeResponse>("/api/auth/me", { method: "GET" });
    return me;
  }

  clearSessionState();
  return null;
}

export async function refreshSession() {
  try {
    const resp = await api<LoginResponse>("/api/auth/refresh", {
      method: "POST",
      headers: CSRF_HEADER,
      body: JSON.stringify({}),
      auth: false
    });
    applyAccessToken(resp);
    return true;
  } catch {
    return false;
  }
}

export function logout() {
  void api<void>("/api/auth/logout", {
    method: "POST",
    headers: CSRF_HEADER,
    body: JSON.stringify({}),
    auth: false,
    retryOnUnauthorized: false
  }).catch(() => {});

  clearSessionState();
}

function applyAccessToken(resp: LoginResponse) {
  token = resp.accessToken;
  setAccessToken(token);
}

function clearSessionState() {
  token = null;
  me = null;
  setAccessToken(null);
  clearLegacyTokenStorage();
}

function clearLegacyTokenStorage() {
  if (typeof window === "undefined") {
    return;
  }

  window.sessionStorage.removeItem(LEGACY_ACCESS_TOKEN_STORAGE_KEY);
  window.sessionStorage.removeItem(LEGACY_REFRESH_TOKEN_STORAGE_KEY);
}
