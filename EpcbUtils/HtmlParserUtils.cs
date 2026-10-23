using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using EpcbModel;
using HtmlAgilityPack;

namespace EpcbUtils
{
	public static class HtmlParserUtils
	{
		private static readonly object RandomLock = new object();
		private static readonly Random Random = new Random();
		private static readonly object DorsalLock = new object();
		private static readonly List<int> Dorsales = Enumerable.Range(0, 100).OrderBy(g => Guid.NewGuid()).ToList();
		private static int _i = 0;

		public static int MaxPlayers { get; set; }

		public static Equipo GetEquipoFromHtml(string url, int pEquipo, int pJugador, bool generatePhotos, bool generateEscudos, int desiredRating = -1)
		{
			if (MaxPlayers <= 0)
			{
				throw new InvalidOperationException("El número máximo de jugadores debe ser mayor que cero.");
			}
			if (desiredRating > 0 && (desiredRating < 11 || desiredRating > 95))
			{
				throw new ArgumentOutOfRangeException("desiredRating", "La media deseada debe estar entre 11 y 95.");
			}

			url = PrepareUrl(url);

			var web = new HtmlWeb();
			var doc = web.Load(url);
			if (doc == null || doc.DocumentNode == null)
			{
				throw new InvalidOperationException("Proballers no devolvió contenido HTML para la URL indicada.");
			}

			var equipoRes = new Equipo
			{
				Puntero = pEquipo
			};

			LoggerUtils.LogString(string.Format("[PARSER] Generating team from Proballers with url {0}", url));

			var identityImage = FindIdentityImage(doc);
			var nombreEquipo = GetAttribute(identityImage, "alt");
			if (string.IsNullOrWhiteSpace(nombreEquipo))
			{
				nombreEquipo = GetText(doc.DocumentNode.SelectSingleNode("//h1"));
			}
			if (string.IsNullOrWhiteSpace(nombreEquipo))
			{
				throw new InvalidOperationException("No se encontró el nombre del equipo en la página de Proballers.");
			}

			if (generateEscudos)
			{
				var escudo = GetAttribute(identityImage, "src");
				if (!string.IsNullOrWhiteSpace(escudo))
				{
					PhotoUtils.CreateEscudos(new Uri(new Uri(url), escudo).AbsoluteUri, pEquipo);
				}
			}

			equipoRes.NombreCorto = nombreEquipo;
			equipoRes.NombreLargo = nombreEquipo;

			var countryNode = doc.DocumentNode.SelectSingleNode("//*[contains(concat(' ', normalize-space(@class), ' '), ' breadcrumb-link ')]");
			var hPais = GetAttribute(countryNode, "title");
			equipoRes.Pais = CountryToPais(string.IsNullOrWhiteSpace(hPais) ? GetText(countryNode) : hPais);

			var punteroJug = pJugador;
			var teamStatistics = new Dictionary<int, PlayerStatistics>();
			var rosterRows = doc.DocumentNode.SelectNodes("//table//tbody/tr");
			if (rosterRows == null || rosterRows.Count == 0)
			{
				throw new InvalidOperationException("No se encontró ninguna plantilla de jugadores en la página del equipo.");
			}

			foreach (var jugador in rosterRows)
			{
				var playerLink = jugador.SelectSingleNode(".//a[contains(@href, '/player/')]");
				if (playerLink == null) continue;

				var playerUrl = GetAttribute(playerLink, "href");
				var nombre = GetText(playerLink);
				if (string.IsNullOrWhiteSpace(nombre))
				{
					nombre = GetAttribute(playerLink.SelectSingleNode(".//img"), "alt");
				}
				nombre = NormalizeText(nombre);
				if (string.IsNullOrWhiteSpace(nombre)) continue;

				var nombreCompleto = NormalizeName(nombre);
				if (equipoRes.Plantilla.Any(j => string.Equals(j.NombreLargo, nombreCompleto, StringComparison.OrdinalIgnoreCase)))
				{
					continue;
				}

				try
				{
					var player = GetJugadorFromUrl(playerUrl, punteroJug, generatePhotos);
					if (string.IsNullOrWhiteSpace(player.NombreLargo)) continue;
					if (desiredRating > 0)
					{
						var statistics = TryReadStatistics(jugador);
						if (statistics != null)
						{
							teamStatistics[punteroJug] = statistics;
						}
					}
					equipoRes.Plantilla.Add(player);
					punteroJug++;
				}
				catch (Exception ex)
				{
					LoggerUtils.LogString(string.Format("[PARSER] No se pudo importar el jugador '{0}': {1}", nombre, ex.Message));
				}

				if (MaxPlayers > 0 && equipoRes.Plantilla.Count >= MaxPlayers) break;
			}

			if (equipoRes.Plantilla.Count == 0)
			{
				throw new InvalidOperationException("No se pudo importar ningún jugador. Puede que Proballers haya cambiado la página o que esta no tenga plantilla.");
			}

			if (desiredRating > 0)
			{
				if (teamStatistics.Count == 0)
				{
					throw new InvalidOperationException("No se encontraron estadísticas compatibles para generar las medias automáticamente.");
				}
				RatingsUtils.GenerateTeamRatings(equipoRes.Plantilla, teamStatistics, desiredRating);
			}

			LoggerUtils.LogString(string.Format("[PARSER] Generated team {0} ({1})", equipoRes.NombreCorto, equipoRes.Puntero));

			return equipoRes;
		}

