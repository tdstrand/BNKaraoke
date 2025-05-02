import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import App from './App';
import reportWebVitals from './reportWebVitals';

const isDevelopment = process.env.NODE_ENV !== 'production';

const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(
  isDevelopment ? (
    <React.StrictMode>
      <App />
    </React.StrictMode>
  ) : (
    <App />
  )
);

reportWebVitals();