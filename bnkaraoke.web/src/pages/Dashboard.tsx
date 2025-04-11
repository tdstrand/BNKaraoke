import React, { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { API_ROUTES } from "../config/apiConfig";
import "../pages/Dashboard.css";

const Dashboard: React.FC = () => {
  const navigate = useNavigate();
  const [firstName, setFirstName] = useState("");
  const [karaokeLibrary, setKaraokeLibrary] = useState<any[]>([]);
  const [totalSongs, setTotalSongs] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize] = useState(50);
  const [favorites, setFavorites] = useState<any[]>([]);
  const [pendingRequests, setPendingRequests] = useState<any[]>([]);
  const [singQueue, setSingQueue] = useState<any[]>([]);
  const [librarySearch, setLibrarySearch] = useState("");
  const [favoritesSearch, setFavoritesSearch] = useState("");
  const [showNotFoundModal, setShowNotFoundModal] = useState(false);
  const [showBrowseModal, setShowBrowseModal] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [browseFilter, setBrowseFilter] = useState({ artist: "", genre: "", popularity: "" });
  const searchInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const token = localStorage.getItem("token");
    const storedFirstName = localStorage.getItem("firstName");
    if (!token) {
      console.log("No token found, redirecting to login");
      navigate("/");
      return;
    }
    if (storedFirstName) setFirstName(storedFirstName);

    fetchKaraokeLibrary(token, 1);
    fetchPendingRequests(token);
  }, [navigate]);

  const fetchKaraokeLibrary = async (token: string, page: number) => {
    try {
      console.log(`Fetching karaoke library from: ${API_ROUTES.SONGS_SEARCH}?query=all&page=${page}&pageSize=${pageSize}`);
      const response = await fetch(`${API_ROUTES.SONGS_SEARCH}?query=all&page=${page}&pageSize=${pageSize}`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      const responseText = await response.text();
      console.log(`Karaoke Library Response - Status: ${response.status}, Body: ${responseText}`);
      if (!response.ok) throw new Error(`Failed to fetch karaoke library: ${response.status} ${response.statusText} - ${responseText}`);
      const data = JSON.parse(responseText);
      console.log("Karaoke Library Parsed Data:", data);
      if (!data || typeof data.totalSongs !== 'number' || !Array.isArray(data.songs)) {
        throw new Error("Invalid response format: Expected { totalSongs, songs }");
      }
      setKaraokeLibrary(data.songs.sort((a: any, b: any) => a.title.localeCompare(b.title)));
      setTotalSongs(data.totalSongs);
      setCurrentPage(data.currentPage || 1);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
      setKaraokeLibrary([]);
      setTotalSongs(0);
      console.error("Fetch Karaoke Library Error:", err);
    }
  };

  const fetchPendingRequests = async (token: string) => {
    try {
      console.log(`Fetching pending requests from: ${API_ROUTES.USER_REQUESTS}, Token: ${token}`);
      const response = await fetch(API_ROUTES.USER_REQUESTS, {
        headers: { Authorization: `Bearer ${token}` },
      });
      const responseText = await response.text();
      console.log(`Pending Requests Response - Status: ${response.status}, Body: ${responseText}`);
      if (!response.ok) throw new Error(`Failed to fetch requests: ${response.status} ${response.statusText} - ${responseText}`);
      const data = JSON.parse(responseText);
      console.log("Pending Requests Parsed Data:", data);
      if (!Array.isArray(data)) {
        throw new Error("Invalid response format: Expected an array");
      }
      setPendingRequests(data.filter((song: any) => song.status === "pending"));
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
      setPendingRequests([]);
      console.error("Fetch Pending Requests Error:", err);
    }
  };

  const handleLibrarySearch = async () => {
    const token = localStorage.getItem("token");
    try {
      console.log(`Searching karaoke library with query: ${librarySearch}`);
      const response = await fetch(`${API_ROUTES.SONGS_SEARCH}?query=${encodeURIComponent(librarySearch)}&page=1&pageSize=${pageSize}`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      const responseText = await response.text();
      console.log(`Search Response - Status: ${response.status}, Body: ${responseText}`);
      if (!response.ok) throw new Error(`Failed to search karaoke library: ${response.status} ${response.statusText} - ${responseText}`);
      const data = JSON.parse(responseText);
      console.log("Search Parsed Data:", data);
      if (!data || typeof data.totalSongs !== 'number' || !Array.isArray(data.songs)) {
        throw new Error("Invalid response format: Expected { totalSongs, songs }");
      }
      if (data.songs.length === 0) {
        setShowNotFoundModal(true);
      } else {
        setKaraokeLibrary(data.songs.sort((a: any, b: any) => a.title.localeCompare(b.title)));
        setTotalSongs(data.totalSongs);
        setCurrentPage(data.currentPage || 1);
        setError(null);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
      setKaraokeLibrary([]);
      console.error("Search Karaoke Library Error:", err);
    }
  };

  const handlePageChange = (newPage: number) => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.log("No token found in handlePageChange");
      return;
    }
    fetchKaraokeLibrary(token, newPage);
  };

  const handleFavoritesSearch = () => {
    const filtered = favorites.filter(
      (song) =>
        song.title.toLowerCase().includes(favoritesSearch.toLowerCase()) ||
        song.artist.toLowerCase().includes(favoritesSearch.toLowerCase())
    );
    setFavorites(filtered);
  };

  const handleBrowseFilter = () => {
    let filtered = karaokeLibrary;
    if (browseFilter.artist) {
      filtered = filtered.filter((song) => song.artist.toLowerCase().includes(browseFilter.artist.toLowerCase()));
    }
    if (browseFilter.genre) {
      filtered = filtered.filter((song) => song.genre.toLowerCase().includes(browseFilter.genre.toLowerCase()));
    }
    if (browseFilter.popularity) {
      filtered = filtered.filter((song) => song.popularity.toString().includes(browseFilter.popularity));
    }
    return filtered;
  };

  const handleAnotherSearch = () => {
    setLibrarySearch("");
    setShowNotFoundModal(false);
    if (searchInputRef.current) searchInputRef.current.focus();
  };

  const handleRequestSongRedirect = () => {
    navigate("/request-song", { state: { searchQuery: librarySearch } });
    setShowNotFoundModal(false);
  };

  return (
    <div className="dashboard-container">
      <h1 className="dashboard-title">Welcome, {firstName || "Singer"}!</h1>
      <div className="card-container">
        <section className="dashboard-card">
          <h2 className="section-title">Available Songs</h2>
          <p className="song-text">{totalSongs} Active Songs</p>
          <div className="search-bar">
            <input
              type="text"
              value={librarySearch}
              onChange={(e) => setLibrarySearch(e.target.value)}
              placeholder="Search Available Songs..."
              ref={searchInputRef}
            />
            <button onClick={handleLibrarySearch} className="dashboard-button">
              Search
            </button>
          </div>
          <button
            className="dashboard-button"
            onClick={() => setShowBrowseModal(true)}
            style={{ marginTop: "10px" }}
          >
            Browse Available Songs
          </button>
        </section>
        <section className="dashboard-card">
          <h2 className="section-title">Your Favorites</h2>
          <div className="search-bar">
            <input
              type="text"
              value={favoritesSearch}
              onChange={(e) => setFavoritesSearch(e.target.value)}
              placeholder="Search Favorites..."
            />
            <button onClick={handleFavoritesSearch} className="dashboard-button">
              Search
            </button>
          </div>
          {favorites.length > 0 ? (
            <ul className="song-list">
              {favorites.map((song) => (
                <li key={song.id} className="song-item">
                  <div>
                    <p className="song-title">{song.title} - {song.artist}</p>
                    <p className="song-text">Genre: {song.genre}</p>
                  </div>
                  <button
                    className="dashboard-button action-button"
                    onClick={() => setSingQueue([...singQueue, song])}
                  >
                    Send to Sing Queue
                  </button>
                </li>
              ))}
            </ul>
          ) : (
            <p className="dashboard-text">No favorites added yet.</p>
          )}
        </section>
        <section className="dashboard-card">
          <h2 className="section-title">Pending Requests</h2>
          {pendingRequests.length > 0 ? (
            <ul className="song-list">
              {pendingRequests.map((song) => (
                <li key={song.id} className="song-item">
                  <p className="song-title">{song.title} - {song.artist}</p>
                  <p className="song-text">Genre: {song.genre} | Awaiting Approval</p>
                </li>
              ))}
            </ul>
          ) : (
            <p className="dashboard-text">No songs awaiting approval yet.</p>
          )}
          {error && <p className="error-text">{error}</p>}
        </section>
        <section className="dashboard-card">
          <h2 className="section-title">Sing Queue</h2>
          {singQueue.length > 0 ? (
            <ul className="song-list">
              {singQueue.map((song) => (
                <li key={song.id} className="song-item">
                  <p className="song-title">{song.title} - {song.artist}</p>
                  <p className="song-text">Genre: {song.genre}</p>
                </li>
              ))}
            </ul>
          ) : (
            <p className="dashboard-text">No songs in your Sing Queue yet.</p>
          )}
        </section>
      </div>

      {showBrowseModal && (
        <div className="modal-overlay">
          <div className="modal-content edit-user-modal">
            <h2 className="modal-title">Browse Available Songs</h2>
            <div className="add-user-form">
              <label className="form-label">Filter by Artist</label>
              <input
                type="text"
                value={browseFilter.artist}
                onChange={(e) => setBrowseFilter({ ...browseFilter, artist: e.target.value })}
                placeholder="Artist Name"
                className="search-bar-input"
              />
              <label className="form-label">Filter by Genre</label>
              <input
                type="text"
                value={browseFilter.genre}
                onChange={(e) => setBrowseFilter({ ...browseFilter, genre: e.target.value })}
                placeholder="Genre"
                className="search-bar-input"
              />
              <label className="form-label">Filter by Popularity</label>
              <input
                type="text"
                value={browseFilter.popularity}
                onChange={(e) => setBrowseFilter({ ...browseFilter, popularity: e.target.value })}
                placeholder="Popularity (0-100)"
                className="search-bar-input"
              />
            </div>
            <ul className="song-list" style={{ maxHeight: "400px", overflowY: "auto" }}>
              {handleBrowseFilter().map((song) => (
                <li key={song.id} className="song-item">
                  <div className="song-info">
                    <p className="song-title">{song.title}</p>
                    <p className="song-text">Artist: {song.artist}</p>
                    <p className="song-text">Genre: {song.genre}</p>
                    <p className="song-text">Popularity: {song.popularity}</p>
                  </div>
                </li>
              ))}
            </ul>
            <div className="modal-buttons">
              <button
                className="dashboard-button"
                onClick={() => handlePageChange(currentPage - 1)}
                disabled={currentPage <= 1}
              >
                Previous
              </button>
              <span>Page {currentPage} of {Math.ceil(totalSongs / pageSize)}</span>
              <button
                className="dashboard-button"
                onClick={() => handlePageChange(currentPage + 1)}
                disabled={currentPage >= Math.ceil(totalSongs / pageSize)}
              >
                Next
              </button>
              <button className="dashboard-button" onClick={() => setShowBrowseModal(false)}>
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {showNotFoundModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3 className="modal-title">Song Not Found</h3>
            <p className="modal-text">"{librarySearch}" isn’t in the Available Songs List.</p>
            <div className="modal-buttons">
              <button className="dashboard-button" onClick={handleAnotherSearch}>
                Do Another Search
              </button>
              <button className="dashboard-button" onClick={handleRequestSongRedirect}>
                Request a Karaoke Song
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default Dashboard;