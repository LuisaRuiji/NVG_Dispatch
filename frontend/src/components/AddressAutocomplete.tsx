import { useState, useEffect, useRef } from "react";
import { Input } from "@/components/ui/input";
import { MapPin, Loader2 } from "lucide-react";

type NominatimResult = {
  place_id: number;
  lat: string;
  lon: string;
  display_name: string;
};

type Props = {
  value: string;
  onChange: (value: string, lat?: number, lon?: number) => void;
  placeholder?: string;
  className?: string;
  disabled?: boolean;
};

export default function AddressAutocomplete({ value, onChange, placeholder, className, disabled }: Props) {
  const [query, setQuery] = useState(value);
  const [results, setResults] = useState<NominatimResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
  const [lastSelectedText, setLastSelectedText] = useState(value);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const isSelectingRef = useRef(false);

  // Sync incoming value
  useEffect(() => {
    if (value !== query) {
      setQuery(value);
      setLastSelectedText(value);
    }
  }, [value]);

  // Click outside to close
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  // Debounced search with Davao viewbox bias
  useEffect(() => {
    if (!query) {
      setResults([]);
      setIsOpen(false);
      return;
    }

    if (isSelectingRef.current) {
      isSelectingRef.current = false;
      return;
    }

    const timer = setTimeout(async () => {
      try {
        setLoading(true);
        // Prioritize Davao City area (viewbox=125.35,7.35,125.75,6.95)
        const res = await fetch(
          `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(
            query
          )}&limit=5&countrycodes=ph&viewbox=125.35,7.35,125.75,6.95`,
          {
            headers: {
              "User-Agent": "",
            },
          }
        );
        if (!res.ok) throw new Error("Network response was not ok");
        const data = await res.json();
        setResults(data);
        setIsOpen(true);
      } catch (error) {
        console.error("Geocoding failed:", error);
      } finally {
        setLoading(false);
      }
    }, 600); // 600ms debounce to respect Nominatim's 1 req/sec limit

    return () => clearTimeout(timer);
  }, [query]);

  const handleSelect = (item: NominatimResult) => {
    isSelectingRef.current = true;
    setQuery(item.display_name);
    setLastSelectedText(item.display_name);
    setIsOpen(false);
    onChange(item.display_name, parseFloat(item.lat), parseFloat(item.lon));
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setQuery(e.target.value);
    onChange(e.target.value); // Pass raw string without coords
    if (!isOpen) setIsOpen(true);
  };

  const handleBlur = () => {
    // Hide dropdown after a short delay so clicking a suggestion works first
    setTimeout(async () => {
      setIsOpen(false);

      if (!query.trim()) {
        onChange("", undefined, undefined);
        return;
      }

      // If the query was modified and not explicitly selected from the list
      if (query !== lastSelectedText) {
        try {
          setLoading(true);
          const res = await fetch(
            `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(
              query
            )}&limit=1&countrycodes=ph&viewbox=125.35,7.35,125.75,6.95`,
            {
              headers: {
                "User-Agent": "NVG_Dispatch_Capstone/1.0",
              },
            }
          );
          if (res.ok) {
            const data = await res.json();
            if (data && data.length > 0) {
              const bestResult = data[0];
              setQuery(bestResult.display_name);
              setLastSelectedText(bestResult.display_name);
              onChange(bestResult.display_name, parseFloat(bestResult.lat), parseFloat(bestResult.lon));
            }
          }
        } catch (error) {
          console.error("Background geocoding failed on blur:", error);
        } finally {
          setLoading(false);
        }
      }
    }, 250);
  };

  return (
    <div className={`relative ${className || ""}`} ref={wrapperRef}>
      <div className="relative">
        <Input
          value={query}
          onChange={handleChange}
          onBlur={handleBlur}
          onFocus={() => {
            if (results.length > 0) setIsOpen(true);
          }}
          placeholder={placeholder || "Search address..."}
          className="pr-10"
          disabled={disabled}
        />
        <div className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400">
          {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <MapPin className="h-4 w-4" />}
        </div>
      </div>

      {isOpen && results.length > 0 && (
        <div className="absolute z-50 mt-1 max-h-60 w-full overflow-y-auto rounded-xl border border-slate-200 bg-white shadow-xl">
          <ul className="py-2">
            {results.map((item) => (
              <li
                key={item.place_id}
                onClick={() => handleSelect(item)}
                className="cursor-pointer px-4 py-2 text-sm text-slate-700 hover:bg-[#175C99]/10 hover:text-[#175C99]"
              >
                {item.display_name}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
