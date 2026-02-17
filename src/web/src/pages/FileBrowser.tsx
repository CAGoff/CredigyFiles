import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { useMsal } from "@azure/msal-react";
import { listFiles, downloadFile, deleteFile } from "../services/api";

interface TransferFile {
  fileName: string;
  sizeBytes: number;
  uploadedAt: string;
  accessTier: string;
}

export default function FileBrowser() {
  const { containerName } = useParams<{ containerName: string }>();
  const { instance } = useMsal();
  const [dir, setDir] = useState<"inbound" | "outbound">("inbound");
  const [files, setFiles] = useState<TransferFile[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!containerName) return;
    let cancelled = false;
    listFiles(instance, containerName, dir)
      .then((data) => {
        if (!cancelled) setFiles(data.files ?? []);
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : "Failed to load files");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [instance, containerName, dir]);

  const switchDir = (next: "inbound" | "outbound") => {
    setLoading(true);
    setError(null);
    setDir(next);
  };

  const handleDownload = async (fileName: string) => {
    if (!containerName) return;
    try {
      const blob = await downloadFile(instance, containerName, fileName, dir);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Download failed");
    }
  };

  const handleDelete = async (fileName: string) => {
    if (!containerName || !confirm(`Delete ${fileName}?`)) return;
    try {
      await deleteFile(instance, containerName, fileName, dir);
      setFiles((prev) => prev.filter((f) => f.fileName !== fileName));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Delete failed");
    }
  };

  return (
    <div>
      <h1>{containerName}</h1>
      <div className="toolbar">
        <div className="button-group">
          <button onClick={() => switchDir("inbound")} disabled={dir === "inbound"}>
            Inbound
          </button>
          <button onClick={() => switchDir("outbound")} disabled={dir === "outbound"}>
            Outbound
          </button>
        </div>
        <Link to={`/upload/${containerName}`} className="btn btn-primary">Upload File</Link>
      </div>

      {error && <p className="error">{error}</p>}

      {loading ? (
        <p>Loading...</p>
      ) : files.length === 0 ? (
        <p>No files in {dir}/</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>File Name</th>
              <th>Size</th>
              <th>Uploaded</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {files.map((f) => (
              <tr key={f.fileName}>
                <td>{f.fileName}</td>
                <td>{(f.sizeBytes / 1024).toFixed(1)} KB</td>
                <td>{new Date(f.uploadedAt).toLocaleString()}</td>
                <td className="actions">
                  <button onClick={() => handleDownload(f.fileName)}>Download</button>
                  <button className="btn-danger" onClick={() => handleDelete(f.fileName)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
