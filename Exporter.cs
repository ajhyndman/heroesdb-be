
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System;

namespace HeroesDB {

	public class Exporter {

		private String connectionString;

		private String outputPath;

		private Dictionary<String, Dictionary<String, String[]>> _primaryProperties;
		private Dictionary<String, Dictionary<String, String[]>> primaryProperties {
			get {
				if (this._primaryProperties == null) {
					Debug.WriteLine("primaryProperties { get {");
					Debug.Indent();
					var properties = new Dictionary<String, Dictionary<String, String[]>>();
					using (var connection = new SQLiteConnection(this.connectionString)) {
						connection.Open();
						var command = connection.CreateCommand();
						command.CommandText = @"
							SELECT DISTINCT
								c.GroupKey AS groupKey,
								c.TypeKey AS typeKey,
								c.TypePrimaryProperties AS typePrimaryProperties
							FROM HDB_Classification AS c;
						";
						var reader = command.ExecuteReader();
						while (reader.Read()) {
							var group = Convert.ToString(reader["groupKey"]);
							var type = Convert.ToString(reader["typeKey"]);
							if (!properties.ContainsKey(group)) {
								Debug.Write(".");
								properties.Add(group, new Dictionary<String, String[]>());
							}
							var typeProperties = new String[0];
							if (reader["typePrimaryProperties"] != DBNull.Value) {
								typeProperties = Convert.ToString(reader["typePrimaryProperties"]).Split(new [] { ", " }, StringSplitOptions.None);
							}
							properties[group].Add(type, typeProperties);
						}
						Debug.WriteLine("");
					}
					this._primaryProperties = properties;
					Debug.Unindent();
					Debug.WriteLine("} }");
				}
				return this._primaryProperties;
			}
		}

		private Dictionary<String, List<Dictionary<String, Object>>> _equipRecipeShops;
		private Dictionary<String, List<Dictionary<String, Object>>> equipRecipeShops {
			get {
				if (this._equipRecipeShops == null) {
					Debug.WriteLine("equipRecipeShops { get {");
					Debug.Indent();
					var shops = new Dictionary<String, List<Dictionary<String, Object>>>();
					using (var connection = new SQLiteConnection(this.connectionString)) {
						connection.Open();
						var command = connection.CreateCommand();
						command.CommandText = @"
							SELECT
								ers.EquipKey AS equipKey,
								ers.ShopKey AS shopKey,
								ers.ShopName AS ShopName
							FROM HDB_EquipRecipeShops AS ers
							ORDER BY
								ers.EquipKey,
								ers.ShopName;
						";
						var reader = command.ExecuteReader();
						while (reader.Read()) {
							var equipKey = Convert.ToString(reader["equipKey"]);
							if (!shops.ContainsKey(equipKey)) {
								Debug.Write(".");
								shops.Add(equipKey, new List<Dictionary<String, Object>>());
							}
							shops[equipKey].Add(new Dictionary<String, Object>() {
								{ "key", reader["shopKey"] },
								{ "name", reader["ShopName"] }
							});
						}
						Debug.WriteLine("");
					}
					this._equipRecipeShops = shops;
					Debug.Unindent();
					Debug.WriteLine("} }");
				}
				return this._equipRecipeShops;
			}
		}

