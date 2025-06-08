using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class DJScreenViewModel
    {
        [RelayCommand]
        private async Task UpdateSingerStatus(object parameter)
        {
            Log.Information("[DJSCREEN] UpdateSingerStatus command invoked");
            if (parameter is Tuple<string, string> tuple && !string.IsNullOrEmpty(_currentEventId))
            {
                var (userId, status) = tuple;
                try
                {
                    bool isLoggedIn = status != "LoggedOut";
                    bool isJoined = status == "Active" || status == "OnBreak";
                    bool isOnBreak = status == "OnBreak";
                    await _apiService.UpdateSingerStatusAsync(_currentEventId, userId, isLoggedIn, isJoined, isOnBreak);
                    Log.Information("[DJSCREEN] Updated singer status for UserId={UserId}, Status={Status}", userId, status);
                    await LoadSingerData();
                }
                catch (Exception ex)
                {
                    Log.Error("[DJSCREEN] Failed to update singer status for UserId={UserId}: {Message}", userId, ex.Message);
                    WarningMessage = $"Failed to update singer status: {ex.Message}";
                }
            }
            else
            {
                Log.Information("[DJSCREEN] UpdateSingerStatus failed: Invalid parameter or no event joined");
                WarningMessage = "Please select a singer and join an event.";
            }
        }

        private void SortSingers()
        {
            try
            {
                Log.Information("[DJSCREEN] Sorting singers");
                var sortedSingers = Singers.OrderBy(singer =>
                {
                    if (singer.IsLoggedIn && singer.IsJoined && !singer.IsOnBreak) return 1; // Green
                    if (singer.IsLoggedIn && singer.IsJoined && singer.IsOnBreak) return 2; // Yellow
                    if (singer.IsLoggedIn && !singer.IsJoined) return 3; // Orange
                    return 4; // Red
                }).ThenBy(singer => singer.DisplayName).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Singers.Clear();
                    foreach (var singer in sortedSingers)
                    {
                        Singers.Add(singer);
                    }

                    GreenSingers.Clear();
                    YellowSingers.Clear();
                    OrangeSingers.Clear();
                    RedSingers.Clear();

                    foreach (var singer in Singers)
                    {
                        if (singer.IsLoggedIn && singer.IsJoined && !singer.IsOnBreak)
                            GreenSingers.Add(singer);
                        else if (singer.IsLoggedIn && singer.IsJoined && singer.IsOnBreak)
                            YellowSingers.Add(singer);
                        else if (singer.IsLoggedIn && !singer.IsJoined)
                            OrangeSingers.Add(singer);
                        else
                            RedSingers.Add(singer);
                    }

                    NonDummySingersCount = Singers.Count;
                    OnPropertyChanged(nameof(NonDummySingersCount));
                    OnPropertyChanged(nameof(GreenSingers));
                    OnPropertyChanged(nameof(YellowSingers));
                    OnPropertyChanged(nameof(OrangeSingers));
                    OnPropertyChanged(nameof(RedSingers));
                    Log.Information("[DJSCREEN] Sorted singers: {Count} singers, Names={Names}",
                        NonDummySingersCount, string.Join(", ", Singers.Select(s => s.DisplayName)));
                });
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to sort singers: {Message}", ex.Message);
                WarningMessage = $"Failed to sort singers: {ex.Message}";
            }
        }

        private async Task LoadSingerData()
        {
            try
            {
                Log.Information("[DJSCREEN] Loading singer data for event: {EventId}", _currentEventId);
                if (string.IsNullOrEmpty(_currentEventId))
                {
                    Log.Information("[DJSCREEN] No event joined, skipping singer data load");
                    Singers.Clear();
                    GreenSingers.Clear();
                    YellowSingers.Clear();
                    OrangeSingers.Clear();
                    RedSingers.Clear();
                    NonDummySingersCount = 0;
                    OnPropertyChanged(nameof(NonDummySingersCount));
                    return;
                }

                var singers = await _apiService.GetSingersAsync(_currentEventId);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Singers.Clear();
                    foreach (var singer in singers)
                    {
                        Singers.Add(singer);
                    }
                    SortSingers();
                    NonDummySingersCount = Singers.Count;
                    Log.Information("[DJSCREEN] Loaded {Count} singers for event {EventId}, Names={Names}",
                        NonDummySingersCount, _currentEventId, string.Join(", ", Singers.Select(s => s.DisplayName)));
                });
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to load singers for event: {EventId}: {Message}", _currentEventId, ex.Message);
                WarningMessage = $"Failed to load singers: {ex.Message}";
            }
        }
    }
}