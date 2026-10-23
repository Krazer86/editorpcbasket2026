using EpcbUtils;
using EpcbUtils.Dialogs;
using Prism.Commands;
using System.Threading;
using System.Windows.Threading;
using System;
using System.Windows;
using Editor_PCBasket___Mou.Views;

namespace Editor_PCBasket___Mou.ViewModels.Dialogs
{
	public class ProballersImportViewModel : BaseDialogViewModel
	{
		public ProballersImportViewModel(DialogOptions options)
			: base(options)
		{
			SecondButtonText = options.SecondButtonText;
			InitializeCommands();
		}
		private void InitializeCommands()
		{
		}

		#region Commands

		private DelegateCommand _acceptCommand;
		public DelegateCommand AcceptCommand =>
			_acceptCommand ?? (_acceptCommand = new DelegateCommand(ExecuteAcceptCommand, CanExecuteAcceptCommand));

		void ExecuteAcceptCommand()
		{
			Application.Current.Dispatcher.Invoke(new Action(() =>
			{
				DialogHelper.CloseDialog();
				DialogHelper.ShowWaitingDialog("Generando equipo, espere por favor...");
			}), DispatcherPriority.Send);

			var thread = new Thread(GenerateTeamThread);
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
		}

		private void GenerateTeamThread()
		{
			try
			{
				var team = GenerateRatings
					? HtmlParserUtils.GetEquipoFromHtml(Url, TeamPointer, PlayerPointer, GeneratePhotos, GenerateBadges, DesiredRating)
					: HtmlParserUtils.GetEquipoFromHtml(Url, TeamPointer, PlayerPointer, GeneratePhotos, GenerateBadges);

				Application.Current.Dispatcher.Invoke(new Action(() =>
				{
					var teamWindow = new EquipoView(team);
					teamWindow.Show();

					DialogHelper.CloseDialog();
				}));

			}
			catch (Exception ex)
			{
				DialogHelper.CloseDialog();
				LoggerUtils.LogException(ex);
				DialogHelper.ShowSimpleDialog(
					"No se pudo importar el equipo desde Proballers: " + ex.Message,
					title: "Error de importación");
			}
		}

		bool CanExecuteAcceptCommand()
		{
			return !string.IsNullOrEmpty(Url) && PlayerPointer > 0 && TeamPointer > 0;
		}

		#endregion

		#region Bindings

		private string _secondButtonText;
		public string SecondButtonText
		{
			get { return _secondButtonText; }
			set { SetProperty(ref _secondButtonText, value); }
		}

		private string _url;
		public string Url
		{
			get { return _url; }
			set
			{
				SetProperty(ref _url, value);
				AcceptCommand.RaiseCanExecuteChanged();
			}
		}

		private int _teamPointer;
		public int TeamPointer
		{
			get { return _teamPointer; }
			set
			{
				SetProperty(ref _teamPointer, value);
				AcceptCommand.RaiseCanExecuteChanged();
			}
		}

		private int _playerPointer;
		public int PlayerPointer
		{
			get { return _playerPointer; }
			set
			{
				SetProperty(ref _playerPointer, value);
				AcceptCommand.RaiseCanExecuteChanged();
			}
		}

		private bool _generateBadges = true;
		public bool GenerateBadges
		{
			get { return _generateBadges; }
			set { SetProperty(ref _generateBadges, value); }
		}

		private bool _generatePhotos = true;
		public bool GeneratePhotos
		{
			get { return _generatePhotos; }
			set { SetProperty(ref _generatePhotos, value); }
		}

		private bool _generateRatings;
		public bool GenerateRatings
		{
			get { return _generateRatings; }
			set { SetProperty(ref _generateRatings, value); }
		}

		private int _desiredRating;
		public int DesiredRating
		{
			get { return _desiredRating; }
			set { SetProperty(ref _desiredRating, value); }
		}

		#endregion
	}
}
