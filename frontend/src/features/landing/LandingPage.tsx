import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import {
    ArrowRight,
    CheckCircle2,
    Map,
    Truck,
    FileText,
    Users,
    Clock,
    ShieldCheck,
    Activity,
    Layers,
    type LucideIcon
} from "lucide-react";
import DemoRequestModal from "@/features/landing/components/DemoRequestModal";
import FadeInSection from "@/features/landing/components/FadeInSection";
import LoginModal from "@/features/landing/components/LoginModal";
import NavBar from "@/features/landing/components/NavBar";
import { useCountUp } from "@/features/landing/hooks/useCountUp";

type DashboardStat = {
    label: string;
    val: number;
    icon: LucideIcon;
};

type DispatchStatus = "Loaded" | "AtPickup" | "EnrouteDropoff" | "Delivered";
type DispatchDoc = "ATW Pending" | "POD Uploaded" | null;

type DispatchActivityRow = {
    id: string;
    status: DispatchStatus;
    destination: string;
    time: string;
    doc: DispatchDoc;
};

type FeatureItem = {
    title: string;
    desc: string;
    icon: LucideIcon;
    reverse?: boolean;
    mobile?: boolean;
    anchorId?: string;
    mock: "dispatch" | "driver" | "documents" | "portal";
};

const dashboardStats: DashboardStat[] = [
    { label: "Active Trips", val: 24, icon: Truck },
    { label: "Document Alerts", val: 3, icon: FileText },
    { label: "Drivers On Road", val: 18, icon: Users }
];

const dispatchActivityRows: DispatchActivityRow[] = [
    { id: "TCKU3421870", status: "Loaded", destination: "DICT Compound, Tagum", time: "09:14 AM", doc: null },
    { id: "MSCU7823410", status: "AtPickup", destination: "Manila South Harbor", time: "09:32 AM", doc: "ATW Pending" },
    { id: "EISU4521983", status: "EnrouteDropoff", destination: "TADECO Dole, Panabo", time: "08:55 AM", doc: null },
    { id: "TGBU9034521", status: "Delivered", destination: "KTC Compound, Davao", time: "07:30 AM", doc: "POD Uploaded" }
];

const productFeatures: FeatureItem[] = [
    {
        title: "Dispatcher Dashboard",
        desc: "Monitor active trips, driver status, and document alerts from a centralized dispatch dashboard.",
        icon: Map,
        mock: "dispatch"
    },
    {
        title: "Driver Mobile Workflow",
        desc: "Drivers execute trips step-by-step using a mobile interface that only shows the next valid action.",
        icon: Truck,
        reverse: true,
        mobile: true,
        mock: "driver"
    },
    {
        title: "Paperless Document Verification",
        desc: "Upload and verify WAYBILL, ATW, and POD documents digitally.",
        icon: ShieldCheck,
        mock: "documents"
    },
    {
        title: "Customer Portal",
        desc: "Clients can submit shipment requests, track deliveries, and download proof of delivery.",
        icon: Users,
        reverse: true,
        anchorId: "customer-portal",
        mock: "portal"
    }
];

const workflowSteps = [
    { title: "Client Request", desc: "Customer submits shipment request", icon: Users },
    { title: "Dispatch", desc: "Dispatcher plans and dispatches trip", icon: Map },
    { title: "Execution", desc: "Driver executes pickup and delivery", icon: Truck },
    { title: "Verification", desc: "Documents uploaded and verified", icon: ShieldCheck },
    { title: "Completion", desc: "Customer downloads proof of delivery", icon: FileText }
];

const platformFeatures = [
    { title: "Smart Dispatch Scheduling", desc: "Prevent driver and truck double-booking.", icon: Clock },
    { title: "Real-Time Fleet Visibility", desc: "Track trip status from dispatch to delivery.", icon: Activity },
    { title: "Paperless Logistics", desc: "Digitize WAYBILL, ATW, and POD documents.", icon: FileText },
    { title: "Customer Shipment Portal", desc: "Provide customers real-time shipment visibility.", icon: Users }
];

