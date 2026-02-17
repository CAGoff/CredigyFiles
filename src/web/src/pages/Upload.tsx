import { useState, useRef } from "react";
import { useParams } from "react-router-dom";
import { useMsal } from "@azure/msal-react";
import { uploadFile } from "../services/api";

export default function Upload() {
  const { containerName } = useParams<{ containerName: string }>();
  const { instance } = useMsal();
  const [dir, setDir] = useState<"inbound" | "outbound">("inbound");
  const [uploading, setUploading] = useState(false);
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleUpload = async () => {
    if (!containerName) return;
    const file = fileInputRef.current?.files?.[0];
    if (!file) return;

    setUploading(true);
    setResult(null);
    setError(null);

    try {
      const response = await uploadFile(instance, containerName, dir, file);
      setResult(`Uploaded: ${response.fileName} (${response.sizeBytes} bytes)`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Upload failed");
    } finally {
      setUploading(false);
    }
  };

  return (
    <div>
      <h1>Upload to {containerName}</h1>
      <div>
        <label>
          Direction:
          <select value={dir} onChange={(e) => setDir(e.target.value as "inbound" | "outbound")}>
            <option value="inbound">Inbound (to third party)</option>
            <option value="outbound">Outbound (from third party)</option>
          </select>
        </label>
      </div>
      <div>
        <input
          type="file"
          ref={fileInputRef}
          accept=".pdf,.xlsx,.xls,.csv,.txt"
        />
        <button onClick={handleUpload} disabled={uploading}>
          {uploading ? "Uploading..." : "Upload"}
        </button>
      </div>
      {result && <p style={{ color: "green" }}>{result}</p>}
      {error && <p style={{ color: "red" }}>{error}</p>}
    </div>
  );
}
