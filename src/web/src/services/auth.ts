import type { Configuration } from "@azure/msal-browser";
import { LogLevel } from "@azure/msal-browser";

const DEV_AUTH = import.meta.env.VITE_DEV_AUTH === "true";

function requireEnv(name: string): string {
  const value = import.meta.env[name];
  if (!value) {
    if (DEV_AUTH) return "00000000-0000-0000-0000-000000000000";
    throw new Error(`Missing required environment variable: ${name}`);
  }
  return value;
}

const clientId = requireEnv("VITE_AAD_CLIENT_ID");
const tenantId = requireEnv("VITE_AAD_TENANT_ID");
const apiClientId = requireEnv("VITE_AAD_API_CLIENT_ID");

export const msalConfig: Configuration = {
  auth: {
    clientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
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
  scopes: [`api://${apiClientId}/Files.ReadWrite`],
};

export const apiScopes = {
  files: [`api://${apiClientId}/Files.ReadWrite`],
  admin: [`api://${apiClientId}/Admin.ReadWrite`],
};
