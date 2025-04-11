import React, { useState, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { API_ROUTES } from "../config/apiConfig"; // Remove BASE_API_URL
import "./Login.css";

const Login = () => {
  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const navigate = useNavigate();

  const userNameRef = useRef<HTMLInputElement>(null);
  const passwordRef = useRef<HTMLInputElement>(null);

  const handleLogin = async () => {
    if (!userName || !password) {
      setError("Please enter both phone number and password");
      return;
    }

    try {
      console.log(`Attempting login fetch to: ${API_ROUTES.LOGIN}`);
      const response = await fetch(API_ROUTES.LOGIN, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ UserName: userName, Password: password }), // Fixed keys
      });

      const responseText = await response.text();
      console.log("Login Raw Response:", responseText);

      if (!response.ok) {
        let errorData;
        try {
          errorData = JSON.parse(responseText);
        } catch {
          errorData = { message: "Invalid credentials or server error" };
        }
        throw new Error(errorData.message || `Login failed: ${response.status} ${response.statusText}`);
      }

      const data = JSON.parse(responseText);
      localStorage.setItem("token", data.token);
      localStorage.setItem("roles", JSON.stringify(data.roles));
      localStorage.setItem("firstName", data.firstName);
      localStorage.setItem("lastName", data.lastName);

      navigate("/dashboard");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
      console.error("Login Error:", err);
    }
  };

  const handleKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter") {
      if (!userName && userNameRef.current) {
        userNameRef.current.focus();
      } else if (!password && passwordRef.current) {
        passwordRef.current.focus();
      } else {
        handleLogin();
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