
using System.Collections.Generic;
using System.Configuration;
using System.Data.SQLite;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System;

namespace HeroesDB {

	public class Setuper {

		private String connectionString;

		private List<String> enabledFeatures;

		public Setuper(String databaseFile) {
			Debug.WriteLine("Setuper({0}) {{", new [] { databaseFile });
			Debug.Indent();
			this.connectionString = String.Format("Data Source={0}; Version=3;", databaseFile);
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		private void loadFeatures() {
			var region = ConfigurationManager.AppSettings["Region"];
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = String.Format(@"
					SELECT f.Feature
					FROM FeatureMatrix AS f
					WHERE f.[{0}] IS NOT NULL;
				", region);
				var reader = command.ExecuteReader();
				this.enabledFeatures = new List<String>();
				while (reader.Read()) {
					this.enabledFeatures.Add(reader["Feature"].ToString());
				}
			}
		}

		private Boolean isFeatured(String features) {
			if (this.enabledFeatures == null) {
				this.loadFeatures();
			}
			var test = new Regex(@"^(?<item>\s*(((!)?\w+)|(\|\|)|(&&))\s*)+$");
			var match = test.Match(features.Trim());
			var captures = match.Groups["item"].Captures;
			var featured = true;
			for (var i = 0; i < captures.Count; i += 1) {
				var item = captures[i].Value.Trim();
				if (item == "&&" || item == "||") {
					continue;
				}
				var disabledFeature = item.StartsWith("!", StringComparison.InvariantCulture);
				var existingFeature = this.enabledFeatures.Contains(item.Replace("!", ""));
				var enabledFeature = (!disabledFeature && existingFeature) || (disabledFeature && !existingFeature);
				if (i == 0) {
					featured = enabledFeature;
				}
				else {
					var previousItem = captures[i - 1].Value.Trim();
					if (previousItem == "||") {
						featured = featured || enabledFeature;
					}
					else if (previousItem == "&&") {
						featured = featured && enabledFeature;
					}
				}
			}
			return featured;
		}

		public void ImportText(String textFile) {
			Debug.WriteLine("ImportText({0}) {{", new [] { textFile });
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_Text;
					CREATE TABLE HDB_Text (
						Key NVARCHAR PRIMARY KEY,
						Text NVARCHAR NOT NULL
					);
				";
				command.ExecuteNonQuery();
				using (var file = File.OpenText(textFile)) {
					var test = new Regex(@"^\s*""(?<key>.*)""\s*""(?<text>.*)""\s*$");
					command.CommandText = "INSERT INTO HDB_Text VALUES (@Key, @Text);";
					command.Parameters.Add("@Key", DbType.String, 250);
					command.Parameters.Add("@Text", DbType.String, 5000);
					while (!file.EndOfStream) {
						var line = file.ReadLine();
						var match = test.Match(line);
						if (match.Success) {
							command.Parameters["@Key"].Value = match.Groups["key"].Value;
							command.Parameters["@Text"].Value = match.Groups["text"].Value;
							command.ExecuteNonQuery();
						}
					}
				}
				transaction.Commit();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetCharacters() {
			Debug.WriteLine("SetCharacters() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_Characters;
					CREATE TABLE HDB_Characters (
						ID INT PRIMARY KEY,
						Name NVARCHAR NOT NULL,
						Description NVARCHAR NOT NULL
					);
					INSERT INTO HDB_Characters
					SELECT
						CASE tn.Text WHEN 'Lann' THEN 1 WHEN 'Fiona' THEN 2 WHEN 'Evie' THEN 4 WHEN 'Karok' THEN 8 WHEN 'Karok' THEN 8 WHEN 'Kai' THEN 16 WHEN 'Vella' THEN 32 WHEN 'Hurk' THEN 64 WHEN 'Lynn' THEN 128 WHEN 'Arisha' THEN 256 END AS ID,
						tn.Text AS Name,
						td.Text AS Description
					FROM HDB_Text AS tn
					LEFT JOIN HDB_Text AS td ON
						td.Key LIKE 'GAMEUI_HEROES_NEWCHARACTERDIALOG_TYPE_COMMENT_%' AND
						REPLACE(td.Key, 'COMMENT_', '') = tn.Key
					WHERE
						tn.Key LIKE 'GAMEUI_HEROES_NEWCHARACTERDIALOG_TYPE_%' AND
						tn.Key NOT LIKE 'GAMEUI_HEROES_NEWCHARACTERDIALOG_TYPE_COMMENT_%';
				";
				command.ExecuteNonQuery();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetFeaturedItems() {
			Debug.WriteLine("SetFeaturedItems() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_FeaturedItems;
					CREATE TABLE HDB_FeaturedItems (
						ItemID INT NOT NULL UNIQUE
					);
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					SELECT
						i._ROWID_ AS ItemID,
						i.Feature
					FROM ItemClassInfo AS i;
				";
				var reader = command.ExecuteReader();
				var featuredItems = new List<Int32>();
				while (reader.Read()) {
					if (this.isFeatured(Convert.ToString(reader["Feature"]))) {
						featuredItems.Add(Convert.ToInt32(reader["ItemID"]));
					}
				}
				reader.Close();
				command.CommandText = "INSERT INTO HDB_FeaturedItems VALUES (@ItemID);";
				command.Parameters.Add("@ItemID", DbType.Int32);
				foreach (var id in featuredItems) {
					command.Parameters["@ItemID"].Value = id;
					command.ExecuteNonQuery();
				}
				transaction.Commit();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetFeaturedEquips() {
			Debug.WriteLine("SetFeaturedEquips() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_FeaturedEquips;
					CREATE TABLE HDB_FeaturedEquips (
						EquipID INT NOT NULL UNIQUE
					);
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					SELECT
						e._ROWID_ AS EquipID,
						e.Feature
					FROM EquipItemInfo AS e
					INNER JOIN ItemClassInfo AS i ON i.ItemClass = e.ItemClass
					INNER JOIN HDB_FeaturedItems AS fi ON fi.ItemID = i._ROWID_;
				";
				var reader = command.ExecuteReader();
				var featuredItems = new List<Int32>();
				while (reader.Read()) {
					if (this.isFeatured(Convert.ToString(reader["Feature"]))) {
						featuredItems.Add(Convert.ToInt32(reader["EquipID"]));
					}
				}
				reader.Close();
				command.CommandText = "INSERT INTO HDB_FeaturedEquips VALUES (@EquipID);";
				command.Parameters.Add("@EquipID", DbType.Int32);
				foreach (var id in featuredItems) {
					command.Parameters["@EquipID"].Value = id;
					command.ExecuteNonQuery();
				}
				transaction.Commit();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetFeaturedRecipes() {
			Debug.WriteLine("SetFeaturedRecipes() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_FeaturedRecipes;
					CREATE TABLE HDB_FeaturedRecipes (
						RecipeType NVARCHAR NOT NULL,
						RecipeKey NVARCHAR NOT NULL,
						RecipeFeature NVARCHAR,
						CONSTRAINT [unique] UNIQUE(RecipeType, RecipeKey, RecipeFeature)
					);
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					SELECT
						t.RecipeType,
						t.RecipeKey,
						t.RecipeFeature
					FROM (
						SELECT DISTINCT
							'npc' AS RecipeType,
							LOWER(cs.RecipeID) AS RecipeKey,
							rmr.ItemClass,
							cs.Feature AS RecipeFeature
						FROM CraftShopInfo AS cs
						INNER JOIN RecipeMaterialInfo AS rmr ON
							rmr.Type = 0 AND
							rmr.RecipeID = cs.RecipeID
						UNION ALL
						SELECT
							'pc' AS RecipeType,
							LOWER(mr.RecipeID) AS RecipeKey,
							mm.ItemClass,
							mr.Feature AS RecipeFeature
						FROM ManufactureRecipeInfo AS mr
						INNER JOIN ManufactureMaterialInfo AS mm ON
							mm.RecipeID = mr.RecipeID AND
							mm.Type = 0
					) AS t
					INNER JOIN ItemClassInfo AS i ON i.ItemClass = t.ItemClass
					INNER JOIN HDB_FeaturedItems AS fi ON fi.ItemID = i._ROWID_;
				";
				var reader = command.ExecuteReader();
				var featuredRecipes = new List<Dictionary<String, Object>>();
				while (reader.Read()) {
					if (this.isFeatured(Convert.ToString(reader["RecipeFeature"]))) {
						featuredRecipes.Add(new Dictionary<String, Object>() {
							{ "RecipeType", reader["RecipeType"] },
							{ "RecipeKey", reader["RecipeKey"] },
							{ "RecipeFeature", reader["RecipeFeature"] },
						});
					}
				}
				reader.Close();
				command.CommandText = "INSERT INTO HDB_FeaturedRecipes VALUES (@RecipeType, @RecipeKey, @RecipeFeature);";
				command.Parameters.Add("@RecipeType", DbType.String);
				command.Parameters.Add("@RecipeKey", DbType.String);
				command.Parameters.Add("@RecipeFeature", DbType.String);
				foreach (var recipe in featuredRecipes) {
					command.Parameters["@RecipeType"].Value = recipe["RecipeType"];
					command.Parameters["@RecipeKey"].Value = recipe["RecipeKey"];
					command.Parameters["@RecipeFeature"].Value = recipe["RecipeFeature"];
					command.ExecuteNonQuery();
				}
				transaction.Commit();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetClassification() {
			Debug.WriteLine("SetClassification() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_Classification;
					CREATE TABLE HDB_Classification (
						ObjectID INT NOT NULL,
						ObjectType NVARACHAR NOT NULL,
						GroupKey NVARCHAR,
						GroupName NVARCHAR,
						GroupOrder INT,
						TypeKey NVARCHAR,
						TypeName NVARCHAR,
						TypeOrder INT,
						TypePrimaryProperties NVARCHAR,
						CategoryKey NVARCHAR,
						CategoryName NVARCHAR,
						CategoryOrder INT,
						Name NVARCHAR,
						CONSTRAINT [unique] UNIQUE(ObjectID, ObjectType, GroupKey, TypeKey, CategoryKey)
					);

					INSERT INTO HDB_Classification
					SELECT
						e._ROWID_ AS ObjectID,
						'equip' AS ObjectType,
						LOWER(i.Category) AS GroupKey,
						tgn.Text AS GroupName,
						1 AS GroupOrder,
						'all' AS TypeKey,
						'All' AS TypeName,
						1 AS TypeOrder,
						'atk, bal, crit, speed, requiredLevel' AS TypePrimaryProperties,
						LOWER(e.EquipClass) AS CategoryKey,
						tcn.Text AS CategoryName,
						CASE e.EquipClass WHEN 'DUALSWORD' THEN 1 WHEN 'DUALSPEAR' THEN 2 WHEN 'LONGSWORD' THEN 3 WHEN 'HAMMER' THEN 4 WHEN 'STAFF' THEN 5 WHEN 'SCYTHE' THEN 6 WHEN 'PILLAR' THEN 7 WHEN 'BLASTER' THEN 8 WHEN 'BOW' THEN 9 WHEN 'CROSSGUN' THEN 10 WHEN 'DUALBLADE' THEN 11 WHEN 'GREATSWORD' THEN 12 WHEN 'BATTLEGLAIVE' THEN 13 WHEN 'LONGBLADE' THEN 14 ELSE 15 END AS CategoryOrder,
						tgn.Text || ', ' || tcn.Text AS Name
					FROM EquipItemInfo AS e
					INNER JOIN HDB_FeaturedEquips AS fe ON fe.EquipID = e._ROWID_
					INNER JOIN ItemClassInfo AS i ON i.ItemClass = e.ItemClass
					INNER JOIN HDB_FeaturedItems AS fi ON fi.ItemID = i._ROWID_
					INNER JOIN HDB_Text AS tgn ON tgn.Key = 'HEROES_ITEM_EQUIP_PART_NAME_' || i.Category
					INNER JOIN HDB_Text AS tcn ON tcn.Key = 'HEROES_ITEM_EQUIP_CLASS_NAME_' || e.EquipClass
					WHERE i.Category = 'WEAPON';

					INSERT INTO HDB_Classification
					SELECT
						s._ROWID_ AS ObjectID,
						'set' AS ObjectType,
						'armor' AS GroupKey,
						tgn.Text AS GroupName,
						2 AS GroupOrder,
						'set' AS TypeKey,
						'Set' AS TypeName,
						1 AS TypeOrder,
						'def, str, int, dex, will, requiredLevel' AS TypePrimaryProperties,
						LOWER(sc.Name) AS CategoryKey,
						sc.Name AS CategoryName,
						sc.ID AS CategoryOrder,
						NULL AS Name
					FROM SetInfo AS s
					INNER JOIN (
						SELECT DISTINCT
							si.SetID,
							c.ID,
							c.Name
						FROM SetItemInfo AS si
						INNER JOIN ItemClassInfo AS i ON i.ItemClass = si.ItemClass
						INNER JOIN HDB_FeaturedItems AS fi ON fi.ItemID = i._ROWID_
						INNER JOIN EquipItemInfo AS e ON e.ItemClass = i.ItemClass
						INNER JOIN HDB_FeaturedEquips AS fe ON fe.EquipID = e._ROWID_
						INNER JOIN HDB_Characters AS c ON c.ID = i.ClassRestriction & c.ID
						WHERE e.EquipClass IN ('CLOTH', 'LIGHT_ARMOR', 'HEAVY_ARMOR', 'PLATE_ARMOR')
					) AS sc ON sc.SetID = s.SetId
					INNER JOIN HDB_Text AS tgn ON tgn.Key = 'HEROES_ITEM_TRADECATEGORY_NAME_ARMOR';

					INSERT INTO HDB_Classification
					SELECT
						e._ROWID_ AS ObjectID,
						'equip' AS ObjectType,
						'armor' AS GroupKey,
						tgn.Text AS GroupName,
						2 AS GroupOrder,
						REPLACE(LOWER(e.EquipClass), '_armor', '') AS TypeKey,
						REPLACE(ttn.Text, ' Armor', '') AS TypeName,
						CASE e.EquipClass WHEN 'CLOTH' THEN 2 WHEN 'LIGHT_ARMOR' THEN 3 WHEN 'HEAVY_ARMOR' THEN 4 WHEN 'PLATE_ARMOR' THEN 5 END AS TypeOrder,
						'def, str, int, classRestriction, requiredLevel' AS TypePrimaryProperties,
						LOWER(i.Category) AS CategoryKey,
						tcn.Text AS CategoryName,
						CASE i.Category WHEN 'HELM' THEN 1 WHEN 'TUNIC' THEN 2 WHEN 'PANTS' THEN 3 WHEN 'GLOVES' THEN 4 WHEN 'BOOTS' THEN 5 END AS CategoryOrder,
						ttn.Text || ', ' || tcn.Text AS Name
					FROM EquipItemInfo AS e
					INNER JOIN HDB_FeaturedEquips AS fe ON fe.EquipID = e._ROWID_
					INNER JOIN ItemClassInfo AS i ON i.ItemClass = e.ItemClass
					INNER JOIN HDB_FeaturedItems AS fi ON fi.ItemID = i._ROWID_
					INNER JOIN HDB_Text AS tgn ON tgn.Key = 'HEROES_ITEM_TRADECATEGORY_NAME_ARMOR'
					INNER JOIN HDB_Text AS ttn ON ttn.Key = 'HEROES_ITEM_EQUIP_CLASS_NAME_' || e.EquipClass
					INNER JOIN HDB_Text AS tcn ON tcn.Key = 'HEROES_ITEM_EQUIP_PART_NAME_' || i.Category
					WHERE e.EquipClass IN ('CLOTH', 'LIGHT_ARMOR', 'HEAVY_ARMOR', 'PLATE_ARMOR');

					INSERT INTO HDB_Classification
					SELECT
						e._ROWID_ AS ObjectID,
						'equip' AS ObjectType,
						LOWER(e.EquipClass) AS GroupKey,
						tgn.Text AS GroupName,
						3 AS GroupOrder,
						CASE WHEN i.Category IN ('ARTIFACT', 'BRACELET') THEN 'special' ELSE 'ordinary' END AS TypeKey,
						CASE WHEN i.Category IN ('ARTIFACT', 'BRACELET') THEN 'Special' ELSE 'Ordinary' END AS TypeName,
						CASE WHEN i.Category IN ('ARTIFACT', 'BRACELET') THEN 2 ELSE 1 END AS TypeOrder,
						CASE WHEN i.Category IN ('ARTIFACT', 'BRACELET') THEN NULL ELSE 'str, int, def, classRestriction, requiredLevel' END AS TypePrimaryProperties,
						LOWER(i.Category) AS CategoryKey,
						tcn.Text AS CategoryName,
						1 AS CategoryOrder,
						tgn.Text || ', ' || tcn.Text AS Name
					FROM EquipItemInfo AS e
					INNER JOIN HDB_FeaturedEquips AS fe ON fe.EquipID = e._ROWID_
					INNER JOIN ItemClassInfo AS i ON i.ItemClass = e.ItemClass
					INNER JOIN HDB_FeaturedItems AS fi ON fi.ItemID = i._ROWID_
					INNER JOIN HDB_Text AS tgn ON tgn.Key = 'HEROES_ITEM_EQUIP_CLASS_NAME_' || e.EquipClass
					INNER JOIN HDB_Text AS tcn ON tcn.Key = 'HEROES_ITEM_EQUIP_PART_NAME_' || i.Category
					WHERE
						e.EquipClass = 'ACCESSORY' AND
						i.Category IN ('ARTIFACT', 'BELT', 'BRACELET', 'CHARM', 'EARRING', 'RING');

					INSERT INTO HDB_Classification
					SELECT
						e._ROWID_ AS ObjectID,
						'equip' AS ObjectType,
						'offhand' AS GroupKey,
						'Off-hand' AS GroupName,
						4 AS GroupOrder,
						CASE WHEN e.EquipClass IN ('LARGESHIELD', 'SHIELD') THEN 'shield' ELSE 'other' END AS TypeKey,
						CASE WHEN e.EquipClass IN ('LARGESHIELD', 'SHIELD') THEN 'Shield' ELSE 'Other' END AS TypeName,
						CASE WHEN e.EquipClass IN ('LARGESHIELD', 'SHIELD') THEN 1 ELSE 2 END AS TypeOrder,
						CASE WHEN e.EquipClass IN ('LARGESHIELD', 'SHIELD') THEN 'def, str, dex, will, requiredLevel' ELSE 'int, atk, crit, def, requiredLevel' END AS TypePrimaryProperties,
						LOWER(e.EquipClass) AS CategoryKey,
						tcn.Text AS CategoryName,
						1 AS CategoryOrder,
						'Off-hand, ' || tcn.Text AS Name
					FROM EquipItemInfo AS e
					INNER JOIN HDB_FeaturedEquips AS fe ON fe.EquipID = e._ROWID_
					INNER JOIN ItemClassInfo AS i ON i.ItemClass = e.ItemClass
					INNER JOIN HDB_FeaturedItems AS fi ON fi.ItemID = i._ROWID_
					INNER JOIN HDB_Text AS tcn ON tcn.Key = 'HEROES_ITEM_EQUIP_CLASS_NAME_' || e.EquipClass
					WHERE e.EquipClass IN ('LARGESHIELD', 'SHIELD', 'SPELLBOOK', 'CASTLET');

					INSERT INTO HDB_Classification
					SELECT
						s._ROWID_ AS ObjectID,
						'set' AS ObjectType,
						NULL AS GroupKey,
						NULL AS GroupName,
						NULL AS GroupOrder,
						NULL AS TypeKey,
						NULL AS TypeName,
						NULL AS TypeOrder,
						NULL AS TypePrimaryProperties,
						NULL AS CategoryKey,
						NULL AS CategoryName,
						NULL AS CategoryOrder,
						NULL AS Name
					FROM SetInfo AS s
					INNER JOIN (
						SELECT DISTINCT si.SetID
						FROM SetItemInfo AS si
						INNER JOIN EquipItemInfo AS e ON e.ItemClass = si.ItemClass
						INNER JOIN HDB_Classification AS c ON
							c.ObjectType = 'equip' AND
							c.ObjectID = e._ROWID_
					) AS fs ON fs.SetID = s.SetId
					LEFT JOIN HDB_Classification AS c ON
						c.ObjectType = 'set' AND
						c.ObjectID = s._ROWID_
					WHERE c.ObjectID IS NULL;

					INSERT INTO HDB_Classification
					SELECT
						i._ROWID_ AS ObjectID,
						'mat' AS ObjectType,
						NULL AS GroupKey,
						NULL AS GroupName,
						NULL AS GroupOrder,
						NULL AS TypeKey,
						NULL AS TypeName,
						NULL AS TypeOrder,
						NULL AS TypePrimaryProperties,
						NULL AS CategoryKey,
						NULL AS CategoryName,
						NULL AS CategoryOrder,
						COALESCE(ecn.Name, tct.Text || ', ' || tcst.Text) AS Name
					FROM (
						SELECT rm.ItemClass
						FROM RecipeMaterialInfo AS rm
						INNER JOIN HDB_FeaturedRecipes AS fr ON
							fr.RecipeType = 'npc' AND
							fr.RecipeKey = LOWER(rm.RecipeID) AND (
								fr.RecipeFeature = rm.Feature OR
								COALESCE(fr.RecipeFeature, rm.Feature) IS NULL
							)
						WHERE rm.Type = 1
						UNION
						SELECT mm.ItemClass
						FROM ManufactureMaterialInfo AS mm
						INNER JOIN ManufactureRecipeInfo AS mr ON mr.RecipeID = mm.RecipeID
						INNER JOIN HDB_FeaturedRecipes AS fr ON
							fr.RecipeType = 'pc' AND
							fr.RecipeKey = LOWER(mr.RecipeID) AND ( 
								fr.RecipeFeature = mr.Feature OR
								COALESCE(fr.RecipeFeature, mr.Feature) IS NULL
							)
						WHERE mm.Type = 1
					) AS m
					INNER JOIN ItemClassInfo AS i ON i.ItemClass = m.ItemClass
					INNER JOIN HDB_FeaturedItems AS fi ON fi.ItemID = i._ROWID_
					LEFT JOIN (
						SELECT
							e.ItemClass,
							c.Name
						FROM EquipItemInfo AS e
						INNER JOIN HDB_FeaturedEquips AS fe ON fe.EquipID = e._ROWID_
						INNER JOIN HDB_Classification AS c ON
							c.ObjectID = e._ROWID_ AND
							c.ObjectType = 'equip'
					) AS ecn ON ecn.ItemClass = m.ItemClass
					INNER JOIN HDB_Text AS tct ON tct.Key = 'HEROES_ITEM_TRADECATEGORY_NAME_MATERIAL'
					INNER JOIN HDB_Text AS tcst ON tcst.Key = 'HEROES_ITEM_TRADECATEGORY_NAME_' || CASE WHEN i.TradeCategorySub IN ('COOKING', 'MATERIAL_CLOTH', 'MATERIAL_LEATHER', 'MATERIAL_ORE', 'MATERIAL_ENCHANT', 'MATERIAL_ENHANCE', 'MATERIAL_SUB', 'MATERIAL_SPIRITINJECTION') THEN i.TradeCategorySub ELSE 'TROPHY' END;
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DELETE FROM HDB_Classification
					WHERE
						HDB_Classification.ObjectType = 'equip' AND
						HDB_Classification.ObjectID IN (
							SELECT e._ROWID_
							FROM EquipItemInfo AS e 
							INNER JOIN ItemClassInfo AS i ON i.ItemClass = e.ItemClass
							LEFT JOIN HDB_Text AS tn ON tn.Key = 'HEROES_ITEM_NAME_' || UPPER(i.ItemClass)
							LEFT JOIN ItemClassInfo AS ai ON ai.ItemClass = 'avatar_' || i.ItemClass
							WHERE
								e.MaxDurability = 999999 OR
								i.ExpireIn IS NOT NULL OR
								i.ItemClass LIKE '%_limitless' OR
								i.ItemClass LIKE '%_comback' OR
								i.ItemClass LIKE '%_comback2' OR
								i.ItemClass LIKE '%_newserver' OR
								i.ItemClass LIKE '%_provide' OR
								i.ItemClass LIKE '%_fruitgift' OR
								i.ItemClass LIKE '%_gift' OR
								i.ItemClass LIKE '%_7days' OR
								tn.Text GLOB '*[^A-z0-9 ()''.:-]*' OR
								tn.Text LIKE '%(Event)' OR
								ai.ItemClass IS NOT NULL
						);

					DELETE FROM HDB_Classification
					WHERE
						HDB_Classification.ObjectType = 'set' AND
						HDB_Classification.ObjectID IN (
							SELECT s._ROWID_
							FROM SetInfo AS s
							WHERE
								s.SetID NOT IN (
									SELECT s.SetID
									FROM SetInfo AS s
									INNER JOIN SetItemInfo AS si ON si.SetID = s.SetID
									INNER JOIN EquipItemInfo AS e ON e.ItemClass = si.ItemClass 
									INNER JOIN HDB_Classification AS c ON
										c.ObjectType = 'equip' AND
										c.ObjectID = e._ROWID_
								)
						);
				";
				command.ExecuteNonQuery();
				transaction.Commit();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetIcons(String iconPath) {
			Debug.WriteLine("SetIcons() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_Icons;
					CREATE TABLE HDB_Icons (
						Icon NVARCHAR NOT NULL,
						IconBG NVARCHAR,
						Material1 NVARCHAR,
						Material2 NVARCHAR,
						Material3 NVARCHAR
					);
					INSERT INTO HDB_Icons
					SELECT DISTINCT
						i.Icon,
						i.IconBG,
						e.Material1,
						e.Material2,
						e.Material3
					FROM ItemClassInfo AS i
					LEFT JOIN EquipItemInfo AS e ON e.ItemClass = i.ItemClass;
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					SELECT i.Icon
					FROM HDB_Icons AS i;
				";
				var reader = command.ExecuteReader();
				var missingIcons = new List<String>();
				while (reader.Read()) {
					var iconFileName = String.Concat(reader["Icon"], ".tga");
					var iconFile = Path.Combine(iconPath, iconFileName);
					if (!File.Exists(iconFile)) {
						missingIcons.Add(Convert.ToString(reader["Icon"]));
					}
				}
				reader.Close();
				command.CommandText = @"
					DELETE FROM HDB_Icons
					WHERE HDB_Icons.Icon = @Icon;
				";
				command.Parameters.Add("@Icon", DbType.String);
				foreach (var icon in missingIcons) {
					command.Parameters["@Icon"].Value = icon;
					command.ExecuteNonQuery();
				}
				transaction.Commit();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetMats() {
			Debug.WriteLine("SetMats() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_Mats;
					CREATE TABLE HDB_Mats (
						ID INT PRIMARY KEY,
						Key NVARCHAR NOT NULL,
						IconID INT,
						Name NVARCHAR NOT NULL,
						Classification NVARCHAR NOT NULL,
						Description NVARCHAR,
						Rarity INT NOT NULL,
						[Order] INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(Key)
					);
					INSERT INTO HDB_Mats
					SELECT
						c.ObjectID AS ID,
						LOWER(i.ItemClass) AS Key,
						ico.ID AS IconID,
						tn.Text AS Name,
						c.Name AS Classification,
						td.Text AS Description,
						i.Rarity,
						CASE WHEN i.ItemClass = 'gold' THEN 3 WHEN i.ItemClass LIKE 'cloth_lvl_' OR i.ItemClass LIKE 'skin_lvl_' OR i.ItemClass LIKE 'iron_ore_lvl_' THEN 2 ELSE 1 END AS [Order]
					FROM HDB_Classification AS c
					INNER JOIN ItemClassInfo AS i ON i._ROWID_ = c.ObjectID
					INNER JOIN HDB_Text AS tn ON tn.Key = 'HEROES_ITEM_NAME_' || UPPER(i.ItemClass)
					LEFT JOIN HDB_Text AS td ON
						td.Key = 'HEROES_ITEM_DESC_' || UPPER(i.ItemClass) AND
						LENGTH(td.Text) > 0
					LEFT JOIN (
						SELECT
							ico.Icon,
							ico.IconBG,
							MIN(ico._ROWID_) AS ID
						FROM HDB_Icons AS ico
						GROUP BY
							ico.Icon,
							ico.IconBG
					) ico ON
						ico.Icon = i.Icon AND (
							ico.IconBG = i.IconBG OR
							COALESCE(ico.IconBG, i.IconBG) IS NULL
						)
					WHERE c.ObjectType = 'mat';
				";
				command.ExecuteNonQuery();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetEquips() {
			Debug.WriteLine("SetEquips() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_Equips;
					CREATE TABLE HDB_Equips (
						ID INT PRIMARY KEY,
						Key NVARCHAR NOT NULL,
						GroupKey NVARCHAR NOT NULL,
						TypeKey NVARCHAR NOT NULL,
						CategoryKey NVARCHAR NOT NULL,
						IconID INT,
						Name NVARCHAR NOT NULL,
						Classification NVARCHAR NOT NULL,
						Description NVARCHAR,
						Rarity INT NOT NULL,
						SetKey NVARCHAR,
						SetName NVARCHAR,
						RequiredSkillName NVARCHAR,
						RequiredSkillRank NVARCHAR,
						RequiredLevel INT NOT NULL,
						ClassRestriction INT NOT NULL,
						ATK INT NOT NULL,
						PATK INT NOT NULL,
						MATK INT NOT NULL,
						SPEED INT NOT NULL,
						CRIT INT NOT NULL,
						BAL INT NOT NULL,
						HP INT NOT NULL,
						DEF INT NOT NULL,
						CRITRES INT NOT NULL,
						STR INT NOT NULL,
						INT INT NOT NULL,
						DEX INT NOT NULL,
						WILL INT NOT NULL,
						STAMINA INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(Key)
					);
					INSERT INTO HDB_Equips
					SELECT
						c.ObjectID AS ID,
						LOWER(e.ItemClass) AS Key,
						c.GroupKey,
						c.TypeKey,
						c.CategoryKey,
						ico._ROWID_ AS IconID,
						tn.Text AS Name,
						c.Name AS classification,
						td.Text AS Description,
						i.Rarity,
						NULL AS SetKey,
						NULL AS SetName,
						trs.Text AS RequiredSkillName,
						sr.Rank_String AS RequiredSkillRank,
						i.RequiredLevel,
						i.ClassRestriction,
						CASE WHEN e.MATK > 0 THEN e.MATK ELSE e.ATK END AS ATK,
						e.ATK AS PATK,
						e.MATK,
						e.ATK_Speed AS SPEED,
						e.Critical AS CRIT,
						e.Balance AS BAL,
						e.HP,
						e.DEF,
						e.Res_Critical AS CRITRES,
						e.STR,
						e.INT,
						e.DEX,
						e.WILL,
						e.STAMINA
					FROM HDB_Classification AS c
					INNER JOIN EquipItemInfo AS e ON e._ROWID_ = c.ObjectID
					INNER JOIN ItemClassInfo AS i ON i.ItemClass = e.ItemClass
					INNER JOIN HDB_FeaturedItems AS fi ON fi.ItemID = i._ROWID_
					INNER JOIN HDB_Text AS tn ON tn.Key = 'HEROES_ITEM_NAME_' || UPPER(i.ItemClass)
					LEFT JOIN HDB_Text AS td ON
						td.Key = 'HEROES_ITEM_DESC_' || UPPER(i.ItemClass) AND
						LENGTH(td.Text) > 0 AND
						td.Text != '0' AND
						td.Text != 'Effective equipment for PVP.' AND
						td.Text != 'pvp\nEquipment' AND
						td.Text != tn.Text
					LEFT JOIN HDB_Text AS trs ON trs.Key = 'HEROES_SKILL_NAME_' || UPPER(i.RequiredSkill)
					LEFT JOIN SkillRankEnum AS sr ON
						sr.Rank_Num = i.RequiredSkillRank AND
						sr.Rank_Num > 0
					LEFT JOIN HDB_Icons AS ico ON
						ico.Icon = i.Icon AND (
							ico.IconBG = i.IconBG OR
							COALESCE(ico.IconBG, i.IconBG) IS NULL
						) AND (
							ico.Material1 = e.Material1 OR
							COALESCE(ico.Material1, e.Material1) IS NULL
						) AND (
							ico.Material2 = e.Material2 OR
							COALESCE(ico.Material2, e.Material2) IS NULL
						) AND (
							ico.Material3 = e.Material3 OR
							COALESCE(ico.Material3, e.Material3) IS NULL
						)
					WHERE
						c.ObjectType = 'equip' AND
						c.ObjectID != 3301;
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_EquipRecipes;
					CREATE TABLE HDB_EquipRecipes (
						EquipKey NVARCHAR NOT NULL,
						Type NVARCHAR NOT NULL,
						MatKey NVARCHAR NOT NULL,
						MatCount INT NOT NULL,
						AppearQuestName NVARCHAR,
						ExpertiseName NVARCHAR,
						ExpertiseExperienceRequired INT,
						CONSTRAINT [unique] UNIQUE(EquipKey, Type, MatKey)
					);
					INSERT INTO HDB_EquipRecipes
					SELECT
						e.Key AS EquipKey,
						fr.RecipeType AS Type,
						m.Key AS MatKey,
						rm.Num AS MatCount,
						t.Text AS AppearQuestName,
						NULL AS ExpertiseName,
						NULL AS ExpertiseExperienceRequired
					FROM HDB_Equips AS e
					INNER JOIN RecipeMaterialInfo AS rmr ON
						rmr.Type = 0 AND
						LOWER(rmr.ItemClass) = e.Key
					INNER JOIN HDB_FeaturedRecipes AS fr ON
						fr.RecipeType = 'npc' AND
						fr.RecipeKey = LOWER(rmr.RecipeID) AND (
							fr.RecipeFeature = rmr.Feature OR
							COALESCE(fr.RecipeFeature, rmr.Feature) IS NULL
						)
					INNER JOIN RecipeMaterialInfo AS rm ON
						rm.Type = 1 AND
						rm.RecipeID = rmr.RecipeID AND (
							rm.Feature = rmr.Feature OR
							COALESCE(rm.Feature, rmr.Feature) IS NULL
						)
					LEFT JOIN HDB_Mats AS m ON m.Key = LOWER(rm.ItemClass)
					INNER JOIN (
						SELECT DISTINCT
							cs.RecipeID,
							cs.Feature,
							cs.AppearQuestID
						FROM CraftShopInfo AS cs
					) AS cs ON
						cs.RecipeID = rmr.RecipeID AND (
							cs.Feature = rmr.Feature OR
							COALESCE(cs.Feature, rmr.Feature) IS NULL
						)
					LEFT JOIN HDB_Text AS t ON t.Key = 'HEROES_QUEST_TITLE_' || UPPER(cs.AppearQuestID);
					INSERT INTO HDB_EquipRecipes
					SELECT
						e.Key AS EquipKey,
						fr.RecipeType AS Type,
						m.Key AS MatKey,
						mm.Num AS MatCount,
						NULL AS AppearQuestName,
						t.Text AS ExpertiseName,
						mr.ExperienceRequired AS ExpertiseExperienceRequired
					FROM HDB_Equips AS e
					INNER JOIN ManufactureMaterialInfo AS mmr ON
						mmr.Type = 0 AND
						LOWER(mmr.ItemClass) = e.Key
					INNER JOIN ManufactureRecipeInfo AS mr ON mr.RecipeID = mmr.RecipeID
					INNER JOIN HDB_FeaturedRecipes AS fr ON
						fr.RecipeType = 'pc' AND
						fr.RecipeKey = LOWER(mr.RecipeID) AND (
							fr.RecipeFeature = mr.Feature OR
							COALESCE(fr.RecipeFeature, mr.Feature) IS NULL
						)
					INNER JOIN ManufactureMaterialInfo AS mm ON
						mm.Type = 1 AND
						mm.RecipeID = mr.RecipeID
					LEFT JOIN HDB_Mats AS m ON m.Key = LOWER(mm.ItemClass)
					LEFT JOIN HDB_Text AS t ON t.Key = 'GAMEUI_HEROES_MANUFACTURE_CRAFTNAME_' || UPPER(mr.ManufactureID);
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_EquipRecipeShops;
					CREATE TABLE HDB_EquipRecipeShops (
						EquipKey NVARCHAR NOT NULL,
						ShopKey NVARCHAR NOT NULL,
						ShopName NVARCHAR NOT NULL,
						CONSTRAINT [unique] UNIQUE(EquipKey, ShopKey)
					);
					INSERT INTO HDB_EquipRecipeShops
					SELECT
						t.EquipKey,
						t.ShopKey,
						tn.Text AS ShopName
					FROM (
						SELECT
							e.Key AS EquipKey,
							LOWER(REPLACE(REPLACE(REPLACE(REPLACE(cs.ShopID, '_ACCESSORY_Craft', ''), '_ARMOR_Craft', ''), '_WEAPON_Craft', ''), '_Craft', '')) AS ShopKey
						FROM HDB_Equips AS e
						INNER JOIN RecipeMaterialInfo AS rmr ON
							rmr.Type = 0 AND
							LOWER(rmr.ItemClass) = e.Key
						INNER JOIN HDB_FeaturedRecipes AS fr ON
							fr.RecipeType = 'npc' AND
							fr.RecipeKey = LOWER(rmr.RecipeID) AND (
								fr.RecipeFeature = rmr.Feature OR
								COALESCE(fr.RecipeFeature, rmr.Feature) IS NULL
							)
						INNER JOIN CraftShopInfo AS cs ON
							cs.RecipeID = rmr.RecipeID AND (
								cs.Feature = rmr.Feature OR (
									COALESCE(cs.Feature, rmr.Feature) IS NULL
								)
							)
					) AS t
					LEFT JOIN HDB_Text AS tn ON tn.Key = 'HEROES_NPC_' || UPPER(t.ShopKey);
				";
				command.ExecuteNonQuery();
				transaction.Commit();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetSets() {
			Debug.WriteLine("SetSets() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.connectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_Sets;
					CREATE TABLE HDB_Sets (
						ID INT PRIMARY KEY,
						Key NVARCHAR NOT NULL,
						GroupKey NVARCHAR,
						TypeKey NVARCHAR,
						IconID INT,
						Name NVARCHAR NOT NULL,
						Rarity INT NOT NULL,
						RequiredLevel INT NOT NULL,
						ClassRestriction INT NOT NULL,
						ATK INT NOT NULL,
						PATK INT NOT NULL,
						MATK INT NOT NULL,
						SPEED INT NOT NULL,
						CRIT INT NOT NULL,
						BAL INT NOT NULL,
						HP INT NOT NULL,
						DEF INT NOT NULL,
						CRITRES INT NOT NULL,
						STR INT NOT NULL,
						INT INT NOT NULL,
						DEX INT NOT NULL,
						WILL INT NOT NULL,
						STAMINA INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(Key)
					);
					INSERT INTO HDB_Sets
					SELECT
						c.ObjectID AS ID,
						LOWER(s.SetID) AS Key,
						c.GroupKey,
						c.TypeKey,
						ss.IconID,
						tn.Text AS Name,
						ss.Rarity,
						ss.RequiredLevel,
						ss.ClassRestriction,
						ss.ATK + COALESCE(se.ATK, 0) AS ATK,
						ss.PATK + COALESCE(se.PATK, 0) AS PATK,
						ss.MATK + COALESCE(se.MATK, 0) AS MATK,
						ss.Speed AS SPEED,
						ss.CRIT,
						ss.BAL,
						ss.HP + COALESCE(se.HP, 0) AS HP,
						ss.DEF + COALESCE(se.DEF, 0) AS DEF,
						ss.CRITRES,
						ss.STR + COALESCE(se.STR, 0) AS STR,
						ss.INT + COALESCE(se.INT, 0) AS INT,
						ss.DEX + COALESCE(se.DEX, 0) AS DEX,
						ss.WILL + COALESCE(se.WILL, 0) AS WILL,
						ss.STAMINA
					FROM (
						SELECT DISTINCT
							c.ObjectID,
							c.GroupKey,
							c.TypeKey
						FROM HDB_Classification AS c
						WHERE c.ObjectType = 'set'
					) AS c
					INNER JOIN SetInfo AS s ON s._ROWID_ = c.ObjectID
					INNER JOIN HDB_Text AS tn ON tn.Key = 'HEROES_ITEM_SETITEM_NAME_' || UPPER(s.SetID)
					INNER JOIN (
						SELECT
							s.SetID,
							MAX(CASE WHEN i.Category = 'HELM' THEN ico._ROWID_ ELSE NULL END) AS IconID,
							MAX(i.Rarity) AS Rarity,
							MAX(i.RequiredLevel) AS RequiredLevel,
							MAX(i.ClassRestriction) AS ClassRestriction,
							SUM(CASE WHEN e.MATK > 0 THEN e.MATK ELSE e.ATK END) AS ATK,
							SUM(e.ATK) AS PATK,
							SUM(e.MATK) AS MATK,
							SUM(e.ATK_Speed) AS SPEED,
							SUM(e.Critical) AS CRIT,
							SUM(e.Balance) AS BAL,
							SUM(e.HP) AS HP,
							SUM(e.DEF) AS DEF,
							SUM(e.Res_Critical) AS CRITRES,
							SUM(e.STR) AS STR,
							SUM(e.INT) AS INT,
							SUM(e.DEX) AS DEX,
							SUM(e.WILL) AS WILL,
							SUM(e.STAMINA) AS STAMINA
						FROM SetInfo AS s
						INNER JOIN SetItemInfo AS si ON si.SetID = s.SetID
						INNER JOIN ItemClassInfo AS i ON i.ItemClass = si.BaseItemClass
						INNER JOIN HDB_FeaturedItems AS fi ON fi.ItemID = i._ROWID_
						INNER JOIN EquipItemInfo AS e ON e.ItemClass = i.ItemClass
						INNER JOIN HDB_FeaturedEquips AS fe ON fe.EquipID = e._ROWID_
						LEFT JOIN HDB_Icons AS ico ON
							ico.Icon = i.Icon AND (
								ico.IconBG = i.IconBG OR
								COALESCE(ico.IconBG, i.IconBG) IS NULL
							) AND (
								ico.Material1 = e.Material1 OR
								COALESCE(ico.Material1, e.Material1) IS NULL
							) AND (
								ico.Material2 = e.Material2 OR
								COALESCE(ico.Material2, e.Material2) IS NULL
							) AND (
								ico.Material3 = e.Material3 OR
								COALESCE(ico.Material3, e.Material3) IS NULL
							)
						GROUP BY s.SetID
					) AS ss ON ss.SetID = s.SetID
					LEFT JOIN (
						SELECT
							se.SetID,
							SUM(CASE WHEN se.EffectTarget IN ('ATK', 'MATK') THEN se.Amount ELSE 0 END) AS ATK,
							SUM(CASE WHEN se.EffectTarget = 'ATK' THEN se.Amount ELSE 0 END) AS PATK,
							SUM(CASE WHEN se.EffectTarget = 'MATK' THEN se.Amount ELSE 0 END) AS MATK,
							SUM(CASE WHEN se.EffectTarget = 'HP' THEN se.Amount ELSE 0 END) AS HP,
							SUM(CASE WHEN se.EffectTarget = 'DEF' THEN se.Amount ELSE 0 END) AS DEF,
							SUM(CASE WHEN se.EffectTarget = 'STR' THEN se.Amount ELSE 0 END) AS STR,
							SUM(CASE WHEN se.EffectTarget = 'INT' THEN se.Amount ELSE 0 END) AS INT,
							SUM(CASE WHEN se.EffectTarget = 'DEX' THEN se.Amount ELSE 0 END) AS DEX,
							SUM(CASE WHEN se.EffectTarget = 'WILL' THEN se.Amount ELSE 0 END) AS WILL
						FROM SetEffectInfo AS se
						INNER JOIN (
							SELECT
								se.SetID,
								se.EffectTarget,
								MAX(se.SetCount) AS MaxSetCount
							FROM SetEffectInfo AS se
							GROUP BY
								se.SetID,
								se.EffectTarget
						) AS sem ON
							sem.SetID = se.SetID AND
							sem.EffectTarget = se.EffectTarget AND
							sem.MaxSetCount = se.SetCount
						GROUP BY se.SetID
					) AS se ON se.SetID = s.SetID;
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_SetCategories;
					CREATE TABLE HDB_SetCategories (
						SetKey NVARCHAR NOT NULL,
						CategoryKey NVARCHAR,
						CONSTRAINT [unique] UNIQUE(SetKey, CategoryKey)
					);
					INSERT INTO HDB_SetCategories
					SELECT
						s.Key AS SetKey,
						c.CategoryKey
					FROM HDB_Classification AS c
					INNER JOIN HDB_Sets AS s ON s.ID = c.ObjectID
					WHERE c.ObjectType = 'set';
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_SetParts;
					CREATE TABLE HDB_SetParts (
						SetKey NVARCHAR NOT NULL,
						EquipKey NVARCHAR,
						EquipName NVARCHAR NOT NULL,
						Base INT NOT NULL,
						[Order] INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(SetKey, EquipKey, EquipName)
					);
					INSERT INTO HDB_SetParts
					SELECT
						s.Key AS SetKey,
						e.Key AS EquipKey,
						tn.Text AS EquipName,
						CASE WHEN sibi.SetID IS NULL THEN 0 ELSE 1 END AS Base,
						CASE i.Category WHEN 'WEAPON' THEN 1 WHEN 'HELM' THEN 2 WHEN 'TUNIC' THEN 3 WHEN 'PANTS' THEN 4 WHEN 'GLOVES' THEN 5 WHEN 'BOOTS' THEN 6 ELSE 7 END AS [Order]
					FROM HDB_Sets AS s
					INNER JOIN (
						SELECT
							si.SetID,
							si.ItemClass
						FROM SetItemInfo AS si
						UNION
						SELECT
							si.SetID,
							si.BaseItemClass
						FROM SetItemInfo AS si
					) AS siai ON LOWER(siai.SetID) = s.Key
					LEFT JOIN (
						SELECT DISTINCT
							si.SetID,
							si.BaseItemClass
						FROM SetItemInfo AS si
					) AS sibi ON
						sibi.SetID = siai.SetID AND
						sibi.BaseItemClass = siai.ItemClass
					INNER JOIN ItemClassInfo AS i ON i.ItemClass = siai.ItemClass
					INNER JOIN HDB_FeaturedItems AS fi ON fi.ItemID = i._ROWID_
					LEFT JOIN HDB_Equips AS e ON e.Key = i.ItemClass
					LEFT JOIN HDB_Classification AS c ON
						c.ObjectType = 'equip' AND
						c.ObjectID = e.ID
					INNER JOIN HDB_Text AS tn ON tn.Key = 'HEROES_ITEM_NAME_' || UPPER(i.ItemClass);
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_SetSkills;
					CREATE TABLE HDB_SetSkills (
						SetKey NVARCHAR NOT NULL,
						SkillName NVARCHAR NOT NULL,
						SkillRank NVARCHAR NOT NULL,
						CONSTRAINT [unique] UNIQUE(SetKey, SkillName)
					);
					INSERT INTO HDB_SetSkills
					SELECT
						s.Key AS SetKey,
						trs.Text AS SkillName,
						sr.Rank_String AS SkillRank
					FROM (
						SELECT
							s.Key,
							i.RequiredSkill,
							MAX(i.RequiredSkillRank) AS RequiredSkillRank
						FROM HDB_Sets AS s
						INNER JOIN SetItemInfo AS si ON LOWER(si.SetID) = s.Key
						INNER JOIN ItemClassInfo AS i ON i.ItemClass = si.BaseItemClass
						INNER JOIN HDB_FeaturedItems AS fi ON fi.ItemID = i._ROWID_
						GROUP BY
							s.Key,
							i.RequiredSkill
					) AS s
					INNER JOIN HDB_Text AS trs ON trs.Key = 'HEROES_SKILL_NAME_' || UPPER(s.RequiredSkill)
					INNER JOIN SkillRankEnum AS sr ON sr.Rank_Num = s.RequiredSkillRank;
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_SetEffects;
					CREATE TABLE HDB_SetEffects (
						SetKey NVARCHAR NOT NULL,
						PartCount INT NOT NULL,
						ATK INT NOT NULL,
						PATK INT NOT NULL,
						MATK INT NOT NULL,
						HP INT NOT NULL,
						DEF INT NOT NULL,
						STR INT NOT NULL,
						INT INT NOT NULL,
						DEX INT NOT NULL,
						WILL INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(SetKey, PartCount)
					);
					INSERT INTO HDB_SetEffects
					SELECT
						s.Key AS SetKey,
						se.SetCount AS PartCount,
						SUM(CASE WHEN se.EffectTarget IN ('ATK', 'MATK') THEN se.Amount ELSE 0 END) AS ATK,
						SUM(CASE WHEN se.EffectTarget = 'ATK' THEN se.Amount ELSE 0 END) AS PATK,
						SUM(CASE WHEN se.EffectTarget = 'MATK' THEN se.Amount ELSE 0 END) AS MATK,
						SUM(CASE WHEN se.EffectTarget = 'HP' THEN se.Amount ELSE 0 END) AS HP,
						SUM(CASE WHEN se.EffectTarget = 'DEF' THEN se.Amount ELSE 0 END) AS DEF,
						SUM(CASE WHEN se.EffectTarget = 'STR' THEN se.Amount ELSE 0 END) AS STR,
						SUM(CASE WHEN se.EffectTarget = 'INT' THEN se.Amount ELSE 0 END) AS INT,
						SUM(CASE WHEN se.EffectTarget = 'DEX' THEN se.Amount ELSE 0 END) AS DEX,
						SUM(CASE WHEN se.EffectTarget = 'WILL' THEN se.Amount ELSE 0 END) AS WILL
					FROM HDB_Sets AS s
					INNER JOIN SetEffectInfo AS se ON LOWER(se.SetID) = s.Key
					GROUP BY
						s.Key,
						se.SetCount;
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					UPDATE HDB_Equips
					SET
						SetKey = (
							SELECT sp.SetKey
							FROM HDB_SetParts AS sp
							WHERE sp.EquipKey = HDB_Equips.Key
						);
					UPDATE HDB_Equips
					SET
						SetName = (
							SELECT s.Name
							FROM HDB_Sets AS s
							WHERE s.Key = HDB_Equips.SetKey
						);
				";
				command.ExecuteNonQuery();
				transaction.Commit();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

	}

}
