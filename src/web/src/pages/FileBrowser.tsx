import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
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

  useEffect(() => {
    if (!containerName) return;
    setLoading(true);
    listFiles(instance, containerName, dir)
      .then((data) => setFiles(data.files ?? []))
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [instance, containerName, dir]);

  const handleDownload = async (fileName: string) => {
    if (!containerName) return;
    const blob = await downloadFile(instance, containerName, fileName, dir);
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleDelete = async (fileName: string) => {
    if (!containerName || !confirm(`Delete ${fileName}?`)) return;
    await deleteFile(instance, containerName, fileName, dir);
    setFiles((prev) => prev.filter((f) => f.fileName !== fileName));
  };

  return (
    <div>
      <h1>{containerName}</h1>
      <div>
        <button onClick={() => setDir("inbound")} disabled={dir === "inbound"}>
          Inbound
        </button>
        <button onClick={() => setDir("outbound")} disabled={dir === "outbound"}>
          Outbound
        </button>
      </div>

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
                <td>
                  <button onClick={() => handleDownload(f.fileName)}>Download</button>
                  <button onClick={() => handleDelete(f.fileName)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
