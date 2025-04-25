// bnkaraoke.web/src/config/apiConfig.ts
// Use environment-based configuration
console.log("NODE_ENV:", process.env.NODE_ENV);

// Dynamically determine the frontend's host
const getFrontendHost = () => {
  const host = window.location.hostname; // e.g., "localhost" or "172.16.1.188"
  const port = window.location.port; // e.g., "8080"
  console.log(`Frontend running on: ${host}:${port}`);
  return host;
};

// Set the base URL based on environment
const API_BASE_URL = process.env.NODE_ENV === "production"
    ? "https://api.bnkaraoke.com" // Unchanged for production
    : process.env.REACT_APP_API_URL || `http://${getFrontendHost()}:7290`; // Dynamic in dev

console.log(`API_BASE_URL set to: ${API_BASE_URL}`);

export const API_ROUTES = {
  LOGIN: `${API_BASE_URL}/api/auth/login`,
  REGISTER: `${API_BASE_URL}/api/auth/register`,
  USER_ROLES: `${API_BASE_URL}/api/auth/roles`,
  PENDING_SONGS: `${API_BASE_URL}/api/songs/pending`,
  YOUTUBE_SEARCH: `${API_BASE_URL}/api/songs/youtube-search`,
  APPROVE_SONGS: `${API_BASE_URL}/api/songs/approve`,
  REJECT_SONG: `${API_BASE_URL}/api/songs/reject`,
  SONGS_SEARCH: `${API_BASE_URL}/api/songs/search`,
  SPOTIFY_SEARCH: `${API_BASE_URL}/api/songs/spotify-search`,
  REQUEST_SONG: `${API_BASE_URL}/api/songs/request`,
  USERS: `${API_BASE_URL}/api/auth/users`,
  UPDATE_USER: `${API_BASE_URL}/api/auth/update-user`,
  DELETE_USER: `${API_BASE_URL}/api/auth/delete-user`,
  USER_REQUESTS: `${API_BASE_URL}/api/songs/user-requests`,
  EVENT_QUEUE: `${API_BASE_URL}/api/events`,
  FAVORITES: `${API_BASE_URL}/api/songs/favorites`,
  ARTISTS: `${API_BASE_URL}/api/songs/artists`,
  GENRES: `${API_BASE_URL}/api/songs/genres`,
  EVENTS: `${API_BASE_URL}/api/events`,
  // New routes for user management
  ADD_USER: `${API_BASE_URL}/api/auth/add-user`,
  FORCE_PASSWORD_CHANGE: `${API_BASE_URL}/api/auth/users`,
  USER_DETAILS: `${API_BASE_URL}/api/auth/user-details`,
  CHANGE_PASSWORD: `${API_BASE_URL}/api/auth/change-password`,
  REGISTRATION_SETTINGS: `${API_BASE_URL}/api/auth/registration-settings`,
};

export default API_BASE_URL;