import { useMsal } from "@azure/msal-react";

/**
 * Returns the app roles from the active account's ID token claims.
 * Roles are defined in the Entra ID app registration and assigned
 * to users/groups via Enterprise Applications.
 */
export function useUserRoles(): string[] {
  const { accounts } = useMsal();
  const account = accounts[0];
  if (!account?.idTokenClaims) return [];

  const roles = (account.idTokenClaims as Record<string, unknown>)["roles"];
  if (Array.isArray(roles)) return roles as string[];
  return [];
}

export function useIsAdmin(): boolean {
  const roles = useUserRoles();
  return roles.includes("SFT.Admin");
}
