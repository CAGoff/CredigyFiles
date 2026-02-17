import { Routes, Route, Link } from "react-router-dom";
import {
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
} from "@azure/msal-react";
import { loginRequest } from "./services/auth";
import Dashboard from "./pages/Dashboard";
import FileBrowser from "./pages/FileBrowser";
import Upload from "./pages/Upload";
import ActivityLog from "./pages/ActivityLog";
import AdminOnboarding from "./pages/AdminOnboarding";
import "./App.css";

function App() {
  const { instance, accounts } = useMsal();
  const userName = accounts[0]?.name ?? accounts[0]?.username;

  const handleLogin = () => {
    instance.loginRedirect(loginRequest);
  };

  const handleLogout = () => {
    instance.logoutRedirect();
  };

  return (
    <div className="app">
      <nav className="navbar">
        <div className="nav-links">
          <Link to="/">Dashboard</Link>
          <Link to="/activity">Activity</Link>
          <Link to="/admin">Admin</Link>
        </div>
        <div className="nav-auth">
          <AuthenticatedTemplate>
            {userName && <span className="nav-user">{userName}</span>}
            <button onClick={handleLogout}>Logout</button>
          </AuthenticatedTemplate>
          <UnauthenticatedTemplate>
            <button onClick={handleLogin}>Login</button>
          </UnauthenticatedTemplate>
        </div>
      </nav>

      <main className="content">
        <AuthenticatedTemplate>
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/files/:containerName" element={<FileBrowser />} />
            <Route path="/upload/:containerName" element={<Upload />} />
            <Route path="/activity" element={<ActivityLog />} />
            <Route path="/activity/:containerName" element={<ActivityLog />} />
            <Route path="/admin" element={<AdminOnboarding />} />
          </Routes>
        </AuthenticatedTemplate>
        <UnauthenticatedTemplate>
          <div className="login-prompt">
            <h1>Secure File Transfer</h1>
            <p>Please log in to access the file transfer portal.</p>
            <button className="btn btn-primary" onClick={handleLogin}>Login with Microsoft</button>
          </div>
        </UnauthenticatedTemplate>
      </main>
    </div>
  );
}

export default App;
