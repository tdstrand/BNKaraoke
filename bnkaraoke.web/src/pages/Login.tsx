import React, { useState, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { API_ROUTES } from "../config/apiConfig";
import "./Login.css";

const Login = () => {
  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const navigate = useNavigate();

  const userNameRef = useRef<HTMLInputElement>(null);
  const passwordRef = useRef<HTMLInputElement>(null);

  const handleLogin = async () => {
    if (!userName || !password) return; // Prevent login if fields are empty

    try {
      const response = await fetch(API_ROUTES.LOGIN, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ userName, password }),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || "Invalid credentials");
      }

      const data = await response.json();
      localStorage.setItem("token", data.token);
      localStorage.setItem("roles", JSON.stringify(data.roles));
      localStorage.setItem("firstName", data.firstName);
      localStorage.setItem("lastName", data.lastName);

      navigate("/dashboard");
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter") {
      if (!userName && userNameRef.current) {
        userNameRef.current.focus(); // ✅ Move to username field if empty
      } else if (!password && passwordRef.current) {
        passwordRef.current.focus(); // ✅ Move to password field if empty
      } else {
        handleLogin(); // ✅ Log in only if both fields are filled
      }
    }
  };

  return (
    <div className="login-container">
      <div className="login-card">
        <h2 className="login-title">Welcome Back</h2>
        {error && <p className="login-error">{error}</p>}
        <div className="login-form">
          <input
            type="text"
            placeholder="Phone Number"
            value={userName}
            onChange={(e) => setUserName(e.target.value)}
            onKeyDown={handleKeyDown}
            ref={userNameRef}
            className="login-input"
          />
          <input
            type="password"
            placeholder="Password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            onKeyDown={handleKeyDown}
            ref={passwordRef}
            className="login-input"
          />
          <button onClick={handleLogin} className="login-button">
            Log in
          </button>
        </div>
      </div>
    </div>
  );
};

export default Login;
