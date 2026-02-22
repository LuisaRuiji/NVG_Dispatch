import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { getMe } from "@/features/auth/authStore";

export default function RequestsPage() {
  const nav = useNavigate();
  const me = getMe();

  useEffect(() => {
    if (!me) {
      nav("/login");
      return;
    }

    if (me.roles?.includes("InventoryOfficer")) {
      nav("/queue/io");
      return;
    }

    if (me.roles?.includes("Manager")) {
      nav("/queue/manager");
      return;
    }

    if (me.roles?.includes("Driver")) {
      nav("/my/requests");
      return;
    }

    nav("/login");
  }, []);

  return (
    <div style={{ maxWidth: 900, margin: "32px auto" }}>
      Redirecting...
    </div>
  );
}
