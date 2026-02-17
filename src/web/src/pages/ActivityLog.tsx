import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { useMsal } from "@azure/msal-react";
import { getContainerActivity, getAllActivity } from "../services/api";

interface ActivityRecord {
  action: string;
  fileName: string;
  directory: string;
  performedBy: string;
  sizeBytes: number;
  timestamp: string;
}

export default function ActivityLog() {
  const { containerName } = useParams<{ containerName: string }>();
  const { instance } = useMsal();
  const [records, setRecords] = useState<ActivityRecord[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchActivity = containerName
      ? getContainerActivity(instance, containerName)
      : getAllActivity(instance);

    fetchActivity
      .then((data) => setRecords(data.activity ?? []))
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [instance, containerName]);

  return (
    <div>
      <h1>Activity Log{containerName ? `: ${containerName}` : " (All)"}</h1>
      {loading ? (
        <p>Loading...</p>
      ) : records.length === 0 ? (
        <p>No activity recorded.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Time</th>
              <th>Action</th>
              <th>File</th>
              <th>Directory</th>
              <th>User</th>
            </tr>
          </thead>
          <tbody>
            {records.map((r, i) => (
              <tr key={i}>
                <td>{new Date(r.timestamp).toLocaleString()}</td>
                <td>{r.action}</td>
                <td>{r.fileName}</td>
                <td>{r.directory}</td>
                <td>{r.performedBy}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
