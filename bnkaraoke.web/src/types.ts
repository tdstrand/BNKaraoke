// BNKaraoke/bnkaraoke.web/src/types.ts
export interface Song {
    id: number;
    title: string;
    artist: string;
    spotifyId?: string;
    youTubeUrl?: string;
    status?: string;
    genre?: string;
    popularity?: number;
    bpm?: number;
    energy?: number;
    valence?: number;
    danceability?: number;
    requestDate?: string;
    requestedBy?: string;
    approvedBy?: string;
    decade?: string;
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
    status?: string;
    singers: string[];
    requests: Array<{ forWhom: string; status: string }>;
  }
  
  export interface Event {
    id: number;
    name: string;
    status: 'Upcoming' | 'Live' | 'Archived';
    date: string;
  }
  
  export interface User {
    firstName: string;
    lastName: string;
  }