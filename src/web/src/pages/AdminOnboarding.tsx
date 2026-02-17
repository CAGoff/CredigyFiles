import { useEffect, useState } from "react";
import { useMsal } from "@azure/msal-react";
import { listThirdParties, createThirdParty } from "../services/api";

interface ThirdPartyInfo {
  id: string;
  companyName: string;
  containerName: string;
  status: string;
  createdAt: string;
}

export default function AdminOnboarding() {
  const { instance } = useMsal();
  const [parties, setParties] = useState<ThirdPartyInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [companyName, setCompanyName] = useState("");
  const [contactEmail, setContactEmail] = useState("");
  const [enableAutomation, setEnableAutomation] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

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
      await createThirdParty(instance, { companyName, contactEmail, enableAutomation });
      setCompanyName("");
      setContactEmail("");
      setEnableAutomation(false);
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
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            {parties.map((p) => (
              <tr key={p.id}>
                <td>{p.companyName}</td>
                <td>{p.containerName}</td>
                <td><span className={`status status-${p.status}`}>{p.status}</span></td>
                <td>{new Date(p.createdAt).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
