import type { IPublicClientApplication } from "@azure/msal-browser";
import { InteractionRequiredAuthError } from "@azure/msal-browser";
import { apiScopes } from "./auth";

const API_BASE = import.meta.env.VITE_API_BASE_URL || "/v1";
const DEV_AUTH = import.meta.env.VITE_DEV_AUTH === "true";

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;

  constructor(status: number, code: string, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
  }
}

async function getToken(msalInstance: IPublicClientApplication, scopes: string[]): Promise<string> {
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length === 0) throw new Error("No authenticated account found");

  try {
    const response = await msalInstance.acquireTokenSilent({
      scopes,
      account: accounts[0],
    });
    return response.accessToken;
  } catch (err) {
    if (err instanceof InteractionRequiredAuthError) {
      await msalInstance.acquireTokenRedirect({ scopes });
      throw new Error("Redirecting to login...");
    }
    throw err;
  }
}

async function apiFetch(
  msalInstance: IPublicClientApplication,
  path: string,
  options: RequestInit = {},
  scopes?: string[]
): Promise<Response> {
  const headers: Record<string, string> = { ...options.headers as Record<string, string> };
  if (!DEV_AUTH) {
    const token = await getToken(msalInstance, scopes ?? apiScopes.files);
    headers.Authorization = `Bearer ${token}`;
  }
  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers,
  });

  if (!response.ok) {
    let code = "UNKNOWN";
    let message = `Request failed with status ${response.status}`;
    try {
      const body = await response.json();
      if (body?.error) {
        code = body.error.code ?? code;
        message = body.error.message ?? message;
      }
    } catch {
      // Response body wasn't JSON — use defaults
    }
    throw new ApiError(response.status, code, message);
  }

  return response;
}

// Container operations
export async function listContainers(msal: IPublicClientApplication) {
  const res = await apiFetch(msal, "/containers");
  return res.json();
}

// File operations
export async function listFiles(msal: IPublicClientApplication, container: string, dir: string) {
  const res = await apiFetch(msal, `/containers/${container}/files?dir=${dir}`);
  return res.json();
}

export async function downloadFile(msal: IPublicClientApplication, container: string, fileName: string, dir: string) {
  const res = await apiFetch(msal, `/containers/${container}/files/${encodeURIComponent(fileName)}?dir=${dir}`);
  return res.blob();
}

export async function uploadFile(msal: IPublicClientApplication, container: string, dir: string, file: File) {
  const formData = new FormData();
  formData.append("file", file);
  const res = await apiFetch(msal, `/containers/${container}/files?dir=${dir}`, {
    method: "POST",
    body: formData,
  });
  return res.json();
}

export async function deleteFile(msal: IPublicClientApplication, container: string, fileName: string, dir: string) {
  await apiFetch(msal, `/containers/${container}/files/${encodeURIComponent(fileName)}?dir=${dir}`, {
    method: "DELETE",
  });
}

// Activity operations
export async function getContainerActivity(msal: IPublicClientApplication, container: string) {
  const res = await apiFetch(msal, `/containers/${container}/activity`);
  return res.json();
}

export async function getAllActivity(msal: IPublicClientApplication) {
  const res = await apiFetch(msal, "/activity", {}, apiScopes.admin);
  return res.json();
}

// Admin operations
export async function listThirdParties(msal: IPublicClientApplication) {
  const res = await apiFetch(msal, "/admin/third-parties", {}, apiScopes.admin);
  return res.json();
}

export async function createThirdParty(
  msal: IPublicClientApplication,
  data: { companyName: string; contactEmail: string; enableAutomation: boolean }
) {
  const res = await apiFetch(msal, "/admin/third-parties", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  }, apiScopes.admin);
  return res.json();
}

export async function getThirdParty(msal: IPublicClientApplication, id: string) {
  const res = await apiFetch(msal, `/admin/third-parties/${id}`, {}, apiScopes.admin);
  return res.json();
}
