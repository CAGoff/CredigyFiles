import { useEffect, useState } from "react";
import { useMsal } from "@azure/msal-react";
import { listContainers } from "../services/api";
import { Link } from "react-router-dom";

export default function Dashboard() {
  const { instance } = useMsal();
  const [containers, setContainers] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    listContainers(instance)
      .then((data) => setContainers(data.containers ?? []))
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load containers"))
      .finally(() => setLoading(false));
  }, [instance]);

  if (loading) return <p>Loading...</p>;
  if (error) return <p className="error">{error}</p>;

  return (
    <div>
      <h1>Secure File Transfer</h1>
      <h2>Third-Party Containers</h2>
      {containers.length === 0 ? (
        <p>No containers available.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Container</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {containers.map((name) => (
              <tr key={name}>
                <td>{name}</td>
                <td className="actions">
                  <Link to={`/files/${name}`}>Files</Link>
                  <Link to={`/upload/${name}`}>Upload</Link>
                  <Link to={`/activity/${name}`}>Activity</Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
