import { Routes, Route, Link } from "react-router-dom";
import {
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
  useIsAuthenticated,
} from "@azure/msal-react";
import { loginRequest } from "./services/auth";
import { useBranding } from "./hooks/useBranding";
import { useIsAdmin } from "./hooks/useUserRoles";
import Dashboard from "./pages/Dashboard";
import FileBrowser from "./pages/FileBrowser";
import Upload from "./pages/Upload";
import ActivityLog from "./pages/ActivityLog";
import AdminOnboarding from "./pages/AdminOnboarding";
import AdminBranding from "./pages/AdminBranding";
import "./App.css";

const DEV_AUTH = import.meta.env.VITE_DEV_AUTH === "true";

function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<Dashboard />} />
      <Route path="/files/:containerName" element={<FileBrowser />} />
      <Route path="/upload/:containerName" element={<Upload />} />
      <Route path="/activity" element={<ActivityLog />} />
      <Route path="/activity/:containerName" element={<ActivityLog />} />
      <Route path="/admin" element={<AdminOnboarding />} />
      <Route path="/admin/branding" element={<AdminBranding />} />
    </Routes>
  );
}

function NavLinks() {
  const isAdmin = useIsAdmin();

  return (
    <div className="nav-links">
      <Link to="/">Dashboard</Link>
      <Link to="/activity">Activity</Link>
      {(DEV_AUTH || isAdmin) && (
        <>
          <Link to="/admin">Onboarding</Link>
          <Link to="/admin/branding">Branding</Link>
        </>
      )}
    </div>
  );
}

function App() {
  const { instance, accounts } = useMsal();
  const msalAuthenticated = useIsAuthenticated();
  const isAuthenticated = DEV_AUTH || msalAuthenticated;
  const { branding } = useBranding();
  const userName = DEV_AUTH ? "dev@localhost" : (accounts[0]?.name ?? accounts[0]?.username);

  const handleLogin = () => {
    instance.loginRedirect(loginRequest);
  };

  const handleLogout = () => {
    instance.logoutRedirect();
  };

  return (
    <div className="app">
      <nav className="navbar">
        <div style={{ display: "flex", alignItems: "center" }}>
          <Link to="/" className="nav-brand">
            {branding.logoUrl && <img src={branding.logoUrl} alt={branding.appName} />}
            {branding.appName}
          </Link>
          {isAuthenticated && <NavLinks />}
        </div>
        <div className="nav-auth">
          {DEV_AUTH ? (
            <>
              <span className="nav-user">{userName}</span>
              <span className="nav-dev-badge">DEV</span>
            </>
          ) : (
            <>
              <AuthenticatedTemplate>
                {userName && <span className="nav-user">{userName}</span>}
                <button onClick={handleLogout}>Logout</button>
              </AuthenticatedTemplate>
              <UnauthenticatedTemplate>
                <button onClick={handleLogin}>Login</button>
              </UnauthenticatedTemplate>
            </>
          )}
        </div>
      </nav>

      <main className="content">
        {DEV_AUTH ? (
          <AppRoutes />
        ) : (
          <>
            <AuthenticatedTemplate>
              <AppRoutes />
            </AuthenticatedTemplate>
            <UnauthenticatedTemplate>
              <div className="login-prompt">
                <h1>{branding.appName}</h1>
                <p>Secure file transfer portal</p>
                <p className="login-subtitle">Sign in to access your files and containers.</p>
                <button className="btn btn-primary" onClick={handleLogin}>Login with Microsoft</button>
              </div>
            </UnauthenticatedTemplate>
          </>
        )}
      </main>
    </div>
  );
}

export default App;
