import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import "../pages/Dashboard.css";

const Header: React.FC = () => {
  console.log("Header component rendering"); // Debug log to confirm rendering

  const navigate = useNavigate();
  const [firstName, setFirstName] = useState("");
  const [roles, setRoles] = useState<string[]>([]);
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);

  useEffect(() => {
    const storedFirstName = localStorage.getItem("firstName");
    const storedRoles = localStorage.getItem("roles");
    console.log("Header - Stored Roles from localStorage:", storedRoles);
    if (storedFirstName) setFirstName(storedFirstName);
    if (storedRoles) {
      const parsedRoles = JSON.parse(storedRoles) || [];
      setRoles(parsedRoles);
      console.log("Header - Parsed Roles set to state:", parsedRoles);
    }
  }, []);

  const adminRoles = ["Song Manager", "User Manager", "Event Manager"];
  const hasAdminRole = roles.some(role => adminRoles.includes(role));

  const handleNavigation = (path: string) => {
    setIsDropdownOpen(false);
    navigate(path);
  };

  const handleLogout = () => {
    console.log("Logout button clicked");
    localStorage.clear();
    navigate("/login"); // Ensure navigation to /login
    window.location.reload(); // Force reload to ensure state reset
  };

  return (
    <div className="dashboard-navbar" style={{ border: "2px solid red" }}>
      {hasAdminRole && (
        <div className="admin-dropdown">
          <button
            className="dashboard-button dropdown-toggle"
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
      <span className="dashboard-user">{firstName || "User"}</span>
      <button className="logout-button" onClick={handleLogout}>
        Logout
      </button>
    </div>
  );
};

export default Header;