import React, { useState, useEffect, useRef } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { LogoutOutlined } from '@ant-design/icons';
import "./Header.css";
import { API_ROUTES } from "../config/apiConfig";
import useEventContext from "../context/EventContext";

const Header: React.FC = () => {
  console.log("Header component rendering");

  const navigate = useNavigate();
  const location = useLocation();
  const { currentEvent, setCurrentEvent, checkedIn, setCheckedIn, isCurrentEventLive, setIsCurrentEventLive } = useEventContext();
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [roles, setRoles] = useState<string[]>([]);
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const [liveEvents, setLiveEvents] = useState<any[]>([]);
  const [upcomingEvents, setUpcomingEvents] = useState<any[]>([]);
  const [isEventDropdownOpen, setIsEventDropdownOpen] = useState(false);
  const [isPreloadDropdownOpen, setIsPreloadDropdownOpen] = useState(false);
  const [isCheckingIn, setIsCheckingIn] = useState(false);
  const [checkInError, setCheckInError] = useState<string | null>(null);
  const [pendingRequests, setPendingRequests] = useState<number>(0);
  const [queues, setQueues] = useState<{ [eventId: number]: any[] }>({});
  const [isOnBreak, setIsOnBreak] = useState<boolean>(false);
  const eventDropdownRef = useRef<HTMLDivElement>(null);
  const preloadDropdownRef = useRef<HTMLDivElement>(null);

  // Fetch user details on mount
  useEffect(() => {
    const fetchUserDetails = async () => {
      const token = localStorage.getItem("token");
      if (!token) {
        console.error("No token found");
        return;
      }
      try {
        console.log(`Fetching user details from: ${API_ROUTES.USER_DETAILS}`);
        const response = await fetch(API_ROUTES.USER_DETAILS, {
          headers: { Authorization: `Bearer ${token}` },
        });
        const responseText = await response.text();
        console.log("User Details Raw Response:", responseText);
        if (!response.ok) throw new Error(`Failed to fetch user details: ${response.status} ${response.statusText} - ${responseText}`);
        const data = JSON.parse(responseText);
        setFirstName(data.firstName);
        setLastName(data.lastName);
        setRoles(data.roles || []);
      } catch (err) {
        console.error("Fetch User Details Error:", err);
        const storedFirstName = localStorage.getItem("firstName");
        const storedLastName = localStorage.getItem("lastName");
        const storedRoles = localStorage.getItem("roles");
        if (storedFirstName) setFirstName(storedFirstName);
        if (storedLastName) setLastName(storedLastName);
        if (storedRoles) {
          const parsedRoles = JSON.parse(storedRoles) || [];
          setRoles(parsedRoles);
        }
      }
    };

    fetchUserDetails();
  }, []);

  // Close dropdowns on route change
  useEffect(() => {
    setIsEventDropdownOpen(false);
    setIsPreloadDropdownOpen(false);
    setIsDropdownOpen(false);
  }, [location]);

  // Close dropdowns on outside click
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (eventDropdownRef.current && !eventDropdownRef.current.contains(event.target as Node)) {
        setIsEventDropdownOpen(false);
      }
      if (preloadDropdownRef.current && !preloadDropdownRef.current.contains(event.target as Node)) {
        setIsPreloadDropdownOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, []);

  // Fetch events and queues on component mount
  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      setLiveEvents([]);
      setUpcomingEvents([]);
      setQueues({});
      return;
    }

    const fetchEventsAndQueues = async () => {
      try {
        const eventsResponse = await fetch(API_ROUTES.EVENTS, {
          headers: { Authorization: `Bearer ${token}` },
        });
        if (!eventsResponse.ok) throw new Error(`Fetch events failed: ${eventsResponse.status}`);
        const eventsData: any[] = await eventsResponse.json();
        console.log("Header - Fetched events:", eventsData);

        // Split events into live and upcoming
        const live = eventsData.filter(e => e.status === "Live") || [];
        const upcoming = eventsData.filter(e => e.status === "Upcoming") || [];
        setLiveEvents(live);
        setUpcomingEvents(upcoming);

        // Set the current event to a live event by default, if available
        const liveEvent = live[0];
        if (!currentEvent) {
          setCurrentEvent(liveEvent || upcoming[0] || null);
          setIsCurrentEventLive(liveEvent != null);
        }
        if (liveEvent) {
          console.log("Selected live event status:", liveEvent.status);
        } else if (upcoming[0]) {
          console.log("Selected upcoming event status:", upcoming[0].status);
        }

        const newQueues: { [eventId: number]: any[] } = {};
        for (const event of eventsData) {
          try {
            const queueResponse = await fetch(`${API_ROUTES.EVENT_QUEUE}/${event.eventId}/queue`, {
              headers: { Authorization: `Bearer ${token}` },
            });
            if (!queueResponse.ok) throw new Error(`Fetch queue failed for event ${event.eventId}: ${queueResponse.status}`);
            const queueData = await queueResponse.json();
            newQueues[event.eventId] = queueData || [];
          } catch (err) {
            console.error(`Fetch queue error for event ${event.eventId}:`, err);
            newQueues[event.eventId] = [];
          }
        }
        setQueues(newQueues);

        const totalPending = Object.values(newQueues).reduce(
          (total, queue) => total + queue.filter(q => q.isCurrentlyPlaying).length,
          0
        );
        setPendingRequests(totalPending);
      } catch (err) {
        console.error("Header - Fetch events error:", err);
        setLiveEvents([]);
        setUpcomingEvents([]);
        setQueues({});
      }
    };

    fetchEventsAndQueues();
  }, []);

  // Fetch queue to determine check-in status when currentEvent changes
  useEffect(() => {
    if (!currentEvent) return;

    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      setCheckedIn(false);
      return;
    }

    fetch(`${API_ROUTES.EVENT_QUEUE}/${currentEvent.eventId}/queue`, {
      headers: { Authorization: `Bearer ${token}` },
    })
      .then(res => {
        if (!res.ok) throw new Error(`Fetch queue failed: ${res.status}`);
        return res.json();
      })
      .then((data: any[]) => {
        console.log("Header - Fetched queue:", data);
        setCheckedIn(data && data.length > 0);
      })
      .catch(err => {
        console.error("Header - Fetch queue error:", err);
        setCheckedIn(false);
      });
  }, [currentEvent]);

  const adminRoles = ["Song Manager", "User Manager", "Event Manager"];
  const hasAdminRole = roles.some(role => adminRoles.includes(role));

  const handleNavigation = (path: string) => {
    setIsDropdownOpen(false);
    navigate(path);
  };

  const handleLogout = () => {
    console.log("Logout button clicked");
    if (window.confirm("Are you sure you want to log out?")) {
      localStorage.clear();
      navigate("/");
    }
  };

  const handleCheckIn = async (event: any) => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      setCheckInError("Authentication token missing. Please log in again.");
      setIsEventDropdownOpen(false);
      return;
    }

    setIsCheckingIn(true);
    setCheckInError(null);

    try {
      const requestorId = localStorage.getItem("userName") || "unknown";
      console.log(`Checking into event: ${event.eventId}, status: ${event.status}, requestorId: ${requestorId}`);
      const response = await fetch(`${API_ROUTES.EVENT_QUEUE}/${event.eventId}/attendance/check-in`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          requestorId: requestorId,
        }),
      });

      if (!response.ok) {
        const errorText = await response.text();
        console.error(`Check-in failed: ${response.status} - ${errorText}`);
        throw new Error(`Check-in failed: ${errorText}`);
      }

      setCurrentEvent(event);
      setIsCurrentEventLive(event.status === "Live");
      setCheckedIn(true);
      setIsEventDropdownOpen(false);
    } catch (err) {
      console.error("Check-in error:", err);
      setCheckInError(err instanceof Error ? err.message : "Failed to check in. Please try again.");
      setIsEventDropdownOpen(true);
    } finally {
      setIsCheckingIn(false);
    }
  };

  const handlePreloadSongs = (event: any) => {
    setCurrentEvent(event);
    setIsCurrentEventLive(event.status === "Live");
    setIsPreloadDropdownOpen(false);
    // No navigation needed; Dashboard.tsx will handle preloading based on currentEvent
  };

  const handleLeaveEvent = async () => {
    if (!currentEvent) {
      console.error("No current event selected");
      return;
    }

    if (window.confirm(`Leave ${currentEvent.eventCode}? Your queue will be ${currentEvent.status === "Live" ? "archived" : "cleared"}.`)) {
      const token = localStorage.getItem("token");
      if (!token) {
        console.error("No token found");
        return;
      }

      try {
        const requestorId = localStorage.getItem("userName") || "unknown";
        console.log(`Checking out of event: ${currentEvent.eventId}, requestorId: ${requestorId}`);
        const response = await fetch(`${API_ROUTES.EVENT_QUEUE}/${currentEvent.eventId}/attendance/check-out`, {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            requestorId: requestorId,
          }),
        });

        if (!response.ok) {
          const errorText = await response.text();
          console.error(`Check-out failed: ${response.status} - ${errorText}`);
          throw new Error(`Check-out failed: ${response.status}`);
        }

        setCurrentEvent(null);
        setIsCurrentEventLive(false);
        setCheckedIn(false);
        setIsOnBreak(false);
      } catch (err) {
        console.error("Check-out error:", err);
        setCurrentEvent(null);
        setIsCurrentEventLive(false);
        setCheckedIn(false);
        setIsOnBreak(false);
      }
    }
  };

  const handleBreakToggle = () => {
    setIsOnBreak(!isOnBreak);
    console.log(`User is now ${isOnBreak ? "off break" : "on break"}`);
  };

  const fullName = firstName || lastName ? `${firstName} ${lastName}`.trim() : "User";

  return (
    <div className="header-container">
      <div className="header-main">
        {hasAdminRole && (
          <div className="admin-dropdown">
            <button
              className="dropdown-toggle"
              onClick={() => setIsDropdownOpen(!isDropdownOpen)}
            >
              Admin
            </button>
            {isDropdownOpen && (
              <ul className="dropdown-menu">
                {roles.includes("Song Manager") && (
                  <li
                    className="dropdown-item"
                    onClick={() => handleNavigation("/song-manager")}
                  >
                    Manage Songs
                  </li>
                )}
                {roles.includes("User Manager") && (
                  <li
                    className="dropdown-item"
                    onClick={() => handleNavigation("/user-management")}
                  >
                    Manage Users
                  </li>
                )}
                {roles.includes("Event Manager") && (
                  <li
                    className="dropdown-item"
                    onClick={() => handleNavigation("/event-manager")}
                  >
                    Manage Events
                  </li>
                )}
              </ul>
            )}
          </div>
        )}
        <span className="header-user" onClick={() => navigate("/profile")} style={{ cursor: "pointer" }}>
          Hello, {fullName}!
        </span>
        {currentEvent && checkedIn ? (
          <div className="event-status">
            <span className="event-name">Checked into: {currentEvent.eventCode}</span>
            {isCurrentEventLive && (
              <button className={isOnBreak ? "back-button" : "break-button"} onClick={handleBreakToggle}>
                {isOnBreak ? "I'm Back" : "Go On Break"}
              </button>
            )}
            <button className="leave-event-button" onClick={handleLeaveEvent}>
              Leave Event
            </button>
          </div>
        ) : (
          <div className="event-actions">
            {!(checkedIn && isCurrentEventLive) && (
              <div className="event-dropdown preload-dropdown" ref={preloadDropdownRef}>
                <button
                  className="preload-button"
                  onClick={() => setIsPreloadDropdownOpen(!isPreloadDropdownOpen)}
                  disabled={upcomingEvents.length === 0}
                  aria-label="Preload Songs for Upcoming Events"
                >
                  Preload Songs
                </button>
                {isPreloadDropdownOpen && (
                  <ul className="event-dropdown-menu">
                    {upcomingEvents.map(event => (
                      <li
                        key={event.eventId}
                        className="event-dropdown-item"
                        onClick={() => handlePreloadSongs(event)}
                      >
                        {event.status}: {event.eventCode} ({event.scheduledDate})
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            )}
            <div className="event-dropdown join-event-dropdown" ref={eventDropdownRef}>
              <button
                className="check-in-button"
                onClick={() => setIsEventDropdownOpen(!isEventDropdownOpen)}
                disabled={isCheckingIn || liveEvents.length === 0}
                aria-label="Join Live Event"
              >
                {isCheckingIn ? "Joining..." : "Join Event"}
              </button>
              {isEventDropdownOpen && (
                <ul className="event-dropdown-menu">
                  {checkInError && (
                    <li className="event-dropdown-error">
                      {checkInError}
                    </li>
                  )}
                  {liveEvents.map(event => (
                    <li
                      key={event.eventId}
                      className="event-dropdown-item"
                      onClick={() => handleCheckIn(event)}
                    >
                      {event.status}: {event.eventCode} ({event.scheduledDate})
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        )}
        <button className="logout-button" onClick={handleLogout}>
          <LogoutOutlined style={{ fontSize: '24px', marginRight: '8px' }} />
          Logout
        </button>
      </div>
      {pendingRequests > 0 && (
        <div className="notification-bar">
          <span>{pendingRequests} song(s) currently playing!</span>
          <button onClick={() => console.log("View notifications")}>View</button>
        </div>
      )}
    </div>
  );
};

export default Header;