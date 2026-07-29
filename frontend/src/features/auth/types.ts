import type { UserRole } from "./roles";

export type LoginRequest = { username: string; password: string; rememberMe?: boolean };
export type LoginResponse = {
  accessToken: string;
  refreshToken?: string | null;
  expiresAtUtc?: string;
  userId?: string;
  roles?: string[];
  mustChangePassword?: boolean;
};

export type MfaRequiredResponse = {
  mfaRequired: true;
  challengeId: string;
  method: string;
  expiresAtUtc: string;
};

export type MfaVerifyRequest = {
  challengeId: string;
  code: string;
  rememberMe?: boolean;
};

export type MfaSetupResponse = {
  secretKey: string;
  otpAuthUri: string;
};

export type MfaStatusResponse = {
  enabled: boolean;
  enabledAt?: string | null;
  lastVerifiedAt?: string | null;
};

export type StepUpResponse = {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  roles: string[];
};

export type MeResponse = {
  userId: string;
  username: string;
  email?: string | null;
  roles: UserRole[];
  mfaEnabled?: boolean;
  mustChangePassword?: boolean;
};
