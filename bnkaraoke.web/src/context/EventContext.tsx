import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';

interface EventContextType {
  currentEvent: any | null;
  setCurrentEvent: (event: any | null) => void;
  checkedIn: boolean;
  setCheckedIn: (value: boolean) => void;
  isCurrentEventLive: boolean;
  setIsCurrentEventLive: (value: boolean) => void;
}

const EventContext = createContext<EventContextType | undefined>(undefined);

export const EventContextProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  // Initialize state from local storage
  const [currentEvent, setCurrentEvent] = useState<any | null>(() => {
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

  return (
    <EventContext.Provider
      value={{
        currentEvent,
        setCurrentEvent,
        checkedIn,
        setCheckedIn,
        isCurrentEventLive,
        setIsCurrentEventLive,
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