const socialProofStats = [
    { value: 3000, suffix: "+", label: "Trips Logged", detail: "From the partner company's 2025 operational data." },
    { value: 38, suffix: "", label: "Weeks of Operations Data", detail: "Real dispatch history across active trucking weeks." },
    { value: 30, suffix: "+", label: "Trucks Managed", detail: "Fleet operations modeled around container trucking work." }
];

const statusBadgeClasses: Record<DispatchStatus, string> = {
    Delivered: "bg-emerald-100 text-emerald-700 border border-emerald-200",
    AtPickup: "bg-blue-100 text-blue-700 border border-blue-200",
    Loaded: "bg-blue-100 text-blue-700 border border-blue-200",
    EnrouteDropoff: "bg-amber-100 text-amber-700 border border-amber-200"
};

const statusDotClasses: Record<DispatchStatus, string> = {
    Delivered: "bg-emerald-400",
    AtPickup: "bg-blue-400",
    Loaded: "bg-blue-400",
    EnrouteDropoff: "bg-amber-400"
};

const docBadgeClasses: Record<Exclude<DispatchDoc, null>, string> = {
    "ATW Pending": "bg-orange-100 text-orange-700 border border-orange-200",
    "POD Uploaded": "bg-emerald-100 text-emerald-700 border border-emerald-200"
};

function usePrefersReducedMotion() {
    const [prefersReducedMotion, setPrefersReducedMotion] = useState(false);

    useEffect(() => {
        if (!("matchMedia" in window)) return;

        const reducedMotionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
        const updatePreference = () => setPrefersReducedMotion(reducedMotionQuery.matches);
        updatePreference();
        reducedMotionQuery.addEventListener("change", updatePreference);
        return () => reducedMotionQuery.removeEventListener("change", updatePreference);
    }, []);

    return prefersReducedMotion;
}

function CountUpValue({ value, suffix = "" }: { value: number; suffix?: string }) {
    const count = useCountUp(value);
    return (
        <>
            {count.toLocaleString()}
            {suffix}
        </>
    );
}

function CountUpOnView({ value, suffix = "" }: { value: number; suffix?: string }) {
    const ref = useRef<HTMLSpanElement | null>(null);
    const [isVisible, setIsVisible] = useState(false);
    const count = useCountUp(value, 1200, isVisible);

    useEffect(() => {
        const node = ref.current;
        if (!node) return;

        if (!("IntersectionObserver" in window)) {
            setIsVisible(true);
            return;
        }

        const observer = new IntersectionObserver(
            ([entry]) => {
                if (entry.isIntersecting) {
                    setIsVisible(true);
                    observer.disconnect();
                }
            },
            { threshold: 0.15 }
        );

        observer.observe(node);
        return () => observer.disconnect();
    }, []);

    return (
        <span ref={ref}>
            {count.toLocaleString()}
            {suffix}
        </span>
    );
}

