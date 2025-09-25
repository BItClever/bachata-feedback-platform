import React, { useEffect, useState } from 'react';
import { usersAPIEx, User } from '../services/api';
import UserCard from '../components/UserCard';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

const Users: React.FC = () => {
  const [items, setItems] = useState<User[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize] = useState(12);
  const [total, setTotal] = useState(0);
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const { t } = useTranslation();

  const load = async () => {
    try {
      setIsLoading(true);
      const resp = await usersAPIEx.getUsersPaged({ page, pageSize, search });
      setItems(resp.data.items);
      setTotal(resp.data.total);
    } catch (error) {
      console.error('Error fetching users:', error);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [page, pageSize]);
  useEffect(() => {
    const id = setTimeout(() => { setPage(1); load(); }, 300); // debounce
    return () => clearTimeout(id);
    // eslint-disable-next-line
  }, [search]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin rounded-full h-32 w-32 border-b-2 border-primary-600"></div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 py-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-gray-900 mb-4">{t('usersList.title')}</h1>

        <div className="max-w-md">
          <input
            type="text"
            placeholder={t('usersList.searchPlaceholder') || 'Search users...'}
            className="input-field"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
      </div>

      {items.length === 0 ? (
        <div className="text-center py-8">
          <p className="text-gray-500">
            {search ? (t('usersList.emptyWithSearch') || 'No users found matching your search.') : (t('usersList.empty') || 'No users found.')}
          </p>
        </div>
      ) : (
        <>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {items.map((user) => (
              <Link key={user.id} to={`/users/${user.id}`} className="block">
                <UserCard user={user} />
              </Link>
            ))}
          </div>

          <div className="mt-6 flex items-center justify-center gap-2">
            <button
              className="btn-secondary"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
            >
              {t('common.prev')}
            </button>
            <span className="text-sm text-gray-700">
              {t('usersList.page', { page, total: totalPages })}
            </span>
            <button
              className="btn-secondary"
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
            >
              {t('common.next')}
            </button>
          </div>
        </>
      )}
    </div>
  );
};

export default Users;