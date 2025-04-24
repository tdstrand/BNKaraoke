// bnkaraoke.web/src/types.ts
export interface Song {
    id: number;
    title: string;
    artist: string;
    genre?: string;
    youTubeUrl?: string;
    status: string;
    approvedBy?: string;
    bpm: number;
    popularity: number;
    requestDate: string;
    requestedBy: string;
    spotifyId: string;
    valence?: number;
    decade?: string;
    musicBrainzId?: string;
    mood?: string;
    lastFmPlaycount?: number;
    danceability?: string;
    energy?: string;
  }
  
  export interface SpotifySong {
    id: string;
    title: string;
    artist: string;
    genre?: string;
    popularity?: number;
    bpm?: number;
    energy?: number;
    valence?: number;
    danceability?: number;
    decade?: string;
  }
  
  export interface QueueItem {
    id: number;
    title: string;
    artist: string;
    status: string;
    singers: string[];
    requests: { forWhom: string }[];
  }
  
  export interface Event {
    id: number;
    eventId: number;
    eventCode: string;
    description: string;
    status: string;
    visibility: string;
    location: string;
    scheduledDate: string;
    scheduledStartTime?: string;
    scheduledEndTime?: string;
    karaokeDJName?: string;
    isCanceled: boolean;
    queueCount: number;
  }
  
  export interface EventQueueItem {
    queueId: number;
    eventId: number;
    songId: number;
    singerId: string;
    position: number;
    status: string;
    isActive: boolean;
    wasSkipped: boolean;
    isCurrentlyPlaying: boolean;
    sungAt?: string;
    isOnBreak: boolean;
  }
  
  export interface User {
    firstName: string;
    lastName: string;
  }