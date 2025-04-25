import { BrowserRouter as Router, Routes, Route, useLocation, Navigate } from "react-router-dom";
import { useEffect, useState } from "react";
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
import RegisterPage from "./pages/RegisterPage";
import ChangePassword from "./pages/ChangePassword";
import Profile from "./pages/Profile";
import { EventContextProvider } from "./context/EventContext";

// Wrapper component to conditionally render the Header and handle MustChangePassword
const HeaderWrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const location = useLocation();
  const [mustChangePassword, setMustChangePassword] = useState<boolean | null>(null);
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(false);

  // Check authentication and MustChangePassword on route change
  useEffect(() => {
    const token = localStorage.getItem("token");
    const storedMustChangePassword = localStorage.getItem("mustChangePassword");

    if (token) {
      setIsAuthenticated(true);
      if (storedMustChangePassword !== null) {
        setMustChangePassword(storedMustChangePassword === "true");
      }
    } else {
      setIsAuthenticated(false);
      setMustChangePassword(null);
    }
  }, [location]);

  // Don't show Header on login, register, or change-password pages
  const showHeader = !["/", "/register", "/change-password"].includes(location.pathname);

  // Redirect to /change-password if MustChangePassword is true and not on the change-password page
  if (isAuthenticated && mustChangePassword && location.pathname !== "/change-password") {
    return <Navigate to="/change-password" replace />;
  }

  // Redirect to /dashboard if authenticated and trying to access login or register
  if (isAuthenticated && (location.pathname === "/" || location.pathname === "/register")) {
    return <Navigate to="/dashboard" replace />;
  }

  // Redirect to / if not authenticated and trying to access protected routes
  if (!isAuthenticated && location.pathname !== "/" && location.pathname !== "/register" && location.pathname !== "/change-password") {
    return <Navigate to="/" replace />;
  }

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
      <EventContextProvider>
        <HeaderWrapper>
          <Routes>
            <Route path="/" element={<Login />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="/change-password" element={<ChangePassword />} />
            <Route path="/profile" element={<Profile />} />
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
      </EventContextProvider>
    </Router>
  );
};

export default App;