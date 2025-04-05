import React, { useEffect, useState } from "react";
import "./Dashboard.css";

const Dashboard = () => {
  const [roles, setRoles] = useState<string[]>([]);
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");

  useEffect(() => {
    const storedRoles = localStorage.getItem("roles");
    const storedFirstName = localStorage.getItem("firstName");
    const storedLastName = localStorage.getItem("lastName");

    console.log("Stored First Name:", storedFirstName); // ✅ Debugging Name Retrieval
    console.log("Stored Last Name:", storedLastName);

    if (storedRoles) setRoles(JSON.parse(storedRoles));
    if (storedFirstName) setFirstName(storedFirstName);
    if (storedLastName) setLastName(storedLastName);
  }, []);

  const handleLogout = () => {
    localStorage.removeItem("token");
    localStorage.removeItem("roles");
    localStorage.removeItem("firstName");
    localStorage.removeItem("lastName");
    window.location.href = "/login";
  };

  return (
    <div className="dashboard-container">
      {/* ✅ Top navbar with name & logout button */}
      <div className="dashboard-navbar">
        {firstName && lastName ? (
          <span className="dashboard-user">{firstName} {lastName}</span>
        ) : (
          <span className="dashboard-user">Loading...</span>
        )}
        <button className="logout-button" onClick={handleLogout}>Logout</button>
      </div>

      <div className="dashboard-card">
        <h2 className="dashboard-title">Dashboard</h2>
        <p className="dashboard-text">Welcome! You're logged in.</p>

        {/* ✅ User Management button (Only for 'User Managers') */}
        {roles.includes("User Manager") ? (
          <button className="dashboard-button">User Management</button>
        ) : null}
      </div>
    </div>
  );
};

export default Dashboard;
