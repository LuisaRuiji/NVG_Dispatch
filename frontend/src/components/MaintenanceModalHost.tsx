import { useEffect, useState } from "react";
import MaintenanceModal from "@/components/MaintenanceModal";
import { onMaintenance } from "@/lib/maintenanceBus";

export default function MaintenanceModalHost() {
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState("");

  useEffect(() => {
    return onMaintenance((detail) => {
      setMessage(detail.message);
      setOpen(true);
    });
  }, []);

  return <MaintenanceModal open={open} message={message} onClose={() => setOpen(false)} />;
}
