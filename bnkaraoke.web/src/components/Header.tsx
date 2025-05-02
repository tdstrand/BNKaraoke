import React, { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { LogoutOutlined } from '@ant-design/icons';
import "./Header.css";
import { API_ROUTES } from "../config/apiConfig";
import useEventContext from "../context/EventContext";
import { AttendanceAction } from "../types";

const Header: React.FC = () => {
  console.log("Header component rendering");

  // Move all Hooks to top level
  const navigate = useNavigate();
  const { currentEvent, setCurrentEvent, checkedIn, setCheckedIn, isCurrentEventLive, setIsCurrentEventLive, isOnBreak, setIsOnBreak } = useEventContext();
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [roles, setRoles] = useState<string[]>([]);
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const [liveEvents, setLiveEvents] = useState<any[]>([]);
  const [upcomingEvents, setUpcomingEvents] = useState<any[]>([]);
  const [isEventDropdownOpen, setIsEventDropdownOpen] = useState(false);
  const [isPreselectDropdownOpen, setIsPreselectDropdownOpen] = useState(false);
  const [isCheckingIn, setIsCheckingIn] = useState(false);
  const [checkInError, setCheckInError] = useState<string | null>(null);
  const [pendingRequests, setPendingRequests] = useState<number>(0);
  const eventDropdownRef = useRef<HTMLDivElement>(null);
  const preselectDropdownRef = useRef<HTMLDivElement>(null);

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
        setFirstName(data.firstName || "");
        setLastName(data.lastName || "");
        setRoles(data.roles || []);
      } catch (err: unknown) {
        const errorMessage = err instanceof Error ? err.message : "Unknown error";
        console.error("Fetch User Details Error:", errorMessage, err);
        const storedFirstName = localStorage.getItem("firstName");
        const storedLastName = localStorage.getItem("lastName");
        const storedRoles = localStorage.getItem("roles");
        if (storedFirstName) setFirstName(storedFirstName);
        if (storedLastName) setLastName(storedLastName);
        if (storedRoles) {
          try {
            const parsedRoles = JSON.parse(storedRoles) || [];
            setRoles(parsedRoles);
          } catch (parseErr) {
            console.error("Parse Roles Error:", parseErr);
          }
        }
      }
    };

    fetchUserDetails();
  }, []); // Run once on mount

  // Close dropdowns on outside click
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      try {
        if (eventDropdownRef.current && !eventDropdownRef.current.contains(event.target as Node)) {
          setIsEventDropdownOpen(false);
        }
        if (preselectDropdownRef.current && !preselectDropdownRef.current.contains(event.target as Node)) {
          setIsPreselectDropdownOpen(false);
        }
      } catch (err: unknown) {
        console.error("HandleClickOutside Error:", err);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, []);

  // Fetch events on mount and auto-select if only one upcoming event
  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      setLiveEvents([]);
      setUpcomingEvents([]);
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
          if (upcoming.length === 1) {
            setCurrentEvent(upcoming[0]);
            setIsCurrentEventLive(false);
            console.log("Auto-selected the only upcoming event:", upcoming[0].eventId);
          } else {
            setCurrentEvent(liveEvent || upcoming[0] || null);
            setIsCurrentEventLive(liveEvent != null);
          }
        }
        if (liveEvent) {
          console.log("Selected live event status:", liveEvent.status);
        } else if (upcoming[0]) {
          console.log("Selected upcoming event status:", upcoming[0].status);
        }
      } catch (err: unknown) {
        const errorMessage = err instanceof Error ? err.message : "Unknown error";
        console.error("Header - Fetch events error:", errorMessage, err);
        setLiveEvents([]);
        setUpcomingEvents([]);
      }
    };

    fetchEventsAndQueues();
  }, [currentEvent, setCurrentEvent]);

  const adminRoles = ["Song Manager", "User Manager", "Event Manager"];
  const hasAdminRole = roles.some(role => adminRoles.includes(role));

  const handleNavigation = (path: string) => {
    try {
      setIsDropdownOpen(false);
      navigate(path);
    } catch (err: unknown) {
      console.error("HandleNavigation Error:", err);
    }
  };

  const handleLogout = () => {
    try {
      console.log("Logout button clicked");
      localStorage.clear();
      navigate("/");
    } catch (err: unknown) {
      console.error("HandleLogout Error:", err);
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
      const requestorUserName = localStorage.getItem("userName") || "unknown";
      console.log(`Checking into event: ${event.eventId}, status: ${event.status}, requestorUserName: ${requestorUserName}`);
      const requestData: AttendanceAction = { requestorUserName };
      const response = await fetch(`/api/events/${event.eventId}/attendance/check-in`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestData),
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
    } catch (err: unknown) {
      const errorMessage = err instanceof Error ? err.message : "Failed to check in";
      console.error("Check-in error:", errorMessage, err);
      setCheckInError(errorMessage);
      setIsEventDropdownOpen(true);
    } finally {
      setIsCheckingIn(false);
    }
  };

  const handlePreselectSongs = (event: any) => {
    try {
      setCurrentEvent(event);
      setIsCurrentEventLive(event.status === "Live");
      setCheckedIn(false);
      setIsPreselectDropdownOpen(false);
    } catch (err: unknown) {
      console.error("HandlePreselectSongs Error:", err);
    }
  };

  const handleLeaveEvent = async () => {
    if (!currentEvent) {
      console.error("No current event selected");
      return;
    }

    try {
      const token = localStorage.getItem("token");
      if (!token) {
        console.error("No token found");
        return;
      }

      const requestorUserName = localStorage.getItem("userName") || "unknown";
      console.log(`Checking out of event: ${currentEvent.eventId}, requestorUserName: ${requestorUserName}`);

      if (currentEvent.status === "Live") {
        const requestData: AttendanceAction = { requestorUserName };
        const response = await fetch(`/api/events/${currentEvent.eventId}/attendance/check-out`, {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(requestData),
        });

        if (!response.ok) {
          const errorText = await response.text();
          console.error(`Check-out failed: ${response.status} - ${errorText}`);
          throw new Error(`Check-out failed: ${response.status}`);
        }
      }

      setCurrentEvent(null);
      setIsCurrentEventLive(false);
      setCheckedIn(false);
      setIsOnBreak(false);
    } catch (err: unknown) {
      console.error("Check-out error:", err);
      setCurrentEvent(null);
      setIsCurrentEventLive(false);
      setCheckedIn(false);
      setIsOnBreak(false);
    }
  };

  const handleBreakToggle = async () => {
    if (!currentEvent) {
      console.error("No current event selected");
      return;
    }

    const token = localStorage.getItem("token");
    if (!token) {
      console.error("No token found");
      return;
    }

    try {
      const requestorUserName = localStorage.getItem("userName") || "unknown";
      const requestData: AttendanceAction = { requestorUserName };
      const endpoint = isOnBreak
        ? `/api/events/${currentEvent.eventId}/attendance/break/end`
        : `/api/events/${currentEvent.eventId}/attendance/break/start`;

      const response = await fetch(endpoint, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestData),
      });

      if (!response.ok) {
        const errorText = await response.text();
        console.error(`Break toggle failed: ${response.status} - ${errorText}`);
        throw new Error(`Break toggle failed: ${response.status}`);
      }

      setIsOnBreak(!isOnBreak);
      console.log(`User is now ${isOnBreak ? "off break" : "on break"}`);
    } catch (err: unknown) {
      console.error("Break toggle error:", err);
    }
  };

  const fullName = firstName || lastName ? `${firstName} ${lastName}`.trim() : "User";

  // Handle render errors gracefully
  try {
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
          {currentEvent && (
            <div className="event-status">
              <span className="event-name">
                {checkedIn ? `Checked into: ${currentEvent.eventCode}` : `Pre-Selecting for: ${currentEvent.eventCode}`}
              </span>
              {checkedIn && isCurrentEventLive && (
                <button className={isOnBreak ? "back-button" : "break-button"} onClick={handleBreakToggle}>
                  {isOnBreak ? "I'm Back" : "Go On Break"}
                </button>
              )}
              {checkedIn && (
                <button className="leave-event-button" onClick={handleLeaveEvent}>
                  Leave Event
                </button>
              )}
            </div>
          )}
          {!currentEvent && (
            <div className="event-actions">
              {!(checkedIn && isCurrentEventLive) && (
                <div className="event-dropdown preselect-dropdown" ref={preselectDropdownRef}>
                  <button
                    className="preselect-button"
                    onClick={() => setIsPreselectDropdownOpen(!isPreselectDropdownOpen)}
                    disabled={upcomingEvents.length === 0}
                    aria-label="Pre-Select Songs for Upcoming Events"
                  >
                    Pre-Select
                  </button>
                  {isPreselectDropdownOpen && upcomingEvents.length > 1 && (
                    <ul className="event-dropdown-menu">
                      {upcomingEvents.map(event => (
                        <li
                          key={event.eventId}
                          className="event-dropdown-item"
                          onClick={() => handlePreselectSongs(event)}
                        >
                          {event.status}: ${event.eventCode} (${event.scheduledDate})
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
                        {event.status}: ${event.eventCode} (${event.scheduledDate})
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
  } catch (error: unknown) {
    console.error('Header render error:', error);
    return <div>Error in Header: {error instanceof Error ? error.message : 'Unknown error'}</div>;
  }
};

export default Header;