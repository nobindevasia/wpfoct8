using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using D2G.Iris.ML.ConfigUI.WPF.Commands;
using D2G.Iris.ML.ConfigUI.WPF.Services;
using D2G.Iris.ML.Core.Models;

namespace D2G.Iris.ML.ConfigUI.WPF.ViewModels
{
    public class DatabaseSettingsViewModel : BaseViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly IDatabaseSchemaLoader _schemaLoader;
        private string _server = "localhost";
        private string _database = "IrisData";
        private string _tableName = "DataTable";
        private string _whereClause = "";
        private bool _isExplorerVisible = false;
        private bool _isLoadingTables = false;
        private TableInfo? _selectedTable;

        public DatabaseSettingsViewModel(IDialogService dialogService, IDatabaseSchemaLoader schemaLoader)
        {
            _dialogService = dialogService;
            _schemaLoader = schemaLoader;

            Databases = new ObservableCollection<string>();
            Tables = new ObservableCollection<TableInfo>();

            InitializeCommands();
        }

        #region Properties

        public string Server
        {
            get => _server;
            set => SetProperty(ref _server, value);
        }

        public string Database
        {
            get => _database;
            set => SetProperty(ref _database, value);
        }

        public string TableName
        {
            get => _tableName;
            set => SetProperty(ref _tableName, value);
        }

        public string WhereClause
        {
            get => _whereClause;
            set => SetProperty(ref _whereClause, value);
        }

        public bool IsExplorerVisible
        {
            get => _isExplorerVisible;
            set => SetProperty(ref _isExplorerVisible, value);
        }

        public bool IsLoadingTables
        {
            get => _isLoadingTables;
            set => SetProperty(ref _isLoadingTables, value);
        }

        public TableInfo? SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value) && value != null)
                {
                    TableName = value.FullName;
                }
            }
        }

        public ObservableCollection<string> Databases { get; }
        public ObservableCollection<TableInfo> Tables { get; }

        #endregion

        #region Commands

        public ICommand TestConnectionCommand { get; private set; } = null!;
        public ICommand ToggleExplorerCommand { get; private set; } = null!;
        public ICommand LoadTablesCommand { get; private set; } = null!;
        public ICommand SelectTableCommand { get; private set; } = null!;

        #endregion

        private void InitializeCommands()
        {
            TestConnectionCommand = new RelayCommand(TestConnection);
            ToggleExplorerCommand = new RelayCommand(OpenExplorerPopup);
            LoadTablesCommand = new RelayCommand(LoadTables, CanLoadTables);
            SelectTableCommand = new RelayCommand(SelectTable, CanSelectTable);
        }

        private void TestConnection()
        {
            try
            {
                var config = GetConfiguration();
                var schemaLoader = new DatabaseSchemaLoader();

                if (schemaLoader.TestConnection(config))
                {
                    _dialogService.ShowInfoDialog("Connection successful!", "Database Connection");
                }
                else
                {
                    _dialogService.ShowErrorDialog("Connection failed. Please check your settings.", "Database Connection");
                }
            }
            catch (System.Exception ex)
            {
                _dialogService.ShowErrorDialog($"Connection error: {ex.Message}", "Database Connection");
            }
        }

        private void OpenExplorerPopup()
        {
            try
            {
                var config = GetConfiguration();
                if (!_schemaLoader.TestConnection(config))
                {
                    _dialogService.ShowErrorDialog("Cannot connect to database. Please check your connection settings.", "Database Connection");
                    return;
                }

                LoadTables();

                var explorerWindow = new Views.DatabaseExplorerWindow(this)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                if (explorerWindow.ShowDialog() == true)
                {
                    if (SelectedTable != null)
                    {
                        TableName = SelectedTable.FullName;
                    }
                }
            }
            catch (System.Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error opening database explorer: {ex.Message}", "Database Explorer");
            }
        }

        private void SelectTable()
        {
            if (SelectedTable != null)
            {
                var window = System.Windows.Application.Current.Windows
                    .OfType<Views.DatabaseExplorerWindow>()
                    .FirstOrDefault(w => w.DataContext == this);

                if (window != null)
                {
                    window.DialogResult = true;
                    window.Close();
                }
            }
        }

        private bool CanSelectTable()
        {
            return SelectedTable != null;
        }

        private bool CanLoadTables()
        {
            return !string.IsNullOrWhiteSpace(Server) &&
                   !string.IsNullOrWhiteSpace(Database) &&
                   !IsLoadingTables;
        }

        private async void LoadTables()
        {
            if (IsLoadingTables) return;

            IsLoadingTables = true;
            Tables.Clear();

            try
            {
                var config = GetConfiguration();

                await System.Threading.Tasks.Task.Run(() =>
                {
                    var tables = _schemaLoader.LoadTables(config);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var table in tables)
                        {
                            Tables.Add(table);
                        }
                    });
                });
            }
            catch (System.Exception ex)
            {
                _dialogService.ShowErrorDialog($"Error loading tables: {ex.Message}", "Database Explorer");
            }
            finally
            {
                IsLoadingTables = false;
            }
        }

        public void SetConfiguration(DatabaseConfig? config)
        {
            if (config == null) return;

            Server = config.Server ?? "localhost";
            Database = config.Database ?? "IrisData";
            TableName = config.TableName ?? "DataTable";
            WhereClause = config.WhereClause ?? "";
        }

        public DatabaseConfig GetConfiguration()
        {
            return new DatabaseConfig
            {
                Server = Server,
                Database = Database,
                TableName = TableName,
                OutputTableName = "",
                WhereClause = WhereClause
            };
        }
    }
}