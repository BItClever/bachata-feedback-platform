import React, { useState, useEffect } from 'react';
import { eventsAPI, usersAPI, reviewsAPI, Event, User } from '../services/api';
import { useAuth } from '../contexts/AuthContext';

interface ReviewModalProps {
  isOpen: boolean;
  onClose: () => void;
  onReviewSubmitted: () => void;
  preselectedUserId?: string;
}

type ReviewType = 'lead' | 'follow' | 'both';

const defaultLead = { technique: 0, musicality: 0, leading: 0, comfort: 0 };
const defaultFollow = { technique: 0, musicality: 0, following: 0, connection: 0 };

const ReviewModal: React.FC<ReviewModalProps> = ({
  isOpen,
  onClose,
  onReviewSubmitted,
  preselectedUserId
}) => {
  const { user: currentUser } = useAuth();
  const [events, setEvents] = useState<Event[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [selectedEventId, setSelectedEventId] = useState<string>(''); // optional
  const [selectedUserId, setSelectedUserId] = useState(preselectedUserId || '');
  const [reviewType, setReviewType] = useState<ReviewType>('both');
  const [leadRatings, setLeadRatings] = useState<Record<string, number>>(defaultLead);
  const [followRatings, setFollowRatings] = useState<Record<string, number>>(defaultFollow);
  const [textReview, setTextReview] = useState('');
  const [isAnonymous, setIsAnonymous] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (isOpen) {
      fetchEvents();
      fetchUsers();
    }
  }, [isOpen]);

  useEffect(() => {
    if (preselectedUserId) {
      setSelectedUserId(preselectedUserId);
    }
  }, [preselectedUserId]);

  const fetchEvents = async () => {
    try {
      const response = await eventsAPI.getEvents();
      setEvents(response.data);
    } catch (error) {
      console.error('Error fetching events:', error);
    }
  };

  const fetchUsers = async () => {
    try {
      const response = await usersAPI.getUsers();
      const filtered = response.data.filter(u => u.id !== currentUser?.id);
      setUsers(filtered);
    } catch (error) {
      console.error('Error fetching users:', error);
    }
  };

  const setRating = (category: 'lead' | 'follow', key: string, value: number) => {
    if (category === 'lead') setLeadRatings(prev => ({ ...prev, [key]: value }));
    else setFollowRatings(prev => ({ ...prev, [key]: value }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');

    try {
      const payload = {
        revieweeId: selectedUserId,
        eventId: selectedEventId ? Number(selectedEventId) : null,
        leadRatings: reviewType === 'follow' ? undefined : leadRatings,
        followRatings: reviewType === 'lead' ? undefined : followRatings,
        textReview: textReview || undefined,
        tags: undefined,
        isAnonymous
      };

      await reviewsAPI.createReview(payload);
      onReviewSubmitted();
      onClose();

      // reset
      setSelectedEventId('');
      setSelectedUserId(preselectedUserId || '');
      setReviewType('both');
      setLeadRatings(defaultLead);
      setFollowRatings(defaultFollow);
      setTextReview('');
      setIsAnonymous(false);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to submit review');
    } finally {
      setIsLoading(false);
    }
  };

  if (!isOpen) return null;

  const StarInput = ({
    value, onChange
  }: { value: number; onChange: (n: number) => void }) => (
    <div className="flex space-x-1">
      {[1,2,3,4,5].map(star => (
        <button
          type="button"
          key={star}
          onClick={() => onChange(star)}
          className={`w-6 h-6 ${star <= value ? 'text-yellow-400' : 'text-gray-300'} hover:text-yellow-400`}
          aria-label={`${star} stars`}
        >
          <svg fill="currentColor" viewBox="0 0 20 20">
            <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.519 4.674a1 1 0 00.95.69h4.915c.969 0 1.371 1.24.588 1.81l-3.976 2.888a1 1 0 00-.363 1.118l1.518 4.674c.3.922-.755 1.688-1.538 1.118l-3.976-2.888a1 1 0 00-1.176 0l-3.976 2.888c-.783.57-1.838-.197-1.538-1.118l1.518-4.674a1 1 0 00-.363-1.118l-3.976-2.888c-.784-.57-.38-1.81.588-1.81h4.914a1 1 0 00.951-.69l1.519-4.674z" />
          </svg>
        </button>
      ))}
    </div>
  );

  const renderBlock = (title: string, state: Record<string, number>, category: 'lead'|'follow') => (
    <div>
      <h3 className="text-lg font-medium text-gray-900 mb-3">{title}</h3>
      <div className="space-y-3">
        {Object.entries(state).map(([k,v]) => (
          <div key={k} className="flex items-center justify-between">
            <span className="capitalize text-gray-700">{k}</span>
            <StarInput value={v} onChange={(n) => setRating(category, k, n)} />
          </div>
        ))}
      </div>
    </div>
  );

  return (
    <div className="fixed inset-0 bg-gray-600 bg-opacity-50 overflow-y-auto h-full w-full z-50">
      <div className="relative top-20 mx-auto p-5 border w-11/12 md:w-3/4 lg:w-1/2 shadow-lg rounded-md bg-white">
        <div className="flex justify-between items-center mb-4">
          <h3 className="text-lg font-bold text-gray-900">Submit Review</h3>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {error && (
          <div className="bg-red-50 border border-red-400 text-red-700 px-4 py-3 rounded mb-4">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-5">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Review for
            </label>
            <select
              value={selectedUserId}
              onChange={(e) => setSelectedUserId(e.target.value)}
              required
              className="input-field"
              disabled={!!preselectedUserId}
            >
              <option value="">Select a dancer</option>
              {users.map(u => (
                <option key={u.id} value={u.id}>{u.firstName} {u.lastName}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Event (optional)
            </label>
            <select
              value={selectedEventId}
              onChange={(e) => setSelectedEventId(e.target.value)}
              className="input-field"
            >
              <option value="">No specific event</option>
              {events.map(ev => (
                <option key={ev.id} value={ev.id}>
                  {ev.name} - {new Date(ev.date).toLocaleDateString()}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">Review Type</label>
            <div className="flex gap-4">
              {(['lead','follow','both'] as ReviewType[]).map(rt => (
                <label key={rt} className="flex items-center gap-2">
                  <input
                    type="radio"
                    name="reviewType"
                    value={rt}
                    checked={reviewType === rt}
                    onChange={() => setReviewType(rt)}
                  />
                  <span className="capitalize">{rt}</span>
                </label>
              ))}
            </div>
          </div>

          {(reviewType === 'lead' || reviewType === 'both') && renderBlock('Lead Skills', leadRatings, 'lead')}
          {(reviewType === 'follow' || reviewType === 'both') && renderBlock('Follow Skills', followRatings, 'follow')}

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Written Feedback (optional)
            </label>
            <textarea
              className="input-field h-24"
              placeholder="Share constructive feedback..."
              value={textReview}
              onChange={(e) => setTextReview(e.target.value)}
            />
          </div>

          <div className="flex items-center">
            <input
              type="checkbox"
              id="isAnonymous"
              checked={isAnonymous}
              onChange={(e) => setIsAnonymous(e.target.checked)}
              className="h-4 w-4 text-primary-600 focus:ring-primary-500 border-gray-300 rounded"
            />
            <label htmlFor="isAnonymous" className="ml-2 block text-sm text-gray-900">
              Submit anonymously
            </label>
          </div>

          <div className="flex gap-4 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1">Cancel</button>
            <button type="submit" disabled={isLoading} className="btn-primary flex-1">
              {isLoading ? 'Submitting...' : 'Submit Review'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default ReviewModal;