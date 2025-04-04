import React from 'react';
import ReactDOM from 'react-dom';
import SingerSearch from './components/SingerSearch';
import SongRequest from './components/SongRequest';
import './index.css';

const searchRoot = document.getElementById('react-root');
const requestRoot = document.getElementById('react-request-root');

if (searchRoot) ReactDOM.render(<SingerSearch />, searchRoot);
if (requestRoot) ReactDOM.render(<SongRequest />, requestRoot);