import React, { useState, useEffect } from 'react';
import { FaMicrophone, FaStar } from 'react-icons/fa';

const SingerSearch: React.FC = () => {
    const [songs, setSongs] = useState<any[]>([]);
    const [query, setQuery] = useState('');

    useEffect(() => {
        const root = document.getElementById('react-root');
        const initialSongs = JSON.parse(root?.dataset.initialSongs || '[]');
        setSongs(initialSongs);
    }, []);

    const handleSearch = async () => {
        const res = await fetch(`/api/songs/search?query=${query}`);
        const data = await res.json();
        setSongs(data);
    };

    return (
        <div className="p-6">
            <div className="flex items-center mb-6 bg-blue-50 p-4 rounded-lg shadow-lg">
                <FaMicrophone className="text-blue-600 text-2xl animate-bounce mr-3" />
                <input
                    type="text"
                    value={query}
                    onChange={(e) => setQuery(e.target.value)}
                    placeholder="Find Your Party Anthem!"
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
                {songs.map((song) => (
                    <div
                        key={song.id}
                        className="bg-white p-5 rounded-lg shadow-md hover:shadow-xl transition-transform transform hover:scale-105"
                    >
                        <p className="font-bold text-blue-900 text-lg font-poppins">
                            {song.title} - {song.artist}
                        </p>
                        <p className="text-gray-600">BPM: {song.bpm} | Genre: {song.genre}</p>
                        <div className="flex space-x-3 mt-3">
                            <button className="bg-coral-500 text-white px-4 py-2 rounded-lg hover:bg-coral-600 animate-pulse">
                                Add to Queue
                            </button>
                            <FaStar
                                className="text-yellow-400 text-2xl hover:animate-spin cursor-pointer"
                                title="Add to Favorites"
                            />
                        </div>
                    </div>
                ))}
            </div>
            <a href="/Request" className="mt-6 inline-block bg-blue-600 text-white px-6 py-3 rounded-lg hover:bg-blue-700 font-poppins">
                Suggest a Party Hit!
            </a>
        </div>
    );
};

export default SingerSearch;