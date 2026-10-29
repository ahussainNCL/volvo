import React from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import { api } from './api';
import './style.css';

const fragment = new URLSearchParams(location.hash.slice(1));
const token = fragment.get('session');
// Erase bootstrap credentials before rendering or doing any network work.
history.replaceState(null, '', location.pathname + location.search);
const bootstrap = token ? api<void>('session', { token }) : Promise.resolve();
createRoot(document.getElementById('root')!).render(<React.StrictMode><App bootstrap={bootstrap} /></React.StrictMode>);
