import { useEffect, useState } from "react";
import { useMsal } from "@azure/msal-react";
import { listThirdParties, createThirdParty, deactivateThirdParty } from "../services/api";

interface ThirdPartyInfo {
  id: string;
  companyName: string;
  containerName: string;
  status: string;
  createdAt: string;
  userGroupId?: string;
  adminGroupId?: string;
}

export default function AdminOnboarding() {
  const { instance } = useMsal();
  const [parties, setParties] = useState<ThirdPartyInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [companyName, setCompanyName] = useState("");
  const [contactEmail, setContactEmail] = useState("");
  const [enableAutomation, setEnableAutomation] = useState(false);
  const [userGroupId, setUserGroupId] = useState("");
  const [adminGroupId, setAdminGroupId] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);

  useEffect(() => {
    listThirdParties(instance)
      .then((data) => setParties(data.thirdParties ?? []))
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load third parties"))
      .finally(() => setLoading(false));
  }, [instance]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setSubmitError(null);
    try {
      await createThirdParty(instance, {
        companyName,
        contactEmail,
        enableAutomation,
        userGroupId: userGroupId || undefined,
        adminGroupId: adminGroupId || undefined,
      });
      setCompanyName("");
      setContactEmail("");
      setEnableAutomation(false);
      setUserGroupId("");
      setAdminGroupId("");
      setLoading(true);
      const data = await listThirdParties(instance);
      setParties(data.thirdParties ?? []);
      setLoading(false);
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : "Provisioning failed");
    } finally {
      setSubmitting(false);
    }
  };

  const handleDeactivate = async (id: string, companyName: string) => {
    if (!confirm(`Deactivate "${companyName}"? This will revoke access but preserve the container and files.`)) return;
    setDeactivatingId(id);
    try {
      await deactivateThirdParty(instance, id);
      const data = await listThirdParties(instance);
      setParties(data.thirdParties ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Deactivation failed");
    } finally {
      setDeactivatingId(null);
    }
  };

  return (
    <div>
      <h1>Third-Party Onboarding</h1>

      <h2>Register New Third Party</h2>
      <form onSubmit={handleCreate}>
        <div className="form-group">
          <label htmlFor="companyName">Company Name</label>
          <input id="companyName" value={companyName} onChange={(e) => setCompanyName(e.target.value)} required />
        </div>
        <div className="form-group">
          <label htmlFor="contactEmail">Contact Email</label>
          <input id="contactEmail" type="email" value={contactEmail} onChange={(e) => setContactEmail(e.target.value)} required />
        </div>
        <div className="form-group">
          <label>
            <input type="checkbox" checked={enableAutomation} onChange={(e) => setEnableAutomation(e.target.checked)} />
            {" "}Enable Automation (App Registration + Certificate)
          </label>
        </div>
        <div className="form-group">
          <label htmlFor="userGroupId">User Group ID</label>
          <input id="userGroupId" value={userGroupId} onChange={(e) => setUserGroupId(e.target.value)} placeholder="Entra ID security group object ID" />
        </div>
        <div className="form-group">
          <label htmlFor="adminGroupId">Admin Group ID</label>
          <input id="adminGroupId" value={adminGroupId} onChange={(e) => setAdminGroupId(e.target.value)} placeholder="Entra ID security group object ID" />
        </div>
        {submitError && <p className="error">{submitError}</p>}
        <button type="submit" className="btn btn-primary" disabled={submitting}>
          {submitting ? "Provisioning..." : "Provision Third Party"}
        </button>
      </form>

      <h2>Registered Third Parties</h2>
      {error && <p className="error">{error}</p>}
      {loading ? (
        <p>Loading...</p>
      ) : parties.length === 0 ? (
        <p>No third parties registered.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Company</th>
              <th>Container</th>
              <th>Status</th>
              <th>User Group</th>
              <th>Admin Group</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {parties.map((p) => (
              <tr key={p.id}>
                <td>{p.companyName}</td>
                <td>{p.containerName}</td>
                <td><span className={`status status-${p.status}`}>{p.status}</span></td>
                <td>{p.userGroupId ? <code title={p.userGroupId}>{p.userGroupId.slice(0, 8)}…</code> : "—"}</td>
                <td>{p.adminGroupId ? <code title={p.adminGroupId}>{p.adminGroupId.slice(0, 8)}…</code> : "—"}</td>
                <td>{new Date(p.createdAt).toLocaleString()}</td>
                <td>
                  {p.status === "active" && (
                    <button
                      className="btn btn-danger"
                      disabled={deactivatingId === p.id}
                      onClick={() => handleDeactivate(p.id, p.companyName)}
                    >
                      {deactivatingId === p.id ? "Deactivating..." : "Deactivate"}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
