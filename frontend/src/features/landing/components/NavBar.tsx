import { useState } from "react";
import { Link } from "react-router-dom";
import { Menu, X } from "lucide-react";
import { useScrolled } from "@/features/landing/hooks/useScrolled";

type NavBarProps = {
    onDemoClick: () => void;
};

export default function NavBar({ onDemoClick }: NavBarProps) {
    const isScrolled = useScrolled();
    const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

    return (
        <>
            {/* 1. Navbar */}
            <nav
                className={`fixed top-4 left-1/2 -translate-x-1/2 w-[95%] max-w-7xl z-50 transition-all duration-300 rounded-full px-6 py-3 flex items-center justify-between ${isScrolled
                        ? "bg-white/80 backdrop-blur-md shadow-lg border border-gray-200/50"
                        : "bg-transparent"
                    }`}
            >
                <div className="flex items-center gap-2">
                    <div className="w-8 h-8 rounded bg-primary flex items-center justify-center text-white font-bold">
                        N
                    </div>
                    <span className="font-bold text-xl tracking-tight text-primary">NVG Dispatch</span>
                </div>

                {/* Desktop Nav */}
                <div className="hidden md:flex items-center gap-8 font-medium text-sm">
                    <a href="#platform" className="hover:text-accent transition-colors">Platform</a>
                    <a href="#workflow" className="hover:text-accent transition-colors">Workflow</a>
                    <a href="#customer-portal" className="hover:text-accent transition-colors">Customer Portal</a>
                </div>

                <div className="hidden md:flex items-center gap-4">
                    <Link to="/login" className="text-sm font-medium hover:text-accent transition-colors">Login</Link>
                    <button
                        type="button"
                        onClick={onDemoClick}
                        className="bg-accent text-white px-5 py-2.5 rounded-full text-sm font-medium hover:bg-accent/90 transition-all hover:scale-105 active:scale-95 shadow-md shadow-accent/20"
                    >
                        Request Demo
                    </button>
                </div>

                {/* Mobile Menu Toggle */}
                <button
                    className="md:hidden text-primary"
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
                    <button
                        type="button"
                        onClick={() => {
                            setMobileMenuOpen(false);
                            onDemoClick();
                        }}
                        className="bg-accent text-white px-6 py-3 rounded-xl text-center font-medium mt-4"
                    >
                        Request Demo
                    </button>
                </div>
            )}
        </>
    );
}
