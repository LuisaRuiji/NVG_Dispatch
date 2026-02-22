import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "@/lib/api";
import { getMe } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";

type RequestListItem = {
  id: string;
  requestType: string;
  status: string;
  assetId?: string | null;
  submittedAt?: string | null;
};

export default function ManagerPendingPage() {
  const nav = useNavigate();
  const me = getMe();
  const [items, setItems] = useState<RequestListItem[]>([]);
  const { toasts, show } = useToast();

  useEffect(() => {
    if (!me) {
      nav("/login");
      return;
    }
    if (!me.roles?.includes("Manager")) {
      show("Access denied.", "error");
      return;
    }

    (async () => {
      try {
        const list = await api<RequestListItem[]>("/api/requests?status=PENDING_MANAGER", { method: "GET" });
        setItems(list);
      } catch (e: any) {
        console.error(e);
        show(e?.message ?? "Failed to load pending manager requests.", "error");
      }
    })();
  }, []);

  return (
    <div style={{ maxWidth: 900, margin: "32px auto" }}>
      <ToastHost toasts={toasts} />
      <button onClick={() => nav("/requests")} style={{ marginBottom: 12 }}>
        Back
      </button>
      <h1>Manager Pending Requests</h1>

      <ul>
        {items.map((item) => (
          <li key={item.id}>
            <Link to={`/requests/${item.id}`}>{item.id} — {item.requestType} — {item.status}</Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
