import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

interface LayoutProps {
  children: React.ReactNode;
}

const Layout: React.FC<LayoutProps> = ({ children }) => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  const isAdmin = !!user?.roles?.includes('Admin');
  const isMod = !!user?.roles?.some(r => r === 'Admin' || r === 'Moderator');

  const NavLinks = () => (
    <>
      {user ? (
        <>
          <Link to="/dashboard" className="nav-link">Dashboard</Link>
          <Link to="/users" className="nav-link">Users</Link>
          <Link to="/events" className="nav-link">Events</Link>
          {isAdmin && <Link to="/admin/roles" className="nav-link">Admin</Link>}
          {isMod && <Link to="/admin/moderation" className="nav-link">Moderation</Link>}
          <Link to="/profile" className="nav-link">{user.firstName} {user.lastName}</Link>
          <Link to="/faq" className="nav-link">FAQ</Link>
          <button onClick={handleLogout} className="btn-primary px-4 py-2">Logout</button>
        </>
      ) : (
        <>
          <Link to="/login" className="nav-link">Login</Link>
          <Link to="/register" className="btn-primary px-4 py-2">Register</Link>
          <Link to="/faq" className="nav-link">FAQ</Link>
        </>
      )}
    </>
  );

  return (
    <div className="min-h-screen bg-gray-50">
      <nav className="bg-white shadow-lg">
        <div className="max-w-7xl mx-auto px-4">
          <div className="flex justify-between h-16">
            <div className="flex items-center">
              <Link to="/" className="flex-shrink-0">
                <h1 className="text-xl font-bold text-primary-600">
                  Bachata Feedback
                </h1>
              </Link>
            </div>

            {/* Desktop */}
            <div className="hidden md:flex items-center space-x-4">
              <NavLinks />
            </div>

            {/* Mobile burger */}
            <div className="flex items-center md:hidden">
              <button
                onClick={() => setOpen(v => !v)}
                className="inline-flex items-center justify-center p-2 rounded-md text-gray-600 hover:bg-gray-100 focus:outline-none"
                aria-label="Menu"
              >
                {open ? '✖' : '☰'}
              </button>
            </div>
          </div>
        </div>

        {/* Mobile panel */}
        {open && (
          <div className="md:hidden border-t border-gray-200">
            <div className="px-4 py-3 flex flex-col space-y-2">
              <NavLinks />
            </div>
          </div>
        )}
      </nav>

      <main className="flex-1">{children}</main>

      <footer className="bg-gray-800 text-white">
        <div className="max-w-7xl mx-auto py-4 px-4 text-center">
          <p>&copy; 2025 Bachata Feedback Platform. All rights reserved.</p>
        </div>
      </footer>

      {/* small styles */}
      <style>{`
        .nav-link {
          @apply text-gray-700 hover:text-primary-600 px-3 py-2 rounded-md text-sm font-medium;
        }
      `}</style>
    </div>
  );
};

export default Layout;