import React, { useState, useEffect } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { reviewsAPI, Review } from '../services/api';

const Dashboard: React.FC = () => {
  const { user } = useAuth();
  const [reviews, setReviews] = useState<Review[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  const fetchReviews = async () => {
    if (!user) return;
    
    try {
      setIsLoading(true);
      setError('');
      const response = await reviewsAPI.getUserReviews(user.id);
      setReviews(response.data);
    } catch (err: any) {
      console.error('Error fetching reviews:', err);
      if (err.response?.status !== 401) {
        setError('Failed to load reviews. Please try again later.');
      }
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchReviews();
  }, [user]);

  if (isLoading) {
    return (
      <div className="flex justify-center items-center min-h-screen">
        <div className="animate-spin rounded-full h-32 w-32 border-b-2 border-primary-600"></div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 py-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-gray-900">
          Welcome, {user?.firstName}!
        </h1>
        <p className="text-gray-600 mt-2">
          Here's your feedback dashboard
        </p>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-400 text-red-700 px-4 py-3 rounded mb-6">
          {error}
          <button 
            onClick={fetchReviews}
            className="ml-4 text-red-800 underline hover:no-underline"
          >
            Try again
          </button>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-8">
        <div className="bg-white rounded-lg shadow p-6">
          <h3 className="text-lg font-medium text-gray-900">Reviews Received</h3>
          <p className="text-3xl font-bold text-primary-600 mt-2">
            {reviews.length}
          </p>
        </div>
        <div className="bg-white rounded-lg shadow p-6">
          <h3 className="text-lg font-medium text-gray-900">Average Rating</h3>
          <p className="text-3xl font-bold text-accent-600 mt-2">
            {reviews.length > 0 
              ? (reviews.reduce((sum, review) => sum + review.rating, 0) / reviews.length).toFixed(1)
              : 'N/A'
            }
          </p>
        </div>
        <div className="bg-white rounded-lg shadow p-6">
          <h3 className="text-lg font-medium text-gray-900">Recent Feedback</h3>
          <p className="text-3xl font-bold text-gray-600 mt-2">
            {reviews.filter(review => 
              new Date(review.createdAt) > new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)
            ).length}
          </p>
        </div>
      </div>

      <div className="bg-white rounded-lg shadow">
        <div className="px-6 py-4 border-b border-gray-200">
          <h2 className="text-xl font-semibold text-gray-900">Recent Reviews</h2>
        </div>
        {reviews.length === 0 ? (
          <div className="px-6 py-8 text-center">
            <p className="text-gray-500">No reviews yet. Dance more and get feedback!</p>
          </div>
        ) : (
          <div className="divide-y divide-gray-200">
            {reviews.slice(0, 5).map((review) => (
              <div key={review.id} className="px-6 py-4">
                <div className="flex justify-between items-start">
                  <div className="flex-1">
                    <div className="flex items-center space-x-2">
                      <span className="text-sm font-medium text-gray-900">
                        {review.isAnonymous ? 'Anonymous' : `${review.reviewer.firstName} ${review.reviewer.lastName}`}
                      </span>
                      <span className="text-sm text-gray-500">•</span>
                      <span className="text-sm text-gray-500">
                        {review.event.name}
                      </span>
                    </div>
                    <p className="text-gray-700 mt-1">{review.content}</p>
                    <p className="text-sm text-gray-500 mt-2">
                      {new Date(review.createdAt).toLocaleDateString()}
                    </p>
                  </div>
                  <div className="flex items-center">
                    <span className="text-lg font-bold text-accent-600">
                      {review.rating}/5
                    </span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};

export default Dashboard;