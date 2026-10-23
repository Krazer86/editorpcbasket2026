using Editor_PCBasket___Mou.Services;
using EpcbUtils;
using EpcbUtils.Dialogs;
using Ookii.Dialogs.Wpf;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using static Editor_PCBasket___Mou.Config.NavigationEnums;

namespace Editor_PCBasket___Mou.ViewModels.Settings
{
	public class SettingsViewModel : BindableBase, INavigationAware
	{
		private INavigationService _navigationService;
		private IDatabaseService _databaseService;
		public SettingsViewModel(INavigationService navigationService, IDatabaseService databaseService)
		{
			_navigationService = navigationService;
			_databaseService = databaseService;

			InitializeCommands();
			CheckPcbPath(PcbPath);
			CheckDatabase();
		}

		private void CheckDatabase()
		{
			PlayersStatus = string.Format("{0} jugadores", _databaseService.GetNumberOfPlayers());
			TeamsStatus = string.Format("{0} equipos", _databaseService.GetNumberOfTeams());
		}

		#region Bindings

		private string _pcbPath = Properties.Settings.Default.Path;
		public string PcbPath
		{
			get { return _pcbPath; }
			set
			{
				SetProperty(ref _pcbPath, value);
				Properties.Settings.Default.Path = value;
			}
		}

		private bool _managerOk;
		public bool ManagerOk
		{
			get { return _managerOk; }
			set { SetProperty(ref _managerOk, value); }
		}

		private string _managerStatus;
		public string ManagerStatus
		{
			get { return _managerStatus; }
			set { SetProperty(ref _managerStatus, value); }
		}

		private bool _eq22Ok;
		public bool Eq22Ok
		{
			get { return _eq22Ok; }
			set { SetProperty(ref _eq22Ok, value); }
		}

		private string _eq22Status;
		public string Eq22Status
		{
			get { return _eq22Status; }
			set { SetProperty(ref _eq22Status, value); }
		}

		private bool _dbcOk;
		public bool DbcOk
		{
			get { return _dbcOk; }
			set { SetProperty(ref _dbcOk, value); }
		}

		private string _dbcStatus;
		public string DbcStatus
		{
			get { return _dbcStatus; }
			set { SetProperty(ref _dbcStatus, value); }
		}

		private string _databaseStatus;
		public string DatabaseStatus
		{
			get { return _databaseStatus; }
			set { SetProperty(ref _databaseStatus, value); }
		}

		private string _playersStatus;
		public string PlayersStatus
		{
			get { return _playersStatus; }
			set { SetProperty(ref _playersStatus, value); }
		}

		private string _teamsStatus;
		public string TeamsStatus
		{
			get { return _teamsStatus; }
			set { SetProperty(ref _teamsStatus, value); }
		}

		private Visibility _iconsVisibility = Visibility.Hidden;
		public Visibility IconsVisibility
		{
			get { return _iconsVisibility; }
			set { SetProperty(ref _iconsVisibility, value); }
		}

		private bool _useEurNationality;
		public bool UseEurNationality
		{
			get { return _useEurNationality; }
			set { SetProperty(ref _useEurNationality, value); }
		}

		private bool _useCotNationality;
		public bool UseCotNationality
		{
			get { return _useCotNationality; }
			set { SetProperty(ref _useCotNationality, value); }
		}

		private bool _adjustBirthDates;
		public bool AdjustBirthDates
		{
			get { return _adjustBirthDates; }
			set { SetProperty(ref _adjustBirthDates, value); }
		}

		private int _maxPlayer;
		public int MaxPlayers
		{
			get { return _maxPlayer; }
			set { SetProperty(ref _maxPlayer, value); }
		}

		private void InitializeSettings()
		{
			PcbPath = Properties.Settings.Default.Path;
			UseCotNationality = Properties.Settings.Default.UseCotNationality;
			UseEurNationality = Properties.Settings.Default.UseEurNationality;
			AdjustBirthDates = Properties.Settings.Default.AdjustBirthDates;
			MaxPlayers = Properties.Settings.Default.MaxPlayers;
		}

		#endregion

		#region Commands

		public DelegateCommand AcceptCommand { get; private set; }
		public DelegateCommand CancelCommand { get; private set; }
		public DelegateCommand SelectPathCommand { get; private set; }
		public DelegateCommand GenerateDbCommand { get; private set; }
		public DelegateCommand CreateEmptyDbCommand { get; private set; }

		private void InitializeCommands()
		{
			AcceptCommand = new DelegateCommand(ExecuteAccept, CanExecuteAccept);
			CancelCommand = new DelegateCommand(ExecuteCancel);

			GenerateDbCommand = new DelegateCommand(ExecuteGenerateDb, CanExecuteGenerateDb);
			CreateEmptyDbCommand = new DelegateCommand(ExecuteCreateEmptyDb);

			SelectPathCommand = new DelegateCommand(ExecuteSelectPath);
		}

		private void ExecuteCreateEmptyDb()
		{
			_databaseService.Reset();
			CheckDatabase();
		}

