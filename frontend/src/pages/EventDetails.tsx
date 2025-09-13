import React, { useEffect, useState } from 'react';
import { Event, eventsAPI, eventsAPIEx } from '../services/api';
import EventReviewModal from '../components/EventReviewModal';
import EventReviewsPanel from '../components/EventReviewsPanel';
import { useParams } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

const EventDetails: React.FC = () => {
    const { id } = useParams<{ id: string }>();
    const eventId = Number(id);
    const { user } = useAuth();
    const [ev, setEv] = useState<Event | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [showReview, setShowReview] = useState(false);
    const [busy, setBusy] = useState(false);
    const [coverFile, setCoverFile] = useState<File | null>(null);
    const [toast, setToast] = useState('');

    const canUploadCover =
        !!user &&
        (user.roles?.includes('Admin') || user.roles?.includes('Organizer') || (ev && ev.createdBy === user.id));

    const fetchEvent = async () => {
        try {
            setLoading(true);
            setError('');
            const r = await eventsAPI.getEvent(eventId);
            setEv(r.data);
        } catch (e: any) {
            setError(e?.response?.data?.message || 'Failed to load event');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        if (!eventId) return;
        fetchEvent();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [eventId]);

    const join = async () => {
        try {
            setBusy(true);
            await eventsAPI.joinEvent(eventId);
            await fetchEvent();
            showToast('Joined the event');
        } catch (e) {
            showToast('Failed to join');
        } finally {
            setBusy(false);
        }
    };

    const leave = async () => {
        try {
            setBusy(true);
            await eventsAPI.leaveEvent(eventId);
            await fetchEvent();
            showToast('Left the event');
        } catch (e) {
            showToast('Failed to leave');
        } finally {
            setBusy(false);
        }
    };

    const uploadCover = async () => {
        if (!coverFile) return;
        try {
            setBusy(true);
            await eventsAPIEx.uploadCover(eventId, coverFile);
            setCoverFile(null);
            await fetchEvent();
            showToast('Cover uploaded');
        } catch (e: any) {
            showToast(e?.response?.data?.message || 'Cover upload failed');
        } finally {
            setBusy(false);
        }
    };

    const showToast = (msg: string) => { setToast(msg); setTimeout(() => setToast(''), 2000); };

    if (loading) {
        return (
            <div className="flex justify-center items-center min-h-screen">
                <div className="animate-spin rounded-full h-32 w-32 border-b-2 border-primary-600"></div>
            </div>
        );
    }

    if (error || !ev) {
        return (
            <div className="max-w-4xl mx-auto px-4 py-8">
                <div className="bg-red-50 border border-red-200 text-red-800 px-4 py-3 rounded">{error || 'Not found'}</div>
            </div>
        );
    }

    return (
        <div className="max-w-4xl mx-auto px-4 py-8">
            {toast && <div className="mb-4 bg-green-50 border border-green-200 text-green-800 px-4 py-2 rounded">{toast}</div>}
            <div className="bg-white rounded-lg shadow overflow-hidden">
                {ev.coverImageLargeUrl && (
                    <img src={ev.coverImageLargeUrl} alt="" className="w-full h-64 object-cover" />
                )}
                <div className="p-6">
                    <div className="flex items-start justify-between">
                        <div>
                            <h1 className="text-2xl font-bold text-gray-900">{ev.name}</h1>
                            <div className="text-gray-600 mt-1">{new Date(ev.date).toLocaleString()}</div>
                            {ev.location && <div className="text-gray-600">{ev.location}</div>}
                            <div className="text-sm text-gray-500 mt-1">Created by {ev.creatorName || 'Unknown'}</div>
                        </div>
                        <div className="space-x-2">
                            {ev.isUserParticipating ? (
                                <button className="btn-secondary" onClick={leave} disabled={busy}>Leave</button>
                            ) : (
                                <button className="btn-primary" onClick={join} disabled={busy}>Join</button>
                            )}
                        </div>
                    </div>

                    <p className="text-gray-800 mt-4">{ev.description}</p>

                    <div className="mt-4 text-sm text-gray-700">
                        <span className="font-semibold">{ev.participantCount || 0}</span> participants
                    </div>

                    {canUploadCover && (
                        <div className="mt-4">
                            <label className="block text-sm font-medium text-gray-700 mb-1">Update cover</label>
                            <div className="flex items-center gap-3">
                                <input type="file" accept="image/jpeg,image/png,image/webp" onChange={(e) => setCoverFile(e.target.files?.[0] || null)} />
                                <button className="btn-secondary" onClick={uploadCover} disabled={busy || !coverFile}>Upload</button>
                            </div>
                            <p className="text-xs text-gray-500 mt-1">JPEG/PNG/WEBP up to 10 MB</p>
                        </div>
                    )}
                </div>
            </div>

            <div className="bg-white rounded-lg shadow mt-6">
                <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
                    <h2 className="text-lg font-semibold text-gray-900">Event reviews</h2>
                    {ev.isUserParticipating && (
                        <button className="btn-primary" onClick={() => setShowReview(true)}>Rate Event</button>
                    )}
                </div>
                <EventReviewsPanel eventId={ev.id} />
            </div>

            {showReview && (
                <EventReviewModal
                    isOpen={showReview}
                    onClose={() => setShowReview(false)}
                    eventId={ev.id}
                    onSubmitted={fetchEvent}
                />
            )}
        </div>
    );
};

export default EventDetails;