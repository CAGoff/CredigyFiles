import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { PublicClientApplication } from "@azure/msal-browser";
import { MsalProvider } from "@azure/msal-react";
import { msalConfig } from "./services/auth";
import { BrandingProvider } from "./contexts/BrandingContext";
import App from "./App";
import "./index.css";

const msalInstance = new PublicClientApplication(msalConfig);

msalInstance.initialize().then(() => {
  msalInstance.handleRedirectPromise().then(() => {
    createRoot(document.getElementById("root")!).render(
      <StrictMode>
        <BrandingProvider>
          <MsalProvider instance={msalInstance}>
            <BrowserRouter>
              <App />
            </BrowserRouter>
          </MsalProvider>
        </BrandingProvider>
      </StrictMode>
    );
  });
});
