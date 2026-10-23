using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace EpcbModel
{
	[Table("Equipos")]
	public class Equipo : BindableBase
	{
		public Equipo()
		{
			Plantilla = new ObservableCollection<Jugador>();
		}

		public int EquipoId { get; set; }

		private int _puntero;
		public int Puntero
		{
			get { return _puntero; }
			set
			{
				if (!SetProperty(ref _puntero, value)) return;
			}
		}

		private string _nombreCorto;

		public string NombreCorto
		{
			get { return _nombreCorto; }
			set { SetProperty(ref _nombreCorto, value); }
		}

		private string _nombreLargo;

		public string NombreLargo
		{
			get { return _nombreLargo; }
			set { SetProperty(ref _nombreLargo, value); }
		}

		private string _pabellon;

		public string Pabellon
		{
			get { return _pabellon; }
			set { SetProperty(ref _pabellon, value); }
		}

		private int _aforo;

		public int Aforo
		{
			get { return _aforo; }
			set { SetProperty(ref _aforo, value); }
		}

		private int _presupuesto;

		public int Presupuesto
		{
			get { return _presupuesto; }
			set { SetProperty(ref _presupuesto, value); }
		}

		private string _entrenador;

		public string Entrenador
		{
			get { return _entrenador; }
			set { SetProperty(ref _entrenador, value); }
		}

		private Pais _pais;

		public Pais Pais
		{
			get { return _pais; }
			set { SetProperty(ref _pais, value); }
		}

		public void ReloadMedias()
		{
			RaisePropertyChanged("MediaPlantilla");
			RaisePropertyChanged("MediaQuinteto");
		}
		public double MediaQuinteto
		{
			get { return Plantilla == null || Plantilla.Count == 0 ? 0 : Math.Round(Plantilla.Sum(j => j.Medias.MediaQuinteto) / (double)Plantilla.Count, 2); }
		}

		public double MediaPlantilla
		{
			get { return Plantilla == null || Plantilla.Count == 0 ? 0 : Math.Round(Plantilla.Sum(j => j.Medias.MediaPlantilla) / (double)Plantilla.Count, 2); }
		}

		private ObservableCollection<Jugador> _plantilla;

		public ObservableCollection<Jugador> Plantilla
		{
			get { return _plantilla; }
			set { SetProperty(ref _plantilla, value); }
		}
	}
}
