using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;

namespace EpcbModel
{
	public class PcBasketContext : DbContext
	{
		public PcBasketContext() : base()
		{
			//Configuration.LazyLoadingEnabled = false;
		}

		public DbSet<Equipo> Equipos { get; set; }
		public DbSet<Jugador> Jugadores { get; set; }

		public Equipo GetEquipo(int puntero)
		{
			return Equipos.Where(e => e.Puntero == puntero).Include("Plantilla").FirstOrDefault();
		}

		public Equipo GetEquipoOfJugador(Jugador jug)
		{			
			return Equipos.Where(e => e.Plantilla.Any(j => j.Puntero == jug.Puntero)).FirstOrDefault();
		}

		public ObservableCollection<Jugador> GetPlantillaOfEquipo(int punteroEquipo)
		{
			var plantilla = Equipos
				.Where(e => e.EquipoId == punteroEquipo)
				.SelectMany(e => e.Plantilla)
				.ToList();

			return new ObservableCollection<Jugador>(plantilla);
		}
	}
}
