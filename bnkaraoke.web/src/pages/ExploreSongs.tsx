// BNKaraoke/bnkaraoke.web/src/pages/ExploreSongs.tsx
import React, { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { API_ROUTES } from '../config/apiConfig';
import SongDetailsModal from '../components/SongDetailsModal';
import './ExploreSongs.css';
import { Song, QueueItem } from '../types';

const ExploreSongs: React.FC = () => {
  const navigate = useNavigate();
  const [queue, setQueue] = useState<QueueItem[]>([]);
  const [favorites, setFavorites] = useState<Song[]>([]);
  const [artistFilter, setArtistFilter] = useState<string>("All Artists");
  const [decadeFilter, setDecadeFilter] = useState<string>("All Decades");
  const [genreFilter, setGenreFilter] = useState<string>("All Genres");
  const [popularityFilter, setPopularityFilter] = useState<string>("All Popularities");
  const [showFilterDropdown, setShowFilterDropdown] = useState<string | null>(null);
  const [browseSongs, setBrowseSongs] = useState<Song[]>([]);
  const [page, setPage] = useState<number>(1);
  const [pageSize] = useState<number>(50);
  const [totalPages, setTotalPages] = useState<number>(1);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [selectedSong, setSelectedSong] = useState<Song | null>(null);
  const [artists, setArtists] = useState<string[]>(["All Artists"]);
  const [genres, setGenres] = useState<string[]>(["All Genres"]);
  const [artistError, setArtistError] = useState<string | null>(null);
  const [genreError, setGenreError] = useState<string | null>(null);
  const maxRetries = 3;

  // Static data for Decade and Popularity (working as-is)
  const decades = ["All Decades", ...["1960s", "1970s", "1980s", "1990s", "2000s", "2010s", "2020s"].sort()];
  const popularityRanges = ["All Popularities", ...["Very Popular (80+)", "Popular (50-79)", "Moderate (20-49)", "Less Popular (0-19)"].sort()];

  // Fetch favorites on mount
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

  // Fetch artists with retry mechanism
  const fetchArtists = useCallback(async (retryCount: number) => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found for artists fetch");
      return;
    }

    try {
      console.log(`Fetching artists from: ${API_ROUTES.ARTISTS}`);
      const response = await fetch(API_ROUTES.ARTISTS, {
        headers: { Authorization: `Bearer ${token}` },
      });
      const responseText = await response.text();
      if (!response.ok) {
        console.error(`Fetch artists failed with status: ${response.status}, response: ${responseText}`);
        throw new Error(`Fetch artists failed with status: ${response.status}`);
      }
      const data = JSON.parse(responseText);
      const artistList = (data as string[] || []).sort();
      setArtists(["All Artists", ...artistList]);
      setArtistError(null);
    } catch (err) {
      console.error("Fetch artists error:", err);
      if (retryCount < maxRetries) {
        console.log(`Retrying artists fetch, attempt ${retryCount + 1}/${maxRetries}`);
        setTimeout(() => fetchArtists(retryCount + 1), 3000); // Retry after 3 seconds
      } else {
        setArtists(["All Artists"]);
        setArtistError("Failed to load artists after retries. Please refresh the page.");
      }
    }
  }, []);

  // Fetch genres with retry mechanism
  const fetchGenres = useCallback(async (retryCount: number) => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found for genres fetch");
      return;
    }

    try {
      console.log(`Fetching genres from: ${API_ROUTES.GENRES}`);
      const response = await fetch(API_ROUTES.GENRES, {
        headers: { Authorization: `Bearer ${token}` },
      });
      const responseText = await response.text();
      if (!response.ok) {
        console.error(`Fetch genres failed with status: ${response.status}, response: ${responseText}`);
        throw new Error(`Fetch genres failed with status: ${response.status}`);
      }
      const data = JSON.parse(responseText);
      const genreList = (data as string[] || []).sort();
      setGenres(["All Genres", ...genreList]);
      setGenreError(null);
    } catch (err) {
      console.error("Fetch genres error:", err);
      if (retryCount < maxRetries) {
        console.log(`Retrying genres fetch, attempt ${retryCount + 1}/${maxRetries}`);
        setTimeout(() => fetchGenres(retryCount + 1), 3000); // Retry after 3 seconds
      } else {
        setGenres(["All Genres"]);
        setGenreError("Failed to load genres after retries. Please refresh the page.");
      }
    }
  }, []);

  // Fetch artists and genres on mount
  useEffect(() => {
    fetchArtists(0);
    fetchGenres(0);
  }, [fetchArtists, fetchGenres]);

  // Fetch songs based on all active filters
  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      setBrowseSongs([]);
      return;
    }

    // Log current filter values for debugging
    console.log("Current filters:", {
      artistFilter,
      decadeFilter,
      genreFilter,
      popularityFilter,
    });

    // Construct query parameters based on all active filters
    const queryParams: string[] = [];
    if (artistFilter !== "All Artists") {
      queryParams.push(`artist=${encodeURIComponent(artistFilter)}`);
    }
    if (decadeFilter !== "All Decades") {
      queryParams.push(`decade=${encodeURIComponent(decadeFilter.toLowerCase())}`);
    }
    if (genreFilter !== "All Genres") {
      queryParams.push(`genre=${encodeURIComponent(genreFilter)}`);
    }
    if (popularityFilter !== "All Popularities") {
      let sortParam = "";
      switch (popularityFilter) {
        case "Very Popular (80+)":
          sortParam = "popularity=80-100";
          break;
        case "Popular (50-79)":
          sortParam = "popularity=50-79";
          break;
        case "Moderate (20-49)":
          sortParam = "popularity=20-49";
          break;
        case "Less Popular (0-19)":
          sortParam = "popularity=0-19";
          break;
        default:
          sortParam = "popularity";
      }
      queryParams.push(sortParam);
    }

    // If no filters are active, fetch all songs
    const queryString = queryParams.length > 0 ? queryParams.join('&') : "query=all";
    const url = `${API_ROUTES.SONGS_SEARCH}?${queryString}&sort=title&page=${page}&pageSize=${pageSize}`;
    console.log("Fetching songs with URL:", url);

    setIsLoading(true);
    fetch(url, {
      headers: { Authorization: `Bearer ${token}` },
    })
      .then(res => {
        if (!res.ok) throw new Error(`Browse failed: ${res.status}`);
        return res.json();
      })
      .then(data => {
        // Filter for active songs only
        const newSongs = ((data.songs as Song[]) || []).filter(song => song.status?.toLowerCase() === 'active');
        // Fallback client-side sorting if backend doesn't support sort=title
        newSongs.sort((a, b) => a.title.localeCompare(b.title));
        setBrowseSongs(newSongs);
        setTotalPages(data.totalPages || 1);
        setIsLoading(false);
      })
      .catch(err => {
        console.error("Browse error:", err);
        setBrowseSongs([]);
        setIsLoading(false);
      });
  }, [artistFilter, decadeFilter, genreFilter, popularityFilter, page, pageSize]);

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
  };

  const handleFilterSelect = (type: string, value: string) => {
    if (type === "Artist") setArtistFilter(value);
    if (type === "Decade") setDecadeFilter(value);
    if (type === "Genre") setGenreFilter(value);
    if (type === "Popularity") setPopularityFilter(value);
    setPage(1);
    setBrowseSongs([]);
    setShowFilterDropdown(null);
  };

  const resetFilter = (type: string) => {
    if (type === "Artist") setArtistFilter("All Artists");
    if (type === "Decade") setDecadeFilter("All Decades");
    if (type === "Genre") setGenreFilter("All Genres");
    if (type === "Popularity") setPopularityFilter("All Popularities");
    setPage(1);
    setBrowseSongs([]);
  };

  const resetAllFilters = () => {
    setArtistFilter("All Artists");
    setDecadeFilter("All Decades");
    setGenreFilter("All Genres");
    setPopularityFilter("All Popularities");
    setPage(1);
    setBrowseSongs([]);
  };

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
  };

  return (
    <div className="explore-songs">
      <header className="explore-header">
        <h1>Explore Songs</h1>
        <div className="header-buttons">
          <button onClick={resetAllFilters} className="reset-button">Reset All</button>
          <button onClick={() => navigate('/dashboard')} className="back-button">Back to Dashboard</button>
        </div>
      </header>

      <section className="browse-section">
        {artistError && <p className="error-message">{artistError}</p>}
        {genreError && <p className="error-message">{genreError}</p>}
        <div className="filter-tabs">
          <div className="filter-tab">
            <div className="filter-tab-header">
              <button
                className={artistFilter !== "All Artists" ? "active" : ""}
                onClick={() => setShowFilterDropdown(showFilterDropdown === "Artist" ? null : "Artist")}
              >
                {artistFilter} ▼
              </button>
              {artistFilter !== "All Artists" && (
                <button className="reset-filter" onClick={() => resetFilter("Artist")}>✕</button>
              )}
            </div>
            {showFilterDropdown === "Artist" && (
              <div className="filter-dropdown">
                {artists.map(artist => (
                  <button
                    key={artist}
                    onClick={() => handleFilterSelect("Artist", artist)}
                  >
                    {artist}
                  </button>
                ))}
              </div>
            )}
          </div>
          <div className="filter-tab">
            <div className="filter-tab-header">
              <button
                className={decadeFilter !== "All Decades" ? "active" : ""}
                onClick={() => setShowFilterDropdown(showFilterDropdown === "Decade" ? null : "Decade")}
              >
                {decadeFilter} ▼
              </button>
              {decadeFilter !== "All Decades" && (
                <button className="reset-filter" onClick={() => resetFilter("Decade")}>✕</button>
              )}
            </div>
            {showFilterDropdown === "Decade" && (
              <div className="filter-dropdown">
                {decades.map(decade => (
                  <button
                    key={decade}
                    onClick={() => handleFilterSelect("Decade", decade)}
                  >
                    {decade}
                  </button>
                ))}
              </div>
            )}
          </div>
          <div className="filter-tab">
            <div className="filter-tab-header">
              <button
                className={genreFilter !== "All Genres" ? "active" : ""}
                onClick={() => setShowFilterDropdown(showFilterDropdown === "Genre" ? null : "Genre")}
              >
                {genreFilter} ▼
              </button>
              {genreFilter !== "All Genres" && (
                <button className="reset-filter" onClick={() => resetFilter("Genre")}>✕</button>
              )}
            </div>
            {showFilterDropdown === "Genre" && (
              <div className="filter-dropdown">
                {genres.map(genre => (
                  <button
                    key={genre}
                    onClick={() => handleFilterSelect("Genre", genre)}
                  >
                    {genre}
                  </button>
                ))}
              </div>
            )}
          </div>
          <div className="filter-tab">
            <div className="filter-tab-header">
              <button
                className={popularityFilter !== "All Popularities" ? "active" : ""}
                onClick={() => setShowFilterDropdown(showFilterDropdown === "Popularity" ? null : "Popularity")}
              >
                {popularityFilter} ▼
              </button>
              {popularityFilter !== "All Popularities" && (
                <button className="reset-filter" onClick={() => resetFilter("Popularity")}>✕</button>
              )}
            </div>
            {showFilterDropdown === "Popularity" && (
              <div className="filter-dropdown">
                {popularityRanges.map(range => (
                  <button
                    key={range}
                    onClick={() => handleFilterSelect("Popularity", range)}
                  >
                    {range}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
        <div className="song-grid">
          {isLoading ? (
            <p>Loading...</p>
          ) : browseSongs.length === 0 ? (
            <p>No songs found</p>
          ) : (
            browseSongs.map(song => (
              <div key={song.id} className="song-card">
                <div className="song-info" onClick={() => setSelectedSong(song)}>
                  <span>{song.title} - {song.artist}</span>
                </div>
              </div>
            ))
          )}
        </div>
        {totalPages > 1 && (
          <div className="pagination">
            <button
              className="pagination-button"
              disabled={page === 1}
              onClick={() => handlePageChange(page - 1)}
            >
              Previous
            </button>
            <span>Page {page} of {totalPages}</span>
            <button
              className="pagination-button"
              disabled={page === totalPages}
              onClick={() => handlePageChange(page + 1)}
            >
              Next
            </button>
          </div>
        )}
      </section>

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
    </div>
  );
};

export default ExploreSongs;