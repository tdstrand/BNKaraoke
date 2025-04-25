import React, { useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import './Login.css';
import LogoDuet from '../assets/TwoSingerMnt.png';
import { API_ROUTES } from '../config/apiConfig';

const Login: React.FC = () => {
  const [userName, setUserName] = useState<string>("");
  const [password, setPassword] = useState<string>("");
  const [error, setError] = useState<string>("");
  const navigate = useNavigate();
  const userNameRef = useRef<HTMLInputElement>(null);
  const passwordRef = useRef<HTMLInputElement>(null);

  // Format phone number as (xxx) xxx-xxxx
  const formatPhoneNumber = (value: string): string => {
    const digits = value.replace(/\D/g, "").slice(0, 10); // Keep only digits, max 10
    if (digits.length === 0) return "";
    if (digits.length <= 3) return `(${digits}`;
    if (digits.length <= 6) return `(${digits.slice(0, 3)}) ${digits.slice(3)}`;
    return `(${digits.slice(0, 3)}) ${digits.slice(3, 6)}-${digits.slice(6)}`;
  };

  // Handle phone input change
  const handlePhoneChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const rawValue = e.target.value.replace(/\D/g, ""); // Store raw digits
    setUserName(rawValue); // Store raw for submission
    e.target.value = formatPhoneNumber(rawValue); // Display formatted
  };

  const handleLogin = async () => {
    if (!userName || !password) {
      setError("Please enter both phone number and password");
      return;
    }
    const cleanPhone = userName.replace(/\D/g, "");
    console.log("Logging in with cleanPhone:", cleanPhone);
    try {
      console.log(`Attempting login fetch to: ${API_ROUTES.LOGIN}`);
      const response = await fetch(API_ROUTES.LOGIN, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ UserName: cleanPhone, Password: password }),
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
        throw new Error(errorData.message || `Login failed: ${response.status}`);
      }
      const data = JSON.parse(responseText);
      console.log("userId from response:", data.userId);
      localStorage.setItem("token", data.token);
      localStorage.setItem("userId", data.userId); // Store userId
      localStorage.setItem("roles", JSON.stringify(data.roles));
      localStorage.setItem("firstName", data.firstName);
      localStorage.setItem("lastName", data.lastName);
      localStorage.setItem("userName", cleanPhone);
      localStorage.setItem("mustChangePassword", data.mustChangePassword.toString());
      navigate(data.mustChangePassword ? "/change-password" : "/dashboard");
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
      <img src={LogoDuet} alt="BNKaraoke.com Logo" className="login-logo" />
      <div className="login-card">
        <h2 className="login-title">Welcome Back</h2>
        {error && <p className="login-error">{error}</p>}
        <div className="login-form">
          <label htmlFor="userName">Phone Number</label>
          <input
            type="text"
            id="userName"
            value={formatPhoneNumber(userName)}
            onChange={handlePhoneChange}
            onKeyDown={handleKeyDown}
            placeholder="(123) 456-7890"
            aria-label="Phone number"
            className="login-input"
            ref={userNameRef}
            maxLength={14}
          />
          <label htmlFor="password">Password</label>
          <input
            type="password"
            id="password"
            value={password}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => setPassword(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Enter password"
            aria-label="Password"
            className="login-input"
            ref={passwordRef}
          />
          <button onClick={handleLogin} className="login-button">
            Log in
          </button>
          <button onClick={() => navigate("/register")} className="login-button secondary-button">
            Register as a New Singer
          </button>
        </div>
        <p className="backlink">
          BPM data provided by <a href="https://getsongbpm.com" target="_blank" rel="noopener noreferrer">GetSongBPM</a>
        </p>
      </div>
    </div>
  );
};

export default Login;