// BNKaraoke/bnkaraoke.web/src/pages/Dashboard.tsx
import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import './Dashboard.css';
import { API_ROUTES } from '../config/apiConfig';
import SongDetailsModal from '../components/SongDetailsModal';
import { Song, SpotifySong, QueueItem, Event, User } from '../types';

const mockEvents: Event[] = [
  { id: 1, name: "Joe's Karaoke Night", status: "Live", date: "2025-04-14" },
  { id: 2, name: "Sally’s Singalong", status: "Upcoming", date: "2025-04-25" },
  { id: 3, name: "March Karaoke Bash", status: "Archived", date: "2025-03-01" },
];

const user: User = { firstName: "Sarah", lastName: "Singer" };

const Dashboard: React.FC = () => {
  const navigate = useNavigate();
  const [currentEvent, setCurrentEvent] = useState<Event>(mockEvents[0]);
  const [queue, setQueue] = useState<QueueItem[]>([]);
  const [favorites, setFavorites] = useState<Song[]>([]);
  const [showReminder, setShowReminder] = useState<boolean>(currentEvent.status === "Live" || currentEvent.status === "Upcoming");
  const [checkedIn, setCheckedIn] = useState<boolean>(false);
  const [searchQuery, setSearchQuery] = useState<string>("");
  const [songs, setSongs] = useState<Song[]>([]);
  const [spotifySongs, setSpotifySongs] = useState<SpotifySong[]>([]);
  const [selectedSpotifySong, setSelectedSpotifySong] = useState<SpotifySong | null>(null);
  const [showSpotifyModal, setShowSpotifyModal] = useState<boolean>(false);
  const [showSpotifyDetailsModal, setShowSpotifyDetailsModal] = useState<boolean>(false);
  const [showRequestConfirmationModal, setShowRequestConfirmationModal] = useState<boolean>(false);
  const [requestedSong, setRequestedSong] = useState<SpotifySong | null>(null);
  const [showActions, setShowActions] = useState<number | null>(null);
  const [selectedSong, setSelectedSong] = useState<Song | null>(null);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [isSearching, setIsSearching] = useState<boolean>(false);
  const [showSearchModal, setShowSearchModal] = useState<boolean>(false);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      setQueue([]);
      return;
    }
    fetch(`${API_ROUTES.EVENT_QUEUE}/${currentEvent.id}/queue`, {
      headers: { Authorization: `Bearer ${token}` },
    })
      .then(res => {
        if (!res.ok) throw new Error(`Fetch queue failed: ${res.status}`);
        return res.json();
      })
      .then((data: QueueItem[]) => {
        console.log("Fetched queue:", data);
        setQueue(data || []);
      })
      .catch(err => {
        console.error("Fetch queue error:", err);
        setQueue([]);
      });
  }, [currentEvent]);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      setFavorites([]);
      return;
    }
    console.log("Fetching favorites from:", API_ROUTES.FAVORITES);
    fetch(`${API_ROUTES.FAVORITES}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
      .then(res => {
        if (!res.ok) {
          console.error(`Fetch favorites failed with status: ${res.status}`);
          throw new Error(`Fetch favorites failed: ${res.status}`);
        }
        return res.json();
      })
      .then((data: Song[]) => {
        console.log("Fetched favorites:", data);
        setFavorites(data || []);
      })
      .catch(err => {
        console.error("Fetch favorites error:", err);
        setFavorites([]);
      });
  }, []);

  const fetchSongs = async () => {
    if (!searchQuery.trim()) {
      console.log("Search query is empty, resetting songs");
      setSongs([]);
      setShowSearchModal(false);
      setSearchError(null);
      return;
    }
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      setSearchError("Authentication token missing. Please log in again.");
      setShowSearchModal(true);
      return;
    }
    setIsSearching(true);
    setSearchError(null);
    console.log(`Fetching songs with query: ${searchQuery}`);
    try {
      const response = await fetch(`${API_ROUTES.SONGS_SEARCH}?query=${encodeURIComponent(searchQuery)}&page=1&pageSize=50`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      if (!response.ok) {
        const errorText = await response.text();
        console.error(`Fetch failed with status: ${response.status}, response: ${errorText}`);
        throw new Error(`Search failed: ${response.status} - ${errorText}`);
      }
      const data = await response.json();
      console.log("Fetch response:", data);
      const fetchedSongs = (data.songs as Song[]) || [];
      console.log("Fetched songs:", fetchedSongs);
      const activeSongs = fetchedSongs.filter(song => song.status && song.status.toLowerCase() === "active");
      console.log("Filtered active songs:", activeSongs);
      setSongs(activeSongs);

      if (activeSongs.length === 0) {
        setSearchError("There are no Karaoke songs Available that match your search terms. Would you like to request a Karaoke Song be added?");
        setShowSearchModal(true);
      } else {
        setShowSearchModal(true);
      }
      setIsSearching(false);
    } catch (err) {
      console.error("Search error:", err);
      setSearchError(err instanceof Error ? err.message : "An unknown error occurred while searching.");
      setSongs([]);
      setShowSearchModal(true);
      setIsSearching(false);
    }
  };

  const fetchSpotifySongs = async () => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      return;
    }
    console.log(`Fetching Spotify songs with query: ${searchQuery}`);
    try {
      const response = await fetch(`${API_ROUTES.SPOTIFY_SEARCH}?query=${encodeURIComponent(searchQuery)}`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      if (!response.ok) {
        const errorText = await response.text();
        console.error(`Spotify fetch failed with status: ${response.status}, response: ${errorText}`);
        throw new Error(`Spotify search failed: ${response.status} - ${errorText}`);
      }
      const data = await response.json();
      console.log("Spotify fetch response:", data);
      const fetchedSpotifySongs = (data.songs as SpotifySong[]) || [];
      console.log("Fetched Spotify songs:", fetchedSpotifySongs);
      setSpotifySongs(fetchedSpotifySongs);
      setShowSpotifyModal(true);
      setShowSearchModal(false);
    } catch (err) {
      console.error("Spotify search error:", err);
      setSearchError(err instanceof Error ? err.message : "An unknown error occurred while searching Spotify.");
      setShowSearchModal(true);
    }
  };

  const handleSpotifySongSelect = (song: SpotifySong) => {
    setSelectedSpotifySong(song);
    setShowSpotifyDetailsModal(true);
  };

  const submitSongRequest = async (song: SpotifySong) => {
    console.log("Submitting song request:", song);
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      setSearchError("Please log in again to request a song.");
      return;
    }

    const userId = `${localStorage.getItem("firstName")} ${localStorage.getItem("lastName")}` || "Unknown User";
    const requestData = {
      title: song.title || "Unknown Title",
      artist: song.artist || "Unknown Artist",
      spotifyId: song.id,
      bpm: song.bpm || 0,
      danceability: song.danceability || 0,
      energy: song.energy || 0,
      valence: song.valence || null,
      popularity: song.popularity || 0,
      genre: song.genre || null,
      status: "pending",
      requestDate: new Date().toISOString(),
      requestedBy: userId,
      decade: song.decade || null
    };

    console.log("Request data:", requestData);

    try {
      setIsSearching(true);
      const response = await fetch(API_ROUTES.REQUEST_SONG, {
        method: "POST",
        headers: {
          "Authorization": `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify(requestData),
      });

      const responseText = await response.text();
      console.log(`Song request response status: ${response.status}, body: ${responseText}`);

      if (!response.ok) {
        console.error(`Failed to submit song request: ${response.status} - ${responseText}`);
        throw new Error(`Song request failed: ${response.status} - ${responseText}`);
      }

      let result = {};
      if (responseText) {
        try {
          result = JSON.parse(responseText);
        } catch (error) {
          console.error("Failed to parse response as JSON:", responseText);
          throw new Error("Invalid response format from server");
        }
      }

      console.log("Parsed response:", result);
      console.log("Setting state: closing Spotify modal, opening confirmation");
      setRequestedSong(song);
      setShowSpotifyDetailsModal(false);
      setShowRequestConfirmationModal(true);
    } catch (err) {
      console.error("Song request error:", err);
      setSearchError(err instanceof Error ? err.message : "Failed to submit song request.");
    } finally {
      setIsSearching(false);
    }
  };

  const handleSearchClick = () => {
    fetchSongs();
  };

  const handleSearchKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") {
      fetchSongs();
    }
  };

  const resetSearch = () => {
    console.log("Resetting search state");
    setSearchQuery("");
    setSongs([]);
    setSpotifySongs([]);
    setSelectedSpotifySong(null);
    setShowSearchModal(false);
    setShowSpotifyModal(false);
    setShowSpotifyDetailsModal(false);
    setShowRequestConfirmationModal(false);
    setRequestedSong(null);
    setSelectedSong(null);
    setSearchError(null);
  };

  const toggleFavorite = async (song: Song) => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found in toggleFavorite");
      return;
    }

    const isFavorite = favorites.some(fav => fav.id === song.id);
    const method = isFavorite ? 'DELETE' : 'POST';
    const url = isFavorite ? `${API_ROUTES.FAVORITES}/${song.id}` : API_ROUTES.FAVORITES;

    console.log(`Toggling favorite for song ${song.id}, isFavorite: ${isFavorite}, method: ${method}, url: ${url}`);

    try {
      const response = await fetch(url, {
        method,
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
        body: method === 'POST' ? JSON.stringify({ songId: song.id }) : undefined,
      });

      const responseText = await response.text();
      console.log(`Toggle favorite response status: ${response.status}, body: ${responseText}`);

      if (!response.ok) {
        console.error(`Failed to ${isFavorite ? 'remove' : 'add'} favorite: ${response.status} - ${responseText}`);
        throw new Error(`${isFavorite ? 'Remove' : 'Add'} favorite failed: ${response.status}`);
      }

      let result;
      try {
        result = JSON.parse(responseText);
      } catch (error) {
        console.error("Failed to parse response as JSON:", responseText);
        throw new Error("Invalid response format from server");
      }

      console.log(`Parsed toggle favorite response:`, result);

      if (result.success) {
        const updatedFavorites = isFavorite
          ? favorites.filter(fav => fav.id !== song.id)
          : [...favorites, { ...song }];
        console.log(`Updated favorites after ${isFavorite ? 'removal' : 'addition'}:`, updatedFavorites);
        setFavorites([...updatedFavorites]);
      } else {
        console.error("Toggle favorite failed: Success flag not set in response");
      }
    } catch (err) {
      console.error(`${isFavorite ? 'Remove' : 'Add'} favorite error:`, err);
    }
  };

  const addToEventQueue = (song: Song) => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found in addToEventQueue");
      return;
    }

    const isInQueue = queue.some(q => q.id === song.id);
    console.log(`Toggling queue for song ${song.id}, isInQueue: ${isInQueue}`);

    const updatedQueue = isInQueue
      ? queue.filter(q => q.id !== song.id)
      : [...queue, { id: song.id, title: song.title, artist: song.artist, status: song.status, singers: ["Me"], requests: [] }];
    console.log(`Updated queue after ${isInQueue ? 'removal' : 'addition'} (placeholder):`, updatedQueue);
    setQueue([...updatedQueue]);
    setSelectedSong(null);
    setShowSearchModal(false);
    setSearchQuery("");
    setSongs([]);
  };

  const handleEventChange = (eventId: number) => {
    const event = mockEvents.find(e => e.id === eventId);
    if (event) {
      setCurrentEvent(event);
      setShowReminder(event.status === "Live" || currentEvent.status === "Upcoming");
      setCheckedIn(false);
    }
  };

  const handleCheckIn = () => {
    setCheckedIn(true);
    setShowReminder(false);
  };

  const handleLeaveEvent = () => {
    if (window.confirm(`Leave ${currentEvent.name}? Your queue will be ${currentEvent.status === "Live" ? "archived" : "cleared"}.`)) {
      const token = localStorage.getItem("token");
      if (!token) {
        console.error("No token found");
        return;
      }
      fetch(`${API_ROUTES.EVENT_QUEUE}/${currentEvent.id}/queue`, {
        method: 'GET',
        headers: { Authorization: `Bearer ${token}` },
      })
        .then(res => {
          if (!res.ok) throw new Error(`Fetch queue failed: ${res.status}`);
          return res.json();
        })
        .then((data: QueueItem[]) => {
          const queueItems: QueueItem[] = data || [];
          Promise.all(queueItems.map((item: QueueItem) =>
            fetch(`${API_ROUTES.EVENT_QUEUE}/${currentEvent.id}/queue/${item.id}`, {
              method: 'DELETE',
              headers: { Authorization: `Bearer ${token}` },
            })
          )).then(() => {
            setQueue([]);
            setCurrentEvent(mockEvents.find(e => e.status !== "Archived") || mockEvents[0]);
            setCheckedIn(false);
            setShowReminder(false);
          });
        })
        .catch(err => {
          console.error("Clear queue error:", err);
          setQueue([]);
          setCurrentEvent(mockEvents.find(e => e.status !== "Archived") || mockEvents[0]);
          setCheckedIn(false);
          setShowReminder(false);
        });
    }
  };

  const handleDeleteSong = (songId: number) => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      return;
    }
    fetch(`${API_ROUTES.EVENT_QUEUE}/${currentEvent.id}/queue/${songId}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${token}` },
    })
      .then(res => {
        if (!res.ok) throw new Error(`Delete from queue failed: ${res.status}`);
        return res.json();
      })
      .then(() => {
        setQueue(queue.filter(s => s.id !== songId));
        setShowActions(null);
      })
      .catch(err => {
        console.error("Delete from queue error:", err);
        setQueue(queue.filter(s => s.id !== songId));
        setShowActions(null);
      });
  };

  const toggleActions = (songId: number) => {
    setShowActions(showActions === songId ? null : songId);
  };

  return (
    <>
      <div className="dashboard">
        <header className="event-header">
          <div>
            <h2>Welcome, {user.firstName} {user.lastName}!</h2>
            <h1>{currentEvent.status === "Live" ? "Live!" : currentEvent.status} {currentEvent.name}</h1>
          </div>
          <select value={currentEvent.id} onChange={e => handleEventChange(Number(e.target.value))} aria-label="Select event">
            {mockEvents.map(event => (
              <option key={event.id} value={event.id}>
                {event.status}: {event.name} ({event.date})
              </option>
            ))}
          </select>
        </header>

        {showReminder && (
          <div className="reminder-banner">
            {currentEvent.status === "Live" ? (
              <>
                Live Now: {currentEvent.name}—check in to sing!
                <button onClick={handleCheckIn}>Check In</button>
                <button onClick={() => setShowReminder(false)}>Dismiss</button>
              </>
            ) : (
              <>
                Tomorrow: {currentEvent.name}—pre-load songs!
                <button onClick={() => navigate("/dashboard")}>Pre-Load</button>
                <button onClick={() => setShowReminder(false)}>Dismiss</button>
              </>
            )}
          </div>
        )}

        <section className="search-section">
          <div className="search-bar-container">
            <input
              type="text"
              placeholder="Search for Karaoke Songs to Sing"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              onKeyDown={handleSearchKeyDown}
              className="search-bar"
              aria-label="Search for karaoke songs"
            />
            <button onClick={handleSearchClick} className="search-button" aria-label="Search">
              ▶
            </button>
            <button onClick={resetSearch} className="reset-button" aria-label="Reset search">
              ■
            </button>
          </div>
        </section>

        <div className="main-content">
          <section className="favorites-section">
            <h2>Your Favorites</h2>
            {favorites.length === 0 ? (
              <p>No favorites added yet.</p>
            ) : (
              <ul className="favorites-list">
                {favorites.map(song => (
                  <li
                    key={song.id}
                    className="favorite-song"
                    onClick={() => setSelectedSong(song)}
                  >
                    <span>{song.title} - {song.artist}</span>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <div className="center-content">
            <button
              className="explore-songs-button"
              onClick={() => navigate('/explore-songs')}
            >
              Explore Songs
            </button>
          </div>

          <aside className="queue-panel">
            <h2>My Sing Queue: {currentEvent.name}</h2>
            {currentEvent.status === "Live" && !checkedIn ? (
              <p>Check in to activate songs! <button onClick={handleCheckIn}>Check In</button></p>
            ) : (
              <p>{currentEvent.status === "Live" ? "Checked in—ready for DJ’s order!" : `Pre-loading for ${currentEvent.date}`}</p>
            )}
            {currentEvent.status === "Live" && <p>Karaoke DJ sets the sing order—keep queuing!</p>}
            {queue.length === 0 ? (
              <p>Add songs for {currentEvent.name}!</p>
            ) : (
              queue.map(song => (
                <div key={song.id} className="queue-song">
                  <span>
                    {song.title} - {song.artist}
                    {song.singers.length > 0 && ` (${song.singers.join(", ")})`}
                    {song.requests.length > 0 && <span className="request-badge">For {song.requests[0].forWhom}</span>}
                    {song.status === "pending" && <span className="request-badge">Pending</span>}
                  </span>
                  <div className="song-actions">
                    <button className="delete-btn" onClick={() => handleDeleteSong(song.id)}>🗑️</button>
                    <button className="more-btn" onClick={() => toggleActions(song.id)}>⋯</button>
                    {showActions === song.id && (
                      <div className="actions-dropdown">
                        <button onClick={() => console.log("Add Co-Singer")}>Add Co-Singer</button>
                        <button onClick={() => console.log("Request for...")}>Request for...</button>
                      </div>
                    )}
                  </div>
                </div>
              ))
            )}
            <button className="leave-btn" onClick={handleLeaveEvent}>Leave Event</button>
          </aside>
        </div>

        {queue.some(s => s.requests.length > 0) && (
          <div className="pending-requests">
            {queue.filter(s => s.requests.length > 0).length} pending requests!
            <button>View</button>
          </div>
        )}

        <nav className="menu-bar">
          <button title="Search" aria-label="Search">🔍</button>
          <button title="Queue" aria-label="Queue">🎤</button>
          <button title="Favorites" aria-label="Favorites">⭐</button>
          <button title="Requests" aria-label="Requests">✉️</button>
          {localStorage.getItem("role")?.includes("Manager") && <button title="Admin" aria-label="Admin">⚙️</button>}
          <button title="Events" aria-label="Events">📅</button>
          <button title="Profile" aria-label="Profile">👤</button>
          <button title="Help" aria-label="Help">?</button>
        </nav>
      </div>

      {showSearchModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3 className="modal-title">Search Results</h3>
            {isSearching ? (
              <p className="modal-text">Loading...</p>
            ) : searchError ? (
              <>
                <p className="modal-text error-text">{searchError}</p>
                <div className="song-actions">
                  <button onClick={fetchSpotifySongs} className="action-button">Yes</button>
                  <button onClick={resetSearch} className="action-button">No</button>
                </div>
              </>
            ) : songs.length === 0 ? (
              <p className="modal-text">No active songs found</p>
            ) : (
              <div className="song-list">
                {songs.map(song => (
                  <div key={song.id} className="song-card" onClick={() => setSelectedSong(song)}>
                    <span className="song-text">{song.title} - {song.artist}</span>
                  </div>
                ))}
              </div>
            )}
            {!searchError && (
              <button onClick={resetSearch} className="modal-cancel">Done</button>
            )}
          </div>
        </div>
      )}

      {showSpotifyModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3 className="modal-title">Spotify Search Results</h3>
            {spotifySongs.length === 0 ? (
              <p className="modal-text">No songs found on Spotify</p>
            ) : (
              <div className="song-list">
                {spotifySongs.map(song => (
                  <div key={song.id} className="song-card" onClick={() => handleSpotifySongSelect(song)}>
                    <span className="song-text">{song.title} - {song.artist}</span>
                  </div>
                ))}
              </div>
            )}
            <button onClick={resetSearch} className="modal-cancel">Done</button>
          </div>
        </div>
      )}

      {showSpotifyDetailsModal && selectedSpotifySong && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3 className="modal-title">{selectedSpotifySong.title}</h3>
            <div className="song-details">
              <p className="modal-text"><strong>Artist:</strong> {selectedSpotifySong.artist}</p>
              {selectedSpotifySong.genre && <p className="modal-text"><strong>Genre:</strong> {selectedSpotifySong.genre}</p>}
              {selectedSpotifySong.popularity && <p className="modal-text"><strong>Popularity:</strong> {selectedSpotifySong.popularity}</p>}
              {selectedSpotifySong.bpm && <p className="modal-text"><strong>BPM:</strong> {selectedSpotifySong.bpm}</p>}
              {selectedSpotifySong.energy && <p className="modal-text"><strong>Energy:</strong> {selectedSpotifySong.energy}</p>}
              {selectedSpotifySong.valence && <p className="modal-text"><strong>Valence:</strong> {selectedSpotifySong.valence}</p>}
              {selectedSpotifySong.danceability && <p className="modal-text"><strong>Danceability:</strong> {selectedSpotifySong.danceability}</p>}
              {selectedSpotifySong.decade && <p className="modal-text"><strong>Decade:</strong> {selectedSpotifySong.decade}</p>}
            </div>
            {searchError && <p className="modal-text error-text">{searchError}</p>}
            <div className="song-actions">
              <button
                onClick={() => submitSongRequest(selectedSpotifySong)}
                className="action-button"
                disabled={isSearching}
              >
                {isSearching ? "Requesting..." : "Add Request for Karaoke Version"}
              </button>
              <button
                onClick={() => {
                  setShowSpotifyDetailsModal(false);
                  setSearchError(null);
                }}
                className="action-button"
                disabled={isSearching}
              >
                Done
              </button>
            </div>
          </div>
        </div>
      )}

      {showRequestConfirmationModal && requestedSong && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3 className="modal-title">Request Submitted</h3>
            <p className="modal-text">
              A request has been made on your behalf to find a Karaoke version of "{requestedSong.title}" by {requestedSong.artist}.
            </p>
            <button onClick={resetSearch} className="modal-cancel">Done</button>
          </div>
        </div>
      )}

      {selectedSong && (
        <SongDetailsModal
          song={selectedSong}
          isFavorite={favorites.some(fav => fav.id === selectedSong.id)}
          isInQueue={queue.some(q => q.id === selectedSong.id)}
          onClose={() => setSelectedSong(null)}
          onToggleFavorite={toggleFavorite}
          onAddToQueue={addToEventQueue}
        />
      )}
    </>
  );
};

export default Dashboard;