		private static HtmlNode FindIdentityImage(HtmlDocument document)
		{
			var identity = document.DocumentNode.SelectSingleNode(
				"//*[contains(concat(' ', normalize-space(@class), ' '), ' identity__picture__box ')]");
			if (identity == null) return null;

			return identity.Name.Equals("img", StringComparison.OrdinalIgnoreCase)
				? identity
				: identity.SelectSingleNode(".//img");
		}

		private static string GetAttribute(HtmlNode node, string name)
		{
			return node == null || node.Attributes[name] == null
				? null
				: HtmlEntity.DeEntitize(node.Attributes[name].Value).Trim();
		}

		private static string GetText(HtmlNode node)
		{
			return node == null ? string.Empty : NormalizeText(node.InnerText);
		}

		private static string NormalizeText(string text)
		{
			return Regex.Replace(HtmlEntity.DeEntitize(text ?? string.Empty), @"\s+", " ").Trim();
		}

		private static string NormalizeName(string name)
		{
			return NormalizeText(name);
		}

		private static PlayerStatistics TryReadStatistics(HtmlNode row)
		{
			var cells = row.SelectNodes("./td");
			if (cells == null || cells.Count == 0) return null;

			var table = row.ParentNode == null ? null : row.ParentNode.ParentNode;
			var header = table == null ? null : table.SelectSingleNode("./thead/tr[last()]");
			if (header != null)
			{
				var headers = header.SelectNodes("./th|./td");
				if (headers == null || headers.Count != cells.Count) return null;

				var indexes = new[]
				{
					FindStatisticColumn(headers, "pts", "points"),
					FindStatisticColumn(headers, "reb", "rebs", "rebounds"),
					FindStatisticColumn(headers, "ast", "assists"),
					FindStatisticColumn(headers, "min", "minutes"),
					FindStatisticColumn(headers, "3p", "3pt", "threepoint", "3points"),
					FindStatisticColumn(headers, "fg", "fieldgoal"),
					FindStatisticColumn(headers, "ft", "freethrow"),
					FindStatisticColumn(headers, "orb", "offreb", "offensiverebound"),
					FindStatisticColumn(headers, "stl", "steals"),
					FindStatisticColumn(headers, "to", "turnover"),
					FindStatisticColumn(headers, "blk", "blocks"),
					FindStatisticColumn(headers, "pf", "fouls"),
					FindStatisticColumn(headers, "eff", "efficiency"),
					FindStatisticColumn(headers, "highpts", "maxpts", "ptsmax"),
					FindStatisticColumn(headers, "highreb", "maxreb", "rebmax"),
					FindStatisticColumn(headers, "highast", "maxast", "astmax")
				};
				if (indexes.Any(index => index < 0 || index >= cells.Count)) return null;

				return CreateStatistics(cells, indexes);
			}

			if (cells.Count < 24) return null;
			return CreateStatistics(cells, new[] { 3, 4, 5, 8, 9, 10, 11, 12, 15, 16, 17, 18, 20, 21, 22, 23 });
		}

		private static int FindStatisticColumn(HtmlNodeCollection headers, params string[] aliases)
		{
			for (var index = 0; index < headers.Count; index++)
			{
				var heading = NormalizeHeader(GetText(headers[index]));
				if (aliases.Contains(heading)) return index;
			}

			for (var index = 0; index < headers.Count; index++)
			{
				var heading = NormalizeHeader(GetText(headers[index]));
				if (aliases.Any(alias => heading == alias ||
					(alias.Length > 2 && (heading.StartsWith(alias) || heading.EndsWith(alias)))))
				{
					return index;
				}
			}
			return -1;
		}

		private static string NormalizeHeader(string header)
		{
			return Regex.Replace(header.ToLowerInvariant(), @"[^a-z0-9]", string.Empty);
		}

