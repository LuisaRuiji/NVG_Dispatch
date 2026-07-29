import { FormEvent, useState } from "react";
import { CheckCircle2, X } from "lucide-react";

type DemoRequestModalProps = {
    open: boolean;
    onClose: () => void;
};

type DemoRequestForm = {
    companyName: string;
    contactName: string;
    email: string;
    phoneNumber: string;
    fleetSize: string;
    message: string;
};

const initialForm: DemoRequestForm = {
    companyName: "",
    contactName: "",
    email: "",
    phoneNumber: "",
    fleetSize: "1-10 trucks",
    message: ""
};

const fleetSizeOptions = ["1-10 trucks", "11-30 trucks", "31-50 trucks", "50+ trucks"];

export default function DemoRequestModal({ open, onClose }: DemoRequestModalProps) {
    const [form, setForm] = useState<DemoRequestForm>(initialForm);
    const [submitting, setSubmitting] = useState(false);
    const [submitted, setSubmitted] = useState(false);
    const [error, setError] = useState<string | null>(null);

    if (!open) return null;

    const updateField = (field: keyof DemoRequestForm, value: string) => {
        setForm((current) => ({ ...current, [field]: value }));
    };

    const closeModal = () => {
        onClose();
        setError(null);
        if (submitted) {
            setSubmitted(false);
            setForm(initialForm);
        }
    };

    const handleSubmit = async (e: FormEvent) => {
        e.preventDefault();
        setSubmitting(true);
        setError(null);

        try {
            const endpoint = import.meta.env.VITE_DEMO_REQUEST_URL as string | undefined;
            if (endpoint) {
                const response = await fetch(endpoint, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(form)
                });

                if (!response.ok) {
                    throw new Error("Demo request failed");
                }
            } else {
                const subject = `Demo request from ${form.companyName}`;
                const body = [
                    `Company name: ${form.companyName}`,
                    `Contact name: ${form.contactName}`,
                    `Email: ${form.email}`,
                    `Phone number: ${form.phoneNumber}`,
                    `Fleet size: ${form.fleetSize}`,
                    `Message: ${form.message || "N/A"}`
                ].join("\n");

                window.location.href = `mailto:demo@nvgdispatch.com?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;
            }

            setSubmitted(true);
        } catch (err) {
            console.error(err);
            setError("We could not send the request. Please try again.");
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div
            className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50 px-4 backdrop-blur-[2px]"
            role="presentation"
            onClick={closeModal}
        >
            <div
                role="dialog"
                aria-modal="true"
                className="w-[min(94vw,560px)] rounded-2xl border border-gray-200 bg-white p-6 shadow-2xl md:p-8"
                onClick={(e) => e.stopPropagation()}
            >
                <div className="flex items-start justify-between gap-4">
                    <div>
                        <p className="text-xs uppercase tracking-[0.25em] text-gray-400">Request Demo</p>
                        <h2 className="mt-2 text-2xl font-semibold text-primary">See VAIA in action</h2>
                    </div>
                    <button
                        type="button"
                        onClick={closeModal}
                        className="flex h-9 w-9 items-center justify-center rounded-lg border border-gray-200 text-gray-500 hover:text-gray-900"
                        aria-label="Close demo request"
                    >
                        <X size={16} />
                    </button>
                </div>

                {submitted ? (
                    <div className="mt-8 rounded-xl border border-emerald-200 bg-emerald-50 p-6 text-center">
                        <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-emerald-100 text-emerald-700">
                            <CheckCircle2 size={24} />
                        </div>
                        <h3 className="text-lg font-semibold text-emerald-900">We'll be in touch within 24 hours.</h3>
                        <button
                            type="button"
                            onClick={closeModal}
                            className="mt-6 rounded-lg bg-primary px-5 py-2.5 text-sm font-semibold text-white hover:bg-primary/90"
                        >
                            Close
                        </button>
                    </div>
                ) : (
                    <form onSubmit={handleSubmit} className="mt-6 grid gap-4">
                        <div className="grid gap-4 md:grid-cols-2">
                            <div>
                                <label className="text-xs uppercase text-gray-500">Company name</label>
                                <input
                                    required
                                    value={form.companyName}
                                    onChange={(e) => updateField("companyName", e.target.value)}
                                    className="mt-1 h-11 w-full rounded-lg border border-gray-200 bg-white px-3 text-sm"
                                />
                            </div>
                            <div>
                                <label className="text-xs uppercase text-gray-500">Contact name</label>
                                <input
                                    required
                                    value={form.contactName}
                                    onChange={(e) => updateField("contactName", e.target.value)}
                                    className="mt-1 h-11 w-full rounded-lg border border-gray-200 bg-white px-3 text-sm"
                                />
                            </div>
                        </div>

                        <div className="grid gap-4 md:grid-cols-2">
                            <div>
                                <label className="text-xs uppercase text-gray-500">Email</label>
                                <input
                                    required
                                    type="email"
                                    value={form.email}
                                    onChange={(e) => updateField("email", e.target.value)}
                                    className="mt-1 h-11 w-full rounded-lg border border-gray-200 bg-white px-3 text-sm"
                                />
                            </div>
                            <div>
                                <label className="text-xs uppercase text-gray-500">Phone number</label>
                                <input
                                    required
                                    value={form.phoneNumber}
                                    onChange={(e) => updateField("phoneNumber", e.target.value)}
                                    className="mt-1 h-11 w-full rounded-lg border border-gray-200 bg-white px-3 text-sm"
                                />
                            </div>
                        </div>

                        <div>
                            <label className="text-xs uppercase text-gray-500">Fleet size</label>
                            <select
                                value={form.fleetSize}
                                onChange={(e) => updateField("fleetSize", e.target.value)}
                                className="mt-1 h-11 w-full rounded-lg border border-gray-200 bg-white px-3 text-sm"
                            >
                                {fleetSizeOptions.map((option) => (
                                    <option key={option} value={option}>
                                        {option}
                                    </option>
                                ))}
                            </select>
                        </div>

                        <div>
                            <label className="text-xs uppercase text-gray-500">Message</label>
                            <textarea
                                value={form.message}
                                onChange={(e) => updateField("message", e.target.value)}
                                className="mt-1 min-h-24 w-full rounded-lg border border-gray-200 bg-white px-3 py-3 text-sm"
                            />
                        </div>

                        {error ? <p className="text-sm text-red-600">{error}</p> : null}

                        <button
                            type="submit"
                            disabled={submitting}
                            className="rounded-lg bg-primary px-4 py-3 text-sm font-semibold text-white hover:bg-[#E65300] disabled:opacity-70"
                        >
                            {submitting ? "Sending..." : "Send demo request"}
                        </button>
                    </form>
                )}
            </div>
        </div>
    );
}
