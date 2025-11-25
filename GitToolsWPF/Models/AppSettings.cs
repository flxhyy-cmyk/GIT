using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GitToolsWPF.Models
{
    public class AppSettings : INotifyPropertyChanged
    {
        private string _gitHubUser = "";
        private string _gitHubToken = "";
        private string _repoUrl = "";
        private string _localFolder = "";
        private string _theme = "System";
        private List<string> _localFolderHistory = new();

        public string GitHubUser
        {
            get => _gitHubUser;
            set { _gitHubUser = value; OnPropertyChanged(); }
        }

        public string GitHubToken
        {
            get => _gitHubToken;
            set { _gitHubToken = value; OnPropertyChanged(); }
        }

        public string RepoUrl
        {
            get => _repoUrl;
            set { _repoUrl = value; OnPropertyChanged(); }
        }

        public string LocalFolder
        {
            get => _localFolder;
            set { _localFolder = value; OnPropertyChanged(); }
        }

        public string Theme
        {
            get => _theme;
            set { _theme = value; OnPropertyChanged(); }
        }

        public List<string> LocalFolderHistory
        {
            get => _localFolderHistory;
            set { _localFolderHistory = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