		private static PlayerStatistics CreateStatistics(HtmlNodeCollection cells, int[] indexes)
		{
			return new PlayerStatistics
			{
				Points = ReadStatistic(cells, indexes[0], 0),
				Rebounds = ReadStatistic(cells, indexes[1], 0),
				Assists = ReadStatistic(cells, indexes[2], 0),
				Minutes = ReadStatistic(cells, indexes[3], 0),
				Pct3Pointers = ReadStatistic(cells, indexes[4], 0, true),
				PctFieldGoalds = ReadStatistic(cells, indexes[5], 35, true),
				PctFreeThrows = ReadStatistic(cells, indexes[6], 60, true),
				OffensiveRebounds = ReadStatistic(cells, indexes[7], 0),
				Steals = ReadStatistic(cells, indexes[8], 0),
				TurnOvers = ReadStatistic(cells, indexes[9], 0),
				Blocks = ReadStatistic(cells, indexes[10], 0),
				Fouls = ReadStatistic(cells, indexes[11], 0),
				Efficiency = ReadStatistic(cells, indexes[12], 0),
				HighestPoints = ReadStatistic(cells, indexes[13], 0),
				HighestRebounds = ReadStatistic(cells, indexes[14], 0),
				HighestAssists = ReadStatistic(cells, indexes[15], 0)
			};
		}

		private static double ReadStatistic(HtmlNodeCollection cells, int index, double defaultValue, bool percentage = false)
		{
			var value = GetText(cells[index]);
			if (percentage) value = value.Replace("%", string.Empty);
			if (string.IsNullOrWhiteSpace(value) || value == "-") return defaultValue;

			double result;
			return double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result)
				? result / (percentage ? 100 : 1)
				: defaultValue;
		}

		private static Pais FindNationality(string biography)
		{
			var normalizedBiography = NormalizeHeader(biography);
			foreach (var country in CountryCodes.OrderByDescending(item => item.Key.Length))
			{
				if (normalizedBiography.Contains(NormalizeHeader(country.Key)))
				{
					return (Pais)country.Value;
				}
			}

			return Pais.LUXEMBURGO;
		}