		private Dictionary<String, Dictionary<String, Dictionary<String, Object>>> _equipRecipes;
		private Dictionary<String, Dictionary<String, Dictionary<String, Object>>> equipRecipes {
			get {
				if (this._equipRecipes == null) {
					Debug.WriteLine("equipRecipes { get {");
					Debug.Indent();
					var recipes = new Dictionary<String, Dictionary<String, Dictionary<String, Object>>>();
					using (var connection = new SQLiteConnection(this.connectionString)) {
						connection.Open();
						var command = connection.CreateCommand();
						command.CommandText = @"
							SELECT
								er.EquipKey AS equipKey,
								er.Type AS type,
								m.Key AS matKey,
								m.IconID AS matIconID,
								m.Name AS matName,
								m.Rarity AS matRarity,
								m.[Order] AS matOrder,
								er.MatCount AS matCount,
								er.AppearQuestName AS appearQuestName,
								er.ExpertiseName AS expertiseName,
								er.ExpertiseExperienceRequired AS expertiseExperienceRequired
							FROM HDB_EquipRecipes AS er
							INNER JOIN HDB_Mats AS m ON m.Key = er.MatKey
							ORDER BY
								er.EquipKey,
								m.[Order],
								m.Rarity DESC,
								m.Name;
						";
						var reader = command.ExecuteReader();
						while (reader.Read()) {
							var equipKey = Convert.ToString(reader["equipKey"]);
							var type = Convert.ToString(reader["type"]);
							if (!recipes.ContainsKey(equipKey)) {
								Debug.Write(".");
								recipes.Add(equipKey, new Dictionary<String, Dictionary<String, Object>>());
							}
							if (!recipes[equipKey].ContainsKey(type)) {
								recipes[equipKey].Add(type, new Dictionary<String, Object>());
								if (reader["appearQuestName"] != DBNull.Value) {
									recipes[equipKey][type].Add("appearQuestName", reader["appearQuestName"]);
								}
								if (reader["expertiseName"] != DBNull.Value) {
									recipes[equipKey][type].Add("expertiseName", reader["expertiseName"]);
									recipes[equipKey][type].Add("expertiseExperienceRequired", reader["expertiseExperienceRequired"]);
								}
								if (type == "npc" && this.equipRecipeShops.ContainsKey(equipKey)) {
									recipes[equipKey][type].Add("shops", this.equipRecipeShops[equipKey]);
								}
							}
							if (!recipes[equipKey][type].ContainsKey("mats")) {
								recipes[equipKey][type].Add("mats", new List<Dictionary<String, Object>>());
							}
							((List<Dictionary<String, Object>>)recipes[equipKey][type]["mats"]).Add(new Dictionary<String, Object>() {
								{ "key", reader["matKey"] },
								{ "iconID", reader["matIconID"] },
								{ "name", reader["matName"] },
								{ "rarity", reader["matRarity"] },
								{ "order", reader["matOrder"] },
								{ "count", reader["matCount"] }
							});
						}
						Debug.WriteLine("");
					}
					this._equipRecipes = recipes;
					Debug.Unindent();
					Debug.WriteLine("} }");
				}
				return this._equipRecipes;
			}
		}

