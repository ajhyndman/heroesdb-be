
using System.Data.SQLite;
using System.Diagnostics;
using System;

namespace HeroesDB {

	class Program {

		public static void Color(Config cfg) {
			while (true) {
				var arg = Console.ReadLine();
				System.Drawing.Color? colour = null;
				var rgb = arg.Split(new [] { ',' });
				if (rgb.Length == 3) {
					colour = System.Drawing.Color.FromArgb(0, Convert.ToInt32(rgb[0]), Convert.ToInt32(rgb[1]), Convert.ToInt32(rgb[2]));
				}
				else if (System.Text.RegularExpressions.Regex.IsMatch(arg, @"^\d+$")) {
					colour = System.Drawing.Color.FromArgb(Convert.ToInt32(arg));
				}
				else if (cfg.Materials.ContainsKey(arg)) {
					colour = cfg.Materials[arg][0];
				}
				if (colour != null) {
					var color = (System.Drawing.Color)colour;
					Console.WriteLine(String.Format("{0}, {1}, {2}\n{3}", color.R, color.G, color.B, color.ToArgb()));
				}
			}
		}

		public static void MissingMaterials(Config cfg) {
			using (var connection = new SQLiteConnection(cfg.ConnectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT t.*
					FROM (
						SELECT
							'i' AS source,
							i.Icon AS key,
							i.Material1 AS material1,
							i.Material2 AS material2,
							i.Material3 AS material3
						FROM HDB_Icons AS i
						LEFT JOIN HDB_Equips AS e ON e.IconKey = i.Key
						LEFT JOIN HDB_Sets AS s ON s.IconKey = i.Key
						LEFT JOIN HDB_Mats AS m ON m.IconKey = i.Key
						WHERE COALESCE(e.ID, s.ID, m.ID) IS NOT NULL
						UNION ALL
						SELECT
							'e' AS source,
							es.EquipKey AS key,
							ei.Material1 AS material1,
							ei.Material2 AS material2,
							ei.Material3 AS material3
						FROM HDB_EquipScreenshots AS es
						INNER JOIN HDB_Equips AS e ON e.Key = es.EquipKey
						INNER JOIN EquipItemInfo AS ei ON ei._ROWID_ = e.ID
					) AS t
					WHERE COALESCE(t.material1, t.material2, t.material3) IS NOT NULL
					ORDER BY
						t.material1,
						t.material2,
						t.material3;
				";
				var reader = command.ExecuteReader();
				while (reader.Read()) {
					var material1Key = Convert.ToString(reader["material1"]);
					var material2Key = Convert.ToString(reader["material2"]);
					var material3Key = Convert.ToString(reader["material3"]);
					var material1Index = 0;
					var material2Index = material2Key == material1Key ? 1 : 0;
					var material3Index = material3Key == material1Key && material3Key == material2Key ? 2 : (material3Key == material1Key || material3Key == material2Key ? 1 : 0);
					if (reader["material1"] != DBNull.Value && (!cfg.Materials.ContainsKey(material1Key) || cfg.Materials[material1Key].Length < material1Index + 1)) {
						Console.WriteLine(String.Format("{0}[{1}] for {2}:{3}", material1Key, material1Index, reader["source"], reader["key"]));
					}
					if (reader["material2"] != DBNull.Value && (!cfg.Materials.ContainsKey(material2Key) || cfg.Materials[material2Key].Length < material2Index + 1)) {
						Console.WriteLine(String.Format("{0}[{1}] for {2}:{3}", material2Key, material2Index, reader["source"], reader["key"]));
					}
					if (reader["material3"] != DBNull.Value && (!cfg.Materials.ContainsKey(material3Key) || cfg.Materials[material3Key].Length < material3Index + 1)) {
						Console.WriteLine(String.Format("{0}[{1}] for {2}:{3}", material3Key, material3Index, reader["source"], reader["key"]));
					}
				}
			}
		}

		public static void Main(String[] args) {
			Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
			Debug.WriteLine("Main() {");
			Debug.Indent();
			try {
				var cfg = new Config();
				var ext = new Extractor();
				var stp = new Setuper(cfg);
				var exp = new Exporter(cfg);
				stp.ImportText();
				stp.SetFeaturedItems();
				stp.SetFeaturedEquips();
				stp.SetFeaturedRecipes();
				stp.SetCharacters();
				stp.SetClassification();
				stp.SetIcons();
				stp.SetMats();
				stp.SetQualityTypes();
				stp.SetEnhanceTypes();
				stp.SetEquips();
				stp.SetEquipParts();
				stp.SetBaseEquips();
				stp.SetSets();
				stp.SetEnchants();
				stp.SetScreenshots();
				var ssr = new Screenshoter(cfg);
				ssr.Screenshot();
				exp.ExportIcons();
				exp.ExportClassification();
				exp.ExportCharacters();
				exp.ExportMats();
				exp.ExportQualityTypes();
				exp.ExportEnhanceTypes();
				exp.ExportEnchants();
				exp.ExportScreenshots();
				exp.ExportEquips();
				exp.ExportEquipParts();
				exp.ExportSets();
				exp.ExportSitemap();
				//MissingMaterials(cfg);
				//Color(cfg);
			}
			catch (Exception exception) {
				Debug.WriteLine(exception.Source);
				Debug.WriteLine(exception.Message);
				Debug.WriteLine(exception.StackTrace);
				throw;
			}
			finally {
				Debug.Unindent();
				Debug.WriteLine("}");
				Console.ReadKey(true);
			}
		}

	}

}
