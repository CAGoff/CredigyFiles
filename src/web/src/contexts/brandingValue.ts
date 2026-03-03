import { createContext } from "react";
import type { BrandingData } from "../services/api";

export interface BrandingContextValue {
  branding: BrandingData;
  loading: boolean;
  refreshBranding: () => Promise<void>;
}

export const defaultBranding: BrandingData = {
  appName: "Credigy Files",
  primaryColor: "#2563eb",
};

export const BrandingContext = createContext<BrandingContextValue>({
  branding: defaultBranding,
  loading: true,
  refreshBranding: async () => {},
});
