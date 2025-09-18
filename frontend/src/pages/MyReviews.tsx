import React, { useEffect, useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { reviewsAPI, Review } from '../services/api';
import { reportsAPI } from '../services/api';
import { Link } from 'react-router-dom';

const badge = (level?: string) =>
  level === 'Red' ? 'bg-red-100 text-red-800' :
  level === 'Yellow' ? 'bg-yellow-100 text-yellow-800' :
  level === 'Green' ? 'bg-green-100 text-green-800' :
  'bg-gray-200 text-gray-700';

const MyReviews: React.FC = () => {
  const { user } = useAuth();
  const [received, setReceived] = useState<Review[]>([]);
  const [given, setGiven] = useState<Review[]>([]);
  const [tab, setTab] = useState<'received'|'given'>('received');
  const [error, setError] = useState('');

  const load = async () => {
    if (!user) return;
    try {
      setError('');
      const [r1, r2] = await Promise.all([
        reviewsAPI.getUserReviews(user.id),
        reviewsAPI.getMyGiven()
      ]);
      setReceived(r1.data);
      setGiven(r2.data);
    } catch (e:any) {
      setError(e.response?.data?.message || 'Failed to load reviews');
    }
  };

  useEffect(()=>{ load(); }, [user]);

  const report = async (id: number, type: 'Review') => {
    try { await reportsAPI.create({ targetType: type, targetId: id, reason: 'Inappropriate' }); alert('Report submitted'); }
    catch (e:any) { alert(e.response?.data?.message || 'Failed'); }
  };

  const list = (items: Review[]) => (
    items.length === 0 ? <div className="p-6 text-gray-500">No reviews.</div> :
    <div className="divide-y">
      {items.map(r => (
        <div key={r.id} className="px-6 py-4">
          <div className="flex justify-between items-start">
            <div className="text-sm text-gray-800">
              {(tab==='received') ? (r.reviewerName || 'Anonymous') : (r.revieweeName || '')}
              {r.moderationLevel && (
                <span
                  title={r.moderationReason ? `Moderation: ${r.moderationLevel} • ${(r.moderationSource==='LLM'?'AI':r.moderationSource)} • ${r.moderationReason}` : `Moderation: ${r.moderationLevel}`}
                  className={`ml-2 text-xs px-2 py-0.5 rounded ${badge(r.moderationLevel)}`}>
                  {r.moderationLevel || 'Pending'}
                </span>
              )}
            </div>
            <div className="text-xs text-gray-500">{new Date(r.createdAt).toLocaleDateString()}</div>
          </div>
          {r.textReview ? <p className="text-gray-700 mt-1">{r.textReview}</p> : <p className="text-gray-400 mt-1">Hidden by privacy settings</p>}
          <div className="mt-2 flex items-center justify-between">
            <div className="text-sm text-gray-600">
              {(() => {
                const parts:number[] = [];
                const l = r.leadRatings ? Object.values(r.leadRatings) : [];
                const f = r.followRatings ? Object.values(r.followRatings) : [];
                if (l.length) parts.push(l.reduce((a,b)=>a+b,0)/l.length);
                if (f.length) parts.push(f.reduce((a,b)=>a+b,0)/f.length);
                return parts.length ? <span className="font-semibold">{(parts.reduce((a,b)=>a+b,0)/parts.length).toFixed(1)}/5</span> : null;
              })()}
            </div>
            {r.moderationSource === 'LLM' && (
              <button className="text-xs text-red-700 hover:underline" onClick={()=>report(r.id, 'Review')}>Report</button>
            )}
          </div>
        </div>
      ))}
    </div>
  );

  return (
    <div className="max-w-6xl mx-auto px-4 py-8">
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-bold">My Reviews</h1>
        <Link to="/dashboard" className="btn-secondary">Back</Link>
      </div>
      {error && <div className="mb-4 bg-red-50 border border-red-200 text-red-800 px-4 py-2 rounded">{error}</div>}
      <div className="bg-white rounded shadow">
        <div className="px-6 py-4 border-b border-gray-200 flex gap-2">
          <button className={`btn-secondary ${tab==='received' ? 'ring-2 ring-primary-400' : ''}`} onClick={()=>setTab('received')}>Received</button>
          <button className={`btn-secondary ${tab==='given' ? 'ring-2 ring-primary-400' : ''}`} onClick={()=>setTab('given')}>Given</button>
        </div>
        {tab==='received' ? list(received) : list(given)}
      </div>
    </div>
  );
};

export default MyReviews;