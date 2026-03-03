import { useState } from "react";
import { useMsal } from "@azure/msal-react";
import { useBranding } from "../hooks/useBranding";
import { updateBranding, uploadLogo, uploadFavicon } from "../services/api";

export default function AdminBranding() {
  const { instance } = useMsal();
  const { branding, refreshBranding } = useBranding();

  const [appName, setAppName] = useState(branding.appName);
  const [primaryColor, setPrimaryColor] = useState(branding.primaryColor);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);

  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [logoPreview, setLogoPreview] = useState<string | null>(null);
  const [uploadingLogo, setUploadingLogo] = useState(false);
  const [logoError, setLogoError] = useState<string | null>(null);

  const [faviconFile, setFaviconFile] = useState<File | null>(null);
  const [faviconPreview, setFaviconPreview] = useState<string | null>(null);
  const [uploadingFavicon, setUploadingFavicon] = useState(false);
  const [faviconError, setFaviconError] = useState<string | null>(null);

  const handleSaveSettings = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setSaveError(null);
    setSaveSuccess(false);
    try {
      await updateBranding(instance, { appName, primaryColor });
      await refreshBranding();
      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 3000);
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : "Failed to save settings");
    } finally {
      setSaving(false);
    }
  };

  const handleLogoSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setLogoFile(file);
    setLogoError(null);
    const reader = new FileReader();
    reader.onload = () => setLogoPreview(reader.result as string);
    reader.readAsDataURL(file);
  };

  const handleLogoUpload = async () => {
    if (!logoFile) return;
    setUploadingLogo(true);
    setLogoError(null);
    try {
      await uploadLogo(instance, logoFile);
      await refreshBranding();
      setLogoFile(null);
      setLogoPreview(null);
    } catch (err) {
      setLogoError(err instanceof Error ? err.message : "Failed to upload logo");
    } finally {
      setUploadingLogo(false);
    }
  };

  const handleFaviconSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setFaviconFile(file);
    setFaviconError(null);
    const reader = new FileReader();
    reader.onload = () => setFaviconPreview(reader.result as string);
    reader.readAsDataURL(file);
  };

  const handleFaviconUpload = async () => {
    if (!faviconFile) return;
    setUploadingFavicon(true);
    setFaviconError(null);
    try {
      await uploadFavicon(instance, faviconFile);
      await refreshBranding();
      setFaviconFile(null);
      setFaviconPreview(null);
    } catch (err) {
      setFaviconError(err instanceof Error ? err.message : "Failed to upload favicon");
    } finally {
      setUploadingFavicon(false);
    }
  };

  return (
    <div>
      <h1>Branding Settings</h1>

      <h2>App Name &amp; Color</h2>
      <form onSubmit={handleSaveSettings}>
        <div className="form-group">
          <label htmlFor="appName">App Name</label>
          <input
            id="appName"
            type="text"
            value={appName}
            onChange={(e) => setAppName(e.target.value)}
            required
          />
        </div>
        <div className="form-group">
          <label htmlFor="primaryColor">Primary Color</label>
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
            <input
              type="color"
              value={primaryColor}
              onChange={(e) => setPrimaryColor(e.target.value)}
              style={{ width: 48, height: 36, padding: 2, cursor: "pointer" }}
            />
            <input
              id="primaryColor"
              type="text"
              value={primaryColor}
              onChange={(e) => setPrimaryColor(e.target.value)}
              pattern="^#[0-9a-fA-F]{6}$"
              placeholder="#2563eb"
              style={{ maxWidth: 140 }}
            />
          </div>
        </div>
        {saveError && <p className="error">{saveError}</p>}
        {saveSuccess && <p style={{ color: "#34d399", fontSize: "0.875rem" }}>Settings saved successfully.</p>}
        <button type="submit" className="btn btn-primary" disabled={saving}>
          {saving ? "Saving..." : "Save Settings"}
        </button>
      </form>

      <h2>Company Logo</h2>
      <p style={{ color: "#a1a1aa", fontSize: "0.875rem", marginBottom: "0.75rem" }}>
        Displayed in the navigation bar. Accepts .png, .jpg, .svg (max 5 MB).
      </p>
      {branding.logoUrl && (
        <div style={{ marginBottom: "0.75rem" }}>
          <label style={{ display: "block", color: "#a1a1aa", fontSize: "0.8125rem", marginBottom: "0.25rem" }}>Current logo:</label>
          <img
            src={branding.logoUrl}
            alt="Current logo"
            style={{ height: 40, background: "#27272a", borderRadius: 6, padding: "0.25rem 0.5rem" }}
          />
        </div>
      )}
      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", marginBottom: "0.5rem" }}>
        <input
          type="file"
          accept=".png,.jpg,.jpeg,.svg"
          onChange={handleLogoSelect}
          style={{ fontSize: "0.875rem" }}
        />
        {logoPreview && (
          <img src={logoPreview} alt="Logo preview" style={{ height: 32, borderRadius: 4 }} />
        )}
      </div>
      {logoError && <p className="error">{logoError}</p>}
      <button
        className="btn btn-primary"
        disabled={!logoFile || uploadingLogo}
        onClick={handleLogoUpload}
      >
        {uploadingLogo ? "Uploading..." : "Upload Logo"}
      </button>

      <h2>Favicon</h2>
      <p style={{ color: "#a1a1aa", fontSize: "0.875rem", marginBottom: "0.75rem" }}>
        Displayed in the browser tab. Accepts .png, .ico (max 1 MB).
      </p>
      {branding.faviconUrl && (
        <div style={{ marginBottom: "0.75rem" }}>
          <label style={{ display: "block", color: "#a1a1aa", fontSize: "0.8125rem", marginBottom: "0.25rem" }}>Current favicon:</label>
          <img
            src={branding.faviconUrl}
            alt="Current favicon"
            style={{ height: 24, background: "#27272a", borderRadius: 4, padding: "0.15rem 0.35rem" }}
          />
        </div>
      )}
      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", marginBottom: "0.5rem" }}>
        <input
          type="file"
          accept=".png,.ico"
          onChange={handleFaviconSelect}
          style={{ fontSize: "0.875rem" }}
        />
        {faviconPreview && (
          <img src={faviconPreview} alt="Favicon preview" style={{ height: 24, borderRadius: 4 }} />
        )}
      </div>
      {faviconError && <p className="error">{faviconError}</p>}
      <button
        className="btn btn-primary"
        disabled={!faviconFile || uploadingFavicon}
        onClick={handleFaviconUpload}
      >
        {uploadingFavicon ? "Uploading..." : "Upload Favicon"}
      </button>
    </div>
  );
}
