using Editor_PCBasket___Mou.Services;
using EpcbModel;
using EpcbUtils;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Windows.Media.Imaging;

namespace Editor_PCBasket___Mou.ViewModels
{
	public class JugadorViewModel : BindableBase
	{
		public JugadorViewModel(IDatabaseService databaseService)
		{
			_jugador = new Jugador();
			MediaDeseada = 70;

			_databaseService = databaseService;
		}

		public bool ApplyChanges = false;

		private string _nombreEquipo;

		public string NombreEquipo
		{
			get { return _nombreEquipo; }
			set { SetProperty(ref _nombreEquipo, value); }
		}

		private BitmapImage _escudoImageSource;

		public BitmapImage EscudoImageSource
		{
			get { return _escudoImageSource; }
			set { SetProperty(ref _escudoImageSource, value); }
		}

		private Jugador _jugador;
		public Jugador Jugador
		{
			get { return _jugador; }
			set
			{
				if(!SetProperty(ref _jugador, value)) return;

				var equipo = _databaseService.GetEquipoOfJugador(Jugador);

				if (equipo == null) return;

				NombreEquipo = equipo.NombreLargo;
				EscudoImageSource = DbdatUtils.GetNanoesc(equipo.Puntero);
			}
		}

		private BitmapImage _banderaImageSource;
		public BitmapImage BanderaImageSource
		{
			get { return _banderaImageSource; }
			set { SetProperty(ref _banderaImageSource, value); }
		}

		private BitmapImage _fotoImageSource;
		public BitmapImage FotoImageSource
		{
			get { return _fotoImageSource; }
			set { SetProperty(ref _fotoImageSource, value); }
		}

		private AbilitiesViewModel _abilities = new AbilitiesViewModel();
		public AbilitiesViewModel Abilities
		{
			get { return _abilities; }
			set { SetProperty(ref _abilities, value); }
		}	

		#region Generador medias 

		private DelegateCommand _generarMediaCommand;

		public DelegateCommand GenerarMediaCommand
		{
			get
			{
				if (_generarMediaCommand == null)
				{
					_generarMediaCommand = new DelegateCommand(ExecuteGenerarMedia, CanExecuteGenerarMedia);
				}
				return _generarMediaCommand;
			}
		}

		private void ExecuteGenerarMedia()
		{
			RatingsUtils.GeneratePlayerRating(Jugador.Medias, Abilities, MediaDeseada, Jugador.Demarcacion);
		}

		private bool CanExecuteGenerarMedia()
		{
			return true;
		}

		private int _mediaDeseada;
		public int MediaDeseada
		{
			get { return _mediaDeseada; }
			set
			{
				value = Math.Min(95, Math.Max(11, value));
				SetProperty(ref _mediaDeseada, value);
				GenerarMediaCommand.RaiseCanExecuteChanged();
			}
		}

		private readonly IDatabaseService _databaseService;

		#endregion
	}
}
