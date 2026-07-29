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
                className={`fixed top-4 left-1/2 -translate-x-1/2 w-[95%] max-w-7xl z-50 transition-colors duration-150 rounded-lg px-6 py-3 flex items-center justify-between ${isScrolled
                        ? "bg-white border border-border"
                        : "bg-transparent"
                    }`}
            >
                <div className="flex items-center gap-2">
                    <div className="w-8 h-8 rounded-lg bg-[#122442] flex items-center justify-center text-white font-bold">
                        V
                    </div>
                    <span className="font-bold text-xl tracking-tight text-foreground">VAIA</span>
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
                        className="bg-primary text-white px-5 py-2.5 rounded-lg text-sm font-medium hover:bg-[#E65300] transition-colors"
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
                        className="bg-primary text-white px-6 py-3 rounded-lg text-center font-medium mt-4"
                    >
                        Request Demo
                    </button>
                </div>
            )}
        </>
    );
}