		private bool CanExecuteGenerateDb()
		{
			return ManagerOk && DbcOk;
		}

		private async void ExecuteGenerateDb()
		{
			Application.Current.Dispatcher.Invoke(new Action(() =>
			{
				DialogHelper.ShowWaitingDialog("Generando BBDD, espere por favor...");
			}), DispatcherPriority.Send);

			try
			{
				await _databaseService.GenerateInitialDatabase();
				DialogHelper.CloseDialog();
				CheckDatabase();
			}
			catch (Exception ex)
			{
				DialogHelper.CloseDialog();
				LoggerUtils.LogException(ex);
			}
		}

		private void ExecuteSelectPath()
		{
			VistaFolderBrowserDialog folderBrowserDialog = new VistaFolderBrowserDialog();
			folderBrowserDialog.ShowDialog();
			var selectedFolder = folderBrowserDialog.SelectedPath;

			if (string.IsNullOrEmpty(selectedFolder)) return;

			CheckPcbPath(selectedFolder);

			PcbPath = selectedFolder;
			Properties.Settings.Default.Path = selectedFolder;
			AcceptCommand.RaiseCanExecuteChanged();
			GenerateDbCommand.RaiseCanExecuteChanged();
		}

		private void ExecuteAccept()
		{
			SaveSettings();
			_navigationService.NavigateTo(NavigationView.MainMenuView, this);
		}

		private void SaveSettings()
		{
			Properties.Settings.Default.Path = PcbPath;
			Properties.Settings.Default.UseCotNationality = UseCotNationality;
			Properties.Settings.Default.UseEurNationality = UseEurNationality;
			Properties.Settings.Default.AdjustBirthDates = AdjustBirthDates;
			Properties.Settings.Default.SettingsCompleted = true;
			Properties.Settings.Default.MaxPlayers = MaxPlayers;
			Properties.Settings.Default.Save();

			DbdatUtils.PcbPath = PcbPath;
			HexUtils.UseCotNationality = UseCotNationality;
			HexUtils.UseEurNationality = UseEurNationality;
			HtmlParserUtils.MaxPlayers = MaxPlayers;
		}

		private bool CanExecuteAccept()
		{
			var folderExists = Directory.Exists(PcbPath);
			return folderExists && ManagerOk;
		}

		private void ExecuteCancel()
		{
			Properties.Settings.Default.Path = DbdatUtils.PcbPath;
			Properties.Settings.Default.UseCotNationality = HexUtils.UseCotNationality;
			Properties.Settings.Default.UseEurNationality = HexUtils.UseEurNationality;

			_navigationService.GoBack(NavigationRegion.MainRegion);
		}

		#endregion

		#region Aux

		private bool CheckPcbPath(string selectedFolder)
		{
			IconsVisibility = string.IsNullOrEmpty(selectedFolder)
				? Visibility.Hidden
				: Visibility.Visible;

			if (string.IsNullOrEmpty(selectedFolder)) return false;

			CheckManager(selectedFolder);
			CheckEq022022(selectedFolder);
			CheckDbcFiles(selectedFolder);

			return ManagerOk;
		}

		private void CheckManager(string selectedFolder)
		{
			if (File.Exists(Path.Combine(selectedFolder, "manager.exe")))
			{
				ManagerOk = true;
				ManagerStatus = "MANAGER.EXE encontrado";
			}
			else
			{
				ManagerOk = false;
				ManagerStatus = "MANAGER.EXE no encontrado";
			}
		}

		private void CheckEq022022(string selectedFolder)
		{
			var eqPath = Path.Combine(selectedFolder, "DBDAT\\eq022022.pkf");
			if (File.Exists(eqPath))
			{
				Eq22Ok = true;
				Eq22Status = "EQ022022.pkf encontrado";
			}
			else
			{
				Eq22Ok = false;
				Eq22Status = "EQ022022.pkf no encontrado";
			}
		}

		private void CheckDbcFiles(string selectedFolder)
		{
			try
			{
				var dbcFiles = Directory.GetFiles(Path.Combine(selectedFolder, "DBDAT\\EQ022022"));
				var validDbc = dbcFiles.Count(dbc => string.Equals("dbc", dbc.Substring(dbc.Length - 3, 3).ToLower()));
				if (validDbc > 0)
				{
					DbcOk = true;
					DbcStatus = string.Format("Encontrados {0} equipos generados", validDbc);
				}
				else
				{
					DbcOk = false;
					DbcStatus = "No se han encontrado equipos generados";
				}
			}
			catch (Exception)
			{
				DbcStatus = "No se han encontrado equipos generados";
				DbcOk = false;
			}
		}

		public void OnNavigatedTo(NavigationContext navigationContext)
		{
			InitializeSettings();
		}

		public bool IsNavigationTarget(NavigationContext navigationContext)
		{
			return true;
		}

		public void OnNavigatedFrom(NavigationContext navigationContext)
		{
		}

		#endregion
	}
}
