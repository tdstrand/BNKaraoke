using BNKaraoke.DJ.Models;
using BNKaraoke.DJ.Services;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BNKaraoke.DJ.ViewModels
{
    public partial class DJScreenViewModel
    {
        private readonly Dictionary<string, Singer> _singerCache = new Dictionary<string, Singer>();

        public IRelayCommand UpdateSingerStatusCommand => new RelayCommand<string>(async parameter =>
        {
            try
            {
                if (string.IsNullOrEmpty(parameter))
                {
                    Log.Warning("[DJSCREEN] UpdateSingerStatusCommand: Parameter is null or empty");
                    return;
                }

                var parts = parameter.Split('|');
                if (parts.Length != 2)
                {
                    Log.Warning("[DJSCREEN] UpdateSingerStatusCommand: Invalid parameter format: {Parameter}", parameter);
                    return;
                }

                var status = parts[0];
                var userId = parts[1];
                var singer = Singers.FirstOrDefault(s => s.UserId == userId);
                if (singer == null)
                {
                    Log.Warning("[DJSCREEN] UpdateSingerStatusCommand: Singer not found for UserId={UserId}", userId);
                    return;
                }

                bool isLoggedIn, isJoined, isOnBreak;
                switch (status)
                {
                    case "Active":
                        isLoggedIn = true;
                        isJoined = true;
                        isOnBreak = false;
                        break;
                    case "OnBreak":
                        isLoggedIn = true;
                        isJoined = true;
                        isOnBreak = true;
                        break;
                    case "NotJoined":
                        isLoggedIn = true;
                        isJoined = false;
                        isOnBreak = false;
                        break;
                    case "LoggedOut":
                        isLoggedIn = false;
                        isJoined = false;
                        isOnBreak = false;
                        break;
                    default:
                        Log.Warning("[DJSCREEN] UpdateSingerStatusCommand: Unknown status: {Status}", status);
                        return;
                }

                await UpdateSingerStatusAsync(singer, isLoggedIn, isJoined, isOnBreak);
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] UpdateSingerStatusCommand failed: {Message}", ex.Message);
            }
        });

        private async Task LoadSingersAsync(string eventId, string updatedUserId = "")
        {
            try
            {
                Log.Information("[DJSCREEN] Loading singer data for event: {EventId}, UpdatedUserId={UpdatedUserId}", eventId, updatedUserId);
                var singers = await _apiService.GetSingersAsync(eventId);
                var updatedSingers = new List<Singer>();

                foreach (var singer in singers)
                {
                    if (_singerCache.TryGetValue(singer.UserId, out var cachedSinger) && cachedSinger.UpdatedAt > singer.UpdatedAt)
                    {
                        Log.Information("[DJSCREEN] Using cached singer for UserId={UserId}: IsLoggedIn={IsLoggedIn}, IsJoined={IsJoined}, IsOnBreak={IsOnBreak}",
                            singer.UserId, cachedSinger.IsLoggedIn, cachedSinger.IsJoined, cachedSinger.IsOnBreak);
                        updatedSingers.Add(cachedSinger);
                    }
                    else
                    {
                        updatedSingers.Add(singer);
                    }
                }

                await UpdateSingersCollectionAsync(updatedSingers, updatedUserId);
                Log.Information("[DJSCREEN] Loaded {Count} singers for event {EventId}, Names={Names}", updatedSingers.Count, eventId, string.Join(", ", updatedSingers.Select(s => s.DisplayName)));
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to load singers for EventId={EventId}: {Message}", eventId, ex.Message);
            }
        }

        private async Task UpdateSingersCollectionAsync(List<Singer> newSingers, string updatedUserId)
        {
            await Task.Run(() =>
            {
                var existingSingers = Singers.ToDictionary(s => s.UserId, s => s);
                foreach (var newSinger in newSingers)
                {
                    if (existingSingers.TryGetValue(newSinger.UserId, out var existingSinger))
                    {
                        Log.Information("[DJSCREEN] Updating singer UserId={UserId}: OldState=[IsLoggedIn={OldLoggedIn},IsJoined={OldJoined},IsOnBreak={OldOnBreak}], NewState=[IsLoggedIn={NewLoggedIn},IsJoined={NewJoined},IsOnBreak={NewOnBreak}]",
                            newSinger.UserId, existingSinger.IsLoggedIn, existingSinger.IsJoined, existingSinger.IsOnBreak,
                            newSinger.IsLoggedIn, newSinger.IsJoined, newSinger.IsOnBreak);

                        existingSinger.UserId = newSinger.UserId;
                        existingSinger.DisplayName = newSinger.DisplayName;
                        existingSinger.IsLoggedIn = newSinger.IsLoggedIn;
                        existingSinger.IsJoined = newSinger.IsJoined;
                        existingSinger.IsOnBreak = newSinger.IsOnBreak;
                        existingSinger.UpdatedAt = newSinger.UpdatedAt;
                    }
                    else
                    {
                        Singers.Add(newSinger);
                    }
                }

                var singersToRemove = existingSingers.Keys.Except(newSingers.Select(s => s.UserId)).ToList();
                foreach (var userId in singersToRemove)
                {
                    Singers.Remove(existingSingers[userId]);
                }

                SortSingers();
            });
        }

        private async Task UpdateSingerStatusAsync(Singer singer, bool isLoggedIn, bool isJoined, bool isOnBreak)
        {
            try
            {
                Log.Information("[DJSCREEN] Sending update singer status request: EventId={EventId}, UserId={UserId}, IsLoggedIn={IsLoggedIn}, IsJoined={IsJoined}, IsOnBreak={IsOnBreak}",
                    CurrentEvent?.EventId, singer.UserId, isLoggedIn, isJoined, isOnBreak);

                var updatedSinger = await _apiService.UpdateSingerStatusAsync(
                    CurrentEvent.EventId.ToString(),
                    singer.UserId,
                    isLoggedIn,
                    isJoined,
                    isOnBreak);

                Log.Information("[DJSCREEN] Successfully updated singer status for EventId={EventId}, RequestorUserName={UserId}, Response={Response}",
                    CurrentEvent.EventId, singer.UserId, System.Text.Json.JsonSerializer.Serialize(updatedSinger));

                // Update local Singers collection with returned Singer
                var existingSinger = Singers.FirstOrDefault(s => s.UserId == updatedSinger.UserId);
                if (existingSinger != null)
                {
                    existingSinger.UserId = updatedSinger.UserId;
                    existingSinger.DisplayName = updatedSinger.DisplayName;
                    existingSinger.IsLoggedIn = updatedSinger.IsLoggedIn;
                    existingSinger.IsJoined = updatedSinger.IsJoined;
                    existingSinger.IsOnBreak = updatedSinger.IsOnBreak;
                    existingSinger.UpdatedAt = DateTime.UtcNow;
                    _singerCache[updatedSinger.UserId] = new Singer
                    {
                        UserId = updatedSinger.UserId,
                        DisplayName = updatedSinger.DisplayName,
                        IsLoggedIn = updatedSinger.IsLoggedIn,
                        IsJoined = updatedSinger.IsJoined,
                        IsOnBreak = updatedSinger.IsOnBreak,
                        UpdatedAt = DateTime.UtcNow
                    };
                }
                else
                {
                    Singers.Add(updatedSinger);
                    _singerCache[updatedSinger.UserId] = updatedSinger;
                }

                SortSingers();
                Log.Information("[DJSCREEN] Locally updated singer status for UserId={UserId}, Status={Status}, SingersCount={Count}",
                    singer.UserId, isJoined ? "Joined" : "NotJoined", Singers.Count);
            }
            catch (Exception ex)
            {
                Log.Error("[DJSCREEN] Failed to update singer status for UserId={UserId}: {Message}", singer.UserId, ex.Message);
            }
        }
    }
}