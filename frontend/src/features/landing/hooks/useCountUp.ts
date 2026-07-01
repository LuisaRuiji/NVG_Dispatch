import { useEffect, useState } from "react";

export function useCountUp(target: number, duration = 1200, enabled = true) {
    const [value, setValue] = useState(0);

    useEffect(() => {
        if (!enabled) {
            setValue(0);
            return;
        }

        if ("matchMedia" in window && window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            setValue(target);
            return;
        }

        let frameId: number;
        const startedAt = performance.now();

        const tick = (now: number) => {
            const progress = Math.min((now - startedAt) / duration, 1);
            const easedProgress = 1 - Math.pow(1 - progress, 3);
            setValue(Math.round(target * easedProgress));

            if (progress < 1) {
                frameId = requestAnimationFrame(tick);
            }
        };

        frameId = requestAnimationFrame(tick);
        return () => cancelAnimationFrame(frameId);
    }, [duration, enabled, target]);

    return value;
}
