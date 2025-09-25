import React, { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { usersAPI, reviewsAPI, Review, User, userPhotosAPI } from '../services/api';
import ReviewModal from '../components/ReviewModal';
import { useAuth } from '../contexts/AuthContext';
import { reportsAPI } from '../services/api';
import { ReasonBadge } from '../components/ReasonBadge';

const badge = (level?: string) =>
    level === 'Red' ? 'bg-red-100 text-red-800' :
        level === 'Yellow' ? 'bg-yellow-100 text-yellow-800' :
            level === 'Green' ? 'bg-green-100 text-green-800' :
                'bg-gray-200 text-gray-700';

const UserDetails: React.FC = () => {
    const { id } = useParams<{ id: string }>();
    const { user: me } = useAuth();
    const [user, setUser] = useState<User | null>(null);
    const [reviews, setReviews] = useState<Review[]>([]);
    const [photos, setPhotos] = useState<Array<{ id: number; isMain: boolean; smallUrl: string; mediumUrl: string; largeUrl: string }>>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [showReviewModal, setShowReviewModal] = useState(false);

    const load = async () => {
        if (!id) return;
        try {
            setLoading(true); setError('');
            const [u, r, ph] = await Promise.all([
                usersAPI.getUser(id),
                reviewsAPI.getUserReviews(id),
                userPhotosAPI.getUser(id)
            ]);
            setUser(u.data as any);
            setReviews(r.data);
            setPhotos(ph.data);
        } catch (e: any) {
            setError(e.response?.data?.message || 'Failed to load user or reviews');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => { load(); }, [id]);

    const report = async (reviewId: number) => {
        try {
            await reportsAPI.create({ targetType: 'Review', targetId: reviewId, reason: 'Inappropriate', description: '' });
            alert('Report submitted');
        } catch (e: any) {
            alert(e.response?.data?.message || 'Failed to report');
        }
    };

    const reportUserPhoto = async (photoId: number) => {
        try {
            await reportsAPI.create({ targetType: 'UserPhoto', targetId: photoId, reason: 'Inappropriate', description: '' });
            alert('Report submitted');
        } catch (e: any) {
            alert(e.response?.data?.message || 'Failed to report');
        }
    };

    const combinedAvg = (r: Review) => {
        const parts: number[] = [];
        const lead = r.leadRatings ? Object.values(r.leadRatings) : [];
        const follow = r.followRatings ? Object.values(r.followRatings) : [];
        const avgLead = lead.length ? lead.reduce((a, b) => a + b, 0) / lead.length : NaN;
        const avgFollow = follow.length ? follow.reduce((a, b) => a + b, 0) / follow.length : NaN;
        if (!isNaN(avgLead)) parts.push(avgLead);
        if (!isNaN(avgFollow)) parts.push(avgFollow);
        return parts.length ? (parts.reduce((a, b) => a + b, 0) / parts.length) : NaN;
    };

    const mainFromPhotos = photos.find(p => p.isMain);

    // Исключать Red из отображаемых средних на карточке — на уровне бекэнда учли в UsersController
    // На уровне клиента можно фильтровать r.moderationLevel==='Red' при расчётах, если бы считали здесь.

    if (loading) {
        return <div className="flex items-center justify-center min-h-screen"><div className="animate-spin rounded-full h-32 w-32 border-b-2 border-primary-600"></div></div>;
    }
    if (error || !user) {
        return <div className="max-w-4xl mx-auto p-6"><div className="bg-red-50 border border-red-200 text-red-800 px-4 py-3 rounded">{error || 'Not found'}</div></div>;
    }

    return (
        <div className="max-w-4xl mx-auto px-4 py-8">
            <div className="bg-white rounded-lg shadow p-6 mb-6">
                <div className="flex items-center">
                    <div className="w-20 h-20 rounded-full overflow-hidden bg-primary-100 flex items-center justify-center">
                        {user.mainPhotoSmallUrl
                            ? <img
                                src={user.mainPhotoSmallUrl}
                                alt=""
                                className="w-20 h-20 object-cover"
                                style={{
                                    objectPosition:
                                        user.mainPhotoFocusX != null && user.mainPhotoFocusY != null
                                            ? `${user.mainPhotoFocusX}% ${user.mainPhotoFocusY}%`
                                            : '50% 50%',
                                }}
                            />
                            : <span className="text-primary-600 font-bold text-xl">{user.firstName[0]}{user.lastName[0]}</span>}
                    </div>
                    <div className="ml-4">
                        <h1 className="text-2xl font-bold">{user.firstName} {user.lastName}</h1>
                        {(typeof user.reviewsReceivedCount !== 'undefined') && (
                            <div className="text-gray-600 text-sm">
                                {user.reviewsReceivedCount} reviews
                                {typeof user.avgRating === 'number' && <> • avg {user.avgRating.toFixed(1)}/5</>}
                                {typeof user.avgRatingUnique === 'number' && <> • by authors {user.avgRatingUnique.toFixed(1)}/5</>}
                            </div>
                        )}
                        {user.dancerRole && <div className="text-gray-600 text-sm mt-1">Role: {user.dancerRole}</div>}
                        {user.selfAssessedLevel && <div className="text-gray-600 text-sm">Level: {user.selfAssessedLevel}</div>}
                    </div>
                    <div className="ml-auto flex gap-2">
                        <Link to="/users" className="btn-secondary">Back</Link>
                        {me && me.id !== user.id && (
                            <button className="btn-primary" onClick={() => setShowReviewModal(true)}>Leave Review</button>
                        )}
                    </div>
                </div>
                {user.bio && <p className="text-gray-700 mt-4">{user.bio}</p>}
                {user.danceStyles && <p className="text-gray-700 mt-1"><span className="font-semibold">Styles:</span> {user.danceStyles}</p>}
                {user.startDancingDate && <p className="text-gray-700 mt-1"><span className="font-semibold">Dancing since:</span> {new Date(user.startDancingDate).getFullYear()}</p>}
            </div>

            <div className="bg-white rounded-lg shadow p-6 mb-6">
                <h2 className="text-lg font-semibold mb-3">Photos</h2>
                {photos.length === 0 ? (
                    <div className="text-gray-500">No photos.</div>
                ) : (
                    <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
                        {photos.map(p => (
                            <div key={p.id} className="rounded overflow-hidden border">
                                <div className="w-full" style={{ aspectRatio: '4 / 3' }}>
                                    <img src={p.largeUrl || p.mediumUrl} alt="" className="w-full h-full object-contain" />
                                </div>
                                {me && (
                                    <div className="p-2 text-right">
                                        <button className="text-sm text-red-600 hover:underline" onClick={() => reportUserPhoto(p.id)}>Report</button>
                                    </div>
                                )}
                            </div>
                        ))}
                    </div>
                )}
            </div>

            <div className="bg-white rounded-lg shadow">
                <div className="px-6 py-4 border-b border-gray-200">
                    <h2 className="text-lg font-semibold">All Reviews</h2>
                </div>
                {reviews.length === 0 ? (
                    <div className="p-6 text-gray-500">No reviews yet.</div>
                ) : (
                    <div className="divide-y">
                        {reviews.map(r => (
                            <div key={r.id} className="px-6 py-4">
                                <div className="flex justify-between">
                                    <div className="text-sm text-gray-800">
                                        {r.reviewerName || 'Anonymous'}
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
                                {r.textReview ? (
                                    <p className="text-gray-700 mt-1">{r.textReview}</p>
                                ) : (
                                    <p className="text-gray-400 mt-1">Hidden by privacy settings</p>
                                )}
                                <div className="mt-2 flex items-center justify-between">
                                    <div className="text-sm text-gray-600">
                                        {(() => {
                                            const v = combinedAvg(r);
                                            return isNaN(v) ? null : <span className="font-semibold">{v.toFixed(1)}/5</span>;
                                        })()}
                                    </div>
                                    <button className="text-xs text-red-700 hover:underline" onClick={() => report(r.id)}>Report</button>
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </div>

            {showReviewModal && me && (
                <ReviewModal
                    isOpen={showReviewModal}
                    onClose={() => setShowReviewModal(false)}
                    onReviewSubmitted={() => { setShowReviewModal(false); load(); }}
                    preselectedUserId={user.id}
                />
            )}
        </div>
    );
};

export default UserDetails;