import React, { useEffect, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import {
    ArrowRight,
    Menu,
    X,
    CheckCircle2,
    Map,
    Truck,
    FileText,
    Users,
    Clock,
    ShieldCheck,
    ChevronRight,
    Activity,
    Layers
} from "lucide-react";
import { login, getDefaultRoute } from "@/features/auth/authStore";
import { useToast } from "@/lib/useToast";
import ToastHost from "@/components/ToastHost";

type LandingPageProps = {
    initialLoginOpen?: boolean;
};

export default function LandingPage({ initialLoginOpen = false }: LandingPageProps) {
    const nav = useNavigate();
    const location = useLocation();
    const [isScrolled, setIsScrolled] = useState(false);
    const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
    const [loginOpen, setLoginOpen] = useState(initialLoginOpen);
    const [username, setUsername] = useState("");
    const [password, setPassword] = useState("");
    const [submitting, setSubmitting] = useState(false);
    const { toasts, show } = useToast();

    useEffect(() => {
        const handleScroll = () => {
            setIsScrolled(window.scrollY > 20);
        };
        window.addEventListener("scroll", handleScroll);
        return () => window.removeEventListener("scroll", handleScroll);
    }, []);

    useEffect(() => {
        setLoginOpen(location.pathname === "/login");
    }, [location.pathname]);

    const closeLogin = () => {
        setLoginOpen(false);
        if (location.pathname === "/login") {
            nav("/", { replace: true });
        }
    };

    const handleLogin = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            setSubmitting(true);
            const me = await login({ username, password });
            nav(getDefaultRoute(me), { replace: true });
        } catch (e: any) {
            console.error(e);
            show(e?.message ?? "Login failed", "error");
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div className="min-h-screen bg-[#F5F7FA] text-[#1A1A1A] font-sans selection:bg-[#1F3A5F] selection:text-white">
            <ToastHost toasts={toasts} />
            {/* 1. Navbar */}
            <nav
                className={`fixed top-4 left-1/2 -translate-x-1/2 w-[95%] max-w-7xl z-50 transition-all duration-300 rounded-full px-6 py-3 flex items-center justify-between ${isScrolled
                        ? "bg-white/80 backdrop-blur-md shadow-lg border border-gray-200/50"
                        : "bg-transparent"
                    }`}
            >
                <div className="flex items-center gap-2">
                    <div className="w-8 h-8 rounded bg-[#1F3A5F] flex items-center justify-center text-white font-bold">
                        N
                    </div>
                    <span className="font-bold text-xl tracking-tight text-[#1F3A5F]">NVG Dispatch</span>
                </div>

                {/* Desktop Nav */}
                <div className="hidden md:flex items-center gap-8 font-medium text-sm">
                    <a href="#platform" className="hover:text-[#E5533D] transition-colors">Platform</a>
                    <a href="#workflow" className="hover:text-[#E5533D] transition-colors">Workflow</a>
                    <a href="#customer-portal" className="hover:text-[#E5533D] transition-colors">Customer Portal</a>
                </div>

                <div className="hidden md:flex items-center gap-4">
                    <Link to="/login" className="text-sm font-medium hover:text-[#E5533D] transition-colors">Login</Link>
                    <Link
                        to="/login"
                        className="bg-[#E5533D] text-white px-5 py-2.5 rounded-full text-sm font-medium hover:bg-[#d44834] transition-all hover:scale-105 active:scale-95 shadow-md shadow-[#E5533D]/20"
                    >
                        Request Demo
                    </Link>
                </div>

                {/* Mobile Menu Toggle */}
                <button
                    className="md:hidden text-[#1F3A5F]"
                    onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
                >
                    {mobileMenuOpen ? <X size={24} /> : <Menu size={24} />}
                </button>
            </nav>

            {/* Mobile Menu Content */}
            {mobileMenuOpen && (
                <div className="fixed inset-0 bg-white z-40 pt-24 px-6 md:hidden flex flex-col gap-6">
                    <a href="#platform" className="text-xl font-medium" onClick={() => setMobileMenuOpen(false)}>Platform</a>
                    <a href="#workflow" className="text-xl font-medium" onClick={() => setMobileMenuOpen(false)}>Workflow</a>
                    <a href="#customer-portal" className="text-xl font-medium" onClick={() => setMobileMenuOpen(false)}>Customer Portal</a>
                    <hr className="border-gray-100" />
                    <Link to="/login" className="text-left text-xl font-medium">Login</Link>
                    <Link
                        to="/login"
                        className="bg-[#E5533D] text-white px-6 py-3 rounded-xl text-center font-medium mt-4"
                    >
                        Request Demo
                    </Link>
                </div>
            )}

            {/* 2. Hero Section */}
            <section className="relative pt-32 pb-20 md:pt-48 md:pb-32 px-6 overflow-hidden">
                {/* Background decorative elements */}
                <div className="absolute top-0 right-0 w-[50vw] h-[50vw] bg-[#1F3A5F]/5 rounded-full blur-3xl -translate-y-1/2 translate-x-1/3 pointer-events-none" />

                <div className="max-w-7xl mx-auto grid grid-cols-1 lg:grid-cols-2 gap-12 items-center">
                    <div className="max-w-2xl relative z-10">
                        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-[#1F3A5F]/10 text-[#1F3A5F] text-xs font-semibold mb-6 uppercase tracking-wider">
                            <span className="w-2 h-2 rounded-full bg-[#E5533D] animate-pulse" />
                            NVG Logistics Platform
                        </div>
                        <h1 className="text-5xl md:text-6xl lg:text-7xl font-extrabold text-[#1F3A5F] leading-[1.1] mb-6 tracking-tight">
                            Run Your Container Dispatch Without Paper
                        </h1>
                        <p className="text-lg md:text-xl text-gray-600 mb-10 leading-relaxed max-w-xl">
                            Plan trips, track drivers in real time, and manage WAYBILL, ATW, and POD documents in one platform.
                        </p>
                        <div className="flex flex-col sm:flex-row gap-4">
                            <Link
                                to="/login"
                                className="bg-[#E5533D] text-white px-8 py-4 rounded-xl font-medium flex items-center justify-center gap-2 hover:bg-[#d44834] hover:-translate-y-0.5 transition-all shadow-lg hover:shadow-xl shadow-[#E5533D]/20"
                            >
                                Request Demo
                                <ArrowRight size={18} />
                            </Link>
                            <Link
                                to="/login"
                                className="bg-white text-[#1F3A5F] border border-gray-200 px-8 py-4 rounded-xl font-medium hover:bg-gray-50 transition-all hover:border-gray-300 hover:-translate-y-0.5 shadow-sm text-center"
                            >
                                Login
                            </Link>
                        </div>
                    </div>

                    {/* Hero Image/Mockup */}
                    <div className="relative z-10 w-full rounded-2xl bg-white border border-gray-200 shadow-2xl shadow-[#1F3A5F]/10 overflow-hidden transform lg:translate-x-8 lg:scale-105">
                        <div className="absolute inset-0 bg-gradient-to-t from-[#1F3A5F]/5 to-transparent pointer-events-none" />
                        <div className="flex items-center gap-2 px-4 py-3 border-b border-gray-100 bg-gray-50">
                            <div className="w-3 h-3 rounded-full bg-red-400" />
                            <div className="w-3 h-3 rounded-full bg-amber-400" />
                            <div className="w-3 h-3 rounded-full bg-green-400" />
                            <div className="ml-4 bg-white px-3 py-1 rounded text-xs text-gray-400 font-mono shadow-sm flex-1 text-center truncate">
                                app.nvgdispatch.com/dashboard
                            </div>
                        </div>
                        {/* Mock Dashboard UI */}
                        <div className="p-6 bg-gray-50 h-[400px] md:h-[500px] flex flex-col gap-4">
                            <div className="grid grid-cols-3 gap-4">
                                {[
                                    { label: "Active Trips", val: "24", icon: Truck },
                                    { label: "Document Alerts", val: "12", icon: FileText },
                                    { label: "Drivers Ready", val: "18", icon: Users }
                                ].map((stat, i) => (
                                    <div key={i} className="bg-white p-4 rounded-xl border border-gray-100 shadow-sm flex flex-col">
                                        <div className="flex items-center justify-between mb-2">
                                            <span className="text-gray-500 text-xs font-medium">{stat.label}</span>
                                            <stat.icon size={14} className="text-[#1F3A5F]" />
                                        </div>
                                        <span className="text-2xl font-bold font-mono text-[#1F3A5F]">{stat.val}</span>
                                    </div>
                                ))}
                            </div>
                            <div className="flex-1 bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden flex flex-col">
                                <div className="px-4 py-3 border-b border-gray-50 font-medium text-sm flex justify-between items-center bg-[#1F3A5F] text-white">
                                    Live Dispatch Activity
                                    <span className="text-xs bg-green-500/20 text-green-300 px-2 py-0.5 rounded-full flex items-center gap-1">
                                        <span className="w-1.5 h-1.5 rounded-full bg-green-400 animate-pulse" />
                                        Live
                                    </span>
                                </div>
                                <div className="p-4 flex-1 space-y-3 font-mono text-xs overflow-y-auto">
                                    {[1, 2, 3, 4].map(i => (
                                        <div key={i} className="flex items-center justify-between p-3 hover:bg-gray-50 rounded-lg border border-gray-100 transition-colors">
                                            <div className="flex items-center gap-3">
                                                <div className={`w-2 h-2 rounded-full ${i === 2 ? 'bg-amber-400' : 'bg-green-400'}`} />
                                                <div>
                                                    <p className="text-[#1F3A5F] font-semibold text-sm">TRK-00{i}</p>
                                                    <p className="text-gray-500 mt-0.5">En route to Port Terminal {i}</p>
                                                </div>
                                            </div>
                                            <div className="text-right">
                                                <span className="text-gray-400 block">10:{i}4 AM</span>
                                                {i === 2 && <span className="text-amber-500 text-[10px] uppercase font-bold tracking-wider mt-1 block">ATW Pending</span>}
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </section>

            {/* 3. Product Screens Section */}
            <section id="platform" className="py-24 bg-white relative">
                <div className="max-w-7xl mx-auto px-6">
                    <div className="text-center mb-16 max-w-2xl mx-auto">
                        <h2 className="text-3xl md:text-5xl font-bold text-[#1F3A5F] mb-6">Platform Overview</h2>
                        <p className="text-gray-600 text-lg">Centralize operations, verify documents, and dispatch smartly from one ecosystem.</p>
                    </div>

                    <div className="space-y-32">
                        {[
                            {
                                title: "Dispatcher Dashboard",
                                desc: "Monitor active trips, driver status, and document alerts from a centralized dispatch dashboard.",
                                icon: Map,
                                imgMock: "Dispatch trips table with status indicators."
                            },
                            {
                                title: "Driver Mobile Workflow",
                                desc: "Drivers execute trips step-by-step using a mobile interface that only shows the next valid action.",
                                icon: Truck,
                                reverse: true,
                                mobile: true,
                                imgMock: "Driver trip detail screen with the Next Action button."
                            },
                            {
                                title: "Paperless Document Verification",
                                desc: "Upload and verify WAYBILL, ATW, and POD documents digitally.",
                                icon: ShieldCheck,
                                imgMock: "Document verification page."
                            },
                            {
                                title: "Customer Portal",
                                desc: "Clients can submit shipment requests, track deliveries, and download proof of delivery.",
                                icon: Users,
                                reverse: true,
                                anchorId: "customer-portal",
                                imgMock: "Customer portal shipment tracking page."
                            }
                        ].map((feature, idx) => (
                            <div
                                key={idx}
                                id={feature.anchorId}
                                className={`flex flex-col ${feature.reverse ? 'md:flex-row-reverse' : 'md:flex-row'} items-center gap-12 md:gap-24`}
                            >
                                <div className="flex-1 space-y-6">
                                    <div className="w-16 h-16 bg-[#F5F7FA] rounded-2xl flex items-center justify-center text-[#E5533D]">
                                        <feature.icon size={32} />
                                    </div>
                                    <h3 className="text-3xl font-bold text-[#1F3A5F]">{feature.title}</h3>
                                    <p className="text-lg text-gray-600 leading-relaxed">{feature.desc}</p>
                                </div>
                                <div className={`flex-1 w-full ${feature.mobile ? 'md:w-1/2 flex justify-center' : ''}`}>
                                    <div className={`bg-white border border-gray-200 shadow-xl shadow-[#1F3A5F]/5 overflow-hidden flex items-center justify-center relative group
                    ${feature.mobile ? 'rounded-[2rem] md:rounded-[2.5rem] w-full max-w-[320px] aspect-[9/19] p-4 border-8 border-gray-900 bg-gray-50' : 'rounded-2xl aspect-[16/10] p-2'}
                  `}>
                                        <div className="w-full h-full bg-gray-50 rounded-xl border border-gray-100 flex flex-col items-center justify-center p-8 text-center">
                                            <div className="w-16 h-16 mx-auto bg-white rounded-full flex items-center justify-center mb-4 text-gray-400 shadow-sm">
                                                <feature.icon size={32} />
                                            </div>
                                            <p className="font-mono text-sm text-gray-500">{feature.imgMock}</p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            </section>

            {/* 4. Workflow Section */}
            <section id="workflow" className="py-24 bg-[#1F3A5F] text-white">
                <div className="max-w-7xl mx-auto px-6">
                    <div className="text-center mb-16">
                        <h2 className="text-3xl md:text-5xl font-bold mb-4">How NVG Dispatch Works</h2>
                        <p className="text-[#a1b3c7] text-lg">5-step operational workflow for end-to- natural logistics.</p>
                    </div>

                    <div className="relative">
                        <div className="hidden md:block absolute top-1/2 left-0 w-full h-0.5 bg-gradient-to-r from-[#1F3A5F] via-[#E5533D]/50 to-[#1F3A5F] -translate-y-1/2" />

                        <div className="grid grid-cols-1 md:grid-cols-5 gap-8">
                            {[
                                { title: "Client Request", desc: "Customer submits shipment request", icon: Users },
                                { title: "Dispatch", desc: "Dispatcher plans and dispatches trip", icon: Map },
                                { title: "Execution", desc: "Driver executes pickup and delivery", icon: Truck },
                                { title: "Verification", desc: "Documents uploaded and verified", icon: ShieldCheck },
                                { title: "Completion", desc: "Customer downloads proof of delivery", icon: FileText }
                            ].map((step, idx) => (
                                <div key={idx} className="relative z-10 flex flex-col items-center text-center group">
                                    <div className="w-16 h-16 rounded-2xl bg-[#0f2038] border border-white/10 shadow-lg flex items-center justify-center text-[#E5533D] mb-6 group-hover:scale-110 group-hover:bg-[#E5533D] group-hover:text-white group-hover:border-[#E5533D] transition-all duration-300">
                                        <step.icon size={28} />
                                    </div>
                                    <h4 className="font-bold text-white mb-2">{step.title}</h4>
                                    <p className="text-sm text-[#a1b3c7] max-w-[180px] leading-snug">{step.desc}</p>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>
            </section>

            {/* 5. Platform Features */}
            <section className="py-24 bg-[#F5F7FA]">
                <div className="max-w-7xl mx-auto px-6">
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
                        {[
                            { title: "Smart Dispatch Scheduling", desc: "Prevent driver and truck double-booking.", icon: Clock },
                            { title: "Real-Time Fleet Visibility", desc: "Track trip status from dispatch to delivery.", icon: Activity },
                            { title: "Paperless Logistics", desc: "Digitize WAYBILL, ATW, and POD documents.", icon: FileText },
                            { title: "Customer Shipment Portal", desc: "Provide customers real-time shipment visibility.", icon: Users },
                        ].map((feature, idx) => (
                            <div key={idx} className="bg-white p-8 rounded-2xl border border-gray-200 hover:border-[#1F3A5F]/20 hover:shadow-xl hover:-translate-y-1 transition-all duration-300">
                                <div className="w-12 h-12 bg-[#F5F7FA] rounded-xl flex items-center justify-center text-[#1F3A5F] mb-6">
                                    <feature.icon size={24} />
                                </div>
                                <h3 className="text-xl font-bold text-[#1F3A5F] mb-3 leading-tight">{feature.title}</h3>
                                <p className="text-gray-600 text-sm leading-relaxed">{feature.desc}</p>
                            </div>
                        ))}
                    </div>
                </div>
            </section>

            {/* 6. Operations Credibility Section */}
            <section className="py-24 bg-white border-y border-gray-100">
                <div className="max-w-4xl mx-auto px-6 text-center">
                    <div className="w-16 h-16 bg-[#1F3A5F]/5 rounded-2xl flex items-center justify-center text-[#1F3A5F] mx-auto mb-8">
                        <Layers size={32} />
                    </div>
                    <h2 className="text-3xl md:text-5xl font-bold text-[#1F3A5F] mb-12">Built for Real Dispatch Operations</h2>

                    <div className="bg-[#F5F7FA] rounded-2xl p-8 md:p-12 text-left border border-gray-100 shadow-sm">
                        <ul className="grid grid-cols-1 md:grid-cols-2 gap-6">
                            {[
                                "Prevent driver and truck scheduling conflicts",
                                "Enforce dispatch lifecycle rules",
                                "Paperless WAYBILL / ATW / POD workflows",
                                "Customer shipment request portal",
                                "Full trip history and audit logging"
                            ].map((point, idx) => (
                                <li key={idx} className="flex items-start gap-4">
                                    <div className="mt-1 w-6 h-6 rounded-full bg-[#E5533D]/10 flex flex-shrink-0 items-center justify-center text-[#E5533D]">
                                        <CheckCircle2 size={16} />
                                    </div>
                                    <span className="text-gray-800 font-medium leading-relaxed">{point}</span>
                                </li>
                            ))}
                        </ul>
                    </div>
                </div>
            </section>

            {/* 7. Call To Action */}
            <section className="py-32 bg-[#eef1f6] relative overflow-hidden">
                <div className="absolute inset-0 bg-[#E5533D]/[0.02]" />
                <div className="max-w-4xl mx-auto px-6 relative z-10 text-center">
                    <h2 className="text-4xl md:text-6xl font-extrabold text-[#1F3A5F] mb-8 tracking-tight">
                        Start Modernizing Your Dispatch Operations
                    </h2>
                    <div className="flex flex-col sm:flex-row gap-4 justify-center">
                        <Link
                            to="/login"
                            className="bg-[#E5533D] text-white px-8 py-4 rounded-xl font-medium hover:bg-[#d44834] hover:scale-105 active:scale-95 transition-all shadow-lg shadow-[#E5533D]/20 text-lg"
                        >
                            Request Demo
                        </Link>
                        <Link
                            to="/login"
                            className="bg-white text-[#1F3A5F] border border-gray-200 px-8 py-4 rounded-xl font-medium hover:bg-gray-50 hover:scale-105 active:scale-95 transition-all shadow-sm text-lg text-center"
                        >
                            Login
                        </Link>
                    </div>
                </div>
            </section>

            {/* 8. Footer */}
            <footer className="bg-[#0f2038] text-white pt-20 pb-10 px-6 mt-auto">
                <div className="max-w-7xl mx-auto">
                    <div className="grid grid-cols-1 md:grid-cols-5 gap-12 mb-16">
                        <div className="col-span-1 md:col-span-2">
                            <div className="flex items-center gap-2 mb-6">
                                <div className="w-8 h-8 rounded bg-white/10 flex items-center justify-center font-bold text-white">
                                    N
                                </div>
                                <span className="font-bold text-xl tracking-tight">NVG Dispatch</span>
                            </div>
                            <p className="text-[#a1b3c7] text-sm mb-8 max-w-sm">
                                Paperless Drayage Dispatch for Modern Logistics.
                            </p>
                            <div className="inline-flex items-center gap-2 px-4 py-2 rounded-lg bg-white/5 border border-white/10 text-xs font-mono text-green-400">
                                <span className="w-2 h-2 rounded-full bg-green-400 animate-pulse" />
                                System Operational ●
                            </div>
                        </div>

                        <div>
                            <h4 className="font-bold mb-6 text-white uppercase text-xs tracking-wider">Platform</h4>
                            <ul className="space-y-4 text-[#a1b3c7] text-sm font-medium">
                                <li><a href="#" className="hover:text-white transition-colors">Features</a></li>
                                <li><a href="#" className="hover:text-white transition-colors">Workflow</a></li>
                                <li><a href="#" className="hover:text-white transition-colors">Integrations</a></li>
                            </ul>
                        </div>

                        <div>
                            <h4 className="font-bold mb-6 text-white uppercase text-xs tracking-wider">Customer Portal</h4>
                            <ul className="space-y-4 text-[#a1b3c7] text-sm font-medium">
                                <li><Link to="/login" className="hover:text-white transition-colors">Submit Request</Link></li>
                                <li><Link to="/login" className="hover:text-white transition-colors">Track Shipment</Link></li>
                                <li><Link to="/login" className="hover:text-white transition-colors">Download Documents</Link></li>
                            </ul>
                        </div>

                        <div>
                            <h4 className="font-bold mb-6 text-white uppercase text-xs tracking-wider">Support</h4>
                            <ul className="space-y-4 text-[#a1b3c7] text-sm font-medium">
                                <li><a href="#" className="hover:text-white transition-colors">Help Center</a></li>
                                <li><a href="#" className="hover:text-white transition-colors">Documentation</a></li>
                                <li><Link to="/login" className="text-[#E5533D] hover:text-white transition-colors">Login</Link></li>
                            </ul>
                        </div>
                    </div>

                    <div className="border-t border-white/10 pt-8 flex flex-col md:flex-row justify-between items-center gap-4 text-[#a1b3c7] text-xs">
                        <p>© {new Date().getFullYear()} NVG Dispatch. All rights reserved.</p>
                        <div className="flex gap-6">
                            <a href="#" className="hover:text-white transition-colors">Privacy Policy</a>
                            <a href="#" className="hover:text-white transition-colors">Terms of Service</a>
                        </div>
                    </div>
                </div>
            </footer>

            {loginOpen ? (
                <div
                    className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 backdrop-blur-[2px]"
                    role="presentation"
                    onClick={closeLogin}
                >
                    <div
                        role="dialog"
                        aria-modal="true"
                        className="w-[min(92vw,520px)] rounded-2xl border border-gray-200 bg-white p-8 shadow-2xl"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="flex items-start justify-between gap-4">
                            <div>
                                <p className="text-xs uppercase tracking-[0.25em] text-gray-400">Sign In</p>
                                <h2 className="mt-2 text-2xl font-semibold text-[#1F3A5F]">Welcome back</h2>
                            </div>
                            <button
                                onClick={closeLogin}
                                className="rounded-lg border border-gray-200 px-3 py-1 text-xs text-gray-500 hover:text-gray-900"
                            >
                                Close
                            </button>
                        </div>

                        <form onSubmit={handleLogin} className="mt-6 space-y-4">
                            <div>
                                <label className="text-xs uppercase text-gray-500">Username</label>
                                <input
                                    value={username}
                                    onChange={(e) => setUsername(e.target.value)}
                                    className="mt-1 h-11 w-full rounded-lg border border-gray-200 bg-white px-3 text-sm"
                                />
                            </div>
                            <div>
                                <label className="text-xs uppercase text-gray-500">Password</label>
                                <input
                                    type="password"
                                    value={password}
                                    onChange={(e) => setPassword(e.target.value)}
                                    className="mt-1 h-11 w-full rounded-lg border border-gray-200 bg-white px-3 text-sm"
                                />
                            </div>
                            <button
                                type="submit"
                                disabled={submitting}
                                className="w-full rounded-lg bg-[#1F3A5F] px-4 py-3 text-sm font-semibold text-white hover:bg-[#182f4d] disabled:opacity-70"
                            >
                                {submitting ? "Signing in..." : "Sign in"}
                            </button>
                        </form>
                    </div>
                </div>
            ) : null}
        </div>
    );
}
