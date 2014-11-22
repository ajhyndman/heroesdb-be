
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System;
using System.Drawing;
using System.Text.RegularExpressions;

namespace HeroesDB {

	public class Exporter {

		private String databaseFile;

		private String outputPath;

		private Dictionary<String, Int32> itemStats;

		public Exporter(string databaseFile, string outputPath) {
			this.databaseFile = databaseFile;
			this.outputPath = outputPath;
		}

		private void loadItemStats() {
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						ist.[Key],
						ist._ROWID_ AS ID
					FROM HDB_ItemStat AS ist
				";
				var reader = command.ExecuteReader();
				this.itemStats = new Dictionary<String, Int32>();
				while (reader.Read()) {
					this.itemStats.Add(Convert.ToString(reader["Key"]), Convert.ToInt32(reader["ID"]));
				}
			}
		}

		private List<Int32> parseTypePrimaryStats(String stats) {
			var result = new List<Int32>();
			if (!String.IsNullOrEmpty(stats)) {
				if (itemStats == null) {
					this.loadItemStats();
				}
				foreach (var stat in stats.Split(',')) {
					result.Add(this.itemStats[stat.Trim()]);
				}
			}
			return result;
		}

		private String removeFormattingTags(String text) {
			text = Regex.Replace(text, @"\s?<font.*<font.*?>\s?", "");
			text = Regex.Replace(text, @"\s?</font>\s?", "");
			text = Regex.Replace(text, @"\s?(\\n)+\s?", " ");
			text = text.Trim();
			return text;
		}

