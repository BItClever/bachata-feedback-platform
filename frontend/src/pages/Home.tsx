import React from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useEffect, useState } from 'react';
import { statsAPI } from '../services/api';
import '../i18n';
import { useTranslation } from 'react-i18next';

const Home: React.FC = () => {
  const { user } = useAuth();
  const { t } = useTranslation();

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
            {t('home.title')}
          </h1>
          <p className="text-xl text-gray-600 mb-8 max-w-3xl mx-auto">
            {t('home.subtitle')}
          </p>

          {!user ? (
            <div className="space-x-4">
              <Link to="/register" className="btn-primary text-lg px-8 py-3">
                {t('home.cta.getStarted')}
              </Link>
              <Link to="/login" className="btn-secondary text-lg px-8 py-3">
                {t('home.cta.signIn')}
              </Link>
            </div>
          ) : (
            <Link to="/dashboard" className="btn-primary text-lg px-8 py-3">
              {t('home.cta.goDashboard')}
            </Link>
          )}
        </div>

        {stats && (
          <div className="mt-10 grid grid-cols-2 md:grid-cols-3 gap-8">
            <div className="bg-white rounded-lg shadow p-4 text-center">
              <div className="text-2xl font-bold text-primary-600">{stats.totalUsers}</div>
              <div className="text-gray-600 text-sm">{t('home.stats.users')}</div>
            </div>
            <div className="bg-white rounded-lg shadow p-4 text-center">
              <div className="text-2xl font-bold text-primary-600">{stats.totalReviews + stats.totalEventReviews}</div>
              <div className="text-gray-600 text-sm">{t('home.stats.totalReviews')}</div>
            </div>
            <div className="bg-white rounded-lg shadow p-4 text-center">
              <div className="text-2xl font-bold text-primary-600">{stats.totalEvents}</div>
              <div className="text-gray-600 text-sm">{t('home.stats.events')}</div>
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
            <h3 className="text-xl font-semibold mb-2">{t('home.features.anonymousTitle')}</h3>
            <p className="text-gray-600">{t('home.features.anonymousText')}</p>
          </div>

          <div className="bg-white rounded-lg shadow-lg p-6 text-center">
            <div className="w-12 h-12 bg-primary-100 rounded-lg mx-auto mb-4 flex items-center justify-center">
              <svg className="w-6 h-6 text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v4a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
              </svg>
            </div>
            <h3 className="text-xl font-semibold mb-2">{t('home.features.progressTitle')}</h3>
            <p className="text-gray-600">{t('home.features.progressText')}</p>
          </div>

          <div className="bg-white rounded-lg shadow-lg p-6 text-center">
            <div className="w-12 h-12 bg-primary-100 rounded-lg mx-auto mb-4 flex items-center justify-center">
              <svg className="w-6 h-6 text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
            </div>
            <h3 className="text-xl font-semibold mb-2">{t('home.features.eventsTitle')}</h3>
            <p className="text-gray-600">{t('home.features.eventsText')}</p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Home;