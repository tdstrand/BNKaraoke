import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { Event, EventQueueItem, EventQueueItemResponse } from '../types';
import { API_ROUTES } from '../config/apiConfig';

interface EventContextType {
  currentEvent: Event | null;
  setCurrentEvent: (event: Event | null) => void;
  checkedIn: boolean;
  setCheckedIn: (value: boolean) => void;
  isCurrentEventLive: boolean;
  setIsCurrentEventLive: (value: boolean) => void;
  isOnBreak: boolean;
  setIsOnBreak: (value: boolean) => void;
}

const EventContext = createContext<EventContextType | undefined>(undefined);

export const EventContextProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  // Initialize state from local storage
  const [currentEvent, setCurrentEvent] = useState<Event | null>(() => {
    const storedEvent = localStorage.getItem("currentEvent");
    return storedEvent ? JSON.parse(storedEvent) : null;
  });
  const [checkedIn, setCheckedIn] = useState<boolean>(() => {
    const storedCheckedIn = localStorage.getItem("checkedIn");
    return storedCheckedIn === "true";
  });
  const [isCurrentEventLive, setIsCurrentEventLive] = useState<boolean>(() => {
    const storedIsLive = localStorage.getItem("isCurrentEventLive");
    return storedIsLive === "true";
  });
  const [isOnBreak, setIsOnBreak] = useState<boolean>(() => {
    const storedIsOnBreak = localStorage.getItem("isOnBreak");
    return storedIsOnBreak === "true";
  });

  // Persist currentEvent to local storage
  useEffect(() => {
    if (currentEvent) {
      localStorage.setItem("currentEvent", JSON.stringify(currentEvent));
    } else {
      localStorage.removeItem("currentEvent");
    }
  }, [currentEvent]);

  // Persist checkedIn to local storage
  useEffect(() => {
    localStorage.setItem("checkedIn", checkedIn.toString());
  }, [checkedIn]);

  // Persist isCurrentEventLive to local storage
  useEffect(() => {
    localStorage.setItem("isCurrentEventLive", isCurrentEventLive.toString());
  }, [isCurrentEventLive]);

  // Persist isOnBreak to local storage
  useEffect(() => {
    localStorage.setItem("isOnBreak", isOnBreak.toString());
  }, [isOnBreak]);

  // Fetch attendance status when currentEvent changes
  useEffect(() => {
    const fetchAttendanceStatus = async () => {
      if (!currentEvent) {
        setCheckedIn(false);
        setIsOnBreak(false);
        return;
      }

      const token = localStorage.getItem("token");
      const userName = localStorage.getItem("userName");
      if (!token || !userName) {
        console.error("No token or userName found");
        setCheckedIn(false);
        setIsOnBreak(false);
        return;
      }

      try {
        // Fetch the user's queue to determine check-in and break status
        const queueResponse = await fetch(`${API_ROUTES.EVENT_QUEUE}/${currentEvent.eventId}/queue`, {
          headers: { Authorization: `Bearer ${token}` },
        });
        if (!queueResponse.ok) {
          const errorText = await queueResponse.text();
          console.error(`Fetch queue failed for event ${currentEvent.eventId}: ${queueResponse.status} - ${errorText}`);
          throw new Error(`Fetch queue failed: ${queueResponse.status}`);
        }
        const queueData: EventQueueItemResponse[] = await queueResponse.json();
        const parsedQueueData: EventQueueItem[] = queueData.map(item => ({
          ...item,
          singers: item.singers ? JSON.parse(item.singers) : [],
        }));
        const userQueue = parsedQueueData.filter(item => item.requestorUserName === userName);

        // If the user has queue items, they are checked in
        const isUserCheckedIn = userQueue.length > 0;
        setCheckedIn(isUserCheckedIn);

        // Determine break status based on queue items
        const userOnBreak = isUserCheckedIn && userQueue.some(item => item.isOnBreak);
        setIsOnBreak(userOnBreak);
      } catch (err) {
        console.error("Fetch attendance status error:", err);
        setCheckedIn(false);
        setIsOnBreak(false);
      }
    };

    fetchAttendanceStatus();
  }, [currentEvent]);

  return (
    <EventContext.Provider
      value={{
        currentEvent,
        setCurrentEvent,
        checkedIn,
        setCheckedIn,
        isCurrentEventLive,
        setIsCurrentEventLive,
        isOnBreak,
        setIsOnBreak,
      }}
    >
      {children}
    </EventContext.Provider>
  );
};

const useEventContext = () => {
  const context = useContext(EventContext);
  if (!context) {
    throw new Error('useEventContext must be used within an EventContextProvider');
  }
  return context;
};

export default useEventContext;