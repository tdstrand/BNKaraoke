using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BNKaraoke.DJ.Services;

namespace BNKaraoke.DJ.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly IApiService _apiService;
        private readonly ISignalRService _signalRService;

        public event PropertyChangedEventHandler? PropertyChanged;

        // Backing fields initialized with defaults.
        private string _loginButtonText = string.Empty;
        private string _loginButtonColor = string.Empty;
        private string _joinLiveEventButtonText = string.Empty;
        private string _joinLiveEventButtonColor = string.Empty;
        private string _currentEventName = string.Empty;

        public string LoginButtonText
        {
            get => _loginButtonText;
            set { _loginButtonText = value; OnPropertyChanged(); }
        }

        public string LoginButtonColor
        {
            get => _loginButtonColor;
            set { _loginButtonColor = value; OnPropertyChanged(); }
        }

        // Instead of using Avalonias Visibility type, we use a Boolean property.
        private bool _isJoinLiveEventButtonVisible;
        public bool IsJoinLiveEventButtonVisible
        {
            get => _isJoinLiveEventButtonVisible;
            set { _isJoinLiveEventButtonVisible = value; OnPropertyChanged(); }
        }

        public string JoinLiveEventButtonText
        {
            get => _joinLiveEventButtonText;
            set { _joinLiveEventButtonText = value; OnPropertyChanged(); }
        }

        public string JoinLiveEventButtonColor
        {
            get => _joinLiveEventButtonColor;
            set { _joinLiveEventButtonColor = value; OnPropertyChanged(); }
        }

        public string CurrentEventName
        {
            get => _currentEventName;
            set { _currentEventName = value; OnPropertyChanged(); }
        }

        private bool _isLoggedIn;
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set { _isLoggedIn = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand LoginCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        public MainWindowViewModel(IApiService apiService, ISignalRService signalRService)
        {
            _apiService = apiService;
            _signalRService = signalRService;

            // Initialize default values.
            IsLoggedIn = false;
            LoginButtonText = "Login";
            LoginButtonColor = "Blue";
            JoinLiveEventButtonText = "Join Live Event";
            JoinLiveEventButtonColor = "Blue";
            IsJoinLiveEventButtonVisible = false; // Button hidden until login.
            CurrentEventName = "No Live Event Joined";

            LoginCommand = new RelayCommand(async (param) => await ToggleLoginAsync());
            OpenSettingsCommand = new RelayCommand(async (param) => await OpenSettingsAsync());
        }

        private async Task ToggleLoginAsync(object? param = null)
        {
            if (!IsLoggedIn)
            {
                // Simulate successful login.
                IsLoggedIn = true;
                LoginButtonText = "Logout";
                LoginButtonColor = "Red";
                IsJoinLiveEventButtonVisible = true;
                CurrentEventName = "Live Event A";
            }
            else
            {
                // Logging out.
                IsLoggedIn = false;
                LoginButtonText = "Login";
                LoginButtonColor = "Blue";
                IsJoinLiveEventButtonVisible = false;
                CurrentEventName = "No Live Event Joined";
            }
            await Task.CompletedTask;
        }

        private async Task OpenSettingsAsync(object? param = null)
        {
            // Settings command (actual window opening is handled in the view).
            await Task.CompletedTask;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // RelayCommand implementation.
    public class RelayCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) =>
            _canExecute == null || _canExecute(parameter);

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        public async void Execute(object? parameter) =>
            await _execute(parameter);
    }
}