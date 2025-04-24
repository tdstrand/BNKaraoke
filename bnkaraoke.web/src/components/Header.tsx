import React, { useState, useEffect, useRef } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import "./Header.css";
import { API_ROUTES } from "../config/apiConfig";

const Header: React.FC = () => {
  console.log("Header component rendering");

  const navigate = useNavigate();
  const location = useLocation(); // To detect route changes
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [roles, setRoles] = useState<string[]>([]);
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const [events, setEvents] = useState<any[]>([]);
  const [currentEvent, setCurrentEvent] = useState<any | null>(null);
  const [checkedIn, setCheckedIn] = useState<boolean>(false);
  const [isEventDropdownOpen, setIsEventDropdownOpen] = useState(false);
  const [isCheckingIn, setIsCheckingIn] = useState(false); // Loading state for check-in
  const [checkInError, setCheckInError] = useState<string | null>(null); // Error state for check-in
  const [pendingRequests, setPendingRequests] = useState<number>(0); // Notification state
  const [queues, setQueues] = useState<{ [eventId: number]: any[] }>({}); // Queues for notification
  const eventDropdownRef = useRef<HTMLDivElement>(null); // Ref for the event dropdown

  // Close dropdown on route change
  useEffect(() => {
    setIsEventDropdownOpen(false); // Close event dropdown when route changes
    setIsDropdownOpen(false); // Close admin dropdown when route changes
  }, [location]);

  // Close dropdown on outside click
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (eventDropdownRef.current && !eventDropdownRef.current.contains(event.target as Node)) {
        setIsEventDropdownOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, []);

  // Fetch user data, events, and queues on component mount
  useEffect(() => {
    const storedFirstName = localStorage.getItem("firstName");
    const storedLastName = localStorage.getItem("lastName");
    const storedRoles = localStorage.getItem("roles");
    console.log("Header - Stored Roles from localStorage:", storedRoles);
    if (storedFirstName) setFirstName(storedFirstName);
    if (storedLastName) setLastName(storedLastName);
    if (storedRoles) {
      const parsedRoles = JSON.parse(storedRoles) || [];
      setRoles(parsedRoles);
      console.log("Header - Parsed Roles set to state:", parsedRoles);
    }

    // Fetch events and queues
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      setEvents([]);
      setQueues({});
      return;
    }

    const fetchEventsAndQueues = async () => {
      try {
        // Fetch events
        const eventsResponse = await fetch(API_ROUTES.EVENTS, {
          headers: { Authorization: `Bearer ${token}` },
        });
        if (!eventsResponse.ok) throw new Error(`Fetch events failed: ${eventsResponse.status}`);
        const eventsData: any[] = await eventsResponse.json();
        console.log("Header - Fetched events:", eventsData);
        setEvents(eventsData || []);
        const liveOrUpcomingEvent = eventsData.find(e => e.status === "Live" || e.status === "Upcoming");
        if (!currentEvent) setCurrentEvent(liveOrUpcomingEvent || eventsData[0] || null);

        // Fetch queues for notification
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

        // Calculate pending requests (songs currently playing)
        const totalPending = Object.values(newQueues).reduce(
          (total, queue) => total + queue.filter(q => q.isCurrentlyPlaying).length,
          0
        );
        setPendingRequests(totalPending);
      } catch (err) {
        console.error("Header - Fetch events error:", err);
        setEvents([]);
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
  }, [currentEvent]); // Added currentEvent to dependency array

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
      const response = await fetch(`${API_ROUTES.EVENT_QUEUE}/${event.eventId}/attendance/check-in`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          singerId: localStorage.getItem("userId") || "unknown",
        }),
      });

      if (!response.ok) {
        const errorText = await response.text();
        console.error(`Check-in failed: ${response.status} - ${errorText}`);
        throw new Error(`Check-in failed: ${errorText}`);
      }

      setCurrentEvent(event);
      setCheckedIn(true);
      setIsEventDropdownOpen(false);
    } catch (err) {
      console.error("Check-in error:", err);
      setCheckInError(err instanceof Error ? err.message : "Failed to check in. Please try again.");
      setIsEventDropdownOpen(false);
    } finally {
      setIsCheckingIn(false);
    }
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
        const response = await fetch(`${API_ROUTES.EVENT_QUEUE}/${currentEvent.eventId}/attendance/check-out`, {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            singerId: localStorage.getItem("userId") || "unknown",
          }),
        });

        if (!response.ok) {
          const errorText = await response.text();
          console.error(`Check-out failed: ${response.status} - ${errorText}`);
          throw new Error(`Check-out failed: ${response.status}`);
        }

        setCurrentEvent(null);
        setCheckedIn(false);
      } catch (err) {
        console.error("Check-out error:", err);
        setCurrentEvent(null);
        setCheckedIn(false);
      }
    }
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
        <span className="header-user">Hello, {fullName}!</span>
        {currentEvent && checkedIn ? (
          <div className="event-status">
            <span className="event-name">Checked into: {currentEvent.eventCode}</span>
            <button className="leave-event-button" onClick={handleLeaveEvent}>
              Leave Event
            </button>
          </div>
        ) : (
          <div className="event-dropdown" ref={eventDropdownRef}>
            <button
              className="check-in-button"
              onClick={() => setIsEventDropdownOpen(!isEventDropdownOpen)}
              disabled={isCheckingIn}
            >
              {isCheckingIn ? "Checking In..." : "Check into an Event"}
            </button>
            {isEventDropdownOpen && (
              <ul className="event-dropdown-menu">
                {checkInError && (
                  <li className="event-dropdown-error">{checkInError}</li>
                )}
                {events
                  .filter(event => event.status === "Live" || event.status === "Upcoming")
                  .map(event => (
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
        )}
        <button className="logout-button" onClick={handleLogout}>
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