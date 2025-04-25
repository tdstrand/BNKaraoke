import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import './Dashboard.css';
import { API_ROUTES } from '../config/apiConfig';
import SongDetailsModal from '../components/SongDetailsModal';
import { Song, SpotifySong, EventQueueItem } from '../types';
import useEventContext from '../context/EventContext';
import { DndContext, closestCenter, KeyboardSensor, PointerSensor, useSensor, useSensors } from '@dnd-kit/core';
import { SortableContext, sortableKeyboardCoordinates, verticalListSortingStrategy, useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';

interface SortableQueueItemProps {
  queueItem: EventQueueItem;
  eventId: number;
  songDetails: Song | null;
  onClick: (song: Song, queueId: number) => void;
}

const SortableQueueItem: React.FC<SortableQueueItemProps> = ({ queueItem, eventId, songDetails, onClick }) => {
  const { attributes, listeners, setNodeRef, transform, transition } = useSortable({ id: queueItem.queueId });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  return (
    <div
      ref={setNodeRef}
      style={style}
      {...attributes}
      {...listeners}
      className="queue-song"
      onClick={() => songDetails && onClick(songDetails, queueItem.queueId)}
    >
      <span>
        {songDetails ? (
          `${songDetails.title} - ${songDetails.artist}`
        ) : (
          queueItem.songId // Fallback if song details aren't loaded
        )}
        {queueItem.isCurrentlyPlaying && <span className="request-badge">Playing</span>}
        {queueItem.wasSkipped && <span className="request-badge">Skipped</span>}
        {queueItem.isOnBreak && <span className="request-badge">On Break</span>}
      </span>
    </div>
  );
};

const Dashboard: React.FC = () => {
  const navigate = useNavigate();
  const { currentEvent, checkedIn, isCurrentEventLive } = useEventContext();
  const [myQueue, setMyQueue] = useState<EventQueueItem[]>([]);
  const [globalQueue, setGlobalQueue] = useState<EventQueueItem[]>([]);
  const [songDetailsMap, setSongDetailsMap] = useState<{ [songId: number]: Song }>({});
  const [favorites, setFavorites] = useState<Song[]>([]);
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
  const [selectedQueueId, setSelectedQueueId] = useState<number | undefined>(undefined);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [isSearching, setIsSearching] = useState<boolean>(false);
  const [showSearchModal, setShowSearchModal] = useState<boolean>(false);

  // Set up sensors for drag-and-drop
  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    })
  );

  // Fetch favorites on component mount
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

  // Fetch queues and song details when currentEvent changes
  useEffect(() => {
    if (!currentEvent) {
      setMyQueue([]);
      setGlobalQueue([]);
      setSongDetailsMap({});
      return;
    }

    const token = localStorage.getItem("token");
    const userName = localStorage.getItem("userName");
    if (!token || !userName) {
      console.error("No token or userName found");
      setMyQueue([]);
      setGlobalQueue([]);
      setSongDetailsMap({});
      return;
    }

    const fetchQueue = async () => {
      try {
        const queueResponse = await fetch(`${API_ROUTES.EVENT_QUEUE}/${currentEvent.eventId}/queue`, {
          headers: { Authorization: `Bearer ${token}` },
        });
        if (!queueResponse.ok) throw new Error(`Fetch queue failed for event ${currentEvent.eventId}: ${queueResponse.status}`);
        const data: EventQueueItem[] = await queueResponse.json();
        console.log(`Fetched queue for event ${currentEvent.eventId}:`, data);

        // Filter for the user's songs
        const userQueue = data.filter(item => item.requestorId === userName);
        setMyQueue(userQueue);

        // Set global queue if checked into a live event
        if (checkedIn && isCurrentEventLive) {
          setGlobalQueue(data);
        } else {
          setGlobalQueue([]);
        }

        // Fetch song details for each queue item
        const songDetails: { [songId: number]: Song } = {};
        for (const item of data) {
          if (!songDetails[item.songId]) {
            try {
              const songResponse = await fetch(`/api/songs/${item.songId}`, {
                headers: { Authorization: `Bearer ${token}` },
              });
              if (!songResponse.ok) throw new Error(`Fetch song details failed for song ${item.songId}: ${songResponse.status}`);
              const songData = await songResponse.json();
              songDetails[item.songId] = songData;
            } catch (err) {
              console.error(`Error fetching song details for song ${item.songId}:`, err);
              // Fallback: use placeholder data if fetching fails
              songDetails[item.songId] = {
                id: item.songId,
                title: `Song ${item.songId}`,
                artist: 'Unknown',
                status: 'unknown',
                bpm: 0,
                danceability: 0,
                energy: 0,
                valence: undefined,
                popularity: 0,
                genre: undefined,
                decade: undefined,
                requestDate: '',
                requestedBy: '',
                spotifyId: undefined
              };
            }
          }
        }
        setSongDetailsMap(songDetails);
      } catch (err) {
        console.error(`Fetch queue error for event ${currentEvent.eventId}:`, err);
        setMyQueue([]);
        setGlobalQueue([]);
        setSongDetailsMap({});
      }
    };

    fetchQueue();
  }, [currentEvent, checkedIn, isCurrentEventLive]);

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
      decade: song.decade || null,
      status: "pending",
      requestDate: new Date().toISOString(),
      requestedBy: userId
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
    setSelectedQueueId(undefined);
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

  const addToEventQueue = async (song: Song, eventId: number) => {
    const token = localStorage.getItem("token");
    const requestorId = localStorage.getItem("userName");
    console.log("addToEventQueue - token:", token, "requestorId:", requestorId);

    if (!token) {
      console.error("No token found in addToEventQueue");
      throw new Error("Authentication token missing. Please log in again.");
    }

    if (!requestorId) {
      console.error("Invalid or missing requestorId in addToEventQueue");
      throw new Error("User not found. Please log in again to add songs to the queue.");
    }

    const queueForEvent = myQueue || [];
    const isInQueue = queueForEvent.some(q => q.songId === song.id);
    if (isInQueue) {
      console.log(`Song ${song.id} is already in the queue for event ${eventId}`);
      return;
    }

    try {
      const response = await fetch(`${API_ROUTES.EVENT_QUEUE}/${eventId}/queue`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          songId: song.id,
          requestorId: requestorId,
        }),
      });

      const responseText = await response.text();
      console.log(`Add to queue response for event ${eventId}: status=${response.status}, body=${responseText}`);

      if (!response.ok) {
        console.error(`Failed to add song to queue for event ${eventId}: ${response.status} - ${responseText}`);
        throw new Error(`Add to queue failed: ${responseText || response.statusText}`);
      }

      const newQueueItem: EventQueueItem = JSON.parse(responseText);
      console.log(`Added to queue for event ${eventId}:`, newQueueItem);
      setMyQueue(prev => [...prev, newQueueItem]);

      // Fetch song details for the new queue item
      try {
        const songResponse = await fetch(`/api/songs/${song.id}`, {
          headers: { Authorization: `Bearer ${token}` },
        });
        if (!songResponse.ok) throw new Error(`Fetch song details failed for song ${song.id}: ${songResponse.status}`);
        const songData = await songResponse.json();
        setSongDetailsMap(prev => ({
          ...prev,
          [song.id]: songData,
        }));
      } catch (err) {
        console.error(`Error fetching song details for song ${song.id}:`, err);
        setSongDetailsMap(prev => ({
          ...prev,
          [song.id]: {
            id: song.id,
            title: song.title || `Song ${song.id}`,
            artist: song.artist || 'Unknown',
            status: song.status || 'unknown',
            bpm: song.bpm || 0,
            danceability: song.danceability || 0,
            energy: song.energy || 0,
            valence: song.valence || undefined,
            popularity: song.popularity || 0,
            genre: song.genre || undefined,
            decade: song.decade || undefined,
            requestDate: song.requestDate || '',
            requestedBy: song.requestedBy || '',
            spotifyId: song.spotifyId || undefined
          },
        }));
      }
    } catch (err) {
      console.error("Add to queue error:", err);
      throw err;
    }
  };

  const handleDeleteSong = async (eventId: number, queueId: number) => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      return;
    }

    try {
      const response = await fetch(`${API_ROUTES.EVENT_QUEUE}/${eventId}/queue/${queueId}/skip`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        const errorText = await response.text();
        console.error(`Skip song failed: ${response.status} - ${errorText}`);
        throw new Error(`Skip song failed: ${response.status}`);
      }

      setMyQueue(prev => prev.filter(q => q.queueId !== queueId));
      setGlobalQueue(prev => prev.filter(q => q.queueId !== queueId));
      setShowActions(null);
    } catch (err) {
      console.error("Skip song error:", err);
      setMyQueue(prev => prev.filter(q => q.queueId !== queueId));
      setGlobalQueue(prev => prev.filter(q => q.queueId !== queueId));
      setShowActions(null);
    }
  };

  const handleQueueItemClick = (song: Song, queueId: number) => {
    setSelectedSong(song);
    setSelectedQueueId(queueId);
  };

  const handleDragEnd = async (event: any) => {
    const { active, over } = event;

    if (!active || !over || active.id === over.id) {
      return;
    }

    const oldIndex = myQueue.findIndex(item => item.queueId === active.id);
    const newIndex = myQueue.findIndex(item => item.queueId === over.id);

    // Reorder the local queue
    const newQueue = [...myQueue];
    const [movedItem] = newQueue.splice(oldIndex, 1);
    newQueue.splice(newIndex,0, movedItem);

    // Update positions
    const updatedQueue = newQueue.map((item, index) => ({
      ...item,
      position: myQueue[index].position, // Maintain the original global positions
    }));

    setMyQueue(updatedQueue);

    // Prepare the new order for the backend
    const newOrder = updatedQueue.map(item => ({
      QueueId: item.queueId,
      Position: item.position,
    }));

    // Send the new order to the backend
    const token = localStorage.getItem("token");
    if (!token || !currentEvent) {
      console.error("No token or current event");
      return;
    }

    try {
      const response = await fetch(`${API_ROUTES.EVENT_QUEUE}/${currentEvent.eventId}/queue/reorder`, {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ NewOrder: newOrder }),
      });

      if (!response.ok) {
        const errorText = await response.text();
        console.error(`Reorder failed: ${response.status} - ${errorText}`);
        throw new Error(`Reorder failed: ${response.status}`);
      }

      // Re-fetch the queue to reflect the updated order
      const queueResponse = await fetch(`${API_ROUTES.EVENT_QUEUE}/${currentEvent.eventId}/queue`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      if (!queueResponse.ok) throw new Error(`Fetch queue failed: ${queueResponse.status}`);
      const data: EventQueueItem[] = await queueResponse.json();
      const userName = localStorage.getItem("userName");
      const userQueue = data.filter(item => item.requestorId === userName);
      setMyQueue(userQueue);
      if (checkedIn && isCurrentEventLive) {
        setGlobalQueue(data);
      }
    } catch (err) {
      console.error("Reorder error:", err);
      // Revert the queue on error
      setMyQueue(myQueue);
    }
  };

  return (
    <div className="dashboard">
      <div className="dashboard-content">
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
            <button
              className="browse-songs-button"
              onClick={() => navigate('/explore-songs')}
            >
              Browse Karaoke Songs
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

          <aside className="queue-panel">
            <h2>My Song Queue</h2>
            {!currentEvent ? (
              <p>Please select an event to view your queue.</p>
            ) : myQueue.length === 0 ? (
              <p>No songs in your queue for this event.</p>
            ) : (
              <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
                <SortableContext items={myQueue.map(item => item.queueId)} strategy={verticalListSortingStrategy}>
                  <div className="event-queue">
                    <h3>Event {currentEvent.eventId}</h3>
                    {myQueue.map(queueItem => (
                      <SortableQueueItem
                        key={queueItem.queueId}
                        queueItem={queueItem}
                        eventId={currentEvent.eventId}
                        songDetails={songDetailsMap[queueItem.songId] || null}
                        onClick={handleQueueItemClick}
                      />
                    ))}
                  </div>
                </SortableContext>
              </DndContext>
            )}
          </aside>

          {checkedIn && isCurrentEventLive && (
            <aside className="global-queue-panel">
              <h2>Global Event Queue</h2>
              {globalQueue.length === 0 ? (
                <p>No songs in the event queue.</p>
              ) : (
                <div className="event-queue">
                  <h3>Event {currentEvent.eventId}</h3>
                  {globalQueue.map(queueItem => (
                    <div key={queueItem.queueId} className="queue-song">
                      <span>
                        {songDetailsMap[queueItem.songId] ? (
                          `${songDetailsMap[queueItem.songId].title} - ${songDetailsMap[queueItem.songId].artist}`
                        ) : (
                          queueItem.songId
                        )}
                        {queueItem.isCurrentlyPlaying && <span className="request-badge">Playing</span>}
                        {queueItem.wasSkipped && <span className="request-badge">Skipped</span>}
                        {queueItem.isOnBreak && <span className="request-badge">On Break</span>}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </aside>
          )}
        </div>

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
          isInQueue={currentEvent ? myQueue.some(q => q.songId === selectedSong.id) : false}
          onClose={() => {
            setSelectedSong(null);
            setSelectedQueueId(undefined);
          }}
          onToggleFavorite={toggleFavorite}
          onAddToQueue={addToEventQueue}
          onDeleteFromQueue={currentEvent && selectedQueueId ? handleDeleteSong : undefined}
          eventId={currentEvent?.eventId}
          queueId={selectedQueueId}
        />
      )}
    </div>
  );
};

export default Dashboard;