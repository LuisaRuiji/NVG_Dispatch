export type LoginRequest = { username: string; password: string };
export type LoginResponse = { accessToken: string; userId?: string; roles?: string[] };

export type MeResponse = {
  userId: string;
  username: string;
  roles: string[];
};
