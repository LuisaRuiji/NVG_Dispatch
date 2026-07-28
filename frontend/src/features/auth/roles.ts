/**
 * The authenticated `/api/auth/me` response is the only role source used by
 * the SPA. Keep server role spelling/normalization at this boundary.
 */
export const USER_ROLES = [
  "SuperAdmin",
  "Admin",
  "Manager",
  "Dispatcher",
  "HeadOfFinance",
  "CEO",
  "InventoryOfficer",
  "Driver",
  "Customer",
  "Owner"
] as const;

export type UserRole = (typeof USER_ROLES)[number];
export type DashboardRole = Exclude<UserRole, "Owner">;

const rolesByNormalizedName: Readonly<Record<string, UserRole>> = {
  superadmin: "SuperAdmin",
  admin: "Admin",
  manager: "Manager",
  dispatcher: "Dispatcher",
  headoffinance: "HeadOfFinance",
  ceo: "CEO",
  inventoryofficer: "InventoryOfficer",
  driver: "Driver",
  customer: "Customer",
  owner: "Owner"
};

/** Normalizes roles once, immediately after the validated identity response. */
export function normalizeUserRoles(roles: readonly string[] | null | undefined): UserRole[] {
  const normalized = new Set<UserRole>();

  for (const role of roles ?? []) {
    const key = role.replace(/[\s_-]/g, "").toLowerCase();
    const normalizedRole = rolesByNormalizedName[key];
    if (normalizedRole) {
      normalized.add(normalizedRole);
    }
  }

  return USER_ROLES.filter((role) => normalized.has(role));
}

/**
 * A user has one dashboard persona. This precedence is shared by the shell
 * and dashboard so a visible role can never disagree with the rendered data.
 */
export function resolvePrimaryRole(roles: readonly UserRole[]): UserRole | null {
  return USER_ROLES.find((role) => roles.includes(role)) ?? null;
}

export function resolveDashboardRole(roles: readonly UserRole[]): DashboardRole | null {
  const role = resolvePrimaryRole(roles);
  return role && role !== "Owner" ? role : null;
}

export function hasRole(roles: readonly UserRole[], role: UserRole): boolean {
  return roles.includes(role);
}
