using EpcbModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Editor_PCBasket___Mou.Services
{
	public interface IDatabaseService
	{
		ObservableCollection<Equipo> TeamsList { get; }

		Equipo GetTeam(int puntero);
		Equipo GetEquipoOfJugador(Jugador jugador);
		IEnumerable<Equipo> GetTeams();
		void AddTeam(Equipo team);
		void DeleteTeam(Equipo team);
		int GetNumberOfTeams();


		int GetNumberOfPlayers();

		Task GenerateInitialDatabase();
		void Reset();
	}
}
