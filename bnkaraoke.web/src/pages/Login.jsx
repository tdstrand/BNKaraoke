import React, { useContext, useState } from 'react';
import { AuthContext } from '../App';
import { useNavigate } from 'react-router-dom';
import './Login.css';

const Login = () => {
    const { setUser } = useContext(AuthContext);
    const navigate = useNavigate();
    const [credentials, setCredentials] = useState({ username: '', password: '' });

    const handleLogin = () => {
        if (credentials.username === "admin" && credentials.password === "password") {
            setUser({ role: "admin" });
            localStorage.setItem("user", JSON.stringify({ role: "admin" }));
            navigate("/");
        } else {
            alert("Invalid credentials!");
        }
    };

    return (
        <div className="login-container">
            <h2>Login to Blue Nest Karaoke</h2>
            <p>Enter your credentials to access your karaoke experience.</p>

            <input
                type="text"
                placeholder="Username"
                className="login-input"
                onChange={(e) => setCredentials({ ...credentials, username: e.target.value })}
            />
            <input
                type="password"
                placeholder="Password"
                className="login-input"
                onChange={(e) => setCredentials({ ...credentials, password: e.target.value })}
            />

            <button className="login-button" onClick={handleLogin}>Login</button>
        </div>
    );
};

export default Login;