		private static string PrepareUrl(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
			{
				throw new ArgumentException("Introduce una URL de equipo de Proballers.", "url");
			}

			Uri uri;
			if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
			{
				var normalizedUrl = url.Trim();
				if (normalizedUrl.StartsWith("www.proballers.com/", StringComparison.OrdinalIgnoreCase) ||
					normalizedUrl.StartsWith("proballers.com/", StringComparison.OrdinalIgnoreCase))
				{
					normalizedUrl = "https://" + normalizedUrl;
				}
				else
				{
					normalizedUrl = "https://www.proballers.com/" + normalizedUrl.TrimStart('/');
				}

				if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out uri))
				{
					throw new ArgumentException("La URL de Proballers no es válida.", "url");
				}
			}

			if (uri.Scheme != Uri.UriSchemeHttps ||
				!(string.Equals(uri.Host, "www.proballers.com", StringComparison.OrdinalIgnoreCase) ||
				  string.Equals(uri.Host, "proballers.com", StringComparison.OrdinalIgnoreCase)))
			{
				throw new ArgumentException("Solo se admiten páginas HTTPS de proballers.com.", "url");
			}

			var builder = new UriBuilder(uri);
			if (builder.Path.Contains("/es/"))
			{
				builder.Path = builder.Path.Replace("/es/", "/").Replace("baloncesto", "basketball").Replace("equipo", "team");
			}
			else if (builder.Path.Contains("/it/"))
			{
				builder.Path = builder.Path.Replace("/it/", "/").Replace("pallacanestro", "basketball").Replace("squadra", "team");
			}
			else if (builder.Path.Contains("/fr/"))
			{
				builder.Path = builder.Path.Replace("/fr/", "/").Replace("equipe", "team");
			}

			return builder.Uri.AbsoluteUri;
		}

		private static Jugador GetJugadorFromUrl(string url, int puntero, bool generatePhotos)
		{
			var profileUri = new Uri(new Uri("https://www.proballers.com/"), url);
			if (profileUri.Scheme != Uri.UriSchemeHttps ||
				!(string.Equals(profileUri.Host, "www.proballers.com", StringComparison.OrdinalIgnoreCase) ||
				  string.Equals(profileUri.Host, "proballers.com", StringComparison.OrdinalIgnoreCase)))
			{
				throw new InvalidOperationException("El enlace del jugador no pertenece a proballers.com.");
			}
			var web = new HtmlWeb();
			var doc = web.Load(profileUri.AbsoluteUri);
			if (doc == null || doc.DocumentNode == null)
			{
				throw new InvalidOperationException("Proballers no devolvió el perfil del jugador.");
			}

			var nombre = GetAttribute(FindIdentityImage(doc), "alt");
			if (string.IsNullOrWhiteSpace(nombre))
			{
				nombre = GetText(doc.DocumentNode.SelectSingleNode("//h1"));
			}
			nombre = NormalizeText(nombre);
			if (string.IsNullOrWhiteSpace(nombre))
			{
				throw new InvalidOperationException("No se encontró el nombre del jugador en su perfil.");
			}

			var jugPlantilla = new Jugador() { Puntero = puntero };
			var nombreCompleto = NormalizeName(nombre);
			var nameParts = nombreCompleto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			var name = nameParts[0];
			var surname = nameParts.Length > 1
				? string.Join(" ", nameParts.Skip(1)).ToUpperInvariant()
				: nameParts[0].ToUpperInvariant();

			jugPlantilla.NombreCorto = surname;
			jugPlantilla.NombreLargo = string.Format("{0} {1}", name, surname);

			var playerProfil = doc.DocumentNode.SelectSingleNode("//*[contains(concat(' ', normalize-space(@class), ' '), ' identity__stats__profil ')]");
			var biographyNode = doc.DocumentNode.SelectSingleNode("//*[contains(concat(' ', normalize-space(@class), ' '), ' banner__biography__content ')]");
			var profileText = NormalizeText(GetText(playerProfil) + " " + GetText(biographyNode));

			var birthMatch = Regex.Match(profileText, @"\b(?:19|20)\d{2}\b");
			if (birthMatch.Success && int.TryParse(birthMatch.Value, out var birthYear))
			{
				jugPlantilla.AnoNacimiento = birthYear;
			}
			else
			{
				jugPlantilla.AnoNacimiento = DateTime.Now.Year - 25;
			}

			var heightMatch = Regex.Match(profileText, @"(?<!\d)(?<meters>[12][.,]\d{1,2})\s*m\b|(?<!\d)(?<centimeters>1\d{2}|2\d{2})\s*cm\b", RegexOptions.IgnoreCase);
			if (heightMatch.Success)
			{
				var centimeters = heightMatch.Groups["centimeters"].Success
					? int.Parse(heightMatch.Groups["centimeters"].Value, CultureInfo.InvariantCulture)
					: (int)Math.Round(double.Parse(heightMatch.Groups["meters"].Value.Replace(',', '.'), CultureInfo.InvariantCulture) * 100);
				jugPlantilla.Altura = centimeters;
				lock (RandomLock)
				{
					jugPlantilla.Peso = centimeters - 100 + Random.Next(-5, 5);
				}
			}

			jugPlantilla.Nacionalidad = FindNationality(profileText);
			jugPlantilla.ColorPiel = IsCotonou(jugPlantilla.Nacionalidad);
			jugPlantilla.Demarcacion = SearchPosition(profileText);

			if (generatePhotos)
			{
				var photoUrl = GetAttribute(FindIdentityImage(doc), "src");
				if (!string.IsNullOrWhiteSpace(photoUrl))
				{
					PhotoUtils.CreatePhotos(new Uri(profileUri, photoUrl).AbsoluteUri, jugPlantilla);
				}
			}

			lock (DorsalLock)
			{
				jugPlantilla.Dorsal = Dorsales[_i];
				_i = (_i + 1) % Dorsales.Count;
			}

			return jugPlantilla;
		}

		private static int SearchPosition(string info)
		{
			info = info.ToLowerInvariant();
			if (info.Contains("point guard"))
			{
				return 0;
			}
			if (info.Contains("shooting guard"))
			{
				return 1;
			}
			if (info.Contains("center"))
			{
				return 4;
			}
			if (info.Contains("power forward"))
			{
				return 3;
			}

			return 2;
		}

		// Deprecated: Proballers ha cambiado la estructura interna
		private static int StringToPosition(string pos)
		{
			var str = pos.Substring(0, pos.IndexOf(" "));

			switch (str)
			{
				case ("PG"):
					return 0;
				case ("SG"):
					return 1;
				case ("SF"):
					return 2;
				case ("PF"):
					return 3;
				case ("C"):
					return 4;
				default:
					return 0;
			}
		}

		private static Pais CountryToPais(string country)
		{
			if (string.IsNullOrWhiteSpace(country)) return Pais.LUXEMBURGO;

			foreach (var entry in CountryCodes)
			{
				if (string.Equals(entry.Key, country.Trim(), StringComparison.OrdinalIgnoreCase))
				{
					return (Pais)entry.Value;
				}
			}
			return Pais.LUXEMBURGO;
		}

		private static Pais NationalityToPais(string country)
		{
			int pais;
			NationalityCodes.TryGetValue(country, out pais);

			if (pais == 0)
			{
				CountryCodes.TryGetValue(country, out pais);
			}

			return pais == 0 ? Pais.LUXEMBURGO : (Pais)pais;
		}

		public static bool IsCotonou(Pais pais)
		{
			switch (pais)
			{
				case (Pais.ANGOLA):
				case (Pais.BENIN):
				case (Pais.BOTSWANA):
				case (Pais.BURKINA_FASO):
				case (Pais.BURUNDI):
				case (Pais.CABO_VERDE):
				case (Pais.CAMERÚN):
				case (Pais.CHAD):
				case (Pais.COMOROS):
				case (Pais.CONGO):
				case (Pais.COSTA_DE_MARFIL):
				case (Pais.DJIBOUTI):
				case (Pais.ERITREA):
				case (Pais.ETIOPIA):
				case (Pais.GABON):
				case (Pais.GAMBIA):
				case (Pais.GHANA):
				case (Pais.GUINEA_ECUATORIANA):
				case (Pais.GUINEA):
				case (Pais.GUINEA_BISSAU):
				case (Pais.ISLAS_MAURICIOS):
				case (Pais.KENIA):
				case (Pais.LESOTHO):
				case (Pais.LIBERIA):
				case (Pais.MADAGASCAR):
				case (Pais.MALAWI):
				case (Pais.MALI):
				case (Pais.MAURITANIA):
				case (Pais.MOZAMBIQUE):
				case (Pais.NAMIBIA):
				case (Pais.NIGER):
				case (Pais.NIGERIA):
				case (Pais.REPUBLICA_CENTRO):
				case (Pais.RUANDA):
				case (Pais.SANTO_TOME_Y_PR):
				case (Pais.SENEGAL):
				case (Pais.SEYCHELLES):
				case (Pais.SIERRA_LEONA):
				case (Pais.SOMALIA):
				case (Pais.SUAZILANDIA):
				case (Pais.SUDAFRICA):
				case (Pais.SUDAN):
				case (Pais.TANZANIA):
				case (Pais.TOGO):
				case (Pais.UGANDA):
				case (Pais.ZAMBIA):
				case (Pais.ZIMBABWE):

				case (Pais.ANTIGUA_Y_BARBU):
				case (Pais.LAS_BAHAMAS):
				case (Pais.BARBADOS):
				case (Pais.BELIZE):
				case (Pais.DOMINICA):
				case (Pais.GRANADA):
				case (Pais.HAITI):
				case (Pais.JAMAICA):
				case (Pais.REPUBLICA_DOMINICANA):
				case (Pais.SAN_KITTS_Y_NEVIS):
				case (Pais.SAN_VICENTE):
				case (Pais.SANTA_LUCIA):
				case (Pais.SURINAM):
				case (Pais.TRINIDAD_Y_TOBAGO):

				case (Pais.FIJI):
				case (Pais.KIRIBATI):
				case (Pais.ISLAS_MARSHALL):
				case (Pais.MICRONESIA):
				case (Pais.NAURU):
				case (Pais.PALAU):
				case (Pais.PAPUA_NUEVA_GUINEA):
				case (Pais.SAMOA):
				case (Pais.ISLAS_SALOMON):
				case (Pais.TONGA):
				case (Pais.TUVALU):
				case (Pais.VANUATU):

					return true;
			}
			return false;
		}

		public static bool IsComunitario(Pais pais)
		{
			switch (pais)
			{
				case (Pais.ALBANIA):
				case (Pais.ANDORRA):
				case (Pais.ARMENIA):
				case (Pais.AZERBAYAN):
				case (Pais.BIELORUSIA):
				case (Pais.BOSNIA):
				case (Pais.BULGARIA):
				case (Pais.CHIPRE):
				case (Pais.CROACIA):
				case (Pais.DINAMARCA):
				case (Pais.ESLOVAQUIA):
				case (Pais.ESLOVENIA):
				case (Pais.ESTONIA):
				case (Pais.GEORGIA):
				case (Pais.HUNGRIA):
				case (Pais.ISLANDIA):
				case (Pais.ISRAEL):
				case (Pais.LETONIA):
				case (Pais.LITUANIA):
				case (Pais.MACEDONIA):
				case (Pais.MALTA):
				case (Pais.MOLDAVIA):
				case (Pais.MONACO):
				case (Pais.POLONIA):
				case (Pais.REP_CHECA):
				case (Pais.RUMANIA):
				case (Pais.RUSIA):
				case (Pais.SUIZA):
				case (Pais.SAN_MARINO):
				case (Pais.TURQUIA):
				case (Pais.UCRANIA):
				case (Pais.YUGOSLAVIA):
				case (Pais.SERBIA):
					return true;
			}
			return false;
		}

		private static readonly Dictionary<string, int> NationalityCodes = new Dictionary<string, int>()
		{
			{"Albanian", 1},
			{"German", 2},
			{"Argentine", 3},
			{"Argentinian", 3},
			{"Australian", 4},
			{"Austrian", 5},
			{"Azerbaijani", 6},
			{"Belarusian", 7},
			{"Bolivian", 8},
			{"Bosnian and Herzegovinan", 9},
			{"Bosnian", 9},
			{"Brazilian", 10},
			{"Bulgarian", 11},
			{"Belgian", 12},
			{"Cameroonian", 13},
			{"Chilean", 14},
			{"Cyprus", 15},
			{"Cypriot", 15},
			{"Colombian", 16},
			{"Croatian", 17},
			{"Danish", 18},
			{"Scottish", 19},
			{"Slovakian", 20},
			{"Slovenian", 21},
			{"Spanish", 22},
			{"Finnish", 23},
			{"French", 24},
			{"Ghanaian", 25},
			{"Greek", 26 },
			{"Dutch", 27},
			{"Honduran", 28},
			{"Hungarian", 29},
			{"British", 30},
			{"Irish", 31},
			{"Northern Irish", 32},
			{"Icelander", 33},
			{"Icelandic", 33},
			{"Faroese", 34},
			{"Israeli", 35},
			{"Italian", 36},
			{"Lithuanian", 37},
			{"Luxembourger", 38},
			{"Macedonian", 39},
			{"Maltan", 40},
			{"Moroccan", 41},
			{"Moldovan", 42},
			{"Nigerian", 43},
			{"Norwegian", 44},
			{"Wales", 45},
			{"Welsh", 45},
			{"Polish", 46},
			{"Portuguese", 47},
			{"Czech", 48},
			{"Romanian", 49},
			{"Russian", 50},
			{"Montenegrin", 58},
			{"Serbian", 58},
			{"South African", 52},
			{"Swedish", 53},
			{"Swiss", 54},
			{"Turkish", 55},
			{"Ukrainian", 56},
			{"Uruguayan", 57},
			//{"", 58},
			{"Peruvian", 59},
			{"Canadian", 60},
			{"American", 61},
			{"Georgian", 62},
			{"Costa Rican", 63},
			{"Paraguayan", 64},
			{"Japanese", 65},
			{"Algerian", 66},
			{"Trinidadian and Tobagonian", 67},
			{"Trinidadian", 67},
			{"Tobagonian", 67},
			{"Senegalese", 68},
			{"Surinamer", 69},
			{"Zambian", 70},
			{"Cape Verdean", 71},
			{"Cabo Verdean", 71},
			{"Venezuelan", 72},
			//{"Rhodesia", 73},
			{"Singaporean", 74},
			{"Andorran", 75},
			{"Mozambican", 76},
			{"Liechtensteiner", 77},
			{"Liberian", 78},
			{"Panamanian", 79},
			//{"Zaire", 80},
			{"Tajikistani", 81},
			{"Tajik", 81},
			{"Uzbekistani", 82},
			{"Uzbek", 82},
			{"Mexican", 83},
			{"Guinean", 84},
			{"Angolean", 85},
			{"Zimbabwean", 86},
			{"Sierra Leonean", 87},
			{"Guadeloupian", 88},
			{"Guadeloupean", 88},
			{"Ecuadorian", 89},
			{"Estonian", 90},
			{"Bissau-Guinean", 91},
			{"Libyan", 92},
			{"Egyptian", 93},
			{"Jamaican", 94},
			//{"", 95},
			//{"", 96},
			{"New Zealand", 97},
			{"New Zealander", 97},
			//{"", 98},
			//{"", 99},
			{"Chadian", 100},
			{"Togolese", 101},
			{"Tanzanian", 103},
			{"Burkinabe", 104},
			{"Gambian", 105},
			{"Rwandan", 106},
			{"Banyarwanda", 106},
			{"Kenyan", 107},
			{"Mauritanian", 108},
			{"Malian", 109},
			{"Malinese", 109},
			{"Ugandan", 110},
			{"Zairean", 111},
			{"Congolese", 111},
			{"Latvian", 112},
			{"Ivorian", 113},
			{"Armenian", 114},
			{"Nicaraguan", 115},
			//{"", 116},
			{"Nigerien", 117},
			{"Barbadian", 118},
			{"Iranian", 119},
			{"Persian", 119},
			{"Qatari", 120},
			{"Tunisian", 121},
			{"Gabonese", 122},
			{"Tahitian", 123},
			{"Mauritian", 124},
			{"Madagascan", 125},
			{"Malagasy", 125},
			{"Martinican", 126},
			{"Martiniquais", 126},
			{"Martiniquai", 126},
			{"Vietnamese", 127},
			{"Haitian", 128},
			{"Antiguan", 129},
			{"Barbudan", 129},
			{"Bahraini", 130},
			{"Bangladeshi", 131},
			{"Belizean", 132},
			{"Beninese", 133},
			{"Beninois", 133},
			{"Bhutanese", 134},
			{"Botswana", 135},
			{"Motswana", 135},
			{"Bruneian", 136},
			{"Burundian", 137},
			{"Cambodian", 138},
			{"Central African", 139},
			{"Chinese", 140},
			//{"", 141},
			{"Yemeni", 142},
			{"Cuban", 143},
			{"Djiboutian", 144},
			{"Dominica", 145},
			{"Dominican", 146},
			{"Salvadoran", 147},
			{"Salvadorian", 147},
			{"Salvadorean", 147},
			{"Equatoguinean", 148},
			{"Equatorial Guinean", 148},
			{"Eritrean", 149},
			{"Ethiopian", 150},
			//{"", 151},
			{"Fijian", 152},
			{"Grenadian", 153},
			{"Guatemalan", 154},
			{"Afghan", 155},
			{"Indian", 156},
			{"Indonesian", 157},
			{"Iraqi", 158},
			{"Jordanian", 159},
			{"Kazakh", 160},
			{"Kiribati", 161},
			{"Kuwaiti", 162},
			{"Kyrgyzstani", 163},
			{"Kyrgyz", 163},
			{"Kirgiz", 163},
			{"Kirghiz", 163},
			{"Lao", 164},
			{"Laotian", 164},
			{"Vaticanian", 165},
			{"Lebanese", 166},
			{"Lesotho", 167},
			{"Basotho", 167},
			{"Mosotho", 167},
			{"Malawian", 168},
			{"Malaysian", 169},
			{"Falkland Islander", 170},
			{"Marshall Islander", 171},
			{"Monacan", 172},
			{"Monegasque", 172},
			{"Mongolian", 173},
			{"Myanmar", 174},
			{"Burmese", 174},
			{"Myanma", 174},
			{"Namibian", 175},
			{"Nauruan", 176},
			{"Nepali", 177},
			{"Nepalese", 177},
			{"North Korean", 178},
			{"Omani", 179},
			{"Pakistani", 180},
			{"Palauan", 181},
			{"Papua New Guinean", 182},
			{"Papuan", 182},
			{"Philippine", 183},
			{"Filipino", 183},
			{"Kittitian", 184},
			{"Nevisian", 184},
			{"Saint Lucian", 185},
			{"Samoan", 186},
			{"Sammarinese", 187},
			//{"", 188},
			{"Saudi Arabian", 189},
			{"Saudi", 189},
			{"Seychellois", 190},
			{"Solomon Islander", 191},
			{"Somali", 192},
			{"Somalian", 192},
			{"Korean", 193},
			{"South Korean", 193},
			{"Sri Lankan", 194},
			{"Sudanese", 195},
			{"Swati", 196},
			{"Swazi", 196},
			{"Eswatini", 196},
			{"Syria", 197},
			{"Taiwanese", 198},
			{"Formosan", 198},
			{"Thailandese", 199},
			{"Thai", 199},
			{"Bahamian", 200},
			{"Tongan", 201},
			{"Turkmen", 202},
			{"Tuvaluan", 203},
			{"Emirati", 204},
			{"Emiri", 204},
			{"Emirian", 204},
			//{"", 205},
			{"Vanuatuan", 206},
			{"Ni-Vanuatu", 206},
		};

		private static readonly Dictionary<string, int> CountryCodes = new Dictionary<string, int>()
		{
			{"Albania", 1},
			{"Germany", 2},
			{"Argentina", 3},
			{"Australia", 4},
			{"Austria", 5},
			{"Azerbaidjan", 6},
			{"Belarus", 7},
			{"Bolivia", 8},
			{"Bosnia and Herzegovina", 9},
			{"Brazil", 10},
			{"Bulgaria", 11},
			{"Belgium", 12},
			{"Cameroon", 13},
			{"Chile", 14},
			{"Cyprus", 15},
			{"Colombia", 16},
			{"Croatia", 17},
			{"Denmark", 18},
			{"Scotland", 19},
			{"Slovakia", 20},
			{"Slovenia", 21},
			{"Spain", 22},
			{"Finland", 23},
			{"France", 24},
			{"Ghana", 25},
			{"Greece", 26 },
			{"Netherlands", 27},
			{"Honduras", 28},
			{"Hungary", 29},
			{"United-Kingdom", 30},
			{"Ireland", 31},
			{"Northern Ireland", 32},
			{"Iceland", 33},
			{"Faroe Islands", 34},
			{"Israel", 35},
			{"Italy", 36},
			{"Lithuania", 37},
			{"Luxembourg", 38},
			{"Macedonia", 39},
			{"Malta", 40},
			{"Morocco", 41},
			{"Moldova", 42},
			{"Nigeria", 43},
			{"Norway", 44},
			{"Wales", 45},
			{"Poland", 46},
			{"Portugal", 47},
			{"Czech Republic", 48},
			{"Romania", 49},
			{"Russia", 50},
			{"Serbia-Montenegro", 58},
			{"South Africa", 52},
			{"Sweden", 53},
			{"Switzerland", 54},
			{"Turkey", 55},
			{"Ukraine", 56},
			{"Uruguay", 57},
			//{"", 58},
			{"Peru", 59},
			{"Canada", 60},
			{"United States", 61},
			{"Georgia", 62},
			{"Costa Rica", 63},
			{"Paraguay", 64},
			{"Japan", 65},
			{"Algeria", 66},
			{"Trinidad and Tobago", 67},
			{"Senegal", 68},
			{"Sénégal", 68},
			{"Surinam", 69},
			{"Zambia", 70},
			{"Cape Verde", 71},
			{"Venezuela", 72},
			//{"Rhodesia", 73},
			{"Singapore", 74},
			{"Andorra", 75},
			{"Mozambique", 76},
			{"Liechtenstein", 77},
			{"Liberia", 78},
			{"Panama", 79},
			//{"Zaire", 80},
			{"Tajikistan", 81},
			{"Uzbekistan", 82},
			{"Mexico", 83},
			{"Guinea", 84},
			{"Angola", 85},
			{"Zimbabwe", 86},
			{"Sierra Leone", 87},
			{"Guadeloupe", 88},
			{"Ecuador", 89},
			{"Estonia", 90},
			{"Guinea Bissau", 91},
			{"Libya", 92},
			{"Egypt", 93},
			{"Jamaica", 94},
			//{"", 95},
			//{"", 96},
			{"New Zealand", 97},
			//{"", 98},
			//{"", 99},
			{"Chad", 100},
			{"Togo", 101},
			{"Guinea Conakry", 102},
			{"Tanzania", 103},
			{"Burkina Faso", 104},
			{"Gambia", 105},
			{"Rwanda", 106},
			{"Kenya", 107},
			{"Mauritania", 108},
			{"Mali", 109},
			{"Uganda", 110},
			{"Zaire", 111},
			{"Congo", 111},
			{"Latvia", 112},
			{"Ivory Coast", 113},
			{"Armenia", 114},
			{"Nicaragua", 115},
			//{"", 116},
			{"Niger", 117},
			{"Barbados", 118},
			{"Iran", 119},
			{"Qatar", 120},
			{"Tunisia", 121},
			{"Gabon", 122},
			{"Tahiti", 123},
			{"Mauritius", 124},
			{"Madagascar", 125},
			{"Martinica", 126},
			{"Vietnam", 127},
			{"Haiti", 128},
			{"Antigua and Barbuda", 129},
			{"Bahrein", 130},
			{"Bangladesh", 131},
			{"Belize", 132},
			{"Benin", 133},
			{"Bhutan", 134},
			{"Botswana", 135},
			{"Brunei", 136},
			{"Burundi", 137},
			{"Cambodia", 138},
			{"Central African Republic", 139},
			{"China", 140},
			//{"", 141},
			{"Yemen", 142},
			{"Cuba", 143},
			{"Djibouti", 144},
			{"Dominica", 145},
			{"Dominican Republic", 146},
			{"El Salvador", 147},
			{"Equatorial Guinea", 148},
			{"Eritrea", 149},
			{"Ethiopia", 150},
			//{"", 151},
			{"Fiji", 152},
			{"Grenade", 153},
			{"Guatemala", 154},
			{"Afghanistan", 155},
			{"India", 156},
			{"Indonesia", 157},
			{"Iraq", 158},
			{"Jordania", 159},
			{"Kazakhstan", 160},
			{"Kiribati", 161},
			{"Kuwait", 162},
			{"Kyrgyzstan", 163},
			{"Laos", 164},
			{"Vatican City", 165},
			{"Lebanon", 166},
			{"Lesotho", 167},
			{"Malawi", 168},
			{"Malaysia", 169},
			{"Falkland Islands", 170},
			{"Marshall Islands", 171},
			{"Monaco", 172},
			{"Mongolia", 173},
			{"Myanmar", 174},
			{"Namibia", 175},
			{"Nauru", 176},
			{"Nepal", 177},
			{"North Korea", 178},
			{"Oman", 179},
			{"Pakistan", 180},
			{"Palau", 181},
			{"Papua New Guinea", 182},
			{"Philippines", 183},
			{"Saint Kitts and Nevis", 184},
			{"Saint Lucia", 185},
			{"Samoa", 186},
			{"San Marino", 187},
			//{"", 188},
			{"Saudi Arabia", 189},
			{"Seychelles", 190},
			{"Solomon Islands", 191},
			{"Somalia", 192},
			{"Korea", 193},
			{"Sri Lanka", 194},
			{"Sudan", 195},
			{"Swaziland", 196},
			{"Eswatini", 196},
			{"Syria", 197},
			{"Taiwan", 198},
			{"Thailand", 199},
			{"The Bahamas", 200},
			{"Bahamas", 200},
			{"Tonga", 201},
			{"Turkmenistan", 202},
			{"Tuvalu", 203},
			{"United Arab Emirates", 204},
			//{"", 205},
			{"Vanuatu", 206},
		};
	}
}
