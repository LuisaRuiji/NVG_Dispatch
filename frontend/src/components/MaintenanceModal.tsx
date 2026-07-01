type Props = {
  open: boolean;
  message: string;
  onClose: () => void;
};

export default function MaintenanceModal({ open, message, onClose }: Props) {
  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-[2px] fade-in"
      role="presentation"
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Maintenance notice"
        className="w-[min(90vw,440px)] rounded-2xl border border-slate-200 bg-white p-6 shadow-xl fade-up"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Maintenance</p>
            <h2 className="mt-2 text-lg font-semibold text-slate-900">Module temporarily unavailable</h2>
          </div>
          <button
            onClick={onClose}
            className="rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-500 hover:text-slate-900"
          >
            Close
          </button>
        </div>
        <p className="mt-3 text-sm text-slate-600">{message}</p>
        <div className="mt-5 flex justify-end">
          <button
            onClick={onClose}
            className="rounded-lg bg-[#175C99] px-4 py-2 text-sm font-semibold text-white hover:bg-[#144c7f]"
          >
            Okay
          </button>
        </div>
      </div>
    </div>
  );
}
