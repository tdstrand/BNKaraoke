import { BrowserRouter as Router, Routes, Route, useLocation } from "react-router-dom";
import Login from "./pages/Login";
import Home from "./pages/Home";
import Dashboard from "./pages/Dashboard";
import SpotifySearchTest from "./pages/SpotifySearchTest";
import PendingRequests from "./pages/PendingRequests";
import RequestSongPage from "./pages/RequestSongPage";
import SongManagerPage from "./pages/SongManagerPage";
import UserManagementPage from "./pages/UserManagementPage";
import Header from "./components/Header";
import ExploreSongs from './pages/ExploreSongs';

// Wrapper component to conditionally render the Header
const HeaderWrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const location = useLocation();
  const showHeader = location.pathname !== "/"; // Don't show Header on the login page

  return (
    <>
      {showHeader && <Header />}
      {children}
    </>
  );
};

const App = () => {
  return (
    <Router>
      <HeaderWrapper>
        <Routes>
          <Route path="/" element={<Login />} />
          <Route path="/home" element={<Home />} />
          <Route path="/dashboard" element={<Dashboard />} />
          <Route path="/request-song" element={<RequestSongPage />} />
          <Route path="/spotify-search" element={<SpotifySearchTest />} />
          <Route path="/pending-requests" element={<PendingRequests />} />
          <Route path="/song-manager" element={<SongManagerPage />} />
          <Route path="/user-management" element={<UserManagementPage />} />
          <Route path="/explore-songs" element={<ExploreSongs />} />
        </Routes>
      </HeaderWrapper>
    </Router>
  );
};

export default App;