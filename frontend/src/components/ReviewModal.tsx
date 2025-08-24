import React, { useState, useEffect } from 'react';
import { eventsAPI, usersAPI, reviewsAPI, Event, User } from '../services/api';
import { useAuth } from '../contexts/AuthContext';

interface ReviewModalProps {
  isOpen: boolean;
  onClose: () => void;
  onReviewSubmitted: () => void;
  preselectedUserId?: string;
}

const ReviewModal: React.FC<ReviewModalProps> = ({
  isOpen,
  onClose,
  onReviewSubmitted,
  preselectedUserId
}) => {
  const { user: currentUser } = useAuth();
  const [events, setEvents] = useState<Event[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [selectedEventId, setSelectedEventId] = useState<number | null>(null);
  const [selectedUserId, setSelectedUserId] = useState(preselectedUserId || '');
  const [isAnonymous, setIsAnonymous] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  // аспекты по умолчанию
  const [leadRatings, setLeadRatings] = useState<{ [key: string]: number }>({
    technique: 0,
    musicality: 0,
    leading: 0,
    connection: 0,
  });

  const [followRatings, setFollowRatings] = useState<{ [key: string]: number }>({
    technique: 0,
    musicality: 0,
    following: 0,
    connection: 0,
  });

  const [textReview, setTextReview] = useState('');

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

  const setLead = (key: string, value: number) =>
    setLeadRatings(prev => ({ ...prev, [key]: value }));

  const setFollow = (key: string, value: number) =>
    setFollowRatings(prev => ({ ...prev, [key]: value }));

  const Star = ({ value, active, onClick }: { value: number; active: boolean; onClick: () => void }) => (
    <button type="button" onClick={onClick} className={active ? 'text-yellow-400' : 'text-gray-300'}>
      <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 20 20">
        <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.519 4.674a1 1 0 00.95.69h4.915c.969 0 1.371 1.24.588 1.81l-3.976 2.888a1 1 0 00-.363 1.118l1.518 4.674c.3.922-.755 1.688-1.538 1.118l-3.976-2.888a1 1 0 00-1.176 0l-3.976 2.888c-.783.57-1.838-.197-1.538-1.118l1.518-4.674a1 1 0 00-.363-1.118l-3.976-2.888c-.784-.57-.38-1.81.588-1.81h4.914a1 1 0 00.951-.69l1.519-4.674z" />
      </svg>
    </button>
  );

  const Stars = ({ current, onChange }: { current: number; onChange: (v: number) => void }) => (
    <div className="flex space-x-1">
      {[1,2,3,4,5].map(n => (
        <Star key={n} value={n} active={n <= current} onClick={() => onChange(n)} />
      ))}
    </div>
  );

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');

    try {
      const data = {
        revieweeId: selectedUserId,
        eventId: selectedEventId ?? undefined, // опционально
        leadRatings: Object.values(leadRatings).some(v => v > 0) ? leadRatings : undefined,
        followRatings: Object.values(followRatings).some(v => v > 0) ? followRatings : undefined,
        textReview: textReview || undefined,
        isAnonymous
      };

      await reviewsAPI.createReview(data);
      onReviewSubmitted();
      onClose();

      // reset
      setSelectedEventId(null);
      setLeadRatings({ technique: 0, musicality: 0, leading: 0, connection: 0 });
      setFollowRatings({ technique: 0, musicality: 0, following: 0, connection: 0 });
      setTextReview('');
      setIsAnonymous(false);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to submit review');
    } finally {
      setIsLoading(false);
    }
  };

  if (!isOpen) return null;

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

        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Event (optional) */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">Event (optional)</label>
            <select
              value={selectedEventId ?? ''}
              onChange={(e) => setSelectedEventId(e.target.value ? Number(e.target.value) : null)}
              className="input-field"
            >
              <option value="">No event</option>
              {events.map((event) => (
                <option key={event.id} value={event.id}>
                  {event.name} - {new Date(event.date).toLocaleDateString()}
                </option>
              ))}
            </select>
          </div>

          {/* User */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">Review for</label>
            <select
              value={selectedUserId}
              onChange={(e) => setSelectedUserId(e.target.value)}
              required
              className="input-field"
              disabled={!!preselectedUserId}
            >
              <option value="">Select a dancer</option>
              {users.map((user) => (
                <option key={user.id} value={user.id}>
                  {user.firstName} {user.lastName}
                </option>
              ))}
            </select>
          </div>

          {/* Lead ratings */}
          <div>
            <h4 className="text-md font-semibold text-gray-800 mb-2">Lead skills</h4>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              {Object.keys(leadRatings).map(key => (
                <div key={key} className="flex items-center justify-between">
                  <span className="capitalize text-gray-700">{key}</span>
                  <Stars current={leadRatings[key]} onChange={(v) => setLead(key, v)} />
                </div>
              ))}
            </div>
          </div>

          {/* Follow ratings */}
          <div>
            <h4 className="text-md font-semibold text-gray-800 mb-2">Follow skills</h4>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              {Object.keys(followRatings).map(key => (
                <div key={key} className="flex items-center justify-between">
                  <span className="capitalize text-gray-700">{key}</span>
                  <Stars current={followRatings[key]} onChange={(v) => setFollow(key, v)} />
                </div>
              ))}
            </div>
          </div>

          {/* Text */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">Written feedback (optional)</label>
            <textarea
              className="input-field h-24"
              placeholder="Share constructive feedback..."
              value={textReview}
              onChange={(e) => setTextReview(e.target.value)}
            />
          </div>

          {/* Anonymous */}
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

          {/* Actions */}
          <div className="flex space-x-4 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1">
              Cancel
            </button>
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