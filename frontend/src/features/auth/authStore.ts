import { setAccessToken, api } from "@/lib/api";
import type { LoginRequest, LoginResponse, MeResponse } from "./types";

const STORAGE_KEY = "nvg_access_token";

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
  if (roles.includes("Dispatcher")) {
    return "/dispatch/board";
  }
  return "/dashboard";
}

export async function login(payload: LoginRequest) {
  const resp = await api<LoginResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(payload),
    auth: false
  });
  token = resp.accessToken;
  setAccessToken(token);
  sessionStorage.setItem(STORAGE_KEY, token);
  me = await api<MeResponse>("/api/auth/me", { method: "GET" });
  return me;
}

export async function loadMeIfTokenExists() {
  const saved = sessionStorage.getItem(STORAGE_KEY);
  if (!saved) {
    return me;
  }
  try {
    token = saved;
    setAccessToken(token);
    me = await api<MeResponse>("/api/auth/me", { method: "GET" });
    return me;
  } catch {
    logout();
    return null;
  }
}

export function logout() {
  token = null;
  me = null;
  setAccessToken(null);
  sessionStorage.removeItem(STORAGE_KEY);
}
