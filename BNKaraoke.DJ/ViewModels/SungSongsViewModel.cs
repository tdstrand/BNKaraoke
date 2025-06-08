using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class SungSongsViewModel : ObservableObject
    {
        private readonly IApiService _apiService;
        private readonly string _eventId;

        [ObservableProperty]
        private ObservableCollection<QueueEntry> _sungSongs = new();

        [ObservableProperty]
        private int _sungCount;

        public IRelayCommand CloseCommand { get; }

        public SungSongsViewModel(IApiService apiService, string eventId)
        {
            _apiService = apiService;
            _eventId = eventId;
            CloseCommand = new RelayCommand(Close);
            InitializeAsync().GetAwaiter().GetResult();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var sungSongs = await _apiService.GetSungQueueAsync(_eventId);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    SungSongs.Clear();
                    foreach (var song in sungSongs)
                    {
                        SungSongs.Add(song);
                    }
                    SungCount = sungSongs.Count;
                    Log.Information("[SUNGSONGSVIEWMODEL] Loaded {Count} sung songs for EventId={EventId}", SungCount, _eventId);
                });
            }
            catch (Exception ex)
            {
                Log.Error("[SUNGSONGSVIEWMODEL] Failed to load sung songs for EventId={EventId}: {Message}", _eventId, ex.Message);
            }
        }

        private void Close()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
            }
        }
    }
}