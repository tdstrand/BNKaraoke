import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { API_ROUTES } from "../config/apiConfig"; // Only API_ROUTES
import "../pages/Dashboard.css";

const UserManagementPage: React.FC = () => {
  const navigate = useNavigate();
  const [users, setUsers] = useState<any[]>([]);
  const [roles, setRoles] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [newUser, setNewUser] = useState({ userName: "", password: "", firstName: "", lastName: "", roles: [] as string[] });
  const [editUser, setEditUser] = useState<any | null>(null);

  useEffect(() => {
    const token = localStorage.getItem("token");
    const storedRoles = localStorage.getItem("roles");
    if (!token) {
      console.log("No token found, redirecting to login");
      navigate("/");
      return;
    }
    if (storedRoles) {
      const parsedRoles = JSON.parse(storedRoles);
      if (!parsedRoles.includes("User Manager")) {
        console.log("User lacks User Manager role, redirecting to dashboard");
        navigate("/dashboard");
        return;
      }
    }
    fetchUsers(token);
    fetchRoles(token);
  }, [navigate]);

  const fetchUsers = async (token: string) => {
    try {
      console.log(`Fetching users from: ${API_ROUTES.USERS}`); // Updated
      const response = await fetch(API_ROUTES.USERS, { // Updated
        headers: { Authorization: `Bearer ${token}` },
      });
      const responseText = await response.text();
      console.log("Users Raw Response:", responseText);
      if (!response.ok) throw new Error(`Failed to fetch users: ${response.status} ${response.statusText} - ${responseText}`);
      const data = JSON.parse(responseText);
      setUsers(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
      setUsers([]);
      console.error("Fetch Users Error:", err);
    }
  };

  const fetchRoles = async (token: string) => {
    try {
      console.log(`Fetching roles from: ${API_ROUTES.USER_ROLES}`); // Updated
      const response = await fetch(API_ROUTES.USER_ROLES, { // Updated
        headers: { Authorization: `Bearer ${token}` },
      });
      const responseText = await response.text();
      console.log("Roles Raw Response:", responseText);
      if (!response.ok) throw new Error(`Failed to fetch roles: ${response.status} ${response.statusText} - ${responseText}`);
      const data = JSON.parse(responseText);
      setRoles(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
      setRoles([]);
      console.error("Fetch Roles Error:", err);
    }
  };

  const handleAddUser = async () => {
    const token = localStorage.getItem("token") || "";
    if (!newUser.userName || !newUser.password) {
      alert("Please enter a username and password for the new user");
      return;
    }
    try {
      const payload = {
        phoneNumber: newUser.userName, // Matches RegisterDto
        password: newUser.password,
        firstName: newUser.firstName,
        lastName: newUser.lastName,
        roles: newUser.roles
      };
      console.log("Add User Payload:", JSON.stringify(payload));
      const response = await fetch(API_ROUTES.REGISTER, { // Updated
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify(payload),
      });
      const responseText = await response.text();
      console.log("Add User Raw Response:", responseText);
      if (!response.ok) throw new Error(`Failed to add user: ${response.status} ${response.statusText} - ${responseText}`);
      alert("User added successfully!");
      setNewUser({ userName: "", password: "", firstName: "", lastName: "", roles: [] });
      fetchUsers(token);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
      console.error("Add User Error:", err);
    }
  };

  const handleUpdateUser = async () => {
    if (!editUser) return;
    const token = localStorage.getItem("token") || "";
    try {
      console.log(`Updating user: ${editUser.userName}`);
      const response = await fetch(API_ROUTES.UPDATE_USER, { // Updated
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          userId: editUser.id,
          userName: editUser.userName,
          password: editUser.password || null,
          firstName: editUser.firstName,
          lastName: editUser.lastName,
          roles: editUser.roles
        }),
      });
      const responseText = await response.text();
      console.log("Update User Raw Response:", responseText);
      if (!response.ok) throw new Error(`Failed to update user: ${response.status} ${response.statusText} - ${responseText}`);
      alert("User updated successfully!");
      setEditUser(null);
      fetchUsers(token);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
      console.error("Update User Error:", err);
    }
  };

  const handleDeleteUser = async (userId: string) => {
    const token = localStorage.getItem("token") || "";
    if (!window.confirm("Are you sure you want to delete this user?")) return;
    try {
      console.log(`Deleting user ${userId}`);
      const response = await fetch(API_ROUTES.DELETE_USER, { // Updated
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ userId }),
      });
      const responseText = await response.text();
      console.log("Delete User Raw Response:", responseText);
      if (!response.ok) throw new Error(`Failed to delete user: ${response.status} ${response.statusText} - ${responseText}`);
      alert("User deleted successfully!");
      setEditUser(null);
      fetchUsers(token);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
      console.error("Delete User Error:", err);
    }
  };

  const openEditUser = (user: any) => {
    setEditUser({ ...user, password: "" });
  };

  return (
    <div className="dashboard-container">
      <div className="dashboard-navbar">
        <button
          className="dashboard-button return-button"
          onClick={() => navigate("/dashboard")}
        >
          Return to Dashboard
        </button>
        <span className="dashboard-user">User Management</span>
        <button className="logout-button" onClick={() => { localStorage.clear(); navigate("/"); }}>
          Logout
        </button>
      </div>
      <h1 className="dashboard-title">User Management</h1>
      <div className="card-container">
        <section className="dashboard-card user-management-section">
          <h2 className="section-title">Edit Users</h2>
          {error && <p className="error-text">{error}</p>}
          {users.length > 0 ? (
            <ul className="user-list">
              {users.map((user) => (
                <li key={user.id} className="user-item">
                  <span className="user-name">{`${user.userName} (${user.firstName} ${user.lastName})`}</span>
                  <button
                    className="dashboard-button edit-button"
                    onClick={() => openEditUser(user)}
                  >
                    Edit
                  </button>
                </li>
              ))}
            </ul>
          ) : (
            <p className="dashboard-text">No users found.</p>
          )}
        </section>
        <section className="dashboard-card add-user-card">
          <h2 className="section-title">Add New User</h2>
          <div className="add-user-form">
            <label className="form-label">Phone Number</label>
            <input
              type="text"
              value={newUser.userName}
              onChange={(e) => setNewUser({ ...newUser, userName: e.target.value })}
              placeholder="Phone Number"
              className="search-bar-input"
            />
            <label className="form-label">Password</label>
            <input
              type="password"
              value={newUser.password}
              onChange={(e) => setNewUser({ ...newUser, password: e.target.value })}
              placeholder="Password"
              className="search-bar-input"
            />
            <label className="form-label">First Name</label>
            <input
              type="text"
              value={newUser.firstName}
              onChange={(e) => setNewUser({ ...newUser, firstName: e.target.value })}
              placeholder="First Name"
              className="search-bar-input"
            />
            <label className="form-label">Last Name</label>
            <input
              type="text"
              value={newUser.lastName}
              onChange={(e) => setNewUser({ ...newUser, lastName: e.target.value })}
              placeholder="Last Name"
              className="search-bar-input"
            />
            <label className="form-label">Roles</label>
            <div className="role-checkboxes">
              {roles.map((role) => (
                <label key={role} className="role-checkbox">
                  <input
                    type="checkbox"
                    checked={newUser.roles.includes(role)}
                    onChange={(e) => {
                      const updatedRoles = e.target.checked
                        ? [...newUser.roles, role]
                        : newUser.roles.filter((r) => r !== role);
                      setNewUser({ ...newUser, roles: updatedRoles });
                    }}
                  />
                  {role}
                </label>
              ))}
            </div>
            <button className="dashboard-button" onClick={handleAddUser}>
              Add User
            </button>
          </div>
        </section>
      </div>

      {editUser && (
        <div className="modal-overlay">
          <div className="modal-content edit-user-modal">
            <h2 className="modal-title">Edit User</h2>
            <div className="add-user-form">
              <label className="form-label">Phone Number</label>
              <input
                type="text"
                value={editUser.userName}
                onChange={(e) => setEditUser({ ...editUser, userName: e.target.value })}
                placeholder="Phone Number"
                className="search-bar-input"
              />
              <label className="form-label">Password (leave blank to keep current)</label>
              <input
                type="password"
                value={editUser.password || ""}
                onChange={(e) => setEditUser({ ...editUser, password: e.target.value })}
                placeholder="New Password (optional)"
                className="search-bar-input"
              />
              <label className="form-label">First Name</label>
              <input
                type="text"
                value={editUser.firstName}
                onChange={(e) => setEditUser({ ...editUser, firstName: e.target.value })}
                placeholder="First Name"
                className="search-bar-input"
              />
              <label className="form-label">Last Name</label>
              <input
                type="text"
                value={editUser.lastName}
                onChange={(e) => setEditUser({ ...editUser, lastName: e.target.value })}
                placeholder="Last Name"
                className="search-bar-input"
              />
              <label className="form-label">Roles</label>
              <div className="role-checkboxes">
                {roles.map((role) => (
                  <label key={role} className="role-checkbox">
                    <input
                      type="checkbox"
                      checked={editUser.roles.includes(role)}
                      onChange={(e) => {
                        const updatedRoles = e.target.checked
                          ? [...editUser.roles, role]
                          : editUser.roles.filter((r: string) => r !== role);
                        setEditUser({ ...editUser, roles: updatedRoles });
                      }}
                    />
                    {role}
                  </label>
                ))}
              </div>
              <div className="modal-buttons">
                <button className="dashboard-button" onClick={handleUpdateUser}>
                  Update
                </button>
                <button
                  className="dashboard-button reject-button"
                  onClick={() => handleDeleteUser(editUser.id)}
                >
                  Delete
                </button>
                <button
                  className="dashboard-button"
                  onClick={() => setEditUser(null)}
                >
                  Cancel
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default UserManagementPage;