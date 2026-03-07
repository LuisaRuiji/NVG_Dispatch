import React, { useEffect, useState } from "react";
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
    Activity
} from "lucide-react";

export default function LandingPage() {
    const [isScrolled, setIsScrolled] = useState(false);
    const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

    useEffect(() => {
        const handleScroll = () => {
            setIsScrolled(window.scrollY > 20);
        };
        window.addEventListener("scroll", handleScroll);
        return () => window.removeEventListener("scroll", handleScroll);
    }, []);

    return (
        <div className="min-h-screen bg-[#F5F7FA] text-[#1A1A1A] font-sans selection:bg-[#1F3A5F] selection:text-white">
            {/* Navbar */}
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
                    <a href="#features" className="hover:text-[#E5533D] transition-colors">Features</a>
                    <a href="#workflow" className="hover:text-[#E5533D] transition-colors">How It Works</a>
                    <a href="#portal" className="hover:text-[#E5533D] transition-colors">Portal</a>
                </div>

                <div className="hidden md:flex items-center gap-4">
                    <button className="text-sm font-medium hover:text-[#E5533D] transition-colors">Login</button>
                    <button className="bg-[#E5533D] text-white px-5 py-2.5 rounded-full text-sm font-medium hover:bg-[#d44834] transition-all hover:scale-105 active:scale-95 shadow-md shadow-[#E5533D]/20">
                        Request Demo
                    </button>
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
                    <a href="#features" className="text-xl font-medium" onClick={() => setMobileMenuOpen(false)}>Features</a>
                    <a href="#workflow" className="text-xl font-medium" onClick={() => setMobileMenuOpen(false)}>How It Works</a>
                    <a href="#portal" className="text-xl font-medium" onClick={() => setMobileMenuOpen(false)}>Portal</a>
                    <hr className="border-gray-100" />
                    <button className="text-left text-xl font-medium">Login</button>
                    <button className="bg-[#E5533D] text-white px-6 py-3 rounded-xl text-center font-medium mt-4">
                        Request Demo
                    </button>
                </div>
            )}

            {/* Hero Section */}
            <section className="relative pt-32 pb-20 md:pt-48 md:pb-32 px-6 overflow-hidden">
                {/* Background decorative elements */}
                <div className="absolute top-0 right-0 w-[50vw] h-[50vw] bg-[#1F3A5F]/5 rounded-full blur-3xl -translate-y-1/2 translate-x-1/3 pointer-events-none" />

                <div className="max-w-7xl mx-auto grid grid-cols-1 lg:grid-cols-2 gap-12 items-center">
                    <div className="max-w-2xl relative z-10">
                        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-[#1F3A5F]/10 text-[#1F3A5F] text-xs font-semibold mb-6 uppercase tracking-wider">
                            <span className="w-2 h-2 rounded-full bg-[#E5533D] animate-pulse" />
                            For Container Hauling
                        </div>
                        <h1 className="text-5xl md:text-6xl lg:text-7xl font-extrabold text-[#1F3A5F] leading-[1.1] mb-6 tracking-tight">
                            Smarter Dispatch for Modern Logistics
                        </h1>
                        <p className="text-lg md:text-xl text-gray-600 mb-10 leading-relaxed max-w-xl">
                            Automate scheduling, track trips in real time, and eliminate paper logistics documents with a single, powerful platform.
                        </p>
                        <div className="flex flex-col sm:flex-row gap-4">
                            <button className="bg-[#1F3A5F] text-white px-8 py-4 rounded-xl font-medium flex items-center justify-center gap-2 hover:bg-[#162A45] hover:-translate-y-0.5 transition-all shadow-lg hover:shadow-xl shadow-[#1F3A5F]/20">
                                Request Demo
                                <ArrowRight size={18} />
                            </button>
                            <button className="bg-white text-[#1F3A5F] border border-gray-200 px-8 py-4 rounded-xl font-medium hover:bg-gray-50 transition-all hover:border-gray-300 hover:-translate-y-0.5 shadow-sm">
                                Login to Platform
                            </button>
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
                                    { label: "Pending Docs", val: "12", icon: FileText },
                                    { label: "On Hold", val: "3", icon: Activity }
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
                                <div className="px-4 py-3 border-b border-gray-50 font-medium text-sm flex justify-between items-center">
                                    Live Dispatch
                                    <span className="text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded-full">System Normal</span>
                                </div>
                                <div className="p-4 flex-1 space-y-3 font-mono text-xs">
                                    {[1, 2, 3, 4].map(i => (
                                        <div key={i} className="flex items-center justify-between p-2 hover:bg-gray-50 rounded cursor-pointer border border-transparent hover:border-gray-100">
                                            <div className="flex items-center gap-3">
                                                <div className={`w-2 h-2 rounded-full ${i === 1 ? 'bg-amber-400' : 'bg-green-400'}`} />
                                                <span className="text-[#1F3A5F] font-semibold">TRK-00{i}</span>
                                                <span className="text-gray-500 hidden sm:inline">→ Port Terminal {i}</span>
                                            </div>
                                            <span className="text-gray-400">10:{i}4 AM</span>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </section>

            {/* Value Proposition */}
            <section id="features" className="py-24 bg-white border-y border-gray-100 relative">
                <div className="max-w-7xl mx-auto px-6">
                    <div className="text-center md:text-left mb-16 max-w-2xl">
                        <h2 className="text-3xl md:text-4xl font-bold text-[#1F3A5F] mb-4">Operations built for speed</h2>
                        <p className="text-gray-600 text-lg">Replacing fragmented communication with a single source of truth.</p>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
                        {[
                            { title: "Smart Dispatch", desc: "Automatically prevent scheduling conflicts for drivers and trucks.", icon: Map },
                            { title: "Real-Time Visibility", desc: "Track every trip status instantly from pickup to drop-off.", icon: Activity },
                            { title: "Paperless Logistics", desc: "Digitize container documentation and approvals to get paid faster.", icon: FileText }
                        ].map((prop, idx) => (
                            <div key={idx} className="group p-8 rounded-2xl bg-[#F5F7FA] border border-gray-100 hover:border-[#1F3A5F]/20 hover:shadow-xl hover:shadow-[#1F3A5F]/5 transition-all duration-300 hover:-translate-y-1">
                                <div className="w-12 h-12 bg-white rounded-xl shadow-sm flex items-center justify-center mb-6 text-[#1F3A5F] group-hover:scale-110 transition-transform">
                                    <prop.icon size={24} />
                                </div>
                                <h3 className="text-xl font-bold text-[#1F3A5F] mb-3">{prop.title}</h3>
                                <p className="text-gray-600 leading-relaxed">{prop.desc}</p>
                            </div>
                        ))}
                    </div>
                </div>
            </section>

            {/* Product Screen Showcase */}
            <section className="py-32 bg-[#1F3A5F] text-white overflow-hidden relative">
                {/* Glow effect */}
                <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[80vw] h-[80vw] bg-[#E5533D]/10 rounded-full blur-[120px] pointer-events-none" />

                <div className="max-w-7xl mx-auto px-6 relative z-10">
                    <div className="text-center mb-20">
                        <h2 className="text-3xl md:text-5xl font-bold mb-6">Real Workflow Interfaces</h2>
                        <p className="text-[#a1b3c7] text-xl max-w-2xl mx-auto">Built for the people who actually use it.</p>
                    </div>

                    <div className="space-y-32">
                        {[
                            {
                                title: "Dispatcher Dashboard",
                                desc: "Monitor active trips, on-hold alerts, and fleet status in real time. Make decisions instantly with a bird's-eye view of your entire operation.",
                                icon: Map,
                                imgMock: "Dashboard view with trip table and status badges."
                            },
                            {
                                title: "Driver Mobile Workflow",
                                desc: "Drivers execute trips step-by-step from a mobile interface designed for on-the-go simplicity. No confusing menus, just the next action required.",
                                icon: Truck,
                                reverse: true,
                                imgMock: "Mobile screen with large Next Action button."
                            },
                            {
                                title: "Document Verification",
                                desc: "Upload, verify, and manage WAYBILL, ATW, and POD documents digitally. Approve them in clicks, not email chains.",
                                icon: ShieldCheck,
                                imgMock: "Split screen: Document scan on left, form on right."
                            }
                        ].map((feature, idx) => (
                            <div key={idx} className={`flex flex-col ${feature.reverse ? 'lg:flex-row-reverse' : 'lg:flex-row'} items-center gap-16`}>
                                <div className="flex-1 space-y-6">
                                    <div className="w-14 h-14 bg-white/10 rounded-2xl flex items-center justify-center text-[#E5533D]">
                                        <feature.icon size={28} />
                                    </div>
                                    <h3 className="text-3xl font-bold">{feature.title}</h3>
                                    <p className="text-lg text-[#a1b3c7] leading-relaxed">{feature.desc}</p>
                                    <ul className="space-y-3 pt-4">
                                        {[1, 2, 3].map(i => (
                                            <li key={i} className="flex items-center gap-3 text-sm font-medium text-white/80">
                                                <CheckCircle2 size={18} className="text-[#E5533D]" />
                                                Key capability {i}
                                            </li>
                                        ))}
                                    </ul>
                                </div>
                                <div className="flex-1 w-full">
                                    <div className="bg-[#0f2038] border border-white/10 rounded-2xl p-2 shadow-2xl overflow-hidden aspect-[4/3] flex items-center justify-center relative group">
                                        <div className="absolute inset-0 bg-gradient-to-tr from-white/5 to-transparent opacity-0 group-hover:opacity-100 transition-opacity" />
                                        <div className="text-center p-8">
                                            <div className="w-16 h-16 mx-auto bg-white/5 rounded-full flex items-center justify-center mb-4 text-white/40">
                                                <feature.icon size={32} />
                                            </div>
                                            <p className="font-mono text-sm text-white/50">{feature.imgMock}</p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            </section>

            {/* Workflow Diagram Section */}
            <section id="workflow" className="py-24 bg-[#F5F7FA]">
                <div className="max-w-7xl mx-auto px-6">
                    <div className="text-center mb-16">
                        <h2 className="text-3xl md:text-4xl font-bold text-[#1F3A5F] mb-4">The Platform Process</h2>
                        <p className="text-gray-600 text-lg">A seamless flow from request to delivery.</p>
                    </div>

                    <div className="relative">
                        <div className="hidden md:block absolute top-1/2 left-0 w-full h-0.5 bg-gradient-to-r from-gray-200 via-[#1F3A5F]/20 to-gray-200 -translate-y-1/2" />

                        <div className="grid grid-cols-1 md:grid-cols-5 gap-8">
                            {[
                                { title: "Client Request", desc: "Customer creates shipment request" },
                                { title: "Dispatch", desc: "Dispatcher plans and assigns trip" },
                                { title: "Execution", desc: "Driver executes pickup & delivery" },
                                { title: "Verification", desc: "Documents uploaded & verified" },
                                { title: "Completion", desc: "Customer downloads POD" }
                            ].map((step, idx) => (
                                <div key={idx} className="relative z-10 flex flex-col items-center text-center">
                                    <div className="w-12 h-12 rounded-full bg-white border-4 border-[#F5F7FA] shadow-md flex items-center justify-center text-[#1F3A5F] font-bold font-mono mb-6">
                                        {idx + 1}
                                    </div>
                                    <h4 className="font-bold text-[#1F3A5F] mb-2">{step.title}</h4>
                                    <p className="text-sm text-gray-500">{step.desc}</p>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>
            </section>

            {/* CTA Section */}
            <section className="py-24 bg-white relative overflow-hidden">
                <div className="absolute inset-0 bg-[#E5533D]/5" />
                <div className="max-w-4xl mx-auto px-6 relative z-10 text-center">
                    <h2 className="text-4xl md:text-5xl font-extrabold text-[#1F3A5F] mb-6">
                        Start Modernizing Your Dispatch Operations
                    </h2>
                    <p className="text-xl text-gray-600 mb-10">
                        Join the forward-thinking logistics companies managing fleets efficiently with NVG Dispatch.
                    </p>
                    <div className="flex flex-col sm:flex-row gap-4 justify-center">
                        <button className="bg-[#E5533D] text-white px-8 py-4 rounded-xl font-medium hover:bg-[#d44834] hover:scale-105 active:scale-95 transition-all shadow-lg shadow-[#E5533D]/20 text-lg">
                            Request Demo
                        </button>
                        <button className="bg-[#1F3A5F] text-white px-8 py-4 rounded-xl font-medium hover:bg-[#162A45] hover:scale-105 active:scale-95 transition-all shadow-[#1F3A5F]/20 text-lg">
                            Login to Platform
                        </button>
                    </div>
                </div>
            </section>

            {/* Footer */}
            <footer className="bg-[#0f2038] text-white pt-20 pb-10 px-6 rounded-t-[3rem] mt-4 relative z-20">
                <div className="max-w-7xl mx-auto">
                    <div className="grid grid-cols-1 md:grid-cols-4 gap-12 mb-16">
                        <div className="col-span-1 md:col-span-1">
                            <div className="flex items-center gap-2 mb-6">
                                <div className="w-8 h-8 rounded bg-white/10 flex items-center justify-center font-bold text-white">
                                    N
                                </div>
                                <span className="font-bold text-xl tracking-tight">NVG Dispatch</span>
                            </div>
                            <p className="text-[#a1b3c7] text-sm mb-6">
                                Paperless Drayage Dispatch for Modern Logistics.
                            </p>
                            <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded bg-white/5 border border-white/10 text-xs font-mono text-green-400">
                                <span className="w-2 h-2 rounded-full bg-green-400 animate-pulse" />
                                System Operational
                            </div>
                        </div>

                        <div>
                            <h4 className="font-semibold mb-6 text-white/90">Platform</h4>
                            <ul className="space-y-4 text-[#a1b3c7] text-sm">
                                <li><a href="#" className="hover:text-white transition-colors">Features</a></li>
                                <li><a href="#" className="hover:text-white transition-colors">Integrations</a></li>
                                <li><a href="#" className="hover:text-white transition-colors">Security</a></li>
                                <li><a href="#" className="hover:text-white transition-colors">Changelog</a></li>
                            </ul>
                        </div>

                        <div>
                            <h4 className="font-semibold mb-6 text-white/90">Company</h4>
                            <ul className="space-y-4 text-[#a1b3c7] text-sm">
                                <li><a href="#" className="hover:text-white transition-colors">About</a></li>
                                <li><a href="#" className="hover:text-white transition-colors">Customers</a></li>
                                <li><a href="#" className="hover:text-white transition-colors">Contact</a></li>
                            </ul>
                        </div>

                        <div>
                            <h4 className="font-semibold mb-6 text-white/90">Support</h4>
                            <ul className="space-y-4 text-[#a1b3c7] text-sm">
                                <li><a href="#" className="hover:text-white transition-colors">Help Center</a></li>
                                <li><a href="#" className="hover:text-white transition-colors">API Documentation</a></li>
                                <li><a href="#" className="text-[#E5533D] hover:text-white transition-colors">Login</a></li>
                            </ul>
                        </div>
                    </div>

                    <div className="border-t border-white/10 pt-8 flex flex-col md:flex-row justify-between items-center gap-4 text-[#a1b3c7] text-xs">
                        <p>© {new Date().getFullYear()} NVG Logistics. All rights reserved.</p>
                        <div className="flex gap-6">
                            <a href="#" className="hover:text-white transition-colors">Privacy Policy</a>
                            <a href="#" className="hover:text-white transition-colors">Terms of Service</a>
                        </div>
                    </div>
                </div>
            </footer>
        </div>
    );
}