		public Exporter(String databaseFile, String outputPath) {
			Debug.WriteLine("Exporter({0}, {1}) {{", new [] { databaseFile, outputPath });
			Debug.Indent();
			this.connectionString = String.Format("Data Source={0}; Version=3;", databaseFile);
			this.outputPath = outputPath;
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		private String removeFormattingTags(String text) {
			text = Regex.Replace(text, "<font.*?>", "");
			text = Regex.Replace(text, "</font>", "");
			text = Regex.Replace(text, @"\\n", " ");
			text = Regex.Replace(text, " {2,}", " ");
			text = Regex.Replace(text, @"\\""", @"""");
			text = text.Trim();
			return text;
		}

		public void ExportCharacters() {
			Debug.WriteLine("ExportCharacters() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.connectionString)) {
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
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void ExportClassification() {
			Debug.WriteLine("ExportClassification() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT DISTINCT
						c.GroupKey AS groupKey,
						c.GroupName AS groupName,
						c.TypeKey AS typeKey,
						c.TypeName AS typeName,
						c.TypePrimaryProperties AS typePrimaryProperties,
						c.CategoryKey AS categoryKey,
						c.CategoryName AS categoryName
					FROM HDB_Classification AS c
					WHERE c.GroupKey IS NOT NULL
					ORDER BY
						c.GroupOrder,
						c.GroupName,
						c.TypeOrder,
						c.TypeName,
						c.CategoryOrder,
						c.CategoryName;
				";
				var reader = command.ExecuteReader();
				var table = new DataTable();
				table.Load(reader);
				var groups = new List<Dictionary<String, Object>>();
				var types = new List<Dictionary<String, Object>>();
				var categories = new List<Dictionary<String, Object>>();
				for (var i = 0; i < table.Rows.Count; i += 1) {
					Debug.Write(".");
					var row = table.Rows[i];
					categories.Add(new Dictionary<String, Object>() {
						{ "key", row["categoryKey"] },
						{ "name", row["categoryName"] }
					});
					var lastRow = table.Rows.Count - 1 == i;
					var sameType = lastRow || Convert.ToString(row["typeKey"]) == Convert.ToString(table.Rows[i + 1]["typeKey"]);
					if (lastRow || !sameType) {
						var primaryProperties = new String[0]; 
						if (row["typePrimaryProperties"] != DBNull.Value) {
							primaryProperties = Convert.ToString(row["typePrimaryProperties"]).Split(new [] { ", " }, StringSplitOptions.None);
						}
						types.Add(new Dictionary<String, Object>() {
							{ "key", row["typeKey"] },
							{ "name", row["typeName"] },
							{ "primaryProperties", primaryProperties },
							{ "categories", categories }
						});
						categories = new List<Dictionary<String, Object>>();
						var sameGroup = lastRow || Convert.ToString(row["groupKey"]) == Convert.ToString(table.Rows[i + 1]["groupKey"]);
						if (lastRow || !sameGroup) {
							groups.Add(new Dictionary<String, Object>() {
								{ "key", row["groupKey"] },
								{ "name", row["groupName"] },
								{ "types",  types }
							});
							types = new List<Dictionary<String, Object>>();
						}
					}
				}
				Debug.WriteLine("");
				var classification = new Dictionary<String, Object>() {
					{ "groups", groups }
				};
				var serializer = new JavaScriptSerializer();
				var json = serializer.Serialize(classification);
				var path = Path.Combine(this.outputPath, "classification.json");
				File.WriteAllText(path, json);
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void ExportIcons(String fromPath) {
			Debug.WriteLine("ExportIcons({0}) {{", new [] { fromPath });
			Debug.Indent();
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
				{ "leather_brown", Color.FromArgb(202, 172, 140) },
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
				{ "beard", Color.FromArgb(180, 153, 127) },
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
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						i._ROWID_ AS id,
						i.Icon AS icon,
						i.Material1 AS material1,
						i.Material2 AS material2,
						i.Material3 AS material3
					FROM HDB_Icons AS i
					LEFT JOIN HDB_Equips AS e ON e.IconID = i._ROWID_
					LEFT JOIN HDB_Sets AS s ON s.IconID = i._ROWID_
					LEFT JOIN HDB_Mats AS m ON m.IconID = i._ROWID_
					WHERE COALESCE(e.ID, s.ID, m.ID) IS NOT NULL;
				";
				var reader = command.ExecuteReader();
				while (reader.Read()) {
					var iconFileName = String.Concat(reader["icon"], ".tga");
					var iconFile = Path.Combine(fromPath, iconFileName);
					if (File.Exists(iconFile)) {
						Debug.Write(".");
						icon = Paloma.TargaImage.LoadTargaImage(iconFile);
						icon.MakeTransparent(Color.FromArgb(0, 0, 0));
						var needColors = false;
						needColors |= reader["material1"] != DBNull.Value;
						needColors |= reader["material2"] != DBNull.Value;
						needColors |= reader["material3"] != DBNull.Value;
						if (needColors) {
							for (var x = 0; x < icon.Width; x += 1) {
								for (var y = 0; y < icon.Height; y += 1) {
									var color = icon.GetPixel(x, y);
									var baseColor = color;
									var brightness = 1.0;
									if (reader["material1"] != DBNull.Value && color.R > color.G && color.R > color.B) {
										var material = Convert.ToString(reader["material1"]);
										baseColor = materials.ContainsKey(material) ? materials[material] : Color.FromArgb(204, 204, 204);
										brightness = Convert.ToDouble(color.R) / 255;
									}
									else if (reader["material2"] != DBNull.Value && color.G > color.R && color.G > color.B) {
										var material = Convert.ToString(reader["material2"]);
										baseColor = materials.ContainsKey(material) ? materials[material] : Color.FromArgb(153, 153, 153);
										brightness = Convert.ToDouble(color.G) / 255;
									}
									else if (reader["material3"] != DBNull.Value && color.B > color.R && color.B > color.G) {
										var material = Convert.ToString(reader["material3"]);
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
						icon.Save(Path.Combine(this.outputPath, "icons", String.Concat(reader["id"], ".png")));
					}
				}
				Debug.WriteLine("");
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void ExportMats() {
			Debug.WriteLine("ExportMats() {");
			Debug.Indent();
			var path = Path.Combine(this.outputPath, "objects", "mats");
			if (!Directory.Exists(path)) {
				Directory.CreateDirectory(path);
			}
			var serializer = new JavaScriptSerializer();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						m.Key AS key,
						m.IconID AS iconID,
						m.Name AS name,
						m.Description AS description,
						m.Rarity AS rarity
					FROM HDB_Mats AS m;
				";
				var reader = command.ExecuteReader();
				while (reader.Read()) {
					Debug.Write(".");
					var mat = new Dictionary<String, Object>();
					for (var i = 0; i < reader.FieldCount; i += 1) {
						if (reader.GetName(i) == "description") {
							var description = this.removeFormattingTags(Convert.ToString(reader["description"]));
							mat.Add(reader.GetName(i), description);
						}
						else {
							mat.Add(reader.GetName(i), reader[i]);
						}
					}
					var json = serializer.Serialize(mat);
					path = Path.Combine(this.outputPath, "objects", "mats");
					path = Path.Combine(path, Path.ChangeExtension(Convert.ToString(reader["key"]), "json"));
					File.WriteAllText(path, json);
				}
				Debug.WriteLine("");
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void ExportEquips() {
			Debug.WriteLine("ExportEquips() {");
			Debug.Indent();
			var path = Path.Combine(this.outputPath, "objects", "equips");
			if (!Directory.Exists(path)) {
				Directory.CreateDirectory(path);
			}
			var serializer = new JavaScriptSerializer();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						e.Key AS key,
						e.GroupKey AS groupKey,
						e.TypeKey AS typeKey,
						e.CategoryKey AS categoryKey,
						e.IconID AS iconID,
						e.Name AS name,
						e.Description AS description,
						e.Rarity AS rarity,
						e.SetKey AS setKey,
						e.SetName AS setName,
						e.RequiredSkillName AS requiredSkillName,
						e.RequiredSkillRank AS requiredSkillRank,
						e.RequiredLevel AS requiredLevel,
						e.ClassRestriction AS classRestriction,
						e.ATK AS atk,
						e.PATK AS patk,
						e.MATK AS matk,
						e.SPEED AS speed,
						e.CRIT AS crit,
						e.BAL AS bal,
						e.HP AS hp,
						e.DEF AS def,
						e.CRITRES AS critres,
						e.STR AS str,
						e.INT AS int,
						e.DEX AS dex,
						e.WILL AS will,
						e.STAMINA AS stamina
					FROM HDB_Equips AS e
					ORDER BY
						e.GroupKey,
						e.TypeKey,
						e.RequiredLevel DESC,
						e.Name;
				";
				var reader = command.ExecuteReader();
				var table = new DataTable();
				table.Load(reader);
				var equips = new List<Dictionary<String, Object>>();
				for (var i = 0; i < table.Rows.Count; i += 1) {
					var row = table.Rows[i];
					var equip = new Dictionary<String, Object>() {
						{ "key", row["key"] },
						{ "type", "equip" },
						{ "categoryKeys", new List<String>() { Convert.ToString(row["categoryKey"]) } },
						{ "iconID", row["iconID"] },
						{ "name", row["name"] },
						{ "rarity", row["rarity"] }
					};
					var properties = this.primaryProperties[Convert.ToString(row["groupKey"])][Convert.ToString(row["typeKey"])];
					if (properties.Length == 0) {
						equip.Add("description", this.removeFormattingTags(Convert.ToString(row["description"])));
					}
					foreach (var property in properties) {
						equip.Add(property, row[property]);
					}
					equips.Add(equip);
					var lastRow = table.Rows.Count - 1 == i;
					var sameType = lastRow || Convert.ToString(row["typeKey"]) == Convert.ToString(table.Rows[i + 1]["typeKey"]);
					if (lastRow || !sameType) {
						Debug.Write(".");
						var json = serializer.Serialize(equips);
						path = Path.Combine(this.outputPath, "objects");
						path = Path.Combine(path, Path.ChangeExtension(String.Concat(row["groupKey"], "-", row["typeKey"]), "json"));
						File.WriteAllText(path, json);
						equips = new List<Dictionary<String, Object>>();
					}
				}
				Debug.WriteLine("");
				foreach (DataRow row in table.Rows) {
					Debug.Write(".");
					var equip = new Dictionary<String, Object>();
					foreach (DataColumn column in table.Columns) {
						switch (column.ColumnName) {
							case "categoryKey":
								equip.Add("categoryKeys", new List<String>() { Convert.ToString(row["categoryKey"]) });
								break;
							case "description":
								var description = this.removeFormattingTags(Convert.ToString(row[column.ColumnName]));
								equip.Add(column.ColumnName, description);
								break;
							case "setKey":
								if (row["setKey"] == DBNull.Value) {
									equip.Add("set", null);
								}
								else {
									equip.Add("set", new Dictionary<String, Object>() {
										{ "key", row["setKey"] },
										{ "name", row["setName"] }
									});
								}
								break;
							case "setName":
								break;
							case "requiredSkillName":
								var skills = new List<Dictionary<String, Object>>();
								if (row["requiredSkillName"] != DBNull.Value) {
									skills.Add(new Dictionary<String, Object>() {
										{ "name", row["requiredSkillName"] },
										{ "rank", row["requiredSkillRank"] }
									});
								}
								equip.Add("requiredSkills", skills);
								break;
							case "requiredSkillRank":
								break;
							default:
								equip.Add(column.ColumnName, row[column.ColumnName]);
								break;
						}
					}
					if (this.equipRecipes.ContainsKey(Convert.ToString(row["key"]))) {
						equip.Add("recipes", this.equipRecipes[Convert.ToString(row["key"])]);
					}
					var json = serializer.Serialize(equip);
					path = Path.Combine(this.outputPath, "objects", "equips");
					path = Path.Combine(path, Path.ChangeExtension(Convert.ToString(row["key"]), "json"));
					File.WriteAllText(path, json);
				}
				Debug.WriteLine("");
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void ExportSets() {
			Debug.WriteLine("ExportSets() {");
			Debug.Indent();
			var path = Path.Combine(this.outputPath, "objects", "sets");
			if (!Directory.Exists(path)) {
				Directory.CreateDirectory(path);
			}
			var serializer = new JavaScriptSerializer();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						sc.SetKey AS setKey,
						sc.CategoryKey AS categoryKey
					FROM HDB_SetCategories AS sc
					ORDER BY
						sc.SetKey,
						sc.CategoryKey;
				";
				var reader = command.ExecuteReader();
				var categoryKeys = new Dictionary<String, List<String>>();
				while (reader.Read()) {
					var setKey = Convert.ToString(reader["setKey"]);
					if (!categoryKeys.ContainsKey(setKey)) {
						Debug.Write(".");
						categoryKeys.Add(setKey, new List<String>());
					}
					if (reader["categoryKey"] != DBNull.Value) {
						categoryKeys[setKey].Add(Convert.ToString(reader["categoryKey"]));
					}
				}
				Debug.WriteLine("");
				reader.Close();
				command.CommandText = @"
					SELECT
						sp.SetKey AS setKey,
						s.Name AS setName,
						sp.EquipKey AS equipKey,
						sp.EquipClassificationText AS equipClassificationText,
						sp.EquipIconID AS equipIconID,
						sp.EquipName AS equipName,
						sp.EquipRarity AS equipRarity,
						sp.EquipATK AS equipATK,
						sp.EquipPATK AS equipPATK,
						sp.EquipMATK AS equipMATK,
						sp.EquipSPEED AS equipSPEED,
						sp.EquipCRIT AS equipCRIT,
						sp.EquipBAL AS equipBAL,
						sp.EquipHP AS equipHP,
						sp.EquipDEF AS equipDEF,
						sp.EquipCRITRES AS equipCRITRES,
						sp.EquipSTR AS equipSTR,
						sp.EquipINT AS equipINT,
						sp.EquipDEX AS equipDEX,
						sp.EquipWILL AS equipWILL,
						sp.EquipSTAMINA AS equipSTAMINA,
						sp.Base AS base
					FROM HDB_SetParts AS sp
					INNER JOIN HDB_Sets AS s ON s.Key = sp.SetKey
					ORDER BY
						sp.SetKey,
						sp.[Order],
						sp.EquipName;
				";
				reader = command.ExecuteReader();
				var parts = new Dictionary<String, List<Dictionary<String, Object>>>();
				while (reader.Read()) {
					var setKey = Convert.ToString(reader["setKey"]);
					if (!parts.ContainsKey(setKey)) {
						Debug.Write(".");
						parts.Add(setKey, new List<Dictionary<String, Object>>());
					}
					var setNameParts = Convert.ToString(reader["setName"]).Split(new [] { ' ' });
					var equipNameShort = Convert.ToString(reader["equipName"]);
					for (var i = 0; i < setNameParts.Length; i += 1) {
						if (!equipNameShort.StartsWith(String.Concat(setNameParts[i], " "), StringComparison.InvariantCulture)) {
							break;
						}
						equipNameShort = equipNameShort.Substring(setNameParts[i].Length).Trim();
					}
					var equip = new Dictionary<String, Object>() {
						{ "key", reader["equipKey"] },
						{ "classificationText", reader["equipClassificationText"] },
						{ "iconID", reader["equipIconID"] },
						{ "name", reader["equipName"] },
						{ "nameShort", equipNameShort },
						{ "rarity", reader["equipRarity"] },
						{ "atk", reader["equipATK"] },
						{ "patk", reader["equipPATK"] },
						{ "matk", reader["equipMATK"] },
						{ "speed", reader["equipSPEED"] },
						{ "crit", reader["equipCRIT"] },
						{ "bal", reader["equipBAL"] },
						{ "hp", reader["equipHP"] },
						{ "def", reader["equipDEF"] },
						{ "critres", reader["equipCRITRES"] },
						{ "str", reader["equipSTR"] },
						{ "int", reader["equipINT"] },
						{ "dex", reader["equipDEX"] },
						{ "will", reader["equipWILL"] },
						{ "stamina", reader["equipSTAMINA"] },
						{ "base", Convert.ToInt32(reader["base"]) == 1 }
					};
					if (this.equipRecipes.ContainsKey(Convert.ToString(reader["equipKey"]))) {
						equip.Add("recipes", this.equipRecipes[Convert.ToString(reader["equipKey"])]);
					}
					parts[setKey].Add(equip);
				}
				reader.Close();
				command.CommandText = @"
					SELECT
						ss.SetKey AS setKey,
						ss.SkillName AS skillName,
						ss.SkillRank AS skillRank
					FROM HDB_SetSkills AS ss
					ORDER BY
						ss.SetKey,
						ss.SkillName;
				";
				reader = command.ExecuteReader();
				var requiredSkills = new Dictionary<String, List<Dictionary<String, Object>>>();
				while (reader.Read()) {
					var setKey = Convert.ToString(reader["setKey"]);
					if (!requiredSkills.ContainsKey(setKey)) {
						Debug.Write(".");
						requiredSkills.Add(setKey, new List<Dictionary<String, Object>>());
					}
					requiredSkills[setKey].Add(new Dictionary<String, Object>() {
						{ "name", reader["skillName"] },
						{ "rank", reader["skillRank"] }
					});
				}
				Debug.WriteLine("");
				reader.Close();
				command.CommandText = @"
					SELECT
						se.SetKey AS setKey,
						se.PartCount AS partCount,
						se.ATK AS atk,
						se.PATK AS patk,
						se.MATK AS matk,
						se.HP AS hp,
						se.DEF AS def,
						se.STR AS str,
						se.INT AS int,
						se.DEX AS dex,
						se.WILL AS will
					FROM HDB_SetEffects AS se
					ORDER BY
						se.SetKey,
						se.PartCount;
				";
				reader = command.ExecuteReader();
				var effects = new Dictionary<String, Dictionary<String, Dictionary<String, Object>>>();
				while (reader.Read()) {
					var setKey = Convert.ToString(reader["setKey"]);
					var partCount = Convert.ToString(reader["partCount"]);
					if (!effects.ContainsKey(setKey)) {
						Debug.Write(".");
						effects.Add(setKey, new Dictionary<String, Dictionary<String, Object>>());
					}
					if (!effects[setKey].ContainsKey(partCount)) {
						effects[setKey].Add(partCount, new Dictionary<String, Object>());
					}
					for (var i = 2; i < reader.FieldCount; i += 1) {
						if (Convert.ToInt32(reader[i]) > 0) {
							effects[setKey][partCount].Add(reader.GetName(i), reader[i]);
						}
					}
				}
				Debug.WriteLine("");
				reader.Close();
				command.CommandText = @"
					SELECT
						s.Key AS key,
						s.GroupKey AS groupKey,
						s.TypeKey AS typeKey,
						s.IconID AS iconID,
						s.Name AS name,
						s.Rarity AS rarity,
						s.RequiredLevel AS requiredLevel,
						s.ClassRestriction AS classRestriction,
						s.ATK AS atk,
						s.PATK AS patk,
						s.MATK AS matk,
						s.SPEED AS speed,
						s.CRIT AS crit,
						s.BAL AS bal,
						s.HP AS hp,
						s.DEF AS def,
						s.CRITRES AS critres,
						s.STR AS str,
						s.INT AS int,
						s.DEX AS dex,
						s.WILL AS will,
						s.STAMINA AS stamina
					FROM HDB_Sets AS s
					ORDER BY
						s.GroupKey,
						s.TypeKey,
						s.RequiredLevel DESC,
						s.Name;
				";
				reader = command.ExecuteReader();
				var table = new DataTable();
				table.Load(reader);
				var sets = new List<Dictionary<String, Object>>();
				for (var i = 0; i < table.Rows.Count; i += 1) {
					var row = table.Rows[i];
					if (row["groupKey"] == DBNull.Value) {
						continue;
					}
					var set = new Dictionary<String, Object>() {
						{ "key", row["key"] },
						{ "type", "set" },
						{ "categoryKeys", categoryKeys[Convert.ToString(row["key"])] },
						{ "iconID", row["iconID"] },
						{ "name", row["name"] },
						{ "rarity", row["rarity"] }
					};
					var properties = this.primaryProperties[Convert.ToString(row["groupKey"])][Convert.ToString(row["typeKey"])];
					foreach (var property in properties) {
						set.Add(property, row[property]);
					}
					sets.Add(set);
					var lastRow = table.Rows.Count - 1 == i;
					var sameType = lastRow || Convert.ToString(row["typeKey"]) == Convert.ToString(table.Rows[i + 1]["typeKey"]);
					if (lastRow || !sameType) {
						Debug.Write(".");
						var json = serializer.Serialize(sets);
						path = Path.Combine(this.outputPath, "objects");
						path = Path.Combine(path, Path.ChangeExtension(String.Concat(row["groupKey"], "-", row["typeKey"]), "json"));
						File.WriteAllText(path, json);
						sets = new List<Dictionary<String, Object>>();
					}
				}
				Debug.WriteLine("");
				foreach (DataRow row in table.Rows) {
					Debug.Write(".");
					var set = new Dictionary<String, Object>();
					var key = Convert.ToString(row["key"]);
					foreach (DataColumn column in table.Columns) {
						set.Add(column.ColumnName, row[column.ColumnName]);
					}
					set.Add("categoryKeys", categoryKeys[key]);
					set.Add("parts", parts[key]);
					set.Add("requiredSkills", requiredSkills.ContainsKey(key) ? requiredSkills[key] : new List<Dictionary<String, Object>>());
					set.Add("effects", effects[key]);
					var json = serializer.Serialize(set);
					path = Path.Combine(this.outputPath, "objects", "sets");
					path = Path.Combine(path, Path.ChangeExtension(key, "json"));
					File.WriteAllText(path, json);
				}
				Debug.WriteLine("");
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

	}

}
