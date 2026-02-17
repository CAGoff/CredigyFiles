import type { IPublicClientApplication } from "@azure/msal-browser";
import { apiScopes } from "./auth";

const API_BASE = import.meta.env.VITE_API_BASE_URL || "https://localhost:5001/v1";

async function getToken(msalInstance: IPublicClientApplication, scopes: string[]): Promise<string> {
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length === 0) throw new Error("No authenticated account found");

  const response = await msalInstance.acquireTokenSilent({
    scopes,
    account: accounts[0],
  });
  return response.accessToken;
}

async function apiFetch(
  msalInstance: IPublicClientApplication,
  path: string,
  options: RequestInit = {},
  scopes?: string[]
): Promise<Response> {
  const token = await getToken(msalInstance, scopes ?? apiScopes.files);
  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      ...options.headers,
      Authorization: `Bearer ${token}`,
    },
  });
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
