import { useEffect, useRef, type ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { getDefaultRoute, getMe } from "@/features/auth/authStore";
import { emitToast } from "@/lib/toastBus";

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
  if (!me) {
    return <Navigate to="/login" replace />;
  }
  const allowed = roles.some((role) => me.roles.includes(role));
  if (!allowed) {
    if (!warnedRef.current) {
      emitToast("Not authorized to view that page.", "error");
      warnedRef.current = true;
    }
    return <Navigate to={getDefaultRoute()} replace />;
  }
  return <>{children}</>;
}
