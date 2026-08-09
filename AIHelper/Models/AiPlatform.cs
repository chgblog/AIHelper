using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AIHelper.Models
{
    /// <summary>
    /// Represents an AI platform configuration
    /// </summary>
    public class AiPlatform : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name;
        private string _url;
        private bool _isActive;
        private string _inputSelector;
        private string _submitSelector;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        public string Url
        {
            get => _url;
            set { if (_url != value) { _url = value; OnPropertyChanged(); } }
        }

        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Custom CSS selector for the input element (textarea or contenteditable)
        /// </summary>
        public string InputSelector
        {
            get => _inputSelector;
            set { if (_inputSelector != value) { _inputSelector = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Custom CSS selector for the submit button
        /// </summary>
        public string SubmitSelector
        {
            get => _submitSelector;
            set { if (_submitSelector != value) { _submitSelector = value; OnPropertyChanged(); } }
        }
    }
}
