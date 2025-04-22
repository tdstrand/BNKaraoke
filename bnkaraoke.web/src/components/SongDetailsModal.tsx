// BNKaraoke/bnkaraoke.web/src/components/SongDetailsModal.tsx
import React from 'react';
import './SongDetailsModal.css';
import { Song } from '../types';

interface SongDetailsModalProps {
  song: Song;
  isFavorite: boolean;
  isInQueue: boolean;
  onClose: () => void;
  onToggleFavorite: (song: Song) => Promise<void>;
  onAddToQueue: (song: Song) => void;
}

const SongDetailsModal: React.FC<SongDetailsModalProps> = ({
  song,
  isFavorite,
  isInQueue,
  onClose,
  onToggleFavorite,
  onAddToQueue,
}) => {
  return (
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
        <div className="song-actions">
          <button
            onClick={() => onToggleFavorite(song)}
            className="action-button"
          >
            {isFavorite ? "Remove from Favorites" : "Add to Favorites"}
          </button>
          <button
            onClick={() => {
              onAddToQueue(song);
              onClose();
            }}
            className="action-button"
          >
            {isInQueue ? "Remove from Event Sing Queue" : "Add to Event Sing Queue"}
          </button>
          <button onClick={onClose} className="action-button">
            Done
          </button>
        </div>
      </div>
    </div>
  );
};

export default SongDetailsModal;