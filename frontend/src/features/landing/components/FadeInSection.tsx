import { type ReactNode, useEffect, useRef, useState } from "react";

type FadeInSectionProps = {
    children: ReactNode;
    className?: string;
    delay?: number;
    direction?: "up" | "left" | "right";
};

const hiddenDirectionClasses = {
    up: "translate-y-[20px]",
    left: "-translate-x-[20px]",
    right: "translate-x-[20px]"
};

export default function FadeInSection({ children, className = "", delay = 0, direction = "up" }: FadeInSectionProps) {
    const ref = useRef<HTMLDivElement | null>(null);
    const [isVisible, setIsVisible] = useState(false);
    const [prefersReducedMotion, setPrefersReducedMotion] = useState(false);

    useEffect(() => {
        const node = ref.current;
        if (!node) return;

        if (!("matchMedia" in window)) {
            setIsVisible(true);
            return;
        }

        const reducedMotionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
        setPrefersReducedMotion(reducedMotionQuery.matches);

        if (reducedMotionQuery.matches) {
            setIsVisible(true);
            return;
        }

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
        <div
            ref={ref}
            style={{ transitionDelay: prefersReducedMotion ? undefined : `${delay}ms` }}
            className={
                prefersReducedMotion
                    ? className
                    : `${className} transition-all duration-500 ease-out ${
                          isVisible ? "translate-x-0 translate-y-0 opacity-100" : `${hiddenDirectionClasses[direction]} opacity-0`
                      }`
            }
        >
            {children}
        </div>
    );
}
