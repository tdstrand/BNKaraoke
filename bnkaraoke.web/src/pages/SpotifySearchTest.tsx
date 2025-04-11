import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { API_ROUTES } from '../config/apiConfig';
import '../components/Home.css';

const SpotifySearchTest: React.FC = () => {
  const navigate = useNavigate();
  const [searchTerm, setSearchTerm] = useState('');
  const [results, setResults] = useState<any[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const token = localStorage.getItem('token');
    if (!token) {
      navigate('/');
    }
  }, [navigate]);

  const handleSearch = async () => {
    const token = localStorage.getItem('token');
    if (!token) {
      navigate('/');
      return;
    }

    const url = `${API_ROUTES.SPOTIFY_SEARCH}?query=${encodeURIComponent(searchTerm)}`;
    console.log('Fetching URL:', url);

    try {
      const response = await fetch(url, {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      });

      const responseText = await response.text();
      console.log('Spotify API Response:', {
        status: response.status,
        body: responseText,
      });

      if (!response.ok) {
        throw new Error(`API error: ${response.status} - ${responseText}`);
      }

      const data = JSON.parse(responseText);
      setResults(data);
      setError(null);
    } catch (err) {
      setError((err as Error).message);
      setResults([]);
    }
  };

  const handleRequest = async (song: any) => {
    const token = localStorage.getItem('token');
    if (!token) {
      navigate('/');
      return;
    }

    const requestBody = {
      title: song.title,
      artist: song.artist,
      spotifyId: song.spotifyId,
      bpm: song.bpm,
      danceability: song.danceability,
      energy: song.energy,
      popularity: song.popularity,
      genre: song.genre, // Ensure Genre is included
      status: "pending",
      requestDate: new Date().toISOString(),
      requestedBy: "12345678901" // Overridden by backend
    };

    try {
      const response = await fetch(API_ROUTES.REQUEST_SONG, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify(requestBody),
      });

      const responseText = await response.text();
      console.log('Request Song Response:', {
        status: response.status,
        body: responseText,
      });

      if (!response.ok) {
        throw new Error(`Request failed: ${response.status} - ${responseText}`);
      }

      const data = JSON.parse(responseText);
      setError(null);
      alert(data.message);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleKeyPress = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      handleSearch();
    }
  };

  return (
    <div className="home-container">
      <header className="home-header">
        <h1>Spotify Search Test</h1>
        <p>Search for your favorite karaoke songs!</p>
      </header>

      <div className="menu">
        <input
          type="text"
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          onKeyPress={handleKeyPress}
          placeholder="Enter song or artist (e.g., Bohemian)"
          style={{ flex: 1, padding: '10px', borderRadius: '8px 0 0 8px', border: 'none', color: '#000' }}
        />
        <button
          onClick={handleSearch}
          className="menu-item"
          style={{ borderRadius: '0 8px 8px 0' }}
        >
          Search
        </button>
      </div>

      {error && <p style={{ color: '#FF6B6B' }}>{error}</p>}
      {results.length > 0 ? (
        <ul style={{ listStyle: 'none', padding: 0 }}>
          {results.map((song) => (
            <li
              key={song.spotifyId}
              style={{ background: '#fff', color: '#000', padding: '15px', borderRadius: '8px', marginBottom: '10px', boxShadow: '0 2px 4px rgba(0,0,0,0.1)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
            >
              <div>
                <p style={{ fontWeight: 'bold' }}>{song.title} - {song.artist}</p>
                <p>Spotify ID: {song.spotifyId}</p>
                <p>Genre: {song.genre} | Popularity: {song.popularity}</p>
              </div>
              <button
                className="menu-item"
                style={{ padding: '5px 10px' }}
                onClick={() => handleRequest(song)}
              >
                Request
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p>No results yet. Enter a search term and click Search!</p>
      )}
    </div>
  );
};

export default SpotifySearchTest;