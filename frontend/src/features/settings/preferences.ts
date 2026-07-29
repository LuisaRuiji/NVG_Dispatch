export type LayoutDensity = "comfortable" | "compact";
export type FontSize = "small" | "medium" | "large";
export type DefaultLanding = "dashboard" | "planning" | "my-trips" | "portal";

export type SettingsPreferences = {
  density: LayoutDensity;
  fontSize: FontSize;
  reducedMotion: boolean;
  defaultLanding: DefaultLanding;
};

const STORAGE_KEY = "vaia_personal_preferences";

const defaults: SettingsPreferences = {
  density: "comfortable",
  fontSize: "medium",
  reducedMotion: false,
  defaultLanding: "dashboard"
};

export function getSettingsPreferences(): SettingsPreferences {
  if (typeof window === "undefined") return defaults;
  try {
    const stored = JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? "{}") as Partial<SettingsPreferences>;
    return {
      density: stored.density === "compact" ? "compact" : "comfortable",
      fontSize: stored.fontSize === "small" || stored.fontSize === "large" ? stored.fontSize : "medium",
      reducedMotion: stored.reducedMotion === true,
      defaultLanding: ["dashboard", "planning", "my-trips", "portal"].includes(stored.defaultLanding ?? "")
        ? stored.defaultLanding as DefaultLanding
        : "dashboard"
    };
  } catch {
    return defaults;
  }
}

export function applySettingsPreferences(preferences: SettingsPreferences) {
  if (typeof window === "undefined") return;
  const root = document.documentElement;
  root.classList.toggle("vaia-density-compact", preferences.density === "compact");
  root.classList.toggle("vaia-font-small", preferences.fontSize === "small");
  root.classList.toggle("vaia-font-large", preferences.fontSize === "large");
  root.classList.toggle("vaia-reduce-motion", preferences.reducedMotion);
}

export function saveSettingsPreferences(preferences: SettingsPreferences) {
  if (typeof window !== "undefined") {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(preferences));
  }
  applySettingsPreferences(preferences);
}

export function initializeSettingsPreferences() {
  applySettingsPreferences(getSettingsPreferences());
}
