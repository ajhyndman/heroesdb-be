
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml;
using System;

namespace HeroesDB {

	public class Exporter {

		private Config config;

		private Dictionary<String, Dictionary<String, String[]>> _primaryProperties;
		private Dictionary<String, Dictionary<String, String[]>> primaryProperties {
			get {
				if (this._primaryProperties == null) {
					Debug.WriteLine("primaryProperties { get {");
					Debug.Indent();
					var properties = new Dictionary<String, Dictionary<String, String[]>>();
					using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
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

		public Exporter(Config config) {
			Debug.WriteLine("Exporter() {");
			Debug.Indent();
			this.config = config;
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
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
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
				var path = Path.Combine(this.config.ExportPath, "characters.json");
				File.WriteAllText(path, json);
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void ExportClassification() {
			Debug.WriteLine("ExportClassification() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
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
				var path = Path.Combine(this.config.ExportPath, "classification.json");
				File.WriteAllText(path, json);
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void ExportQualityTypes() {
			Debug.WriteLine("ExportQualityTypes() {");
			Debug.Indent();
			var path = Path.Combine(this.config.ExportPath, "quality-types");
			if (!Directory.Exists(path)) {
				Directory.CreateDirectory(path);
			}
			var serializer = new JavaScriptSerializer();
			Action<String, Dictionary<String, Dictionary<String, Object>>> serialize = (key, qualityType) => {
				var json = serializer.Serialize(qualityType);
				path = Path.Combine(this.config.ExportPath, "quality-types");
				path = Path.Combine(path, Path.ChangeExtension(key, "json"));
				File.WriteAllText(path, json);
			};
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						q.Key AS key,
						q.Level AS level,
						q.Property AS property,
						q.Improvement AS improvement
					FROM HDB_QualityTypes AS q
					ORDER BY
						q.Key,
						q.Level,
						q.Property;
				";
				var reader = command.ExecuteReader();
				var previousKey = "";
				var qualityType = new Dictionary<String, Dictionary<String, Object>>();
				while (reader.Read()) {
					var key = Convert.ToString(reader["key"]);
					if (key != previousKey) {
						Debug.Write(".");
						if (qualityType.Count > 0) {
							serialize(previousKey, qualityType);
							qualityType.Clear();
						}
						previousKey = key;
					}
					var level = Convert.ToString(reader["level"]);
					if (!qualityType.ContainsKey(level)) {
						qualityType.Add(level, new Dictionary<String, Object>());
					}
					qualityType[level].Add(Convert.ToString(reader["property"]), reader["improvement"]);
				}
				if (qualityType.Count > 0) {
					serialize(previousKey, qualityType);
				}
				Debug.WriteLine("");
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void ExportEnhanceTypes() {
			Debug.WriteLine("ExportEnhanceTypes() {");
			Debug.Indent();
			var path = Path.Combine(this.config.ExportPath, "enhance-types");
			if (!Directory.Exists(path)) {
				Directory.CreateDirectory(path);
			}
			var serializer = new JavaScriptSerializer();
			Action<String, Dictionary<String, Object>> serialize = (key, enhanceType) => {
				var json = serializer.Serialize(enhanceType);
				path = Path.Combine(this.config.ExportPath, "enhance-types");
				path = Path.Combine(path, Path.ChangeExtension(key, "json"));
				File.WriteAllText(path, json);
			};
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						e.Key AS key,
						e.Level AS level,
						e.Chance AS chance,
						e.Risk AS risk,
						e.Mat1Key AS mat1Key,
						e.Mat1IconKey AS mat1IconKey,
						e.Mat1Name AS mat1Name,
						e.Mat1Rarity AS mat1Rarity,
						e.Mat1Count AS mat1Count,
						e.Mat2Key AS mat2Key,
						e.Mat2IconKey AS mat2IconKey,
						e.Mat2Name AS mat2Name,
						e.Mat2Rarity AS mat2Rarity,
						e.Mat2Count AS mat2Count,
						e.Mat3Key AS mat3Key,
						e.Mat3IconKey AS mat3IconKey,
						e.Mat3Name AS mat3Name,
						e.Mat3Rarity AS mat3Rarity,
						e.Mat3Count AS mat3Count,
						e.Property AS property,
						e.Improvement AS improvement
					FROM HDB_EnhanceTypes AS e
					ORDER BY
						e.Key,
						e.Level,
						e.Property;
				";
				var reader = command.ExecuteReader();
				var previousKey = "";
				var enhanceType = new Dictionary<String, Object>();
				while (reader.Read()) {
					var key = Convert.ToString(reader["key"]);
					if (key != previousKey) {
						Debug.Write(".");
						if (enhanceType.Count > 0) {
							serialize(previousKey, enhanceType);
							enhanceType.Clear();
						}
						previousKey = key;
						var mats = new Dictionary<String, Object> {};
						mats.Add(Convert.ToString(reader["mat1Key"]), new Dictionary<String, Object>() {
							{ "iconKey", reader["mat1IconKey"] },
							{ "name", reader["mat1Name"] },
							{ "rarity", reader["mat1Rarity"] },
							{ "order", 3 }
						});
						mats.Add(Convert.ToString(reader["mat2Key"]), new Dictionary<String, Object>() {
							{ "iconKey", reader["mat2IconKey"] },
							{ "name", reader["mat2Name"] },
							{ "rarity", reader["mat2Rarity"] },
							{ "order", 2 }
						});
						mats.Add(Convert.ToString(reader["mat3Key"]), new Dictionary<String, Object>() {
							{ "iconKey", reader["mat3IconKey"] },
							{ "name", reader["mat3Name"] },
							{ "rarity", reader["mat3Rarity"] },
							{ "order", 1 }
						});
						enhanceType.Add("mats", mats);
						enhanceType.Add("keys", new List<String>());
					}
					var level = Convert.ToString(reader["level"]);
					if (!enhanceType.ContainsKey(level)) {
						((List<String>)enhanceType["keys"]).Add(level);
						var mats = new Dictionary<String, Object>() {
							{ Convert.ToString(reader["mat1Key"]), reader["mat1Count"] },
							{ Convert.ToString(reader["mat2Key"]), reader["mat2Count"] },
							{ Convert.ToString(reader["mat3Key"]), reader["mat3Count"] }
						};
						enhanceType.Add(level, new Dictionary<String, Object>() {
							{ "level", reader["level"] },
							{ "chance", reader["chance"] },
							{ "risk", reader["risk"] },
							{ "mats", mats }
						});
					}
					((Dictionary<String, Object>)enhanceType[level]).Add(Convert.ToString(reader["property"]), reader["improvement"]);
				}
				if (enhanceType.Count > 0) {
					serialize(previousKey, enhanceType);
				}
				Debug.WriteLine("");
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void ExportEnchants() {
			Debug.WriteLine("ExportEnchants() {");
			Debug.Indent();
			var path = Path.Combine(this.config.ExportPath, "objects", "enchants");
			if (!Directory.Exists(path)) {
				Directory.CreateDirectory(path);
			}
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						e.Key AS key,
						e.Name AS name,
						e.Prefix AS prefix,
						e.Rank AS rank,
						er.GroupKey AS groupKey,
						er.TypeKey AS typeKey,
						er.CategoryKey AS categoryKey,
						er.EquipKey AS equipKey
					FROM HDB_Enchants AS e
					INNER JOIN HDB_EnchantRestrictions AS er ON er.EnchantKey = e.Key
					ORDER BY
						e.Level DESC,
						e.Name,
						e.Key;
				";
				var reader = command.ExecuteReader();
				var enchants = new List<Dictionary<String, Object>>();
				Dictionary<String, Object> enchant = null;
				while (reader.Read()) {
					if (enchant == null || Convert.ToString(enchant["key"]) != Convert.ToString(reader["key"])) {
						if (enchant != null) {
							Debug.Write(".");
							enchants.Add(enchant);
						}
						enchant = new Dictionary<String, Object>() {
							{ "key", reader["key"] },
							{ "name", reader["name"] },
							{ "prefix", Convert.ToBoolean(reader["prefix"]) },
							{ "rank", reader["rank"] },
							{ "restrictions", new List<Object>() }
						};
					}
					var restriction = new String[4];
					restriction[0] = Convert.ToString(reader["groupKey"]);
					if (reader["typeKey"] != DBNull.Value) {
						restriction[1] = Convert.ToString(reader["typeKey"]);
					}
					if (reader["categoryKey"] != DBNull.Value) {
						restriction[2] = Convert.ToString(reader["categoryKey"]);
					}
					if (reader["equipKey"] != DBNull.Value) {
						restriction[3] = Convert.ToString(reader["equipKey"]);
					}
					var restrictions = (List<Object>)enchant["restrictions"];
					restrictions.Add(restriction);
				}
				if (enchant != null) {
					Debug.Write(".");
					enchants.Add(enchant);
				}
				reader.Close();
				var serializer = new JavaScriptSerializer();
				var json = serializer.Serialize(enchants);
				path = Path.Combine(this.config.ExportPath, "objects", "enchants.json");
				File.WriteAllText(path, json);
				Debug.WriteLine("");
				Action<Dictionary<String, Object>> serialize = (enchantObject) => {
					json = serializer.Serialize(enchantObject);
					path = Path.Combine(this.config.ExportPath, "objects", "enchants");
					path = Path.Combine(path, Path.ChangeExtension(Convert.ToString(enchantObject["key"]), "json"));
					File.WriteAllText(path, json);
				};
				command.CommandText = @"
					SELECT
						e.Key AS key,
						e.Name AS name,
						e.Prefix AS prefix,
						e.Rank AS rank,
						e.RestrictionsText AS restrictionsText,
						e.MinSuccessChance AS minSuccessChance,
						e.MaxSuccessChance AS maxSuccessChance,
						e.BreakChance AS breakChance,
						ep.Property AS property,
						ep.Value AS value,
						ep.Condition AS condition
					FROM HDB_Enchants AS e
					INNER JOIN HDB_EnchantProperties AS ep ON ep.EnchantKey = e.Key
					ORDER BY
						e.Key,
						CASE WHEN ep.Condition IS NULL THEN 1 ELSE 2 END,
						ep.[Order];
				";
				reader = command.ExecuteReader();
				enchant = null;
				while (reader.Read()) {
					if (enchant == null || Convert.ToString(reader["key"]) != Convert.ToString(enchant["key"])) {
						Debug.Write(".");
						if (enchant != null) {
							serialize(enchant);
						}
						enchant = new Dictionary<String, Object>() {
							{ "key", reader["key"] },
							{ "name", reader["name"] },
							{ "prefix", Convert.ToBoolean(reader["prefix"]) },
							{ "rank", reader["rank"] },
							{ "restrictionsText", reader["restrictionsText"] },
							{ "minSuccessChance", reader["minSuccessChance"] },
							{ "maxSuccessChance", reader["maxSuccessChance"] },
							{ "breakChance", reader["breakChance"] },
							{ "properties", new List<Dictionary<String, Object>>() }
						};
					}
					var properties = (List<Dictionary<String, Object>>)enchant["properties"];
					properties.Add(new Dictionary<String, Object>() {
						{ "key", Convert.ToString(reader["property"]) },
						{ "value", reader["value"] },
						{ "condition", reader["condition"] }
					});
				}
				if (enchant != null) {
					serialize(enchant);
				}
				Debug.WriteLine("");
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void ExportIcons() {
			Debug.WriteLine("ExportIcons() {");
			Debug.Indent();
			var path = Path.Combine(this.config.ExportPath, "icons");
			Directory.CreateDirectory(path);
			foreach (var file in new List<String>() { "blank", "enchant_scroll" }) {
				var icon = Paloma.TargaImage.LoadTargaImage(Path.Combine(this.config.IconImportPath, Path.ChangeExtension(file, "tga")));
				icon.Save(Path.Combine(this.config.ExportPath, "icons", Path.ChangeExtension(file, "png")));
			}
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						i.Key AS key,
						i.Icon AS icon,
						i.Material1 AS material1,
						i.Material2 AS material2,
						i.Material3 AS material3
					FROM HDB_Icons AS i
					LEFT JOIN HDB_Equips AS e ON e.IconKey = i.Key
					LEFT JOIN HDB_Sets AS s ON s.IconKey = i.Key
					LEFT JOIN HDB_Mats AS m ON m.IconKey = i.Key
					WHERE COALESCE(e.ID, s.ID, m.ID) IS NOT NULL;
				";
				var reader = command.ExecuteReader();
				while (reader.Read()) {
					var iconFileName = String.Concat(reader["icon"], ".tga");
					var iconFile = Path.Combine(this.config.IconImportPath, iconFileName);
					if (File.Exists(iconFile)) {
						Debug.Write(".");
						var icon = Paloma.TargaImage.LoadTargaImage(iconFile);
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
									var material1Key = Convert.ToString(reader["material1"]);
									var material2Key = Convert.ToString(reader["material2"]);
									var material3Key = Convert.ToString(reader["material3"]);
									var material1Index = 0;
									var material2Index = material2Key == material1Key ? 1 : 0;
									var material3Index = material3Key == material1Key && material3Key == material2Key ? 2 : (material3Key == material1Key || material3Key == material2Key ? 1 : 0);
									if (reader["material1"] != DBNull.Value && color.R > color.G && color.R > color.B) {
										baseColor = this.config.Materials[material1Key][material1Index];
										brightness = Convert.ToDouble(color.R) / 255;
									}
									else if (reader["material2"] != DBNull.Value && color.G > color.R && color.G > color.B) {
										baseColor = this.config.Materials[material2Key][material2Index];
										brightness = Convert.ToDouble(color.G) / 255;
									}
									else if (reader["material3"] != DBNull.Value && color.B > color.R && color.B > color.G) {
										baseColor = this.config.Materials[material3Key][material3Index];
										brightness = Convert.ToDouble(color.B) / 255;
									}
									if (baseColor != color) {
										var newColor = Color.FromArgb(Convert.ToInt32(baseColor.R * brightness), Convert.ToInt32(baseColor.G * brightness), Convert.ToInt32(baseColor.B * brightness));
										icon.SetPixel(x, y, newColor);
									}
								}
							}
						}
						icon.Save(Path.Combine(this.config.ExportPath, "icons", String.Concat(reader["key"], ".png")));
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
			var path = Path.Combine(this.config.ExportPath, "objects", "mats");
			if (!Directory.Exists(path)) {
				Directory.CreateDirectory(path);
			}
			var serializer = new JavaScriptSerializer();
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						m.Key AS key,
						m.IconKey AS iconKey,
						m.Name AS name,
						m.Classification AS classification,
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
					path = Path.Combine(this.config.ExportPath, "objects", "mats");
					path = Path.Combine(path, Path.ChangeExtension(Convert.ToString(reader["key"]), "json"));
					File.WriteAllText(path, json);
				}
				Debug.WriteLine("");
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void ExportScreenshots() {
			Debug.WriteLine("ExportScreenshots() {");
			Debug.Indent();
			foreach (var objectTypeDirectory in new [] { "equips", "sets" }) {
				Debug.WriteLine(objectTypeDirectory);
				var inputPath = Path.Combine(this.config.RootPath, "screenshots", objectTypeDirectory);
				var outputPath = Path.Combine(this.config.ExportPath, "screenshots", objectTypeDirectory);
				if (!Directory.Exists(outputPath)) {
					Directory.CreateDirectory(outputPath);
				}
				var command = String.Format(@"
					FOR %%f IN (*.jpeg) DO (
						""{0}"" ^
							%%f ^
							-shave 500x0 ^
							-colorspace RGB ^
							-filter LanczosRadius ^
							-distort Resize 1000 ^
							-colorspace sRGB ^
							..\overlay.png ^
							-gravity southeast ^
							-composite ^
							""{2}\%%f""
					)
					""{1}"" ^
					-overwrite_original ^
					-iptc:source=""HeroesDB.net"" ^
					{2}
				", this.config.ImageMagickConvert, this.config.ExifTool, outputPath);
				var commandFile = Path.Combine(this.config.RootPath, "screenshots", "command.bat");
				File.WriteAllText(commandFile, command);
				var process = new Process();
				process.StartInfo.FileName = commandFile;
				process.StartInfo.WorkingDirectory = inputPath;
				process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				process.StartInfo.Arguments = "/C";
				process.Start();
				process.WaitForExit();
				File.Delete(commandFile);
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void ExportEquips() {
			Debug.WriteLine("ExportEquips() {");
			Debug.Indent();
			var path = Path.Combine(this.config.ExportPath, "objects", "equips");
			if (!Directory.Exists(path)) {
				Directory.CreateDirectory(path);
			}
			var serializer = new JavaScriptSerializer();
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				var shops = new Dictionary<String, List<Dictionary<String, Object>>>();
				command.CommandText = @"
					SELECT
						ers.EquipKey AS equipKey,
						ers.ShopKey AS shopKey,
						ers.ShopName AS shopName
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
						{ "name", reader["shopName"] }
					});
				}
				Debug.WriteLine("");
				reader.Close();
				var recipes = new Dictionary<String, Dictionary<String, Dictionary<String, Object>>>();
				command.CommandText = @"
					SELECT
						er.EquipKey AS equipKey,
						er.Type AS type,
						m.Key AS matKey,
						m.IconKey AS matIconKey,
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
				reader = command.ExecuteReader();
				while (reader.Read()) {
					var equipKey = Convert.ToString(reader["equipKey"]);
					var type = Convert.ToString(reader["type"]);
					if (!recipes.ContainsKey(equipKey)) {
						Debug.Write(".");
						recipes.Add(equipKey, new Dictionary<String, Dictionary<String, Object>>());
					}
					if (!recipes[equipKey].ContainsKey(type)) {
						recipes[equipKey].Add(type, new Dictionary<String, Object>());
						if (type == "npc") {
							var appearQuestNames = reader["appearQuestName"] == DBNull.Value ? new String[0] : new [] { Convert.ToString(reader["appearQuestName"]) };
							recipes[equipKey][type].Add("appearQuestNames", appearQuestNames);
							recipes[equipKey][type].Add("shops", shops[equipKey]);
						}
						else if (type == "pc") {
							var expertises = new List<Object>();
							if (reader["expertiseName"] != DBNull.Value) {
								expertises.Add(new Dictionary<String, Object>() {
									{"name", reader["expertiseName"] },
									{"experienceRequired", reader["expertiseExperienceRequired"] }
								});
							}
							recipes[equipKey][type].Add("expertises", expertises);
						}
					}
					if (!recipes[equipKey][type].ContainsKey("mats")) {
						recipes[equipKey][type].Add("mats", new List<Dictionary<String, Object>>());
					}
					((List<Dictionary<String, Object>>)recipes[equipKey][type]["mats"]).Add(new Dictionary<String, Object>() {
						{ "key", reader["matKey"] },
						{ "iconKey", reader["matIconKey"] },
						{ "name", reader["matName"] },
						{ "rarity", reader["matRarity"] },
						{ "order", reader["matOrder"] },
						{ "count", reader["matCount"] }
					});
				}
				Debug.WriteLine("");
				reader.Close();
				command.CommandText = @"
					SELECT
						es.EquipKey AS equipKey,
						es.CharacterID AS characterID,
						es.Camera AS camera
					FROM HDB_EquipScreenshots AS es
					INNER JOIN HDB_Equips AS e ON e.Key = es.EquipKey
					WHERE es.Ready = 1
					ORDER BY
						es.EquipKey,
						CASE
							WHEN e.MATK > e.PATK OR e.INT > e.STR THEN CASE WHEN es.CharacterID IN (4, 256) THEN 1 ELSE 2 END
							ELSE CASE WHEN es.CharacterID IN (4, 256) THEN 2 ELSE 1 END
						END,
						es.CharacterID,
						CASE SUBSTR(es.Camera, 2, 2) WHEN 'df' THEN 1 WHEN 'fr' THEN 2 WHEN 'lf' THEN 3 WHEN 'dr' THEN 4 WHEN 'dl' THEN 5 WHEN 'rb' THEN 6 WHEN 'bl' THEN 7 WHEN 'db' THEN 8 END;
				";
				reader = command.ExecuteReader();
				var screenshots = new Dictionary<String, List<String>>();
				while (reader.Read()) {
					var equipKey = Convert.ToString(reader["equipKey"]);
					var screenshotFilename = String.Concat(reader["equipKey"], "_", reader["characterID"], reader["camera"], ".jpeg");
					if (File.Exists(Path.Combine(this.config.ExportPath, "screenshots", "equips", screenshotFilename))) {
						if (!screenshots.ContainsKey(equipKey)) {
							Debug.Write(".");
							screenshots.Add(equipKey, new List<String>());
						}
						screenshots[equipKey].Add(screenshotFilename);
					}
				}
				Debug.WriteLine("");
				reader.Close();
				command.CommandText = @"
					SELECT
						e.Key AS key,
						e.GroupKey AS groupKey,
						e.TypeKey AS typeKey,
						e.CategoryKey AS categoryKey,
						e.IconKey AS iconKey,
						e.Name AS name,
						e.Classification AS classification,
						e.Description AS description,
						e.Rarity AS rarity,
						e.QualityTypeKey AS qualityTypeKey,
						e.EnhanceTypeKey AS enhanceTypeKey,
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
						e.STAMINA AS stamina,
						e.DURABILITY AS durability,
						e.WEIGHT AS weight
					FROM HDB_Equips AS e
					ORDER BY
						e.GroupKey,
						e.TypeKey,
						e.RequiredLevel DESC,
						e.Name;
				";
				reader = command.ExecuteReader();
				var table = new DataTable();
				table.Load(reader);
				var equips = new List<Dictionary<String, Object>>();
				for (var i = 0; i < table.Rows.Count; i += 1) {
					var row = table.Rows[i];
					var equip = new Dictionary<String, Object>() {
						{ "key", row["key"] },
						{ "type", "equip" },
						{ "categoryKeys", new List<String>() { Convert.ToString(row["categoryKey"]) } },
						{ "iconKey", row["iconKey"] },
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
						path = Path.Combine(this.config.ExportPath, "objects");
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
					var key = Convert.ToString(row["key"]);
					equip.Add("recipes", recipes.ContainsKey(key) ? recipes[key] : null);
					equip.Add("screenshots", screenshots.ContainsKey(key) ? screenshots[key] : new List<String>());
					var json = serializer.Serialize(equip);
					path = Path.Combine(this.config.ExportPath, "objects", "equips");
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
			var path = Path.Combine(this.config.ExportPath, "objects", "sets");
			if (!Directory.Exists(path)) {
				Directory.CreateDirectory(path);
			}
			var serializer = new JavaScriptSerializer();
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
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
						sp.EquipKey AS equipKey,
						sp.EquipName AS equipName,
						sp.Base AS base
					FROM HDB_SetParts AS sp
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
					var equip = new Dictionary<String, Object>() {
						{ "key", reader["equipKey"] },
						{ "name", reader["equipName"] },
						{ "base", Convert.ToInt32(reader["base"]) == 1 }
					};
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
						ss.SetKey AS setKey,
						ss.CharacterID AS characterID,
						ss.Camera AS camera
					FROM HDB_SetScreenshots AS ss
					INNER JOIN HDB_Sets AS s ON s.Key = ss.SetKey
					WHERE ss.ready = 1
					ORDER BY
						ss.SetKey,
						CASE
							WHEN s.MATK > s.PATK OR s.INT > s.STR THEN CASE WHEN ss.CharacterID IN (4, 256) THEN 1 ELSE 2 END
							ELSE CASE WHEN ss.CharacterID IN (4, 256) THEN 2 ELSE 1 END
						END,
						ss.CharacterID,
						CASE SUBSTR(ss.Camera, 2, 2) WHEN 'df' THEN 1 WHEN 'fr' THEN 2 WHEN 'lf' THEN 3 WHEN 'dr' THEN 4 WHEN 'dl' THEN 5 WHEN 'rb' THEN 6 WHEN 'bl' THEN 7 WHEN 'db' THEN 8 END;
				";
				reader = command.ExecuteReader();
				var screenshots = new Dictionary<String, List<String>>();
				while (reader.Read()) {
					var screenshotFilename = String.Concat(reader["setKey"], "_", reader["characterID"], reader["camera"], ".jpeg");
					if (File.Exists(Path.Combine(this.config.ExportPath, "screenshots", "sets", screenshotFilename))) {
						var setKey = Convert.ToString(reader["setKey"]);
						if (!screenshots.ContainsKey(setKey)) {
							Debug.Write(".");
							screenshots.Add(setKey, new List<String>());
						}
						screenshots[setKey].Add(screenshotFilename);
					}
				}
				Debug.WriteLine("");
				reader.Close();
				command.CommandText = @"
					SELECT
						s.Key AS key,
						s.GroupKey AS groupKey,
						s.TypeKey AS typeKey,
						s.IconKey AS iconKey,
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
						s.STAMINA AS stamina,
						s.WEIGHT AS weight
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
						{ "iconKey", row["iconKey"] },
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
						path = Path.Combine(this.config.ExportPath, "objects");
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
					set.Add("screenshots", screenshots.ContainsKey(key) ? screenshots[key] : new List<String>());
					var json = serializer.Serialize(set);
					path = Path.Combine(this.config.ExportPath, "objects", "sets");
					path = Path.Combine(path, Path.ChangeExtension(key, "json"));
					File.WriteAllText(path, json);
				}
				Debug.WriteLine("");
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void ExportSitemap() {
			Debug.WriteLine("ExportSitemap() {");
			Debug.Indent();
			using (var xml = XmlWriter.Create(Path.Combine(this.config.WwwPath, "sitemap.xml"))) {
				xml.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
				Action<String, DateTime, Double> writeUrl = (loc, lastmod, priority) => {
					xml.WriteStartElement("url");
					xml.WriteElementString("loc", String.Concat("http://www.heroesdb.net/", loc));
					xml.WriteElementString("lastmod", XmlConvert.ToString(lastmod, XmlDateTimeSerializationMode.Utc));
					xml.WriteElementString("changefreq", "monthly");
					xml.WriteElementString("priority", XmlConvert.ToString(priority));
					xml.WriteEndElement();
				};
				var url = "";
				var urlFile = Path.Combine(this.config.ExportPath, "classification.json");
				writeUrl(url, File.GetLastWriteTime(urlFile), 1);
				Debug.WriteLine(".");
				using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
					connection.Open();
					var command = connection.CreateCommand();
					command.CommandText = @"
						SELECT DISTINCT
							c.GroupKey,
							c.TypeKey
						FROM HDB_Classification AS c
						WHERE c.GroupKey IS NOT NULL
						ORDER BY
							c.GroupOrder,
							c.GroupName,
							c.TypeOrder,
							c.TypeName;
					";
					var reader = command.ExecuteReader();
					while (reader.Read()) {
						url = String.Concat("items", "/", reader["GroupKey"], "/", reader["TypeKey"]);
						urlFile = Path.Combine(this.config.ExportPath, "objects", Path.ChangeExtension(String.Concat(reader["GroupKey"], "-", reader["TypeKey"]), ".json"));
						writeUrl(url, File.GetLastWriteTime(urlFile), 0.9);
						Debug.Write(".");
					}
					Debug.WriteLine("");
					reader.Close();
					command.CommandText = @"
						SELECT DISTINCT
							c.GroupKey,
							c.TypeKey,
							c.CategoryKey
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
					reader = command.ExecuteReader();
					while (reader.Read()) {
						url = String.Concat("items", "/", reader["GroupKey"], "/", reader["TypeKey"], "/", reader["CategoryKey"]);
						urlFile = Path.Combine(this.config.ExportPath, "objects", Path.ChangeExtension(String.Concat(reader["GroupKey"], "-", reader["TypeKey"]), ".json"));
						writeUrl(url, File.GetLastWriteTime(urlFile), 0.8);
						Debug.Write(".");
					}
					Debug.WriteLine("");
					reader.Close();
					command.CommandText = @"
						SELECT
							e.Key,
							e.GroupKey,
							e.TypeKey,
							e.CategoryKey
						FROM HDB_Equips AS e
						ORDER BY
							e.GroupKey,
							e.TypeKey,
							e.RequiredLevel DESC,
							e.Name;
					";
					reader = command.ExecuteReader();
					while (reader.Read()) {
						url = String.Concat("items", "/", reader["GroupKey"], "/", reader["TypeKey"], "/", reader["CategoryKey"], "/", reader["Key"], ".equip");
						urlFile = Path.Combine(this.config.ExportPath, "objects", "equips", Path.ChangeExtension(Convert.ToString(reader["Key"]), ".json"));
						writeUrl(url, File.GetLastWriteTime(urlFile), 0.7);
						Debug.Write(".");
					}
					Debug.WriteLine("");
					reader.Close();
					command.CommandText = @"
						SELECT
							s.Key,
							s.GroupKey,
							s.TypeKey
						FROM HDB_Sets AS s
						ORDER BY
							s.GroupKey,
							s.TypeKey,
							s.RequiredLevel DESC,
							s.Name;
					";
					reader = command.ExecuteReader();
					while (reader.Read()) {
						url = String.Concat("items", "/", reader["GroupKey"], "/", reader["TypeKey"], "/", reader["Key"], ".set");
						urlFile = Path.Combine(this.config.ExportPath, "objects", "sets", Path.ChangeExtension(Convert.ToString(reader["Key"]), ".json"));
						writeUrl(url, File.GetLastWriteTime(urlFile), 0.7);
						Debug.Write(".");
					}
					Debug.WriteLine("");
				}
				xml.WriteEndElement();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

	}

}
