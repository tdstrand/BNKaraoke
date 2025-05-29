using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace BNKaraoke.DJ.Models;

public class QueueEntry : INotifyPropertyChanged
{
    private int _queueId;
    private int _eventId;
    private int _songId;
    private string? _songTitle;
    private string? _songArtist;
    private string? _requestorDisplayName;
    private string? _videoLength;
    private int _position;
    private string? _status;
    private string? _requestorUserName;
    private List<string>? _singers;
    private bool _isActive;
    private bool _wasSkipped;
    private bool _isCurrentlyPlaying;
    private DateTime? _sungAt;
    private string? _genre;
    private string? _decade;
    private string? _youTubeUrl;
    private bool _isVideoCached;
    private bool _isOnBreak;

    public int QueueId
    {
        get => _queueId;
        set
        {
            _queueId = value;
            OnPropertyChanged(nameof(QueueId));
        }
    }

    public int EventId
    {
        get => _eventId;
        set
        {
            _eventId = value;
            OnPropertyChanged(nameof(EventId));
        }
    }

    public int SongId
    {
        get => _songId;
        set
        {
            _songId = value;
            OnPropertyChanged(nameof(SongId));
        }
    }

    public string? SongTitle
    {
        get => _songTitle;
        set
        {
            _songTitle = value;
            OnPropertyChanged(nameof(SongTitle));
        }
    }

    public string? SongArtist
    {
        get => _songArtist;
        set
        {
            _songArtist = value;
            OnPropertyChanged(nameof(SongArtist));
        }
    }

    public string? RequestorDisplayName
    {
        get => _requestorDisplayName;
        set
        {
            _requestorDisplayName = value;
            OnPropertyChanged(nameof(RequestorDisplayName));
        }
    }

    public string? VideoLength
    {
        get => _videoLength;
        set
        {
            _videoLength = value;
            OnPropertyChanged(nameof(VideoLength));
        }
    }

    public int Position
    {
        get => _position;
        set
        {
            _position = value;
            OnPropertyChanged(nameof(Position));
        }
    }

    public string? Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }

    public string? RequestorUserName
    {
        get => _requestorUserName;
        set
        {
            _requestorUserName = value;
            OnPropertyChanged(nameof(RequestorUserName));
        }
    }

    public List<string>? Singers
    {
        get => _singers;
        set
        {
            _singers = value;
            OnPropertyChanged(nameof(Singers));
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            OnPropertyChanged(nameof(IsActive));
        }
    }

    public bool WasSkipped
    {
        get => _wasSkipped;
        set
        {
            _wasSkipped = value;
            OnPropertyChanged(nameof(WasSkipped));
        }
    }

    public bool IsCurrentlyPlaying
    {
        get => _isCurrentlyPlaying;
        set
        {
            _isCurrentlyPlaying = value;
            OnPropertyChanged(nameof(IsCurrentlyPlaying));
        }
    }

    public DateTime? SungAt
    {
        get => _sungAt;
        set
        {
            _sungAt = value;
            OnPropertyChanged(nameof(SungAt));
        }
    }

    public string? Genre
    {
        get => _genre;
        set
        {
            _genre = value;
            OnPropertyChanged(nameof(Genre));
        }
    }

    public string? Decade
    {
        get => _decade;
        set
        {
            _decade = value;
            OnPropertyChanged(nameof(Decade));
        }
    }

    public string? YouTubeUrl
    {
        get => _youTubeUrl;
        set
        {
            _youTubeUrl = value;
            OnPropertyChanged(nameof(YouTubeUrl));
        }
    }

    public bool IsVideoCached
    {
        get => _isVideoCached;
        set
        {
            _isVideoCached = value;
            OnPropertyChanged(nameof(IsVideoCached));
        }
    }

    public bool IsOnBreak
    {
        get => _isOnBreak;
        set
        {
            _isOnBreak = value;
            OnPropertyChanged(nameof(IsOnBreak));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}