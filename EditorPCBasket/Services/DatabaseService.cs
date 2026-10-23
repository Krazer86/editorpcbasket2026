using EpcbModel;
using EpcbUtils;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace Editor_PCBasket___Mou.Services
{
	public class DatabaseService : IDatabaseService
	{
		private readonly PcBasketDbLocator _locator;
		private readonly object _databaseLock = new object();

		public ObservableCollection<Equipo> TeamsList { get; private set; }

		public DatabaseService()
		{
			_locator = new PcBasketDbLocator();
			TeamsList = new ObservableCollection<Equipo>();
			ReplaceTeamsList(GetTeams());
		}

		#region DB Creation

		public async Task GenerateInitialDatabase()
		{
			var teams = await Task.Run(ReadInitialTeams);
			if (teams.Count == 0)
			{
				throw new InvalidOperationException("No se han encontrado equipos válidos para importar.");
			}

			List<Equipo> importedTeams;
			lock (_databaseLock)
			{
				try
				{
					using (var transaction = _locator.Current.Database.BeginTransaction())
					{
						_locator.Current.Jugadores.RemoveRange(_locator.Current.Jugadores.ToList());
						_locator.Current.Equipos.RemoveRange(_locator.Current.Equipos.ToList());
						_locator.Current.SaveChanges();

						_locator.Current.Equipos.AddRange(teams);
						_locator.Current.SaveChanges();
						transaction.Commit();
					}
				}
				catch
				{
					_locator.Reset();
					throw;
				}

				importedTeams = LoadTeams();
			}

			ReplaceTeamsList(importedTeams);
		}

		private List<Equipo> ReadInitialTeams()
		{
			var equiposList = new List<Equipo>();
			var directory = Path.Combine(Properties.Settings.Default.Path, "DBDAT", "EQ022022");

			foreach (var dbc in Directory.GetFiles(directory, "*.dbc"))
			{
				using (var eqFile = File.OpenRead(dbc))
				{
					var equipo = HexUtils.ReadEquipoBytes(eqFile);
					if (equipo.Puntero == -5) continue;
					equiposList.Add(equipo);
				}
			}

			return equiposList;
		}

		public void Reset()
		{
			lock (_databaseLock)
			{
				using (var transaction = _locator.Current.Database.BeginTransaction())
				{
					_locator.Current.Jugadores.RemoveRange(_locator.Current.Jugadores.ToList());
					_locator.Current.Equipos.RemoveRange(_locator.Current.Equipos.ToList());
					_locator.Current.SaveChanges();
					transaction.Commit();
				}

				_locator.Reset();
			}

			UpdateTeamsList(new List<Equipo>());
		}

		#endregion

		#region Teams

		public Equipo GetTeam(int puntero)
		{
			lock (_databaseLock)
			{
				return _locator.Current.GetEquipo(puntero);
			}
		}

		public Equipo GetEquipoOfJugador(Jugador jugador)
		{
			lock (_databaseLock)
			{
				return _locator.Current.GetEquipoOfJugador(jugador);
			}
		}

		public IEnumerable<Equipo> GetTeams()
		{
			lock (_databaseLock)
			{
				return LoadTeams();
			}
		}

		private List<Equipo> LoadTeams()
		{
			return _locator.Current.Equipos.Include("Plantilla")
				.Where(e => e.Puntero > 0)
				.OrderBy(e => e.Puntero)
				.ToList();
		}

		public void DeleteTeam(Equipo team)
		{
			try
			{
				lock (_databaseLock)
				{
					_locator.Current.Equipos.Remove(team);
					_locator.Current.SaveChanges();
				}
			}
			catch (Exception ex)
			{
				LoggerUtils.LogException(ex);
				return;
			}

			UpdateTeamsList(() => TeamsList.Remove(team));
		}

		public void AddTeam(Equipo team)
		{
			var added = false;
			lock (_databaseLock)
			{
				if (!_locator.Current.Equipos.Any(p => p.Puntero == team.Puntero))
				{
					_locator.Current.Equipos.Add(team);
					added = true;
				}

				_locator.Current.SaveChanges();
			}

			if (added)
			{
				UpdateTeamsList(() => TeamsList.Add(team));
			}
		}

		public int GetNumberOfTeams()
		{
			lock (_databaseLock)
			{
				return _locator.Current.Equipos.Count();
			}
		}

		#endregion

		#region Players

		public int GetNumberOfPlayers()
		{
			lock (_databaseLock)
			{
				return _locator.Current.Jugadores.Count();
			}
		}

		#endregion

		private void ReplaceTeamsList(IEnumerable<Equipo> teams)
		{
			var orderedTeams = teams.OrderBy(team => team.Puntero).ToList();
			UpdateTeamsList(() =>
			{
				TeamsList.Clear();
				foreach (var team in orderedTeams)
				{
					TeamsList.Add(team);
				}
			});
		}

		private void UpdateTeamsList(Action update)
		{
			var dispatcher = Application.Current.Dispatcher;
			if (dispatcher.CheckAccess())
			{
				update();
			}
			else
			{
				dispatcher.Invoke(update);
			}
		}

	}
}
