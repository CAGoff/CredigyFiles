import { useEffect, useState, useCallback } from "react";
import type { ReactNode } from "react";
import { getBranding } from "../services/api";
import { BrandingContext, defaultBranding } from "./brandingValue";

/** Lighten a hex color by mixing with white (~40% lighter). */
function lightenHex(hex: string): string {
  try {
    const r = parseInt(hex.slice(1, 3), 16);
    const g = parseInt(hex.slice(3, 5), 16);
    const b = parseInt(hex.slice(5, 7), 16);
    const mix = (c: number) => Math.min(255, Math.round(c + (255 - c) * 0.4));
    return `#${mix(r).toString(16).padStart(2, "0")}${mix(g).toString(16).padStart(2, "0")}${mix(b).toString(16).padStart(2, "0")}`;
  } catch {
    return "#60a5fa";
  }
}

export function BrandingProvider({ children }: { children: ReactNode }) {
  const [branding, setBranding] = useState(defaultBranding);
  const [loading, setLoading] = useState(true);

  const fetchBranding = useCallback(async () => {
    try {
      const data = await getBranding();
      setBranding(data);
    } catch {
      // Fall back to defaults silently
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchBranding();
  }, [fetchBranding]);

  useEffect(() => {
    document.documentElement.style.setProperty("--color-primary", branding.primaryColor);
    document.documentElement.style.setProperty("--color-primary-light", lightenHex(branding.primaryColor));
    document.title = branding.appName;

    if (branding.faviconUrl) {
      let link = document.querySelector<HTMLLinkElement>("link[rel='icon']");
      if (!link) {
        link = document.createElement("link");
        link.rel = "icon";
        document.head.appendChild(link);
      }
      link.href = branding.faviconUrl;
    }
  }, [branding]);

  return (
    <BrandingContext.Provider value={{ branding, loading, refreshBranding: fetchBranding }}>
      {children}
    </BrandingContext.Provider>
  );
}
