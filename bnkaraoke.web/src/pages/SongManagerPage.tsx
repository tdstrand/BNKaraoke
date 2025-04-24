import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { API_ROUTES } from "../config/apiConfig";
import "../pages/Dashboard.css";

const SongManagerPage: React.FC = () => {
  const navigate = useNavigate();
  const [pendingSongs, setPendingSongs] = useState<any[]>([]);
  const [youtubeResults, setYoutubeResults] = useState<any[]>([]);
  const [selectedSongId, setSelectedSongId] = useState<number | null>(null);
  const [manualLinks, setManualLinks] = useState<{ [key: number]: string }>({});
  const [showManualInput, setShowManualInput] = useState<{ [key: number]: boolean }>({});
  const [showYoutubeModal, setShowYoutubeModal] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [roles, setRoles] = useState<string[]>([]);

  useEffect(() => {
    const token = localStorage.getItem("token");
    const storedRoles = localStorage.getItem("roles");
    if (!token) {
      console.log("No token found, redirecting to login");
      navigate("/");
      return;
    }
    if (storedRoles) {
      const parsedRoles = JSON.parse(storedRoles);
      setRoles(parsedRoles);
      if (!parsedRoles.includes("Song Manager")) {
        console.log("User lacks Song Manager role, redirecting to dashboard");
        navigate("/dashboard");
        return;
      }
    }
    fetchPendingSongs(token);
  }, [navigate]);

  const fetchPendingSongs = async (token: string) => {
    try {
      console.log(`Fetching pending songs from: ${API_ROUTES.PENDING_SONGS}`);
      const response = await fetch(API_ROUTES.PENDING_SONGS, {
        headers: { Authorization: `Bearer ${token}` },
      });
      const responseText = await response.text();
      console.log("Pending Songs Raw Response:", responseText);
      if (!response.ok) {
        throw new Error(`Failed to fetch pending songs: ${response.status} ${response.statusText} - ${responseText}`);
      }
      const data = JSON.parse(responseText);
      setPendingSongs(data);
      setError(null);
    } catch (err) {
      const errorMessage = err instanceof Error ? 
        `${err.message} (Network error: ${err.cause ? err.cause : 'Unknown'})` : 
        "Unknown fetch error";
      setError(errorMessage);
      setPendingSongs([]);
      console.error("Fetch Pending Songs Error:", errorMessage, {
        name: (err as Error).name,
        message: (err as Error).message,
        stack: (err as Error).stack,
        cause: (err as Error).cause
      });
    }
  };

  const handleYoutubeSearch = async (songId: number, title: string, artist: string, token: string) => {
    try {
      const query = `Karaoke ${title} ${artist}`;
      console.log(`Fetching YouTube search for song ${songId} from: ${API_ROUTES.YOUTUBE_SEARCH}`);
      const response = await fetch(`${API_ROUTES.YOUTUBE_SEARCH}?query=${encodeURIComponent(query)}`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      const responseText = await response.text();
      console.log(`YouTube Search Raw Response for Song ${songId}:`, responseText);
      if (!response.ok) {
        throw new Error(`Failed to search YouTube: ${response.status} ${response.statusText} - ${responseText}`);
      }
      const data = JSON.parse(responseText);
      console.log(`YouTube Results Parsed for Song ${songId}:`, data);
      setYoutubeResults(data);
      setSelectedSongId(songId);
      setShowYoutubeModal(true);
      setError(null);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : "Unknown fetch error";
      setError(errorMessage);
      setYoutubeResults([]);
      console.error("YouTube Search Error:", errorMessage, err);
    }
  };

  const handleApproveSong = async (songId: number, YouTubeUrl: string, token: string) => {
    try {
      console.log(`Approving song ${songId} at: ${API_ROUTES.APPROVE_SONGS}`); // Fixed typo: APPROVE_SONG -> APPROVE_SONGS
      const response = await fetch(API_ROUTES.APPROVE_SONGS, { // Fixed typo: APPROVE_SONG -> APPROVE_SONGS
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ id: songId, YouTubeUrl }),
      });
      const responseText = await response.text();
      console.log(`Approve Song Raw Response for Song ${songId}:`, responseText);
      if (!response.ok) {
        throw new Error(`Failed to approve song: ${response.status} ${response.statusText} - ${responseText}`);
      }
      alert("Song approved successfully!");
      fetchPendingSongs(token);
      setShowManualInput((prev) => ({ ...prev, [songId]: false }));
      setShowYoutubeModal(false);
      setYoutubeResults([]);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : "Unknown fetch error";
      setError(errorMessage);
      console.error("Approve Song Error:", errorMessage, err);
    }
  };

  const handleRejectSong = async (songId: number, token: string) => {
    try {
      console.log(`Rejecting song ${songId} at: ${API_ROUTES.REJECT_SONG}`);
      const response = await fetch(API_ROUTES.REJECT_SONG, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ id: songId }),
      });
      const responseText = await response.text();
      console.log(`Reject Song Raw Response for Song ${songId}:`, responseText);
      if (!response.ok) {
        throw new Error(`Failed to reject song: ${response.status} ${response.statusText} - ${responseText}`);
      }
      alert("Song rejected successfully!");
      fetchPendingSongs(token);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : "Unknown fetch error";
      setError(errorMessage);
      console.error("Reject Song Error:", errorMessage, err);
    }
  };

  const toggleManualInput = (songId: number) => {
    setShowManualInput((prev) => ({ ...prev, [songId]: !prev[songId] }));
  };

  const handleManualLinkChange = (songId: number, value: string) => {
    setManualLinks((prev) => ({ ...prev, [songId]: value }));
  };

  const token = localStorage.getItem("token") || "";

  return (
    <div className="dashboard-container">
      <div className="dashboard-navbar">
        <span className="dashboard-user">Song Manager</span>
        <button className="logout-button" onClick={() => { localStorage.clear(); navigate("/"); }}>
          Logout
        </button>
      </div>

      <h1 className="dashboard-title">Song Manager</h1>

      <section className="dashboard-card">
        <h2 className="section-title">Pending Songs</h2>
        {error && <p className="error-text">{error}</p>}
        {pendingSongs.length > 0 ? (
          <ul className="song-list">
            {pendingSongs.map((song) => (
              <li key={song.id} className="song-item song-manager-item">
                <div className="song-info">
                  <p className="song-title">{song.title} - {song.artist}</p>
                  <p className="song-text">Genre: {song.genre} | Requested by: {song.requestedBy}</p>
                </div>
                <div className="song-actions">
                  <button
                    className="dashboard-button"
                    onClick={() => handleYoutubeSearch(song.id, song.title, song.artist, token)}
                  >
                    Find Karaoke Video
                  </button>
                  <button
                    className="dashboard-button"
                    onClick={() => toggleManualInput(song.id)}
                  >
                    Add Manual Link
                  </button>
                  <button
                    className="dashboard-button reject-button"
                    onClick={() => handleRejectSong(song.id, token)}
                  >
                    Reject
                  </button>
                </div>
                {showManualInput[song.id] && (
                  <div className="manual-link-input">
                    <input
                      type="text"
                      value={manualLinks[song.id] || ""}
                      onChange={(e) => handleManualLinkChange(song.id, e.target.value)}
                      placeholder="Enter YouTube URL"
                      className="search-bar-input"
                    />
                    <button
                      className="dashboard-button approve-button"
                      onClick={() => handleApproveSong(song.id, manualLinks[song.id] || "", token)}
                    >
                      Submit Manual Link
                    </button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        ) : (
          <p className="dashboard-text">No pending songs to review.</p>
        )}
      </section>

      {showYoutubeModal && selectedSongId && (
        <div className="modal-overlay">
          <div className="modal-content edit-user-modal">
            <h2 className="modal-title">Select Karaoke Video for {pendingSongs.find(s => s.id === selectedSongId)?.title}</h2>
            {youtubeResults.length > 0 ? (
              <div className="youtube-results" style={{ maxHeight: "400px", overflowY: "auto" }}>
                {youtubeResults.map((video: any) => (
                  <div key={video.videoId} className="youtube-item">
                    <iframe
                      width="300"
                      height="169"
                      src={`https://www.youtube.com/embed/${video.videoId}`}
                      title={video.title}
                      frameBorder="0"
                      allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                      allowFullScreen
                    />
                    <div style={{ display: "flex", flexDirection: "column", gap: "10px" }}>
                      <a
                        href={video.url}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="watch-link"
                      >
                        Watch on YouTube
                      </a>
                      <button
                        className="dashboard-button approve-button"
                        onClick={() => handleApproveSong(selectedSongId, video.url, token)}
                      >
                        Accept
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <p className="song-text">No karaoke videos found.</p>
            )}
            <div className="modal-buttons">
              <button className="dashboard-button" onClick={() => setShowYoutubeModal(false)}>
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default SongManagerPage;