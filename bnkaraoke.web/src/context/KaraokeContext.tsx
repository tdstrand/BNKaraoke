import React, { createContext, useContext, useState, ReactNode } from 'react';
import axios from 'axios';
import apiConfig from '../config/apiConfig';

// Define types for the context state
interface Event {
  eventId: number;
  eventCode: string;
  description: string;
  status: string;
  visibility: string;
  location: string;
  scheduledDate: string;
  scheduledStartTime: string | null;
  scheduledEndTime: string | null;
  karaokeDJName: string | null;
  isCanceled: boolean;
  requestLimit: number;
  queueCount: number;
}

interface EventQueueItem {
  queueId: number;
  eventId: number;
  songId: number;
  requestorId: string;
  singers: string[];
  position: number;
  status: string;
  isActive: boolean;
  wasSkipped: boolean;
  isCurrentlyPlaying: boolean;
  sungAt: string | null;
  isOnBreak: boolean;
}

interface User {
  id: string;
  userName: string;
  firstName: string;
  lastName: string;
}

interface KaraokeContextType {
  activeEvent: Event | null;
  setActiveEvent: (event: Event | null) => void;
  userQueue: EventQueueItem[];
  setUserQueue: (queue: EventQueueItem[]) => void;
  activeEventQueue: EventQueueItem[];
  setActiveEventQueue: (queue: EventQueueItem[]) => void;
  coSingers: User[];
  setCoSingers: (users: User[]) => void;
  fetchActiveEvent: (eventId: number) => Promise<void>;
  fetchUserQueue: (eventId: number, userId: string) => Promise<void>;
  fetchActiveEventQueue: (eventId: number) => Promise<void>;
  fetchCoSingers: () => Promise<void>;
}

const KaraokeContext = createContext<KaraokeContextType | undefined>(undefined);

interface KaraokeProviderProps {
  children: ReactNode;
}

export const KaraokeProvider: React.FC<KaraokeProviderProps> = ({ children }) => {
  const [activeEvent, setActiveEvent] = useState<Event | null>(null);
  const [userQueue, setUserQueue] = useState<EventQueueItem[]>([]);
  const [activeEventQueue, setActiveEventQueue] = useState<EventQueueItem[]>([]);
  const [coSingers, setCoSingers] = useState<User[]>([]);

  // Fetch the active event
  const fetchActiveEvent = async (eventId: number) => {
    try {
      const response = await axios.get(`${apiConfig.baseUrl}/api/events/${eventId}`, {
        headers: { Authorization: `Bearer ${localStorage.getItem('token')}` },
      });
      setActiveEvent(response.data);
    } catch (error) {
      console.error('Error fetching active event:', error);
    }
  };

  // Fetch the user's queue (Personal Queue)
  const fetchUserQueue = async (eventId: number, userId: string) => {
    try {
      const response = await axios.get(`${apiConfig.baseUrl}/api/events/${eventId}/queue`, {
        headers: { Authorization: `Bearer ${localStorage.getItem('token')}` },
      });
      const userQueueItems = response.data.filter(
        (item: EventQueueItem) => item.requestorId === userId || item.singers.includes(userId)
      );
      setUserQueue(userQueueItems);
    } catch (error) {
      console.error('Error fetching user queue:', error);
    }
  };

  // Fetch the active event queue
  const fetchActiveEventQueue = async (eventId: number) => {
    try {
      const response = await axios.get(`${apiConfig.baseUrl}/api/events/${eventId}/queue`, {
        headers: { Authorization: `Bearer ${localStorage.getItem('token')}` },
      });
      setActiveEventQueue(response.data);
    } catch (error) {
      console.error('Error fetching active event queue:', error);
    }
  };

  // Fetch co-singers (all users)
  const fetchCoSingers = async () => {
    try {
      const response = await axios.get(`${apiConfig.baseUrl}/api/songs/users`, {
        headers: { Authorization: `Bearer ${localStorage.getItem('token')}` },
      });
      setCoSingers(response.data);
    } catch (error) {
      console.error('Error fetching co-singers:', error);
    }
  };

  return (
    <KaraokeContext.Provider
      value={{
        activeEvent,
        setActiveEvent,
        userQueue,
        setUserQueue,
        activeEventQueue,
        setActiveEventQueue,
        coSingers,
        setCoSingers,
        fetchActiveEvent,
        fetchUserQueue,
        fetchActiveEventQueue,
        fetchCoSingers,
      }}
    >
      {children}
    </KaraokeContext.Provider>
  );
};

// Custom hook to use the context
export const useKaraokeContext = () => {
  const context = useContext(KaraokeContext);
  if (!context) {
    throw new Error('useKaraokeContext must be used within a KaraokeProvider');
  }
  return context;
};