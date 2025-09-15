import React, { useEffect, useState } from 'react';
import { moderationAdminAPI } from '../services/api';
import { useAuth } from '../contexts/AuthContext';

const AdminModeration: React.FC = () => {
  const { user } = useAuth();
  const [jobs, setJobs] = useState<any[]>([]);
  const [error, setError] = useState('');

  const load = async () => {
    try {
      const res = await moderationAdminAPI.getJobs();
      setJobs(res.data);
    } catch (e:any) { setError(e.response?.data?.message || 'Failed to load'); }
  };

  useEffect(()=>{ load(); }, []);

  const setLevel = async (type: 'Review'|'EventReview', id: number, level: 'Green'|'Yellow'|'Red') => {
    try {
      if (type==='Review') await moderationAdminAPI.setReviewLevel(id, level);
      else await moderationAdminAPI.setEventReviewLevel(id, level);
      await load();
    } catch (e:any) { setError(e.response?.data?.message || 'Failed'); }
  };

  const requeue = async (type: 'Review'|'EventReview', id: number) => {
    try { await moderationAdminAPI.requeue(type, id); await load(); }
    catch (e:any) { setError(e.response?.data?.message || 'Failed'); }
  };

  const canSee = !!user?.roles?.some(r => r==='Admin' || r==='Moderator');
  if (!canSee) return <div className="p-8">Access denied</div>;

  return (
    <div className="max-w-7xl mx-auto px-4 py-8">
      <h1 className="text-2xl font-bold mb-4">Moderation</h1>
      {error && <div className="mb-4 bg-red-50 border border-red-200 text-red-800 px-4 py-2 rounded">{error}</div>}
      <div className="bg-white rounded shadow">
        <div className="divide-y">
          {jobs.map(j => (
            <div key={j.id} className="p-4 flex items-center justify-between">
              <div>
                <div className="font-medium">{j.targetType} #{j.targetId}</div>
                <div className="text-sm text-gray-500">{j.status} • attempts: {j.attempts} • {new Date(j.createdAt).toLocaleString()}</div>
                {j.lastError && <div className="text-sm text-red-600">Last error: {j.lastError}</div>}
              </div>
              <div className="flex gap-2">
                <button className="btn-secondary" onClick={()=>setLevel(j.targetType, j.targetId, 'Green')}>Set Green</button>
                <button className="btn-secondary" onClick={()=>setLevel(j.targetType, j.targetId, 'Yellow')}>Set Yellow</button>
                <button className="btn-secondary" onClick={()=>setLevel(j.targetType, j.targetId, 'Red')}>Set Red</button>
                <button className="btn-primary" onClick={()=>requeue(j.targetType, j.targetId)}>Requeue</button>
              </div>
            </div>
          ))}
          {jobs.length===0 && <div className="p-4 text-gray-500">No jobs</div>}
        </div>
      </div>
    </div>
  );
};

export default AdminModeration;