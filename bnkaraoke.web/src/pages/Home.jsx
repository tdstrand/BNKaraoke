import React, { useState, useEffect, useContext } from 'react';
import { AuthContext } from '../App';
import Navbar from '../components/Navbar';
import { useNavigate } from 'react-router-dom';
import './Home.css';

const Home = () => {
    const { user } = useContext(AuthContext);
    const navigate = useNavigate();
    const [userRole, setUserRole] = useState('guest');

    useEffect(() => {
        const storedUser = JSON.parse(localStorage.getItem('user'));
        if (storedUser?.role) {
            setUserRole(storedUser.role);
        }
    }, []);

    const menuItems = {
        admin: ['Manage Users', 'Event Controls', 'Song Requests'],
        user: ['Browse Songs', 'Request a Song', 'Upcoming Events'],
        guest: ['View Songs', 'Login/Register']
    };

    return (
        <div className="home-container">
            <Navbar />
            <header className="home-header">
                <h1>Welcome to Blue Nest Karaoke</h1>
                <p>Bringing music to life, one song at a time.</p>
            </header>

            {user ? (
                <nav className="menu">
                    {menuItems[userRole]?.map((item, index) => (
                        <button key={index} className="menu-item">{item}</button>
                    ))}
                </nav>
            ) : (
                <button className="menu-item" onClick={() => navigate('/login')}>
                    Login/Register
                </button>
            )}
        </div>
    );
};

export default Home;
