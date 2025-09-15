import React, { useState } from 'react';
import { User } from '../services/api';
import ReviewModal from './ReviewModal';

interface UserCardProps {
  user: User;
}

const UserCard: React.FC<UserCardProps> = ({ user }) => {
  const [showReviewModal, setShowReviewModal] = useState(false);

  return (
    <>
      <div className="bg-white rounded-lg shadow-lg p-6">
        <div className="text-center mb-4">
          <div className="w-16 h-16 bg-primary-100 rounded-full mx-auto mb-3 flex items-center justify-center overflow-hidden">
            {user.mainPhotoSmallUrl ? (
              <img
                src={user.mainPhotoSmallUrl}
                alt={`${user.firstName} ${user.lastName}`}
                className="w-16 h-16 object-cover rounded-full"
                loading="lazy"
              />
            ) : (
              <span className="text-primary-600 font-semibold text-lg">
                {user.firstName[0]}{user.lastName[0]}
              </span>
            )}
          </div>
          <h3 className="font-semibold text-lg text-gray-900">
            {user.firstName} {user.lastName}
          </h3>
          {user.nickname && (
            <p className="text-gray-600">"{user.nickname}"</p>
          )}
        </div>

        <div className="space-y-2 text-sm text-gray-600">
          {user.selfAssessedLevel && (
            <p><span className="font-medium">Level:</span> {user.selfAssessedLevel}</p>
          )}
          {user.startDancingDate && (
            <p>
              <span className="font-medium">Dancing since:</span>{' '}
              {new Date(user.startDancingDate).getFullYear()}
            </p>
          )}
          {user.danceStyles && (
            <p><span className="font-medium">Styles:</span> {user.danceStyles}</p>
          )}
        </div>

        {user.bio && (
          <div className="mt-4">
            <p className="text-gray-700 text-sm">{user.bio}</p>
          </div>
        )}

        <div className="mt-6">
          <button
            onClick={() => setShowReviewModal(true)}
            className="btn-primary w-full"
          >
            Leave Review
          </button>
        </div>
      </div>

      {showReviewModal && (
        <ReviewModal
          isOpen={showReviewModal}
          onClose={() => setShowReviewModal(false)}
          onReviewSubmitted={() => {
            setShowReviewModal(false);
          }}
          preselectedUserId={user.id}
        />
      )}
    </>
  );
};

export default UserCard;