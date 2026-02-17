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

function App() {
  const { instance } = useMsal();

  const handleLogin = () => {
    instance.loginRedirect(loginRequest);
  };

  const handleLogout = () => {
    instance.logoutRedirect();
  };

  return (
    <div>
      <nav>
        <Link to="/">Dashboard</Link>
        {" | "}
        <Link to="/activity">Activity</Link>
        {" | "}
        <Link to="/admin">Admin</Link>
        {" | "}
        <AuthenticatedTemplate>
          <button onClick={handleLogout}>Logout</button>
        </AuthenticatedTemplate>
        <UnauthenticatedTemplate>
          <button onClick={handleLogin}>Login</button>
        </UnauthenticatedTemplate>
      </nav>

      <main>
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
          <h1>Secure File Transfer</h1>
          <p>Please log in to access the file transfer portal.</p>
          <button onClick={handleLogin}>Login with Microsoft</button>
        </UnauthenticatedTemplate>
      </main>
    </div>
  );
}

export default App;
