import React, { useState, useEffect } from 'react';
import { eventsAPI, Event } from '../services/api';
import EventModal from '../components/EventModal';
import EventReviewModal from '../components/EventReviewModal';

const Events: React.FC = () => {
  const [events, setEvents] = useState<Event[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [showEventReviewModal, setShowEventReviewModal] = useState(false);
  const [eventToReview, setEventToReview] = useState<Event | null>(null);

  const fetchEvents = async () => {
    try {
      setIsLoading(true);
      setError('');
      const response = await eventsAPI.getEvents();
      setEvents(response.data);
    } catch (err: any) {
      console.error('Error fetching events:', err);
      if (err.response?.status !== 401) {
        setError('Failed to load events. Please try again later.');
      }
    } finally {
      setIsLoading(false);
    }
  };
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

  useEffect(() => {
    fetchEvents();
  }, []);

  if (isLoading) {
    return (
      <div className="flex justify-center items-center min-h-screen">
        <div className="animate-spin rounded-full h-32 w-32 border-b-2 border-primary-600"></div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 py-8">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold text-gray-900">Events</h1>
        <button
          onClick={() => setIsModalOpen(true)}
          className="btn-primary"
        >
          Create Event
        </button>
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
          <p className="text-gray-500 text-lg">No events found. Create the first one!</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {events.map((event) => (
            <div key={event.id} className="bg-white rounded-lg shadow-lg overflow-hidden hover:shadow-xl transition-shadow duration-300">
              <div className="p-6">
                <h3 className="text-xl font-semibold text-gray-900 mb-2">{event.name}</h3>
                <p className="text-gray-600 mb-4 line-clamp-3">{event.description}</p>
                
                <div className="space-y-2 text-sm text-gray-500 mb-4">
                  <div className="flex items-center">
                    <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
                    </svg>
                    {new Date(event.date).toLocaleDateString()} at {new Date(event.date).toLocaleTimeString()}
                  </div>
                  <div className="flex items-center">
                    <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
                    </svg>
                    {event.location}
                  </div>
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
              </div>
            </div>
          ))}
        </div>
      )}
      {showEventReviewModal && eventToReview && (
        <EventReviewModal
          isOpen={showEventReviewModal}
          onClose={() => { setShowEventReviewModal(false); setEventToReview(null); }}
          eventId={eventToReview.id}
          onSubmitted={fetchEvents}
        />
      )}

      <EventModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onEventCreated={fetchEvents}
      />
    </div>
  );
};

export default Events;