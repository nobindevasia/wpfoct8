using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using D2G.Iris.ML.ConfigUI.WPF.Commands;
using D2G.Iris.ML.ConfigUI.WPF.Models;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class TrainingLogsViewModel : BaseViewModel
    {
        private string _logText = "";
        private readonly TextWriter _originalConsoleOut;

        public TrainingLogsViewModel()
        {
            LogEntries = new ObservableCollection<LogEntry>();
            InitializeCommands();
            _originalConsoleOut = Console.Out;
            RedirectConsoleOutput();
            LogMessage("Welcome to Iris ML Configuration Tool", "Info");
            LogMessage("Use the tabs to configure your model settings and click 'Launch Training' to start training", "Info");
        }

        #region Properties

        public ObservableCollection<LogEntry> LogEntries { get; }

        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        #endregion

        #region Commands

        public ICommand ClearLogsCommand { get; private set; } = null!;
        public ICommand SaveLogsCommand { get; private set; } = null!;

        #endregion

        private void InitializeCommands()
        {
            ClearLogsCommand = new RelayCommand(ClearLogs);
            SaveLogsCommand = new RelayCommand(SaveLogs);
        }

        private void RedirectConsoleOutput()
        {
            var textWriter = new ConsoleTextWriter(this);
            Console.SetOut(textWriter);
        }

        public void LogMessage(string message, string level)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message
                };

                LogEntries.Add(logEntry);


                string timestamp = logEntry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                string formattedMessage = $"[{timestamp}] [{level}] {message}{Environment.NewLine}";
                LogText += formattedMessage;


                while (LogEntries.Count > 1000)
                {
                    LogEntries.RemoveAt(0);
                }
            });
        }

        public void ClearLogs()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                LogEntries.Clear();
                LogText = "";
            });
        }

        private void SaveLogs()
        {
            try
            {
                string filename = $"Training_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                File.WriteAllText(filename, LogText);
                LogMessage($"Logs saved to: {filename}", "Info");
            }
            catch (Exception ex)
            {
                LogMessage($"Error saving logs: {ex.Message}", "Error");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Console.SetOut(_originalConsoleOut);
            }
            base.Dispose(disposing);
        }
    }

    public class ConsoleTextWriter : TextWriter
    {
        private readonly TrainingLogsViewModel _logsViewModel;

        public ConsoleTextWriter(TrainingLogsViewModel logsViewModel)
        {
            _logsViewModel = logsViewModel ?? throw new ArgumentNullException(nameof(logsViewModel));
        }

        public override void Write(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _logsViewModel.LogMessage(value, "Console");
            }
        }

        public override void WriteLine(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _logsViewModel.LogMessage(value, "Console");
            }
        }

        public override Encoding Encoding => Encoding.UTF8;
    }
}