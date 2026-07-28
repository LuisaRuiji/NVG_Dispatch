import React, { useState, useEffect, useRef } from "react";
import { Input } from "./input";
import { Search, MapPin } from "lucide-react";

interface NominatimResult {
  place_id: number;
  display_name: string;
  lat: string;
  lon: string;
}

interface LocationAutocompleteProps {
  value: string;
  onChange: (value: string, lat?: number, lon?: number) => void;
  placeholder?: string;
  className?: string;
}

export function LocationAutocomplete({ value, onChange, placeholder, className }: LocationAutocompleteProps) {
  const [query, setQuery] = useState(value);
  const [results, setResults] = useState<NominatimResult[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setQuery(value);
  }, [value]);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }
    function handleScroll() {
      setIsOpen(false);
    }
    document.addEventListener("mousedown", handleClickOutside);
    window.addEventListener("scroll", handleScroll, { passive: true });
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      window.removeEventListener("scroll", handleScroll);
    };
  }, []);

  useEffect(() => {
    const delayDebounceFn = setTimeout(async () => {
      if (!query || query.length < 3 || query === value) {
        setResults([]);
        return;
      }

      setIsSearching(true);
      try {
        const response = await fetch(
          `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}&limit=5`,
          {
            headers: {
              "Accept-Language": "en-US,en;q=0.9",
            },
          }
        );
        const data = await response.json();
        setResults(data);
        setIsOpen(true);
      } catch (error) {
        console.error("Error fetching locations:", error);
      } finally {
        setIsSearching(false);
      }
    }, 500);

    return () => clearTimeout(delayDebounceFn);
  }, [query, value]);

  const handleSelect = (result: NominatimResult) => {
    setQuery(result.display_name);
    setIsOpen(false);
    onChange(result.display_name, parseFloat(result.lat), parseFloat(result.lon));
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setQuery(e.target.value);
    onChange(e.target.value, undefined, undefined);
  };

  return (
    <div ref={wrapperRef} className={`relative ${className || ""}`}>
      <div className="relative">
        <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
        <Input
          type="text"
          value={query}
          onChange={handleInputChange}
          placeholder={placeholder || "Search location..."}
          className="pl-9"
          onFocus={() => {
            if (results.length > 0) setIsOpen(true);
          }}
        />
        {isSearching && (
          <div className="absolute right-3 top-3 h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        )}
      </div>

      {isOpen && results.length > 0 && (
        <ul className="absolute z-20 mt-1 max-h-60 w-full overflow-auto rounded-md border border-slate-200 bg-white py-1 shadow-lg focus:outline-none dark:border-slate-800 dark:bg-slate-950">
          {results.map((result) => (
            <li
              key={result.place_id}
              className="relative cursor-pointer select-none py-2 pl-3 pr-9 hover:bg-slate-100 dark:hover:bg-slate-800"
              onClick={() => handleSelect(result)}
            >
              <div className="flex items-start">
                <MapPin className="mr-2 mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
                <span className="block truncate text-sm">{result.display_name}</span>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
