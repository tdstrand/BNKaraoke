import React, { useState } from 'react';
import { FaMicrophone } from 'react-icons/fa';

const SongRequest: React.FC = () => {
    const [query, setQuery] = useState('');
    const [results, setResults] = useState<any[]>([]);

    const handleSearch = async () => {
        // Placeholder: Replace with real Spotify API call
        const mockResults = [
            { id: '1', title: 'Sweet Caroline', artist: 'Neil Diamond', bpm: 127.8, genre: 'Pop' },
            { id: '2', title: 'Bohemian Rhapsody', artist: 'Queen', bpm: 71, genre: 'Rock' },
        ];
        setResults(mockResults);
    };

    const handleRequest = async (song: any) => {
        await fetch('/api/songs/request', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(song),
        });
        alert('Song requested!');
    };

    return (
        <div className="p-6">
            <div className="flex items-center mb-6 bg-blue-50 p-4 rounded-lg shadow-lg">
                <FaMicrophone className="text-blue-600 text-2xl animate-bounce mr-3" />
                <input
                    type="text"
                    value={query}
                    onChange={(e) => setQuery(e.target.value)}
                    placeholder="Search Spotify for a Party Hit!"
                    className="w-full p-3 border rounded-lg focus:ring-2 focus:ring-blue-500 font-poppins"
                />
                <button
                    onClick={handleSearch}
                    className="ml-2 bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700"
                >
                    Search
                </button>
            </div>
            <div className="space-y-4">
                {results.map((song) => (
                    <div
                        key={song.id}
                        className="bg-white p-5 rounded-lg shadow-md hover:shadow-xl transition-transform transform hover:scale-105"
                    >
                        <p className="font-bold text-blue-900 text-lg font-poppins">
                            {song.title} - {song.artist}
                        </p>
                        <p className="text-gray-600">BPM: {song.bpm} | Genre: {song.genre}</p>
                        <button
                            onClick={() => handleRequest(song)}
                            className="mt-3 bg-coral-500 text-white px-4 py-2 rounded-lg hover:bg-coral-600 animate-pulse"
                        >
                            Request This Hit!
                        </button>
                    </div>
                ))}
            </div>
        </div>
    );
};

export default SongRequest;