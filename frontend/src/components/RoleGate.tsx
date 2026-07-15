import { useEffect, useRef, type ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { getDefaultRoute, getMe } from "@/features/auth/authStore";
import { emitToast } from "@/lib/toastBus";

export const LAST_AUTHORIZED_ROUTE_KEY = "vaia_last_authorized_route";

type Props = {
  roles: string[];
  children: ReactNode;
};

export default function RoleGate({ roles, children }: Props) {
  const me = getMe();
  const warnedRef = useRef(false);
  const location = useLocation();

  useEffect(() => {
    warnedRef.current = false;
  }, [location.pathname]);

  const allowed = me ? roles.some((role) => me.roles.includes(role)) : false;

  useEffect(() => {
    if (me && allowed) {
      rememberAuthorizedRoute(location.pathname);
    }
  }, [allowed, location.pathname, me]);

  if (!me) {
    return <Navigate to="/login" replace />;
  }
  if (!allowed) {
    if (!warnedRef.current) {
      emitToast("Not authorized to view that page.", "error");
      warnedRef.current = true;
    }
    return <Navigate to={getSafeReturnRoute(location.pathname)} replace />;
  }

  return <>{children}</>;
}

function rememberAuthorizedRoute(pathname: string) {
  if (typeof window === "undefined") {
    return;
  }

  window.sessionStorage.setItem(LAST_AUTHORIZED_ROUTE_KEY, pathname);
}

export function getSafeReturnRoute(currentPathname: string) {
  if (typeof window === "undefined") {
    return getDefaultRoute();
  }

  const previousRoute = window.sessionStorage.getItem(LAST_AUTHORIZED_ROUTE_KEY);
  if (previousRoute && previousRoute !== currentPathname) {
    return previousRoute;
  }

  return getDefaultRoute();
}
