import React, { useState } from 'react';
import { eventReviewsAPI } from '../services/api';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  eventId: number;
  onSubmitted: () => void;
}

const defaultRatings = { location: 0, music: 0, crowd: 0, organization: 0 };

const EventReviewModal: React.FC<Props> = ({ isOpen, onClose, eventId, onSubmitted }) => {
  const [ratings, setRatings] = useState<Record<string, number>>(defaultRatings);
  const [textReview, setTextReview] = useState('');
  const [isAnonymous, setIsAnonymous] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  if (!isOpen) return null;

  const setRating = (k: string, v: number) =>
    setRatings(prev => ({ ...prev, [k]: v }));

  const StarInput = ({ value, onChange }: { value: number; onChange: (n:number)=>void }) => (
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

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');
    try {
      await eventReviewsAPI.create({
        eventId,
        ratings,
        textReview: textReview || undefined,
        tags: undefined,
        isAnonymous,
      });
      onSubmitted();
      onClose();
      setRatings(defaultRatings);
      setTextReview('');
      setIsAnonymous(false);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to submit event review');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-gray-600 bg-opacity-50 overflow-y-auto h-full w-full z-50">
      <div className="relative top-20 mx-auto p-5 border w-11/12 md:w-3/4 lg:w-1/2 shadow-lg rounded-md bg-white">
        <div className="flex justify-between items-center mb-4">
          <h3 className="text-lg font-bold text-gray-900">Rate Event</h3>
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
          {Object.entries(ratings).map(([k,v]) => (
            <div key={k} className="flex items-center justify-between">
              <span className="capitalize text-gray-700">{k}</span>
              <StarInput value={v} onChange={(n)=>setRating(k, n)} />
            </div>
          ))}

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">Written Feedback (optional)</label>
            <textarea
              className="input-field h-24"
              value={textReview}
              onChange={(e)=>setTextReview(e.target.value)}
              placeholder="What did you like about the event?"
            />
          </div>

          <div className="flex items-center">
            <input
              type="checkbox"
              id="isAnonymousEvent"
              checked={isAnonymous}
              onChange={(e)=>setIsAnonymous(e.target.checked)}
              className="h-4 w-4 text-primary-600 focus:ring-primary-500 border-gray-300 rounded"
            />
            <label htmlFor="isAnonymousEvent" className="ml-2 block text-sm text-gray-900">
              Submit anonymously
            </label>
          </div>

          <div className="flex gap-4 pt-2">
            <button type="button" onClick={onClose} className="btn-secondary flex-1">Cancel</button>
            <button type="submit" disabled={isLoading} className="btn-primary flex-1">
              {isLoading ? 'Submitting...' : 'Submit'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default EventReviewModal;