import React, { useEffect, useState } from 'react'; // Added explicit import
import { BrowserRouter as Router, Routes, Route, useLocation, Navigate } from "react-router-dom";
import Login from "./pages/Login";
import Home from "./pages/Home";
import Dashboard from "./pages/Dashboard";
import SpotifySearchTest from "./pages/SpotifySearchTest";
import PendingRequests from "./pages/PendingRequests";
import RequestSongPage from "./pages/RequestSongPage";
import SongManagerPage from "./pages/SongManagerPage";
import UserManagementPage from "./pages/UserManagementPage";
import Header from "./components/Header";
import ExploreSongs from "./pages/ExploreSongs";
import RegisterPage from "./pages/RegisterPage";
import KaraokeChannelsPage from "./pages/KaraokeChannelsPage";
import ChangePassword from "./pages/ChangePassword";
import Profile from "./pages/Profile";
import { EventContextProvider } from "./context/EventContext";

// Configure Router with v7 flags
const routerFutureConfig = {
  v7_startTransition: true,
  v7_relativeSplatPath: true
};

// Error Boundary Props
interface ErrorBoundaryProps {
  children: React.ReactNode;
}

// Error Boundary State
interface ErrorBoundaryState {
  error: string | null;
  errorInfo: React.ErrorInfo | null;
}

// Error Boundary Component
class ErrorBoundary extends React.Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null, errorInfo: null };

  static getDerivedStateFromError(error: Error): Partial<ErrorBoundaryState> {
    return { error: error.message };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo) {
    console.error('ErrorBoundary caught:', error, info);
    this.setState({ error: error.message, errorInfo: info });
  }

  render() {
    if (this.state.error) {
      return (
        <div style={{ color: 'red', margin: '10px', padding: '10px', background: 'rgba(255, 255, 255, 0.1)', borderRadius: '5px' }}>
          <h3>Application Error</h3>
          <p>{this.state.error}</p>
          <pre>{this.state.errorInfo?.componentStack}</pre>
        </div>
      );
    }
    return this.props.children;
  }
}

// HeaderWrapper Component
const HeaderWrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const location = useLocation();
  const [mustChangePassword, setMustChangePassword] = useState<boolean | null>(null);
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(false);

  console.log('HeaderWrapper initializing', { location: location.pathname });

  useEffect(() => {
    console.log('HeaderWrapper useEffect running', { location: location.pathname });
    try {
      const token = localStorage.getItem("token");
      const storedMustChangePassword = localStorage.getItem("mustChangePassword");

      console.log('HeaderWrapper useEffect: token=', token, 'mustChangePassword=', storedMustChangePassword);

      if (token) {
        setIsAuthenticated(true);
        if (storedMustChangePassword !== null) {
          setMustChangePassword(storedMustChangePassword === "true");
        }
      } else {
        setIsAuthenticated(false);
        setMustChangePassword(null);
      }
    } catch (error) {
      console.error('HeaderWrapper useEffect error:', error);
    }
  }, [location]);

  const showHeader = !["/", "/register", "/change-password"].includes(location.pathname);

  try {
    if (isAuthenticated && mustChangePassword && location.pathname !== "/change-password") {
      console.log('HeaderWrapper redirecting to /change-password');
      return <Navigate to="/change-password" replace />;
    }

    if (isAuthenticated && (location.pathname === "/" || location.pathname === "/register")) {
      console.log('HeaderWrapper redirecting to /dashboard');
      return <Navigate to="/dashboard" replace />;
    }

    if (!isAuthenticated && location.pathname !== "/" && location.pathname !== "/register" && location.pathname !== "/change-password") {
      console.log('HeaderWrapper redirecting to /');
      return <Navigate to="/" replace />;
    }

    return (
      <>
        {showHeader && <Header />}
        {children}
      </>
    );
  } catch (error) {
    console.error('HeaderWrapper render error:', error);
    return <div>Error in HeaderWrapper: {error instanceof Error ? error.message : 'Unknown error'}</div>;
  }
};

const App = () => {
  console.log('App component initializing');
  const [consoleErrors, setConsoleErrors] = useState<string[]>([]);

  useEffect(() => {
    console.log('App useEffect running');
    const originalConsoleError = console.error;
    console.error = (...args) => {
      setConsoleErrors((prev) => [...prev, args.join(' ')]);
      originalConsoleError(...args);
    };
    window.onerror = (message, source, lineno, colno, error) => {
      setConsoleErrors((prev) => [...prev, `Error: ${message} at ${source}:${lineno}`]);
      return true;
    };
    return () => {
      console.error = originalConsoleError;
      window.onerror = null;
    };
  }, []);

  return (
    <div>
      {consoleErrors.length > 0 && (
        <div style={{ color: 'red', margin: '10px', background: 'rgba(255, 255, 255, 0.1)', padding: '10px', borderRadius: '5px' }}>
          <h3>Console Errors:</h3>
          <ul>
            {consoleErrors.map((err, index) => (
              <li key={index}>{err}</li>
            ))}
          </ul>
        </div>
      )}
      <ErrorBoundary>
        <Router future={routerFutureConfig}>
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
                <Route path="/karaoke-channels" element={<KaraokeChannelsPage />} />
              </Routes>
            </HeaderWrapper>
          </EventContextProvider>
        </Router>
      </ErrorBoundary>
    </div>
  );
};

export default App;