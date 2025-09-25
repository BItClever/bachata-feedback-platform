import React, { useEffect, useMemo, useState } from 'react';
import { eventReviewsAPI, EventReview } from '../services/api';
import { reportsAPI } from '../services/api';
import { ReasonBadge } from '../components/ReasonBadge';

interface EventReviewsPanelProps {
  eventId: number;
}

const EventReviewsPanel: React.FC<EventReviewsPanelProps> = ({ eventId }) => {
  const [reviews, setReviews] = useState<EventReview[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const fetchReviews = async () => {
    try {
      setLoading(true);
      setError('');
      const res = await eventReviewsAPI.getByEvent(eventId);
      setReviews(res.data);
    } catch (err: any) {
      console.error('Error loading event reviews:', err);
      setError('Failed to load event reviews.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchReviews();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eventId]);

  // Средние по каждому аспекту (на основе всех отзывов)
  const averages = useMemo(() => {
    const acc: Record<string, { sum: number; count: number }> = {};
    for (const r of reviews) {
      if (!r.ratings) continue;
      for (const [k, v] of Object.entries(r.ratings)) {
        if (!acc[k]) acc[k] = { sum: 0, count: 0 };
        acc[k].sum += v;
        acc[k].count += 1;
      }
    }
    const out: Record<string, number> = {};
    for (const [k, { sum, count }] of Object.entries(acc)) {
      out[k] = count ? sum / count : 0;
    }
    return out;
  }, [reviews]);

  if (loading) {
    return (
      <div className="p-4">
        <div className="animate-pulse text-gray-500">Loading event reviews...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-4 bg-red-50 border border-red-200 text-red-700 rounded">
        {error}
      </div>
    );
  }

  return (
    <div className="p-4 border-t border-gray-200">
      {reviews.length === 0 ? (
        <div className="text-gray-500">No reviews for this event yet.</div>
      ) : (
        <>
          {/* Средние по аспектам */}
          {Object.keys(averages).length > 0 && (
            <div className="mb-4">
              <h4 className="text-sm font-semibold text-gray-900 mb-2">Average by aspects</h4>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                {Object.entries(averages).map(([k, v]) => (
                  <div key={k} className="flex justify-between bg-gray-50 rounded px-3 py-2">
                    <span className="capitalize text-gray-700">{k}</span>
                    <span className="font-semibold text-gray-900">{v.toFixed(1)}/5</span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Последние отзывы */}
          <div>
            <h4 className="text-sm font-semibold text-gray-900 mb-2">Recent reviews</h4>
            <div className="space-y-3">
              {reviews.slice(0, 5).map((r) => (
                <div key={r.id} className="border border-gray-200 rounded p-3">
                  <div className="flex justify-between">
                    <div className="text-sm text-gray-800">
                      {r.isAnonymous ? 'Anonymous' : r.reviewerName}
                      {r.moderationLevel && (
                        <ReasonBadge
                          level={r.moderationLevel}
                          source={r.moderationSource}
                          reason={r.moderationReason}
                          reasonRu={r.moderationReasonRu}
                          reasonEn={r.moderationReasonEn}
                          className="ml-2"
                        />
                      )}
                    </div>
                    <div className="text-xs text-gray-500">{new Date(r.createdAt).toLocaleDateString()}</div>
                  </div>
                  {r.textReview && (
                    <p className="text-gray-700 text-sm mt-1">{r.textReview}</p>
                  )}
                  <div className="mt-2 flex items-center justify-between">
                    <div className="text-sm text-gray-600"></div>
                    <button
                      className="text-xs text-red-700 hover:underline"
                      onClick={async () => { try { await reportsAPI.create({ targetType: 'EventReview', targetId: r.id, reason: 'Inappropriate' }); alert('Report submitted'); } catch (e: any) { alert(e.response?.data?.message || 'Failed'); } }}>
                      Report
                    </button>
                  </div>
                  {r.ratings && (
                    <div className="mt-2 grid grid-cols-2 gap-1 text-xs text-gray-600">
                      {Object.entries(r.ratings).map(([k, v]) => (
                        <div key={k} className="flex justify-between bg-gray-50 px-2 py-1 rounded">
                          <span className="capitalize">{k}</span>
                          <span className="font-semibold">{v}/5</span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
};

export default EventReviewsPanel;