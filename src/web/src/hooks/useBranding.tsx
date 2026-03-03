import { useContext } from "react";
import { BrandingContext } from "../contexts/brandingValue";

export function useBranding() {
  return useContext(BrandingContext);
}
