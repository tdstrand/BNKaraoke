import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import './SongDetailsModal.css';
import { Song } from '../types';
import { API_ROUTES } from '../config/apiConfig';
import useEventContext from '../context/EventContext';

interface SongDetailsModalProps {
  song: Song;
  isFavorite: boolean;
  isInQueue: boolean;
  onClose: () => void;
  onToggleFavorite: (song: Song) => Promise<void>;
  onAddToQueue?: (song: Song, eventId: number) => Promise<void>;
  onDeleteFromQueue?: (eventId: number, queueId: number) => Promise<void>;
  eventId?: number;
  queueId?: number;
}

const SongDetailsModal: React.FC<SongDetailsModalProps> = ({
  song,
  isFavorite,
  isInQueue,
  onClose,
  onToggleFavorite,
  onAddToQueue,
  onDeleteFromQueue,
  eventId,
  queueId,
}) => {
  const navigate = useNavigate();
  const { currentEvent, checkedIn, isCurrentEventLive } = useEventContext();
  const [events, setEvents] = useState<any[]>([]);
  const [isAddingToQueue, setIsAddingToQueue] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showEventSelectionModal, setShowEventSelectionModal] = useState(false);
  const [userName, setUserName] = useState<string | null>(localStorage.getItem("userName"));

  // Fetch events only if currentEvent is unset and events are needed
  useEffect(() => {
    if (currentEvent || isInQueue || !onAddToQueue) return; // Skip if currentEvent exists or not adding to queue

    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      setEvents([]);
      setError("Authentication token missing. Please log in again.");
      navigate("/");
      return;
    }

    fetch(API_ROUTES.EVENTS, {
      headers: { Authorization: `Bearer ${token}` },
    })
      .then(res => {
        if (!res.ok) throw new Error(`Fetch events failed: ${res.status}`);
        return res.json();
      })
      .then((data: any[]) => {
        console.log("SongDetailsModal - Fetched events:", data);
        setEvents(data || []);
      })
      .catch(err => {
        console.error("SongDetailsModal - Fetch events error:", err);
        setEvents([]);
        setError("Failed to load events. Please try again.");
      });
  }, [navigate, currentEvent, isInQueue, onAddToQueue]);

  const handleAddToQueue = async (eventId: number) => {
    console.log("handleAddToQueue called with eventId:", eventId, "song:", song, "onAddToQueue:", !!onAddToQueue);
    if (!onAddToQueue) {
      console.error("onAddToQueue is not defined");
      setError("Cannot add to queue: Functionality not available.");
      return;
    }

    if (!eventId) {
      console.error("Event ID is missing");
      setError("Please select an event to add the song to the queue.");
      return;
    }

    setIsAddingToQueue(true);
    setError(null);

    try {
      await onAddToQueue(song, eventId);
      console.log("Song successfully added to queue for eventId:", eventId);
      setShowEventSelectionModal(false);
      onClose();
    } catch (err) {
      console.error("SongDetailsModal - Add to queue error:", err);
      const errorMessage = err instanceof Error ? err.message : "Failed to add song to queue. Please try again.";
      setError(errorMessage);
      if (errorMessage.includes("User not found")) {
        localStorage.clear();
        navigate("/");
      }
    } finally {
      setIsAddingToQueue(false);
    }
  };

  const handleDeleteFromQueue = async () => {
    console.log("handleDeleteFromQueue called with eventId:", eventId, "queueId:", queueId, "onDeleteFromQueue:", !!onDeleteFromQueue);
    if (!onDeleteFromQueue || !eventId || !queueId) {
      console.error("Cannot delete from queue: Missing onDeleteFromQueue, eventId, or queueId");
      setError("Cannot delete from queue: Missing information.");
      return;
    }

    setIsDeleting(true);
    setError(null);

    try {
      await onDeleteFromQueue(eventId, queueId);
      console.log("Song successfully deleted from queue for eventId:", eventId, "queueId:", queueId);
      onClose();
    } catch (err) {
      console.error("SongDetailsModal - Delete from queue error:", err);
      const errorMessage = err instanceof Error ? err.message : "Failed to delete song from queue. Please try again.";
      setError(errorMessage);
      if (errorMessage.includes("User not found")) {
        localStorage.clear();
        navigate("/");
      }
    } finally {
      setIsDeleting(false);
    }
  };

  const handleOpenEventSelection = () => {
    console.log("handleOpenEventSelection called with userName:", userName);
    if (!userName) {
      console.error("UserName not found in localStorage");
      setError("User not found. Please log in again to add songs to the queue.");
      localStorage.clear();
      navigate("/");
      return;
    }
    setShowEventSelectionModal(true);
  };

  console.log("Rendering SongDetailsModal with song:", song, "isFavorite:", isFavorite, "isInQueue:", isInQueue, "eventId:", eventId, "queueId:", queueId);

  return (
    <>
      <div className="modal-overlay">
        <div className="modal-content">
          <h3 className="modal-title">{song.title}</h3>
          <div className="song-details">
            <p className="modal-text"><strong>Artist:</strong> {song.artist}</p>
            {song.genre && <p className="modal-text"><strong>Genre:</strong> {song.genre}</p>}
            {song.popularity && <p className="modal-text"><strong>Popularity:</strong> {song.popularity}</p>}
            {song.bpm && <p className="modal-text"><strong>BPM:</strong> {song.bpm}</p>}
            {song.energy && <p className="modal-text"><strong>Energy:</strong> {song.energy}</p>}
            {song.valence && <p className="modal-text"><strong>Valence:</strong> {song.valence}</p>}
            {song.danceability && <p className="modal-text"><strong>Danceability:</strong> {song.danceability}</p>}
            {song.decade && <p className="modal-text"><strong>Decade:</strong> {song.decade}</p>}
          </div>
          {error && <p className="modal-error">{error}</p>}
          <div className="song-actions">
            <button
              onClick={() => {
                console.log("Toggle favorite button clicked for song:", song);
                onToggleFavorite(song);
              }}
              className="action-button"
            >
              {isFavorite ? "Remove from Favorites" : "Add to Favorites"}
            </button>
            {isInQueue && onDeleteFromQueue && eventId && queueId ? (
              <button
                onClick={() => {
                  console.log("Remove from Queue button clicked");
                  handleDeleteFromQueue();
                }}
                className="action-button"
                disabled={isDeleting}
              >
                {isDeleting ? "Deleting..." : "Remove from Queue"}
              </button>
            ) : (
              <button
                onClick={() => {
                  console.log("Add to Queue button clicked with currentEvent:", currentEvent);
                  currentEvent ? handleAddToQueue(currentEvent.eventId) : handleOpenEventSelection();
                }}
                className="action-button"
                disabled={isAddingToQueue || (!currentEvent && (events.length === 0 || !userName)) || !onAddToQueue || isInQueue}
              >
                {isAddingToQueue ? "Adding..." : currentEvent ? `Add to Queue: ${currentEvent.eventCode}` : "Add to Queue"}
              </button>
            )}
          </div>
          <div className="modal-footer">
            <button onClick={onClose} className="action-button">
              Done
            </button>
          </div>
        </div>
      </div>

      {showEventSelectionModal && (
        <div className="modal-overlay secondary-modal">
          <div className="modal-content">
            <h3 className="modal-title">Select Event Queue</h3>
            {error && <p className="modal-error">{error}</p>}
            <div className="event-list">
              {events
                .filter(event => event.status === "Live" || event.status === "Upcoming")
                .map(event => (
                  <div
                    key={event.eventId}
                    className="event-item"
                    onClick={() => {
                      console.log("Event selected for adding to queue:", event);
                      handleAddToQueue(event.eventId);
                    }}
                  >
                    {event.status}: {event.eventCode} ({event.scheduledDate})
                  </div>
                ))}
            </div>
            <div className="modal-footer">
              <button
                onClick={() => {
                  console.log("Cancel event selection modal");
                  setShowEventSelectionModal(false);
                }}
                className="action-button"
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
};

export default SongDetailsModal;