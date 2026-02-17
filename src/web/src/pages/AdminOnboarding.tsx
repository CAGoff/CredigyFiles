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
  const [companyName, setCompanyName] = useState("");
  const [contactEmail, setContactEmail] = useState("");
  const [enableAutomation, setEnableAutomation] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const fetchParties = () => {
    setLoading(true);
    listThirdParties(instance)
      .then((data) => setParties(data.thirdParties ?? []))
      .catch(console.error)
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchParties();
  }, [instance]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      await createThirdParty(instance, { companyName, contactEmail, enableAutomation });
      setCompanyName("");
      setContactEmail("");
      setEnableAutomation(false);
      fetchParties();
    } catch (err) {
      console.error(err);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div>
      <h1>Third-Party Onboarding</h1>

      <h2>Register New Third Party</h2>
      <form onSubmit={handleCreate}>
        <div>
          <label>Company Name: </label>
          <input value={companyName} onChange={(e) => setCompanyName(e.target.value)} required />
        </div>
        <div>
          <label>Contact Email: </label>
          <input type="email" value={contactEmail} onChange={(e) => setContactEmail(e.target.value)} required />
        </div>
        <div>
          <label>
            <input type="checkbox" checked={enableAutomation} onChange={(e) => setEnableAutomation(e.target.checked)} />
            Enable Automation (App Registration + Certificate)
          </label>
        </div>
        <button type="submit" disabled={submitting}>
          {submitting ? "Provisioning..." : "Provision Third Party"}
        </button>
      </form>

      <h2>Registered Third Parties</h2>
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
                <td>{p.status}</td>
                <td>{new Date(p.createdAt).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
