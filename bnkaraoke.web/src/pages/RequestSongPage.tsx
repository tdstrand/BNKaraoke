import React, { useState, useEffect } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { API_ROUTES } from "../config/apiConfig"; // Only API_ROUTES
import "../pages/Dashboard.css";

const RequestSongPage: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const initialQuery = location.state && location.state.searchQuery ? location.state.searchQuery : "";
  const [searchQuery, setSearchQuery] = useState<string>(initialQuery);
  const [searchResults, setSearchResults] = useState<any[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (searchQuery) {
      handleSearch(localStorage.getItem("token") || "");
    }
  }, [searchQuery]);

  const handleSearch = async (token: string) => {
    try {
      const response = await fetch(`${API_ROUTES.SPOTIFY_SEARCH}?query=${encodeURIComponent(searchQuery)}`, { // Updated
        headers: { Authorization: `Bearer ${token}` },
      });
      const responseText = await response.text();
      console.log("Request Song Search Raw Response:", responseText);
      if (!response.ok) throw new Error(`Failed to search: ${response.status} ${response.statusText} - ${responseText}`);
      const data = JSON.parse(responseText);
      setSearchResults(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
      setSearchResults([]);
    }
  };

  const handleRequestSong = async (song: any) => {
    const token = localStorage.getItem("token") || "";
    const requestBody = {
      title: song.title,
      artist: song.artist,
      spotifyId: song.spotifyId,
      bpm: song.bpm,
      danceability: song.danceability,
      energy: song.energy,
      popularity: song.popularity,
      genre: song.genre,
      status: "pending",
      requestDate: new Date().toISOString(),
      requestedBy: "12345678901",
    };

    try {
      const response = await fetch(API_ROUTES.REQUEST_SONG, { // Updated
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify(requestBody),
      });
      const responseText = await response.text();
      console.log("Request Song Raw Response:", responseText);
      if (!response.ok) throw new Error(`Failed to request song: ${response.status} ${response.statusText} - ${responseText}`);
      alert("Song requested successfully!");
      navigate("/dashboard");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
    }
  };

  return (
    <div className="dashboard-container">
      <h1 className="dashboard-title">Request a Karaoke Song</h1>
      <div className="dashboard-card">
        <div className="search-bar">
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search for a song..."
          />
          <button onClick={() => handleSearch(localStorage.getItem("token") || "")} className="dashboard-button">
            Search
          </button>
        </div>
        {error && <p className="error-text">{error}</p>}
        {searchResults.length > 0 ? (
          <ul className="song-list">
            {searchResults.map((song) => (
              <li key={song.spotifyId} className="song-item">
                <div>
                  <p className="song-title">{song.title} - {song.artist}</p>
                  <p className="song-text">Genre: {song.genre}</p>
                </div>
                <button
                  className="dashboard-button action-button"
                  onClick={() => handleRequestSong(song)}
                >
                  Request This Song
                </button>
              </li>
            ))}
          </ul>
        ) : (
          <p className="dashboard-text">No results yet. Search for a song!</p>
        )}
      </div>
    </div>
  );
};

export default RequestSongPage;