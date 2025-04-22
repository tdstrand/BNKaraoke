import { BrowserRouter as Router, Routes, Route } from "react-router-dom";
import Login from "./pages/Login";
import Home from "./pages/Home";
import Dashboard from "./pages/Dashboard";
import SpotifySearchTest from "./pages/SpotifySearchTest";
import PendingRequests from "./pages/PendingRequests";
import RequestSongPage from "./pages/RequestSongPage";
import SongManagerPage from "./pages/SongManagerPage";
import UserManagementPage from "./pages/UserManagementPage"; // Added import
import Header from "./components/Header";
import ExploreSongs from './pages/ExploreSongs';

const App = () => {
  return (
    <Router>
      <Routes>
        <Route path="/" element={<Login />} />
        <Route path="/home" element={<><Header /><Home /></>} />
        <Route path="/dashboard" element={<><Header /><Dashboard /></>} />
        <Route path="/request-song" element={<RequestSongPage />} />
        <Route path="/spotify-search" element={<><Header /><SpotifySearchTest /></>} />
        <Route path="/pending-requests" element={<><Header /><PendingRequests /></>} />
        <Route path="/song-manager" element={<SongManagerPage />} />
        <Route path="/user-management" element={<UserManagementPage />} />
        <Route path="/explore-songs" element={<ExploreSongs />} />
      </Routes>
    </Router>
  );
};

export default App;