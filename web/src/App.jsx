import React, { useState, useEffect } from 'react';

const THEME_KEY = 'credigy-theme';

export default function App() {
  const [theme, setTheme] = useState('light');

  useEffect(() => {
    const saved = localStorage.getItem(THEME_KEY);
    if (saved === 'dark' || saved === 'light') {
      setTheme(saved);
      document.documentElement.dataset.theme = saved;
    }
  }, []);

  const toggleTheme = () => {
    const next = theme === 'light' ? 'dark' : 'light';
    setTheme(next);
    document.documentElement.dataset.theme = next;
    localStorage.setItem(THEME_KEY, next);
  };

  return (
    <div className="page">
      <header className="header">
        <div>
          <div className="eyebrow">Credigy Files</div>
          <h1>Secure Vendor Exchange</h1>
          <p className="lede">
            Sign in with your Entra ID guest or internal account to access the Inbound/Outbound folders for your vendor
            containers.
          </p>
        </div>
        <button className="theme-toggle" onClick={toggleTheme}>
          {theme === 'light' ? '🌙 Dark' : '☀️ Light'}
        </button>
      </header>

      <main className="panel">
        <section className="card">
          <h2>Next steps</h2>
          <ul>
            <li>Wire OIDC login for users and admins.</li>
            <li>Call the API for container browsing, upload/download, and audit.</li>
            <li>Limit listings to Hot tier blobs and enforce role-based permissions.</li>
          </ul>
        </section>

        <section className="card">
          <h2>Admin portal</h2>
          <p>Manage group → container mappings, limits, and review audit entries.</p>
          <a className="link" href="/admin">Go to admin portal</a>
        </section>
      </main>
    </div>
  );
}
