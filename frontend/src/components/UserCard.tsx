import React, { useState } from 'react';
import { User } from '../services/api';
import ReviewModal from './ReviewModal';
import { useTranslation } from 'react-i18next';

interface UserCardProps {
  user: User;
}

const UserCard: React.FC<UserCardProps> = ({ user }) => {
  const [showReviewModal, setShowReviewModal] = useState(false);
  const { t } = useTranslation();

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
                style={{
                  objectPosition:
                    user.mainPhotoFocusX != null && user.mainPhotoFocusY != null
                      ? `${user.mainPhotoFocusX}% ${user.mainPhotoFocusY}%`
                      : '50% 50%',
                }}
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
          {(typeof user.reviewsReceivedCount !== 'undefined') && (
            <p className="text-gray-600 text-sm">
              {user.reviewsReceivedCount} {t('userCard.reviews')}
              {typeof user.avgRating === 'number' &&
                <> • {t('userCard.avg', { value: user.avgRating.toFixed(1) })}</>
              }
            </p>
          )}
        </div>

        <div className="space-y-2 text-sm text-gray-600">
          {user.selfAssessedLevel && (
            <p><span className="font-medium">{t('userCard.level')}</span> {user.selfAssessedLevel}</p>
          )}
          {user.startDancingDate && (
            <p>
              <span className="font-medium">{t('userCard.dancingSince')}</span>{' '}
              {new Date(user.startDancingDate).getFullYear()}
            </p>
          )}
          {user.danceStyles && (
            <p><span className="font-medium">{t('userCard.styles')}</span> {user.danceStyles}</p>
          )}
        </div>

        {user.bio && (
          <div className="mt-4">
            <p className="text-gray-700 text-sm">{user.bio}</p>
          </div>
        )}

        <div className="mt-6">
          <button
            onClick={(e) => { e.preventDefault(); e.stopPropagation(); setShowReviewModal(true); }}
            className="btn-primary w-full"
          >
            {t('userCard.leaveReview')}
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