function FeatureMock({ feature }: { feature: FeatureItem }) {
    const dispatchRows = [
        { container: "TCKU3421870", driver: "Mario Dela Cruz", status: "Loaded" as DispatchStatus },
        { container: "MSCU7823410", driver: "Ana Reyes", status: "AtPickup" as DispatchStatus },
        { container: "EISU4521983", driver: "Jun Santos", status: "EnrouteDropoff" as DispatchStatus }
    ];

    if (feature.mock === "driver") {
        return (
            <div className="flex h-full w-full flex-col rounded-2xl bg-white p-4 text-left shadow-sm">
                <div className="rounded-2xl bg-primary p-4 text-white">
                    <div className="flex items-center justify-between">
                        <p className="text-xs text-white/70">Assigned Trip</p>
                        <span className="rounded-full bg-white/10 px-2.5 py-1 text-[10px] font-bold text-accent">Loaded</span>
                    </div>
                    <h4 className="mt-3 font-mono text-xl font-bold">MSCU7823410</h4>
                    <div className="mt-4 space-y-3 text-sm">
                        <div>
                            <p className="text-xs uppercase text-white/50">Pickup</p>
                            <p className="font-medium text-white">Manila South Harbor</p>
                        </div>
                        <div>
                            <p className="text-xs uppercase text-white/50">Dropoff</p>
                            <p className="font-medium text-white">Calamba Logistics Hub</p>
                        </div>
                    </div>
                </div>
                <div className="mt-4 flex-1 space-y-3">
                    {[
                        { label: "Gate pass checked", done: true },
                        { label: "Container sealed", done: true },
                        { label: "Mark cargo loaded", done: false }
                    ].map((item) => (
                        <div key={item.label} className="flex items-center gap-3 rounded-xl border border-gray-100 bg-gray-50 p-3">
                            <div className={`flex h-7 w-7 items-center justify-center rounded-full ${item.done ? "bg-accent text-white" : "bg-white text-gray-400"} text-xs font-bold`}>
                                {item.done ? <CheckCircle2 size={14} /> : "3"}
                            </div>
                            <span className="text-sm font-medium text-gray-700">{item.label}</span>
                        </div>
                    ))}
                </div>
                <button type="button" className="mt-5 w-full rounded-xl bg-accent py-3 text-sm font-semibold text-white shadow-lg shadow-accent/20">
                    Mark as Loaded
                </button>
            </div>
        );
    }

    if (feature.mock === "documents") {
        return (
            <div className="w-full space-y-4 text-left">
                <div className="rounded-2xl border border-emerald-200 bg-white p-5 shadow-sm">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-xs uppercase tracking-wider text-gray-400">ATW</p>
                            <h4 className="mt-1 font-mono text-lg font-bold text-primary">ATW-2026-0418</h4>
                            <p className="mt-1 text-xs text-gray-500">Matched to MSCU7823410</p>
                        </div>
                        <span className="flex items-center gap-1.5 rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-700">
                            <CheckCircle2 size={14} />
                            Verified
                        </span>
                    </div>
                    <div className="mt-5 grid grid-cols-2 gap-3 text-xs">
                        <div className="rounded-xl bg-gray-50 p-3">
                            <p className="text-gray-400">Seal No.</p>
                            <p className="mt-1 font-mono font-bold text-primary">NVG-88421</p>
                        </div>
                        <div className="rounded-xl bg-gray-50 p-3">
                            <p className="text-gray-400">Uploaded By</p>
                            <p className="mt-1 font-semibold text-primary">Driver App</p>
                        </div>
                    </div>
                </div>
                <div className="flex items-center justify-between rounded-2xl border border-gray-100 bg-white p-5 shadow-sm">
                    <div>
                        <p className="text-xs uppercase tracking-wider text-gray-400">POD</p>
                        <h4 className="mt-1 font-mono text-base font-bold text-primary">Proof of Delivery</h4>
                    </div>
                    <span className="rounded-full bg-orange-100 px-3 py-1 text-xs font-semibold text-orange-700">
                        Awaiting Upload
                    </span>
                </div>
            </div>
        );
    }

    if (feature.mock === "portal") {
        return (
            <div className="w-full rounded-2xl bg-white p-6 text-left shadow-sm">
                <div className="flex flex-wrap items-start justify-between gap-4 border-b border-gray-100 pb-5">
                    <div>
                        <p className="text-xs uppercase tracking-wider text-gray-400">Shipment Tracking</p>
                        <h4 className="mt-1 font-mono text-xl font-bold text-primary">TGBU9034521</h4>
                    </div>
                    <span className="rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-700">EnrouteDropoff</span>
                </div>
                <div className="mt-6">
                    <div className="h-2 overflow-hidden rounded-full bg-gray-100">
                        <div className="h-full w-3/5 rounded-full bg-accent" />
                    </div>
                    <div className="mt-3 grid grid-cols-5 gap-2 text-[10px] font-semibold text-gray-400">
                        {["Submitted", "Dispatched", "Loaded", "Enroute", "Delivered"].map((step, idx) => (
                            <span key={step} className={idx < 4 ? "text-primary" : ""}>
                                {step}
                            </span>
                        ))}
                    </div>
                </div>
                <div className="mt-6 grid grid-cols-2 gap-3 text-sm">
                    <div className="rounded-xl bg-gray-50 p-4">
                        <p className="text-xs uppercase text-gray-400">Destination</p>
                        <p className="mt-1 font-semibold text-primary">KTC Compound, Davao</p>
                    </div>
                    <div className="rounded-xl bg-gray-50 p-4">
                        <p className="text-xs uppercase text-gray-400">ETA</p>
                        <p className="mt-1 font-mono font-bold text-primary">11:45 AM</p>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="w-full overflow-hidden rounded-2xl border border-gray-100 bg-white text-left shadow-sm">
            <div className="grid grid-cols-[1.2fr_1fr_1fr_auto] gap-3 border-b border-gray-100 bg-gray-50 px-4 py-3 text-[11px] font-bold uppercase tracking-wider text-gray-400">
                <span>Container</span>
                <span>Driver</span>
                <span>Status</span>
                <span />
            </div>
            <div className="divide-y divide-gray-100">
                {dispatchRows.map((row) => (
                    <div key={row.container} className="grid grid-cols-[1.2fr_1fr_1fr_auto] items-center gap-3 px-4 py-4">
                        <div>
                            <p className="font-mono text-sm font-bold text-primary">{row.container}</p>
                            <p className="mt-1 text-xs text-gray-500">Container movement</p>
                        </div>
                        <p className="text-sm font-medium text-gray-700">{row.driver}</p>
                        <span className={`w-fit rounded-full px-2.5 py-1 text-[10px] font-bold ${statusBadgeClasses[row.status]}`}>
                            {row.status}
                        </span>
                        <button type="button" className="rounded-lg bg-accent px-3 py-2 text-xs font-semibold text-white shadow-sm shadow-accent/20">
                            Dispatch
                        </button>
                    </div>
                ))}
            </div>
        </div>
    );
}

export default function LandingPage() {
    const [demoOpen, setDemoOpen] = useState(false);
    const workflowRef = useRef<HTMLElement | null>(null);
    const [workflowLineVisible, setWorkflowLineVisible] = useState(false);
    const prefersReducedMotion = usePrefersReducedMotion();
    const openDemo = () => setDemoOpen(true);
    const closeDemo = () => setDemoOpen(false);

    useEffect(() => {
        const node = workflowRef.current;
        if (!node) return;

        if (prefersReducedMotion) {
            setWorkflowLineVisible(true);
            return;
        }

        if (!("IntersectionObserver" in window)) {
            setWorkflowLineVisible(true);
            return;
        }

        const observer = new IntersectionObserver(
            ([entry]) => {
                if (entry.isIntersecting) {
                    setWorkflowLineVisible(true);
                    observer.disconnect();
                }
            },
            { threshold: 0.15 }
        );

        observer.observe(node);
        return () => observer.disconnect();
    }, [prefersReducedMotion]);

    return (
        <div className="min-h-screen bg-background text-foreground font-sans selection:bg-primary selection:text-white">
            <NavBar onDemoClick={openDemo} />

            {/* 2. Hero Section */}
            <section className="relative overflow-hidden px-6 pb-20 pt-32 md:pb-32 md:pt-48">
                <div className="absolute right-0 top-0 h-[50vw] w-[50vw] -translate-y-1/2 translate-x-1/3 rounded-full bg-primary/5 blur-3xl pointer-events-none" />

                <div className="mx-auto grid max-w-7xl grid-cols-1 items-center gap-12 lg:grid-cols-2">
                    <FadeInSection className="relative z-10 max-w-2xl">
                        <div className="mb-6 inline-flex items-center gap-2 rounded-full bg-primary/10 px-3 py-1 text-xs font-semibold uppercase tracking-wider text-primary">
                            <span className="h-2 w-2 rounded-full bg-accent animate-pulse" />
                            NVG Logistics Platform
                        </div>
                        <h1 className="mb-6 text-5xl font-extrabold leading-[1.1] text-primary md:text-6xl lg:text-7xl">
                            Run Your Container Dispatch Without Paper
                        </h1>
                        <p className="mb-3 max-w-xl text-lg leading-relaxed text-gray-600 md:text-xl">
                            Plan trips, track drivers in real time, and manage WAYBILL, ATW, and POD documents in one platform.
                        </p>
                        <p className="mb-10 max-w-xl text-base font-medium text-primary">
                            Built for drayage and port container operators in Mindanao.
                        </p>
                        <div className="flex flex-col gap-4 sm:flex-row">
                            <button
                                type="button"
                                onClick={openDemo}
                                className="flex items-center justify-center gap-2 rounded-xl bg-accent px-8 py-4 font-medium text-white shadow-lg shadow-accent/20 transition-all hover:-translate-y-0.5 hover:bg-accent/90 hover:shadow-xl"
                            >
                                Request Demo
                                <ArrowRight size={18} />
                            </button>
                            <Link
                                to="/login"
                                className="rounded-xl border border-gray-200 bg-white px-8 py-4 text-center font-medium text-primary shadow-sm transition-all hover:-translate-y-0.5 hover:border-gray-300 hover:bg-gray-50"
                            >
                                Login
                            </Link>
                        </div>
                    </FadeInSection>

                    <FadeInSection delay={150} className="relative z-10 w-full overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-2xl shadow-primary/10 transform lg:translate-x-8 lg:scale-105">
                        <div className="absolute inset-0 bg-gradient-to-t from-primary/5 to-transparent pointer-events-none" />
                        <div className="flex items-center gap-2 border-b border-gray-100 bg-gray-50 px-4 py-3">
                            <div className="h-3 w-3 rounded-full bg-red-400" />
                            <div className="h-3 w-3 rounded-full bg-amber-400" />
                            <div className="h-3 w-3 rounded-full bg-green-400" />
                            <div className="ml-4 flex-1 truncate rounded bg-white px-3 py-1 text-center font-mono text-xs text-gray-400 shadow-sm">
                                app.nvgdispatch.com/dashboard
                            </div>
                        </div>
                        <div className="flex h-[430px] flex-col gap-4 bg-gray-50 p-4 md:h-[500px] md:p-6">
                            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3 md:gap-4">
                                {dashboardStats.map((stat) => (
                                    <div key={stat.label} className="flex flex-col rounded-xl border border-gray-100 bg-white p-4 shadow-sm">
                                        <div className="mb-2 flex items-center justify-between">
                                            <span className="text-xs font-medium text-gray-500">{stat.label}</span>
                                            <stat.icon size={14} className="text-primary" />
                                        </div>
                                        <span className="font-mono text-2xl font-bold text-primary">
                                            <CountUpValue value={stat.val} />
                                        </span>
                                    </div>
                                ))}
                            </div>
                            <div className="flex flex-1 flex-col overflow-hidden rounded-xl border border-gray-100 bg-white shadow-sm">
                                <div className="flex items-center justify-between border-b border-gray-50 bg-primary px-4 py-3 text-sm font-medium text-white">
                                    Live Dispatch Activity
                                    <span className="flex items-center gap-1 rounded-full bg-green-500/20 px-2 py-0.5 text-xs text-green-300">
                                        <span className="h-1.5 w-1.5 rounded-full bg-green-400 animate-pulse" />
                                        Live
                                    </span>
                                </div>
                                <div className="flex-1 space-y-3 overflow-y-auto p-4 font-mono text-xs">
                                    {dispatchActivityRows.map((row) => (
                                        <div key={row.id} className="flex items-center justify-between gap-4 rounded-lg border border-gray-100 p-3 transition-colors hover:bg-gray-50">
                                            <div className="flex min-w-0 items-center gap-3">
                                                <div className={`h-2 w-2 flex-shrink-0 rounded-full ${statusDotClasses[row.status]}`} />
                                                <div className="min-w-0">
                                                    <div className="flex flex-wrap items-center gap-2">
                                                        <p className="font-semibold text-primary text-sm">{row.id}</p>
                                                        <span className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${statusBadgeClasses[row.status]}`}>
                                                            {row.status}
                                                        </span>
                                                    </div>
                                                    <p className="mt-0.5 truncate text-gray-500">{row.destination}</p>
                                                </div>
                                            </div>
                                            <div className="flex flex-shrink-0 flex-col items-end gap-1 text-right">
                                                <span className="block text-gray-400">{row.time}</span>
                                                {row.doc ? (
                                                    <span className={`block rounded-full px-2 py-0.5 text-[10px] font-bold uppercase ${docBadgeClasses[row.doc]}`}>
                                                        {row.doc}
                                                    </span>
                                                ) : null}
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        </div>
                    </FadeInSection>
                </div>
            </section>

            {/* Social Proof */}
            <section className="border-y border-gray-100 bg-white py-20">
                <div className="mx-auto max-w-7xl px-6">
                    <FadeInSection className="mx-auto max-w-3xl text-center">
                        <h2 className="text-3xl font-bold text-primary md:text-5xl">Trusted for Real Trucking Operations</h2>
                        <p className="mt-4 text-sm text-gray-500">
                            These numbers come from the partner company's 2025 operational data.
                        </p>
                    </FadeInSection>

                    <div className="mt-12 grid grid-cols-1 gap-6 md:grid-cols-3">
                        {socialProofStats.map((stat, idx) => (
                            <FadeInSection key={stat.label} delay={idx * 80} className="rounded-xl border border-gray-100 bg-background p-6 text-center">
                                <p className="font-mono text-3xl font-bold text-primary">
                                    <CountUpOnView value={stat.value} suffix={stat.suffix} />
                                </p>
                                <h3 className="mt-2 font-semibold text-gray-900">{stat.label}</h3>
                                <p className="mt-2 text-sm leading-relaxed text-gray-600">{stat.detail}</p>
                            </FadeInSection>
                        ))}
                    </div>

                    <FadeInSection delay={240} className="mx-auto mt-10 max-w-4xl rounded-2xl border border-gray-100 bg-background p-8 shadow-sm">
                        <blockquote className="text-xl font-medium leading-relaxed text-primary">
                            "Before NVG Dispatch, we were coordinating everything through WhatsApp and spreadsheets. Now the whole dispatch lifecycle is in one place."
                        </blockquote>
                        <p className="mt-5 text-sm font-semibold text-gray-700">
                            Operations Manager, Container Trucking Company &mdash; Panabo, Davao
                        </p>
                        <p className="mt-1 text-xs text-gray-500">Anonymized until company approval.</p>
                    </FadeInSection>
                </div>
            </section>

            {/* 3. Product Screens Section */}
            <section id="platform" className="relative bg-white py-24">
                <div className="mx-auto max-w-7xl px-6">
                    <FadeInSection className="mx-auto mb-16 max-w-2xl text-center">
                        <h2 className="mb-6 text-3xl font-bold text-primary md:text-5xl">Platform Overview</h2>
                        <p className="text-lg text-gray-600">Centralize operations, verify documents, and dispatch smartly from one ecosystem.</p>
                    </FadeInSection>

                    <div className="space-y-32">
                        {productFeatures.map((feature, idx) => (
                            <FadeInSection
                                key={feature.title}
                                delay={idx * 80}
                                direction={feature.reverse ? "right" : "left"}
                                className={`flex flex-col ${feature.reverse ? "md:flex-row-reverse" : "md:flex-row"} items-center gap-12 md:gap-24`}
                            >
                                <div id={feature.anchorId} className="flex-1 space-y-6">
                                    <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-background text-accent">
                                        <feature.icon size={32} />
                                    </div>
                                    <h3 className="text-3xl font-bold text-primary">{feature.title}</h3>
                                    <p className="text-lg leading-relaxed text-gray-600">{feature.desc}</p>
                                </div>
                                <div className={`w-full flex-1 ${feature.mobile ? "flex justify-center md:w-1/2" : ""}`}>
                                    <div
                                        className={`relative flex items-center justify-center overflow-hidden border border-gray-200 bg-white shadow-xl shadow-primary/5 group ${
                                            feature.mobile ? "aspect-[9/19] w-full max-w-[320px] rounded-[2rem] border-8 border-gray-900 bg-gray-50 p-4 md:rounded-[2.5rem]" : "aspect-[16/10] rounded-2xl p-4"
                                        }`}
                                    >
                                        <div className="flex h-full w-full flex-col items-center justify-center rounded-xl border border-gray-100 bg-gray-50 p-6 text-center">
                                            <FeatureMock feature={feature} />
                                        </div>
                                    </div>
                                </div>
                            </FadeInSection>
                        ))}
                    </div>
                </div>
            </section>

            {/* 4. Workflow Section */}
            <section id="workflow" ref={workflowRef} className="bg-primary py-24 text-white">
                <div className="mx-auto max-w-7xl px-6">
                    <FadeInSection className="mb-16 text-center">
                        <h2 className="mb-4 text-3xl font-bold md:text-5xl">How NVG Dispatch Works</h2>
                        <p className="text-lg text-muted-foreground">End-to-end logistics operations for container trucking companies.</p>
                    </FadeInSection>

                    <div className="relative">
                        <div
                            className={`absolute left-0 top-1/2 hidden h-0.5 w-full origin-left -translate-y-1/2 bg-gradient-to-r from-primary via-accent/50 to-primary md:block ${
                                prefersReducedMotion ? "" : "transition-transform duration-[800ms] ease-out"
                            } ${
                                workflowLineVisible ? "scale-x-100" : "scale-x-0"
                            }`}
                        />

                        <div className="grid grid-cols-1 gap-8 md:grid-cols-5">
                            {workflowSteps.map((step, idx) => (
                                <FadeInSection key={step.title} delay={idx * 100} className="relative z-10 flex flex-col items-center text-center group">
                                    <div className="mb-6 flex h-16 w-16 items-center justify-center rounded-2xl border border-white/10 bg-slate-900 text-accent shadow-lg transition-all duration-300 group-hover:scale-110 group-hover:border-accent group-hover:bg-accent group-hover:text-white">
                                        <step.icon size={28} />
                                    </div>
                                    <h4 className="mb-2 font-bold text-white">{step.title}</h4>
                                    <p className="max-w-[180px] text-sm leading-snug text-muted-foreground">{step.desc}</p>
                                </FadeInSection>
                            ))}
                        </div>
                    </div>
                </div>
            </section>

            {/* 5. Platform Features */}
            <section className="bg-background py-24">
                <div className="mx-auto max-w-7xl px-6">
                    <div className="grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-4">
                        {platformFeatures.map((feature, idx) => (
                            <FadeInSection
                                key={feature.title}
                                delay={idx * 80}
                                className="rounded-2xl border border-gray-200 bg-white p-8 transition-all duration-300 hover:-translate-y-1 hover:border-primary/20 hover:shadow-xl"
                            >
                                <div className="mb-6 flex h-12 w-12 items-center justify-center rounded-xl bg-background text-primary">
                                    <feature.icon size={24} />
                                </div>
                                <h3 className="mb-3 text-xl font-bold leading-tight text-primary">{feature.title}</h3>
                                <p className="text-sm leading-relaxed text-gray-600">{feature.desc}</p>
                            </FadeInSection>
                        ))}
                    </div>
                </div>
            </section>

            {/* 6. Operations Credibility Section */}
            <section className="border-y border-gray-100 bg-white py-24">
                <FadeInSection className="mx-auto max-w-4xl px-6 text-center">
                    <div className="mx-auto mb-8 flex h-16 w-16 items-center justify-center rounded-2xl bg-primary/5 text-primary">
                        <Layers size={32} />
                    </div>
                    <h2 className="mb-12 text-3xl font-bold text-primary md:text-5xl">Built for Real Dispatch Operations</h2>

                    <div className="rounded-2xl border border-gray-100 bg-background p-8 text-left shadow-sm md:p-12">
                        <ul className="grid grid-cols-1 gap-6 md:grid-cols-2">
                            {[
                                "Prevent driver and truck scheduling conflicts",
                                "Enforce dispatch lifecycle rules",
                                "Paperless WAYBILL / ATW / POD workflows",
                                "Customer shipment request portal",
                                "Full trip history and audit logging",
                                "Post-delivery heuristic recommendations",
                                "Versioned document upload and verification"
                            ].map((point, idx) => (
                                <li key={point}>
                                    <FadeInSection delay={idx * 60} className="flex items-start gap-4">
                                        <div className="mt-1 flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-full bg-accent/10 text-accent">
                                            <CheckCircle2 size={16} />
                                        </div>
                                        <span className="font-medium leading-relaxed text-gray-800">{point}</span>
                                    </FadeInSection>
                                </li>
                            ))}
                        </ul>
                    </div>
                </FadeInSection>
            </section>

            {/* 7. Call To Action */}
            <section className="relative overflow-hidden bg-[#eef1f6] py-32">
                <div className="absolute inset-0 bg-accent/[0.02]" />
                <FadeInSection direction="up" className="relative z-10 mx-auto max-w-4xl px-6 text-center">
                    <h2 className="mb-8 text-4xl font-extrabold text-primary md:text-6xl">
                        Ready to Replace Your WhatsApp Dispatch Board?
                    </h2>
                    <div className="flex flex-col justify-center gap-4 sm:flex-row">
                        <button
                            type="button"
                            onClick={openDemo}
                            className="rounded-xl bg-accent px-8 py-4 text-lg font-medium text-white shadow-lg shadow-accent/20 transition-all hover:scale-105 hover:bg-accent/90 active:scale-95"
                        >
                            Request Demo
                        </button>
                        <Link
                            to="/login"
                            className="rounded-xl border border-gray-200 bg-white px-8 py-4 text-center text-lg font-medium text-primary shadow-sm transition-all hover:scale-105 hover:bg-gray-50 active:scale-95"
                        >
                            Login
                        </Link>
                    </div>
                </FadeInSection>
            </section>

            {/* 8. Footer */}
            <footer className="mt-auto bg-slate-900 px-6 pb-10 pt-20 text-white">
                <div className="mx-auto max-w-7xl">
                    <div className="mb-16 grid grid-cols-1 gap-12 md:grid-cols-5">
                        <div className="col-span-1 md:col-span-2">
                            <div className="mb-6 flex items-center gap-2">
                                <div className="flex h-8 w-8 items-center justify-center rounded bg-white/10 font-bold text-white">
                                    N
                                </div>
                                <span className="text-xl font-bold">NVG Dispatch</span>
                            </div>
                            <p className="mb-8 max-w-sm text-sm text-muted-foreground">
                                Paperless container dispatch for Mindanao trucking companies.
                            </p>
                            <div className="inline-flex items-center gap-2 rounded-lg border border-white/10 bg-white/5 px-4 py-2 font-mono text-xs text-green-400">
                                <span className="h-2 w-2 rounded-full bg-green-400 animate-pulse" />
                                System Operational
                            </div>
                        </div>

                        <div>
                            <h4 className="mb-6 text-xs font-bold uppercase tracking-wider text-white">Platform</h4>
                            <ul className="space-y-4 text-sm font-medium text-muted-foreground">
                                <li><a href="#platform" className="hover:text-white transition-colors">Features</a></li>
                                <li><a href="#workflow" className="hover:text-white transition-colors">Workflow</a></li>
                                <li><a href="#platform" className="hover:text-white transition-colors">Integrations</a></li>
                            </ul>
                        </div>

                        <div>
                            <h4 className="mb-6 text-xs font-bold uppercase tracking-wider text-white">Customer Portal</h4>
                            <ul className="space-y-4 text-sm font-medium text-muted-foreground">
                                <li><Link to="/login" className="hover:text-white transition-colors">Submit Request</Link></li>
                                <li><Link to="/login" className="hover:text-white transition-colors">Track Shipment</Link></li>
                                <li><Link to="/login" className="hover:text-white transition-colors">Download Documents</Link></li>
                            </ul>
                        </div>

                        <div>
                            <h4 className="mb-6 text-xs font-bold uppercase tracking-wider text-white">Support</h4>
                            <ul className="space-y-4 text-sm font-medium text-muted-foreground">
                                <li><a href="#platform" className="hover:text-white transition-colors">Help Center</a></li>
                                <li><a href="#workflow" className="hover:text-white transition-colors">Documentation</a></li>
                                <li><Link to="/login" className="text-accent hover:text-white transition-colors">Login</Link></li>
                            </ul>
                        </div>
                    </div>

                    <div className="flex flex-col items-center justify-between gap-4 border-t border-white/10 pt-8 text-xs text-muted-foreground md:flex-row">
                        <p>&copy; {new Date().getFullYear()} NVG Dispatch. All rights reserved.</p>
                        <div className="flex gap-6">
                            <a href="#platform" className="hover:text-white transition-colors">Privacy Policy</a>
                            <a href="#workflow" className="hover:text-white transition-colors">Terms of Service</a>
                        </div>
                    </div>
                </div>
            </footer>

            <DemoRequestModal open={demoOpen} onClose={closeDemo} />
            <LoginModal />
        </div>
    );
}
