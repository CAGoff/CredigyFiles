import type { Configuration } from "@azure/msal-browser";
import { LogLevel } from "@azure/msal-browser";

// Placeholder GUID used when no real Entra ID config is provided (dev mode)
const PLACEHOLDER_GUID = "00000000-0000-0000-0000-000000000000";

export const msalConfig: Configuration = {
  auth: {
    clientId: import.meta.env.VITE_AAD_CLIENT_ID || PLACEHOLDER_GUID,
    authority: `https://login.microsoftonline.com/${import.meta.env.VITE_AAD_TENANT_ID || PLACEHOLDER_GUID}`,
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

const apiClientId = import.meta.env.VITE_AAD_API_CLIENT_ID || PLACEHOLDER_GUID;

export const loginRequest = {
  scopes: [`api://${apiClientId}/Files.ReadWrite`],
};

export const apiScopes = {
  files: [`api://${apiClientId}/Files.ReadWrite`],
  admin: [`api://${apiClientId}/Admin.ReadWrite`],
};
