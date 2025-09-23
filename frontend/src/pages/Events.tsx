import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { eventsAPI, Event } from '../services/api';
import { eventsAPIEx2 } from '../services/api';
import EventModal from '../components/EventModal';
import EventReviewModal from '../components/EventReviewModal';
import EventReviewsPanel from '../components/EventReviewsPanel';
import { useAuth } from '../contexts/AuthContext';

const Events: React.FC = () => {
  const { user } = useAuth();
  const [events, setEvents] = useState<Event[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [showEventReviewModal, setShowEventReviewModal] = useState(false);
  const [eventToReview, setEventToReview] = useState<Event | null>(null);
  const [openReviewsForEventId, setOpenReviewsForEventId] = useState<number | null>(null);

  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize] = useState(9);
  const [total, setTotal] = useState(0);
  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const canCreateEvent =
    (user?.permissions && user.permissions.includes('events.create')) ||
    (user?.roles && (user.roles.includes('Admin') || user.roles.includes('Organizer')));

  const fetchEvents = async () => {
    try {
      setIsLoading(true);
      setError('');
      const response = await eventsAPIEx2.getEventsPaged({ page, pageSize, search });
      setEvents(response.data.items);
      setTotal(response.data.total);
    } catch (err: any) {
      console.error('Error fetching events:', err);
      if (err.response?.status !== 401) {
        setError('Failed to load events. Please try again later.');
      }
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { fetchEvents(); /* eslint-disable-next-line */ }, [page, pageSize]);
  useEffect(() => {
    const id = setTimeout(() => { setPage(1); fetchEvents(); }, 300);
    return () => clearTimeout(id);
    // eslint-disable-next-line
  }, [search]);

  const handleJoinEvent = async (eventId: number) => {
    try {
      await eventsAPI.joinEvent(eventId);
      fetchEvents();
    } catch (error) {
      console.error('Error joining event:', error);
    }
  };

  const handleLeaveEvent = async (eventId: number) => {
    try {
      await eventsAPI.leaveEvent(eventId);
      fetchEvents();
    } catch (error) {
      console.error('Error leaving event:', error);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center items-center min-h-screen">
        <div className="animate-spin rounded-full h-32 w-32 border-b-2 border-primary-600"></div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 py-8">
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3 mb-8">
        <h1 className="text-3xl font-bold text-gray-900">Events</h1>
        <div className="flex items-center gap-3">
          <input
            type="text"
            placeholder="Search events..."
            className="input-field"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          {canCreateEvent && (
            <button
              onClick={() => setIsModalOpen(true)}
              className="btn-primary whitespace-nowrap"
            >
              Create Event
            </button>
          )}
        </div>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-400 text-red-700 px-4 py-3 rounded mb-6">
          {error}
          <button
            onClick={fetchEvents}
            className="ml-4 text-red-800 underline hover:no-underline"
          >
            Try again
          </button>
        </div>
      )}

      {events.length === 0 ? (
        <div className="text-center py-12">
          <p className="text-gray-500 text-lg">No events found.</p>
        </div>
      ) : (
        <>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {events.map((event) => (
              <div key={event.id} className="bg-white rounded-lg shadow-lg overflow-hidden hover:shadow-xl transition-shadow duration-300">
                {event.coverImageSmallUrl && (
                  <img src={event.coverImageSmallUrl} alt="" className="w-full h-40 object-cover" />
                )}
                <div className="p-6">
                  <h3 className="text-xl font-semibold text-gray-900 mb-2">
                    <Link to={`/events/${event.id}`} className="hover:underline">{event.name}</Link>
                  </h3>
                  <p className="text-gray-600 mb-4 line-clamp-3">{event.description}</p>

                  <div className="space-y-2 text-sm text-gray-500 mb-4">
                    <div className="flex items-center">
                      <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                      </svg>
                      {new Date(event.date).toLocaleDateString()} at {new Date(event.date).toLocaleTimeString()}
                    </div>
                    {event.location && (
                      <div className="flex items-center">
                        <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
                        </svg>
                        {event.location}
                      </div>
                    )}
                    <div className="flex items-center">
                      <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                      </svg>
                      {event.participantCount || 0} participants
                    </div>
                    <div className="flex items-center">
                      <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                      </svg>
                      Created by {event.creatorName || 'Unknown'}
                    </div>
                  </div>
                </div>

                <div className="px-6 pb-6">
                  {event.isUserParticipating ? (
                    <div className="mt-3 grid grid-cols-2 gap-3">
                      <button
                        onClick={() => handleLeaveEvent(event.id)}
                        className="btn-secondary w-full"
                      >
                        Leave Event
                      </button>
                      <button
                        onClick={() => { setEventToReview(event); setShowEventReviewModal(true); }}
                        className="btn-primary w-full"
                      >
                        Rate Event
                      </button>
                    </div>
                  ) : (
                    <button
                      onClick={() => handleJoinEvent(event.id)}
                      className="btn-primary w-full"
                    >
                      Join Event
                    </button>
                  )}
                  <div className="mt-3">
                    <button
                      onClick={() =>
                        setOpenReviewsForEventId(prev => (prev === event.id ? null : event.id))
                      }
                      className="btn-secondary w-full"
                    >
                      {openReviewsForEventId === event.id ? 'Hide reviews' : 'View reviews'}
                    </button>
                  </div>
                </div>
                {openReviewsForEventId === event.id && (
                  <div className="mt-3 bg-white border border-gray-200 rounded-lg">
                    <EventReviewsPanel eventId={event.id} />
                  </div>
                )}
              </div>
            ))}
          </div>

          <div className="mt-6 flex items-center justify-center gap-2">
            <button
              className="btn-secondary"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
            >
              Prev
            </button>
            <span className="text-sm text-gray-700">
              Page {page} / {totalPages}
            </span>
            <button
              className="btn-secondary"
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
            >
              Next
            </button>
          </div>
        </>
      )}

      {showEventReviewModal && eventToReview && (
        <EventReviewModal
          isOpen={showEventReviewModal}
          onClose={() => { setShowEventReviewModal(false); setEventToReview(null); }}
          eventId={eventToReview.id}
          onSubmitted={fetchEvents}
        />
      )}

      {isModalOpen && canCreateEvent && (
        <EventModal
          isOpen={isModalOpen}
          onClose={() => setIsModalOpen(false)}
          onEventCreated={fetchEvents}
        />
      )}
    </div>
  );
};

export default Events;