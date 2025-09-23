import React from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useEffect, useState } from 'react';
import { statsAPI } from '../services/api';

const Home: React.FC = () => {
  const { user } = useAuth();

  const [stats, setStats] = useState<{ totalUsers: number; totalReviews: number; totalEventReviews: number; totalEvents: number } | null>(null);
  useEffect(() => {
    (async () => {
      try {
        const r = await statsAPI.get();
        setStats(r.data);
      } catch { /* ignore for public */ }
    })();
  }, []);

  return (
    <div className="bg-gradient-to-br from-primary-50 to-pink-100 min-h-screen">
      <div className="max-w-7xl mx-auto px-4 py-16">
        <div className="text-center">
          <h1 className="text-4xl md:text-6xl font-bold text-gray-900 mb-6">
            Bachata Feedback
            <span className="text-primary-600"> Platform</span>
          </h1>
          <p className="text-xl text-gray-600 mb-8 max-w-3xl mx-auto">
            Share feedback, improve your dancing, and connect with the bachata community.
            Get constructive reviews from dance partners and track your progress.
          </p>

          {!user ? (
            <div className="space-x-4">
              <Link
                to="/register"
                className="btn-primary text-lg px-8 py-3"
              >
                Get Started
              </Link>
              <Link
                to="/login"
                className="btn-secondary text-lg px-8 py-3"
              >
                Sign In
              </Link>
            </div>
          ) : (
            <Link
              to="/dashboard"
              className="btn-primary text-lg px-8 py-3"
            >
              Go to Dashboard
            </Link>
          )}
        </div>

        {stats && (
          <div className="mt-10 grid grid-cols-2 md:grid-cols-3 gap-8">
            <div className="bg-white rounded-lg shadow p-4 text-center">
              <div className="text-2xl font-bold text-primary-600">{stats.totalUsers}</div>
              <div className="text-gray-600 text-sm">Users</div>
            </div>
            <div className="bg-white rounded-lg shadow p-4 text-center">
              <div className="text-2xl font-bold text-primary-600">{stats.totalReviews + stats.totalEventReviews}</div>
              <div className="text-gray-600 text-sm">Total reviews</div>
            </div>
            <div className="bg-white rounded-lg shadow p-4 text-center">
              <div className="text-2xl font-bold text-primary-600">{stats.totalEvents}</div>
              <div className="text-gray-600 text-sm">Events</div>
            </div>
          </div>
        )}

        {/* Features */}
        <div className="mt-20 grid md:grid-cols-3 gap-8">
          <div className="bg-white rounded-lg shadow-lg p-6 text-center">
            <div className="w-12 h-12 bg-primary-100 rounded-lg mx-auto mb-4 flex items-center justify-center">
              <svg className="w-6 h-6 text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 8h10m0 0V6a2 2 0 00-2-2H9a2 2 0 00-2 2v2m0 0v10a2 2 0 002 2h6a2 2 0 002-2V8" />
              </svg>
            </div>
            <h3 className="text-xl font-semibold mb-2">Anonymous Feedback</h3>
            <p className="text-gray-600">
              Give and receive honest feedback anonymously to improve your dancing without awkwardness.
            </p>
          </div>

          <div className="bg-white rounded-lg shadow-lg p-6 text-center">
            <div className="w-12 h-12 bg-primary-100 rounded-lg mx-auto mb-4 flex items-center justify-center">
              <svg className="w-6 h-6 text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v4a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
              </svg>
            </div>
            <h3 className="text-xl font-semibold mb-2">Track Progress</h3>
            <p className="text-gray-600">
              Monitor your improvement over time with detailed ratings and constructive comments.
            </p>
          </div>

          <div className="bg-white rounded-lg shadow-lg p-6 text-center">
            <div className="w-12 h-12 bg-primary-100 rounded-lg mx-auto mb-4 flex items-center justify-center">
              <svg className="w-6 h-6 text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
            </div>
            <h3 className="text-xl font-semibold mb-2">Community Events</h3>
            <p className="text-gray-600">
              Connect feedback to specific events and build a stronger dance community.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Home;