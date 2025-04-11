// Temporary for dev testing
const API_BASE_URL = "https://localhost:7280"; // Force dev URL

console.log(`API_BASE_URL set to: ${API_BASE_URL}`);

export const API_ROUTES = {
  LOGIN: `${API_BASE_URL}/api/auth/login`,
  REGISTER: `${API_BASE_URL}/api/auth/register`,
  USER_ROLES: `${API_BASE_URL}/api/auth/roles`,
  PENDING_SONGS: `${API_BASE_URL}/api/songs/pending`,
  YOUTUBE_SEARCH: `${API_BASE_URL}/api/songs/youtube-search`,
  APPROVE_SONG: `${API_BASE_URL}/api/songs/approve`,
  REJECT_SONG: `${API_BASE_URL}/api/songs/reject`,
  SONGS_SEARCH: `${API_BASE_URL}/api/songs/search`,
  SPOTIFY_SEARCH: `${API_BASE_URL}/api/songs/spotify-search`,
  REQUEST_SONG: `${API_BASE_URL}/api/songs/request`,
  USERS: `${API_BASE_URL}/api/auth/users`,
  UPDATE_USER: `${API_BASE_URL}/api/auth/update-user`,
  DELETE_USER: `${API_BASE_URL}/api/auth/delete-user`,
  USER_REQUESTS: `${API_BASE_URL}/api/songs/user-requests`
};

export default API_BASE_URL;