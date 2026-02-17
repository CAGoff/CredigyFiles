import { useEffect, useState } from "react";
import { useMsal } from "@azure/msal-react";
import { listContainers } from "../services/api";
import { Link } from "react-router-dom";

export default function Dashboard() {
  const { instance } = useMsal();
  const [containers, setContainers] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    listContainers(instance)
      .then((data) => setContainers(data.containers ?? []))
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [instance]);

  if (loading) return <p>Loading...</p>;

  return (
    <div>
      <h1>Secure File Transfer</h1>
      <h2>Third-Party Containers</h2>
      {containers.length === 0 ? (
        <p>No containers available.</p>
      ) : (
        <ul>
          {containers.map((name) => (
            <li key={name}>
              <Link to={`/files/${name}`}>{name}</Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
