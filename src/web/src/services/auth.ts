import type { Configuration } from "@azure/msal-browser";
import { LogLevel } from "@azure/msal-browser";

export const msalConfig: Configuration = {
  auth: {
    clientId: import.meta.env.VITE_AAD_CLIENT_ID || "YOUR_SPA_CLIENT_ID",
    authority: `https://login.microsoftonline.com/${import.meta.env.VITE_AAD_TENANT_ID || "YOUR_TENANT_ID"}`,
    redirectUri: window.location.origin,
    postLogoutRedirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: "sessionStorage",
  },
  system: {
    loggerOptions: {
      logLevel: LogLevel.Warning,
    },
  },
};

export const loginRequest = {
  scopes: [
    `api://${import.meta.env.VITE_AAD_API_CLIENT_ID || "YOUR_API_CLIENT_ID"}/Files.ReadWrite`,
  ],
};

export const apiScopes = {
  files: [`api://${import.meta.env.VITE_AAD_API_CLIENT_ID || "YOUR_API_CLIENT_ID"}/Files.ReadWrite`],
  admin: [`api://${import.meta.env.VITE_AAD_API_CLIENT_ID || "YOUR_API_CLIENT_ID"}/Admin.ReadWrite`],
};
