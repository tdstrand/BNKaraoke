import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import './SongDetailsModal.css';
import { Song } from '../types';
import { API_ROUTES } from '../config/apiConfig';

interface SongDetailsModalProps {
  song: Song;
  isFavorite: boolean;
  isInQueue: boolean;
  onClose: () => void;
  onToggleFavorite: (song: Song) => Promise<void>;
  onAddToQueue?: (song: Song, eventId: number) => Promise<void>;
  onDeleteFromQueue?: (eventId: number, queueId: number) => Promise<void>; // New prop for deleting
  eventId?: number; // Optional eventId for deleting
  queueId?: number; // Optional queueId for deleting
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
  const [events, setEvents] = useState<any[]>([]);
  const [isAddingToQueue, setIsAddingToQueue] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showEventSelectionModal, setShowEventSelectionModal] = useState(false);
  const [userName, setUserName] = useState<string | null>(localStorage.getItem("userName"));

  // Fetch events on component mount
  useEffect(() => {
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
  }, [navigate]);

  const handleAddToQueue = async (eventId: number) => {
    if (!onAddToQueue) {
      setError("Cannot add to queue: Functionality not available.");
      return;
    }

    setIsAddingToQueue(true);
    setError(null);

    try {
      await onAddToQueue(song, eventId);
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
    if (!onDeleteFromQueue || !eventId || !queueId) {
      setError("Cannot delete from queue: Missing information.");
      return;
    }

    setIsDeleting(true);
    setError(null);

    try {
      await onDeleteFromQueue(eventId, queueId);
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
    if (!userName) {
      setError("User not found. Please log in again to add songs to the queue.");
      localStorage.clear();
      navigate("/");
      return;
    }
    setShowEventSelectionModal(true);
  };

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
              onClick={() => onToggleFavorite(song)}
              className="action-button"
            >
              {isFavorite ? "Remove from Favorites" : "Add to Favorites"}
            </button>
            {isInQueue && onDeleteFromQueue && eventId && queueId ? (
              <button
                onClick={handleDeleteFromQueue}
                className="action-button"
                disabled={isDeleting}
              >
                {isDeleting ? "Deleting..." : "Remove from Queue"}
              </button>
            ) : (
              <button
                onClick={handleOpenEventSelection}
                className="action-button"
                disabled={isAddingToQueue || events.length === 0 || !userName || !onAddToQueue || isInQueue}
              >
                {isAddingToQueue ? "Adding..." : "Add to Queue"}
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
                    onClick={() => handleAddToQueue(event.eventId)}
                  >
                    {event.status}: {event.eventCode} ({event.scheduledDate})
                  </div>
                ))}
            </div>
            <div className="modal-footer">
              <button
                onClick={() => setShowEventSelectionModal(false)}
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