		public void ExportCharacters() {
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						c.ID AS id,
						c.Name AS name,
						c.Description AS description
					FROM HDB_Characters AS c
					ORDER BY c.ID;
				";
				var reader = command.ExecuteReader();
				var characters = new List<Dictionary<String, Object>>();
				while (reader.Read()) {
					var character = new Dictionary<String, Object>();
					for (var i = 0; i < reader.FieldCount; i += 1) {
						if (reader.GetName(i) == "description") {
							var description = this.removeFormattingTags(Convert.ToString(reader["description"]));
							character.Add(reader.GetName(i), description);
						}
						else {
							character.Add(reader.GetName(i), reader[i]);
						}
					}
					characters.Add(character);
				}
				var serializer = new JavaScriptSerializer();
				var json = serializer.Serialize(characters);
				var path = Path.Combine(this.outputPath, "characters.json");
				File.WriteAllText(path, json);
			}
		}

		public void ExportItemStats() {
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						ist._ROWID_ AS id,
						ist.Type AS type,
						ist.Name AS name,
						ist.ShortName AS shortName,
						ist.Description AS description,
						ist.[Order] AS [order]
					FROM HDB_ItemStat AS ist;
				";
				var reader = command.ExecuteReader();
				var stats = new List<Dictionary<String, Object>>();
				while (reader.Read()) {
					var stat = new Dictionary<String, Object>();
					for (var i = 0; i < reader.FieldCount; i += 1) {
						if (reader.GetName(i) == "description") {
							var description = this.removeFormattingTags(Convert.ToString(reader["description"]));
							stat.Add(reader.GetName(i), description);
						}
						else {
							stat.Add(reader.GetName(i), reader[i]);
						}
					}
					stats.Add(stat);
				}
				var serializer = new JavaScriptSerializer();
				var json = serializer.Serialize(stats);
				var path = Path.Combine(this.outputPath, "item-stats.json");
				File.WriteAllText(path, json);
			}
		}

		public void ExportItemGroups() {
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						ig._ROWID_ AS GroupID,
						ig.Name AS GroupName,
						it._ROWID_ AS TypeID,
						it.Name AS TypeName,
						it.PrimaryStats AS TypePrimaryStats,
						ic._ROWID_ AS CategoryID,
						ic.Name AS CategoryName
					FROM HDB_ItemGroup AS ig
					INNER JOIN HDB_ItemType AS it ON it.GroupKey = ig.[Key]
					INNER JOIN HDB_ItemCategory AS ic ON
						ic.GroupKey = ig.[Key] AND
						ic.TypeKey = it.[Key]
					ORDER BY
						ig.[Order],
						ig.Name,
						it.[Order],
						it.Name,
						ic.[Order],
						ic.Name;
				";
				var reader = command.ExecuteReader();
				var table = new DataTable();
				table.Load(reader);
				var groups = new List<Dictionary<String, Object>>();
				var types = new List<Dictionary<String, Object>>();
				var categories = new List<Dictionary<String, Object>>();
				for (var i = 0; i < table.Rows.Count; i += 1) {
					var row = table.Rows[i];
					var categoryData = new Dictionary<String, Object>() {
						{ "id", row["CategoryID"] },
						{ "name", row["CategoryName"] }
					};
					categories.Add(categoryData);
					var lastRow = table.Rows.Count - 1 == i;
					var sameType = (lastRow || Convert.ToInt32(row["TypeID"]) == Convert.ToInt32(table.Rows[i + 1]["TypeID"]));
					if (lastRow || !sameType) {
						var primaryStats =  this.parseTypePrimaryStats(Convert.ToString(row["TypePrimaryStats"]));
						var typeData = new Dictionary<String, Object>() {
							{ "id", row["TypeID"] },
							{ "name", row["TypeName"] },
							{ "primaryStats", primaryStats },
							{ "categories", categories }
						};
						types.Add(typeData);
						categories = new List<Dictionary<String, Object>>();
						var sameGroup = (lastRow || Convert.ToInt32(row["GroupID"]) == Convert.ToInt32(table.Rows[i + 1]["GroupID"]));
						if (lastRow || !sameGroup) {
							var groupData = new Dictionary<String, Object>() {
								{ "id", row["GroupID"] },
								{ "name", row["GroupName"] },
								{ "types",  types }
							};
							groups.Add(groupData);
							types = new List<Dictionary<String, Object>>();
						}
					}
				}
				var serializer = new JavaScriptSerializer();
				var json = serializer.Serialize(groups);
				var path = Path.Combine(this.outputPath, "item-groups.json");
				File.WriteAllText(path, json);
			}
		}

		private Dictionary<String, Object> addSetFields(Dictionary<String, Object> item) {
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						ist.[Key],
						ist.Name
					FROM HDB_ItemStat AS ist
					ORDER BY ist.[Order]
				";
				var reader = command.ExecuteReader();
				var stats = new Dictionary<String, String>();
				while (reader.Read()) {
					stats.Add(Convert.ToString(reader["Key"]), Convert.ToString(reader["Name"]));
				}
				reader.Close();
				command.CommandText = @"
					SELECT DISTINCT i.Name
					FROM HDB_ItemClassification AS ic
					INNER JOIN HDB_Item AS i ON i.ID = ic.ItemID
					INNER JOIN ItemClassInfo AS ici ON ici._ROWID_ = ic.ItemID
					WHERE ic.SetID = @SetID
					ORDER BY CASE ici.Category WHEN 'HELM' THEN 1 WHEN 'TUNIC' THEN 2 WHEN 'PANTS' THEN 3 WHEN 'GLOVES' THEN 4 WHEN 'BOOTS' THEN 5 END;
				";
				command.Parameters.Add("@SetID", DbType.Int32).Value = Convert.ToString(item["id"]).Replace("s", "");
				reader = command.ExecuteReader();
				var items = new List<String>();
				var setNameParts = Convert.ToString(item["name"]).Split(new [] { ' ' });
				while (reader.Read()) {
					var name = Convert.ToString(reader["name"]);
					for (var i = 0; i < setNameParts.Length; i++) {
						if (!name.StartsWith(String.Concat(setNameParts[i], " "), StringComparison.InvariantCulture)) {
							break;
						}
						name = name.Replace(setNameParts[i], "").Trim();
					}
					items.Add(name);
				}
				reader.Close();
				item.Add("items", items);

				command.CommandText = @"
					SELECT DISTINCT
						trs.Text AS RequiredSkill,
						sr.Rank_String AS RequiredSkillRank
					FROM (
						SELECT
							ici.RequiredSkill,
							MAX(ici.RequiredSkillRank) AS RequiredSkillRank
						FROM HDB_ItemClassification AS ic
						INNER JOIN HDB_Item AS i ON i.ID = ic.ItemID
						INNER JOIN ItemClassInfo AS ici ON ici._ROWID_ = ic.ItemID
						WHERE ic.SetID = @SetID
						GROUP BY ici.RequiredSkill
					) AS t
					LEFT JOIN HDB_Text AS trs ON trs.[Key] = 'HEROES_SKILL_NAME_' || UPPER(t.RequiredSkill)
					LEFT JOIN SkillRankEnum AS sr ON sr.Rank_Num = t.RequiredSkillRank
					ORDER BY trs.Text;
				";
				reader = command.ExecuteReader();
				var requiredSkills = new Dictionary<String, Object>();
				while (reader.Read()) {
					requiredSkills.Add(Convert.ToString(reader["requiredSkill"]), reader["requiredSkillRank"]);
				}
				reader.Close();
				item.Add("requiredSkills", requiredSkills);

				command.CommandText = @"
					SELECT
						se.SetCount,
						SUM(CASE WHEN se.EffectTarget = 'ATK' THEN se.Amount ELSE 0 END) AS ATK,
						SUM(CASE WHEN se.EffectTarget = 'MATK' THEN se.Amount ELSE 0 END) AS MATK,
						--SUM(CASE WHEN se.EffectTarget = 'HP' THEN se.Amount ELSE 0 END) AS HP,
						SUM(CASE WHEN se.EffectTarget = 'DEF' THEN se.Amount ELSE 0 END) AS DEF,
						SUM(CASE WHEN se.EffectTarget = 'STR' THEN se.Amount ELSE 0 END) AS STR,
						SUM(CASE WHEN se.EffectTarget = 'INT' THEN se.Amount ELSE 0 END) AS INT,
						SUM(CASE WHEN se.EffectTarget = 'DEX' THEN se.Amount ELSE 0 END) AS DEX,
						SUM(CASE WHEN se.EffectTarget = 'WILL' THEN se.Amount ELSE 0 END) AS WILL
					FROM SetInfo AS s
					INNER JOIN SetEffectInfo AS se ON se.SetID = s.SetID
					WHERE s._ROWID_ = @SetID
					GROUP BY se.SetCount
					ORDER BY se.SetCount;
				";
				reader = command.ExecuteReader();
				var setEffects = new Dictionary<String, Object>();
				while (reader.Read()) {
					var effects = new Dictionary<String, Object>();
					for (var i = 0; i < reader.FieldCount; i += 1) {
						if (reader.GetName(i) != "SetCount" && Convert.ToInt32(reader[i]) > 0) {
							effects.Add(stats[Convert.ToString(reader.GetName(i))], reader[i]);
						}
					}
					setEffects.Add(Convert.ToString(reader["SetCount"]), effects);
				}
				reader.Close();
				item.Add("setEffects", setEffects);
			}
			item.Remove("requiredSkill");
			item.Remove("requiredSkillRank");
			return item;
		}

		public void ExportItems() {
			var path = Path.Combine(this.outputPath, "items");
			if (Directory.Exists(path)) {
				Directory.Delete(path, true);
			}
			Directory.CreateDirectory(path);
			path = Path.Combine(this.outputPath, "type-items");
			if (Directory.Exists(path)) {
				Directory.Delete(path, true);
			}
			Directory.CreateDirectory(path);
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						it._ROWID_ AS ID,
						it.PrimaryStats
					FROM HDB_ItemType AS it;
				";
				var reader = command.ExecuteReader();
				var typePrimaryStats = new Dictionary<Int32, List<Int32>>();
				while (reader.Read()) {
					var primaryStats = this.parseTypePrimaryStats(Convert.ToString(reader["PrimaryStats"]));
					typePrimaryStats.Add(Convert.ToInt32(reader["ID"]), primaryStats);
				}
				reader.Close();
				command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						i.ID AS id,
						i.GroupID AS groupId,
						i.TypeID AS typeId,
						i.CategoryID AS categoryId,
						i.IconID AS iconId,
						i.Name AS name,
						i.Description AS description,
						i.Rarity AS rarity,
						i.RequiredSkill AS requiredSkill,
						i.RequiredSkillRank AS requiredSkillRank,
						i.RequiredLevel AS RequiredLevel,
						i.ClassRestriction AS ClassRestriction,
						i.HDBATK AS HDBATK,
						i.ATK AS ATK,
						i.MATK AS MATK,
						i.ATK_Speed AS ATK_Speed,
						i.Critical AS Critical,
						i.Balance AS Balance,
						i.DEF AS DEF,
						i.Res_Critical AS Res_Critical,
						i.STR AS STR,
						i.DEX AS DEX,
						i.INT AS INT,
						i.WILL AS WILL,
						i.STAMINA AS STAMINA
					FROM HDB_Item AS i
					ORDER BY
						i.GroupID,
						i.TypeID,
						i.RequiredLevel DESC,
						i.Name,
						i.CategoryID;
				";
				reader = command.ExecuteReader();
				var itemNames = new Dictionary<String, List<String>>();
				var typeItems = new List<Dictionary<String, Object>>();
				var skipForTypeItems = new List<String>() { "RequiredSkill", "RequiredSkillRank" };
				var serializer = new JavaScriptSerializer();
				var json = "";
				var previousTypeId = -1;
				while (reader.Read()) {
					if (!itemNames.ContainsKey(Convert.ToString(reader["name"]))) {
						itemNames.Add(Convert.ToString(reader["name"]), new List<String>());
					}
					if (!itemNames[Convert.ToString(reader["name"])].Contains(Convert.ToString(reader["id"]))) {
						itemNames[Convert.ToString(reader["name"])].Add(Convert.ToString(reader["id"]));
					}
					if (previousTypeId != Convert.ToInt32(reader["typeId"])) {
						if (previousTypeId != -1) {
							json = serializer.Serialize(typeItems);
							path = Path.Combine(this.outputPath, "type-items");
							path = Path.Combine(path, Path.ChangeExtension(Convert.ToString(previousTypeId), "json"));
							File.WriteAllText(path, json);
							typeItems = new List<Dictionary<String, Object>>();
						}
						previousTypeId = Convert.ToInt32(reader["typeId"]);
					}
					var typeItemAlreadyAdded = false;
					foreach (var typeItemTmp in typeItems) {
						if (Convert.ToString(typeItemTmp["id"]) == Convert.ToString(reader["id"])) {
							typeItemAlreadyAdded = true;
							((List<Int32>)typeItemTmp["categoryId"]).Add(Convert.ToInt32(reader["categoryId"]));
						}
					}
					if (!typeItemAlreadyAdded) {
						var item = new Dictionary<String, Object>();
						item["stats"] = new Dictionary<String, Object>();
						var typeItem = new Dictionary<String, Object>();
						typeItem["stats"] = new Dictionary<String, Object>();
						var description = "";
						for (var i = 0; i < reader.FieldCount; i += 1) {
							if (reader.GetName(i) == "categoryId") {
								item.Add(reader.GetName(i), new List<Int32>() { Convert.ToInt32(reader[i]) });
								typeItem.Add(reader.GetName(i), new List<Int32>() { Convert.ToInt32(reader[i]) });
							}
							else if (reader.GetName(i) == "description") {
								description = this.removeFormattingTags(Convert.ToString(reader["description"]));
								item.Add(reader.GetName(i), description);
							}
							else if (this.itemStats.ContainsKey(reader.GetName(i))) {
								var statId = this.itemStats[reader.GetName(i)];
								((Dictionary<String, Object>)item["stats"]).Add(Convert.ToString(statId), reader[i]);
								if (typePrimaryStats[Convert.ToInt32(reader["typeId"])].Contains(statId)) {
									((Dictionary<String, Object>)typeItem["stats"]).Add(Convert.ToString(statId), reader[i]);
								}
							}
							else {
								item.Add(reader.GetName(i), reader[i]);
								if (!skipForTypeItems.Contains(reader.GetName(i))) {
									typeItem.Add(reader.GetName(i), reader[i]);
								}
							}
						}
						if (((Dictionary<String, Object>)typeItem["stats"]).Count == 0) {
							typeItem.Add("description", description);
						}
						typeItems.Add(typeItem);
						if (Convert.ToString(reader["id"]).StartsWith("s", StringComparison.InvariantCulture)) {
							this.addSetFields(item);
						}
						json = serializer.Serialize(item);
						path = Path.Combine(this.outputPath, "items");
						path = Path.Combine(path, Path.ChangeExtension(Convert.ToString(item["id"]), "json"));
						File.WriteAllText(path, json);
					}
				}
				json = serializer.Serialize(typeItems);
				path = Path.Combine(this.outputPath, "type-items");
				path = Path.Combine(path, Path.ChangeExtension(Convert.ToString(previousTypeId), "json"));
				File.WriteAllText(path, json);
				json = serializer.Serialize(itemNames);
				path = Path.Combine(this.outputPath, Path.ChangeExtension("item-names", "json"));
				File.WriteAllText(path, json);
			}
		}

		public void ExportIcons(String fromPath) {
			/*
				SELECT
					t.Material,
					COUNT(*)
				FROM (
					SELECT ico.Material1 as Material
					FROM HDB_Item AS i
					INNER JOIN HDB_Icon AS ico ON ico._ROWID_ = i.IconID
					UNION ALL
					SELECT ico.Material2
					FROM HDB_Item AS i
					INNER JOIN HDB_Icon AS ico ON ico._ROWID_ = i.IconID
					UNION ALL
					SELECT ico.Material3
					FROM HDB_Item AS i
					INNER JOIN HDB_Icon AS ico ON ico._ROWID_ = i.IconID
				) AS t
				WHERE t.material IS NOT NULL
				GROUP BY t.Material
				ORDER BY COUNT(*) DESC;
			 */
			var path = Path.Combine(this.outputPath, "icons");
			if (Directory.Exists(path)) {
				Directory.Delete(path, true);
			}
			Directory.CreateDirectory(path);
			var materials = new Dictionary<String, Color>() {
				{ "metal", Color.FromArgb(204, 204, 204) },
				{ "metal_dark", Color.FromArgb(148, 152, 155) },
				{ "metal_silver_ex", Color.FromArgb(243, 241, 238) },
				{ "leather_dark", Color.FromArgb(73, 0, 2) },
				{ "leather", Color.FromArgb(162, 78, 4) },
				{ "metal_gold_ex", Color.FromArgb(218, 179, 0) },
				{ "metal_luxury", Color.FromArgb(221, 205, 204) },
				{ "metal_gold", Color.FromArgb(212, 174, 55) },
				{ "cloth_dark", Color.FromArgb(186, 171, 170) },
				{ "cloth_bw", Color.FromArgb(243, 240, 240) },
				{ "cloth", Color.FromArgb(222, 213, 213) },
				{ "metal_colorful", Color.FromArgb(188, 198, 204) },
				{ "leather_colorful", Color.FromArgb(212, 134, 134) },
				{ "rainbow", Color.FromArgb(229, 6, 102) },
				{ "metal_bronze", Color.FromArgb(166, 152, 131) },
				{ "skull", Color.FromArgb(247, 249, 243) },
				{ "leather_brown", Color.FromArgb(101, 84, 66) },
				{ "white", Color.FromArgb(255, 255, 255) },
				{ "leather_enamel", Color.FromArgb(246, 246, 247) },
				{ "cloth_bright", Color.FromArgb(217, 233, 244) },
				{ "black", Color.FromArgb(0, 0, 0) },
				{ "leather_crimson", Color.FromArgb(211, 8, 8) },
				{ "weapon_edge", Color.FromArgb(230, 229, 232) },
				{ "silk", Color.FromArgb(252, 253, 244) },
				{ "wood", Color.FromArgb(187, 163, 125) },
				{ "gray_brown", Color.FromArgb(120, 111, 106) },
				{ "metal_red", Color.FromArgb(109, 13, 15) },
				{ "innerarmor", Color.FromArgb(252, 245, 198) },
				{ "gray", Color.FromArgb(153, 153, 153) },
				{ "leather_red", Color.FromArgb(124, 29, 32) },
				{ "frame_red", Color.FromArgb(244, 30, 8) },
				{ "nightmare", Color.FromArgb(124, 58, 73) },
				{ "leather_glasgavelen", Color.FromArgb(102, 99, 51) },
				{ "chinese", Color.FromArgb(255, 89, 0) },
				{ "fur_mono", Color.FromArgb(255, 250, 227) },
				{ "leaf_green", Color.FromArgb(168, 210, 110) },
				{ "leather_flatbrown", Color.FromArgb(166, 144, 121) },
				{ "flat_red", Color.FromArgb(255, 70, 24) },
				{ "laghodessa", Color.FromArgb(144, 166, 5) },
				{ "cloth_santa", Color.FromArgb(230, 31, 45) },
				{ "giantspider", Color.FromArgb(141, 134, 108) },
				{ "beard", Color.FromArgb(94, 56, 53) },
				{ "fix_bluechannel", Color.FromArgb(2, 93, 140) },
				{ "fix_greenchannel", Color.FromArgb(27, 175, 27) },
				{ "fix_redchannel", Color.FromArgb(240, 35, 17) },
				{ "red_strawberry", Color.FromArgb(242, 58, 101) },
				{ "flat_white", Color.FromArgb(239, 232, 215) },
				{ "leather_wonderland", Color.FromArgb(238, 12, 73) },
				{ "flat_black", Color.FromArgb(50, 48, 41) },
				{ "flat_pumpkin", Color.FromArgb(255, 155, 0) },
				{ "brown", Color.FromArgb(96, 72, 48) },
				{ "champagne_gold", Color.FromArgb(246, 236, 177) },
				{ "military", Color.FromArgb(128, 128, 93) },
				{ "flat_devcatorange", Color.FromArgb(255, 153, 0) },
				{ "flat_gray", Color.FromArgb(182, 177, 175) },
				{ "korean", Color.FromArgb(214, 30, 0) },
				{ "crystal", Color.FromArgb(192, 255, 255) },
				{ "crystal_blue", Color.FromArgb(66, 171, 236) },
				{ "event_rabbit_moon_C1", Color.FromArgb(249, 250, 198) },
				{ "flat_pumpkinlumi", Color.FromArgb(233, 128, 66) },
				{ "flat_volcanicred", Color.FromArgb(150, 27, 22) },
				{ "bloodlord", Color.FromArgb(247, 90, 66) },
				{ "cash_angelring", Color.FromArgb(255, 234, 232) },
				{ "darkblue", Color.FromArgb(8, 55, 105) },
				{ "event_euro2012_lower", Color.FromArgb(233, 255, 255) },
				{ "flat_bandage", Color.FromArgb(218, 191, 180) },
				{ "flat_bloodyred", Color.FromArgb(178, 55, 7) },
				{ "flat_camelbrown", Color.FromArgb(206, 175, 138) },
				{ "flat_milkypink", Color.FromArgb(235, 167, 174) },
				{ "flat_smoketurquoise", Color.FromArgb(114, 165, 169) }
			};
			var icon = Paloma.TargaImage.LoadTargaImage(Path.Combine(fromPath, "blank.tga"));
			icon.Save(Path.Combine(this.outputPath, "icons", "0.png"));
			//icon = Paloma.TargaImage.LoadTargaImage(Path.Combine(fromPath, "package_newbieperfect.tga"));
			//icon.Save(Path.Combine(this.outputPath, "icons", "set.png"));
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						ii._ROWID_ AS ID,
						ii.Icon AS Icon,
						ii.Material1 AS Material1,
						ii.Material2 AS Material2,
						ii.Material3 AS Material3
					FROM HDB_ItemIcon AS ii
					INNER JOIN HDB_Item AS i ON i.IconID = ii._ROWID_;
				";
				var reader = command.ExecuteReader();
				while (reader.Read()) {
					var iconFileName = String.Concat(reader["Icon"], ".tga");
					var iconFile = Path.Combine(fromPath, iconFileName);
					if (File.Exists(iconFile)) {
						icon = Paloma.TargaImage.LoadTargaImage(iconFile);
						icon.MakeTransparent(Color.FromArgb(0, 0, 0));
						var needColors = false;
						needColors |= reader["Material1"] != DBNull.Value;
						needColors |= reader["Material2"] != DBNull.Value;
						needColors |= reader["Material3"] != DBNull.Value;
						if (needColors) {
							for (var x = 0; x < icon.Width; x += 1) {
								for (var y = 0; y < icon.Height; y += 1) {
									var color = icon.GetPixel(x, y);
									var baseColor = color;
									var brightness = 1.0;
									if (reader["Material1"] != DBNull.Value && color.R > color.G && color.R > color.B) {
										var material = Convert.ToString(reader["Material1"]);
										baseColor = materials.ContainsKey(material) ? materials[material] : Color.FromArgb(204, 204, 204);
										brightness = Convert.ToDouble(color.R) / 255;
									}
									else if (reader["Material2"] != DBNull.Value && color.G > color.R && color.G > color.B) {
										var material = Convert.ToString(reader["Material2"]);
										baseColor = materials.ContainsKey(material) ? materials[material] : Color.FromArgb(153, 153, 153);
										brightness = Convert.ToDouble(color.G) / 255;
									}
									else if (reader["Material3"] != DBNull.Value && color.B > color.R && color.B > color.G) {
										var material = Convert.ToString(reader["Material3"]);
										baseColor = materials.ContainsKey(material) ? materials[material] : Color.FromArgb(102, 102, 102);
										brightness = Convert.ToDouble(color.B) / 255;
									}
									if (baseColor != color) {
										var newColor = Color.FromArgb(Convert.ToInt32(baseColor.R * brightness), Convert.ToInt32(baseColor.G * brightness), Convert.ToInt32(baseColor.B * brightness));;
										icon.SetPixel(x, y, newColor);
									}
								}
							}
						}
						icon.Save(Path.Combine(this.outputPath, "icons", String.Concat(reader["ID"], ".png")));
					}
				}
			}
		}

	}

}
