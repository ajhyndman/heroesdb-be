
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

		private Config config;

		private List<String> enabledFeatures;

		public Setuper(Config config) {
			Debug.WriteLine("Setuper() {");
			Debug.Indent();
			this.config = config;
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		private void loadFeatures() {
			var region = ConfigurationManager.AppSettings["Region"];
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
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

		public void ImportText() {
			Debug.WriteLine("ImportText() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
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
				using (var file = File.OpenText(this.config.TextImportFile)) {
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
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
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
						CASE tn.Text WHEN 'Lann' THEN 1 WHEN 'Fiona' THEN 2 WHEN 'Evie' THEN 4 WHEN 'Karok' THEN 8 WHEN 'Karok' THEN 8 WHEN 'Kai' THEN 16 WHEN 'Vella' THEN 32 WHEN 'Hurk' THEN 64 WHEN 'Lynn' THEN 128 WHEN 'Arisha' THEN 256 WHEN 'Sylas' THEN 512 END AS ID,
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
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
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
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
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
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
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
            WHERE cs.ShopID != '_Craft'
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
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
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
						CASE e.EquipClass WHEN 'DUALSWORD' THEN 1 WHEN 'DUALSPEAR' THEN 2 WHEN 'LONGSWORD' THEN 3 WHEN 'HAMMER' THEN 4 WHEN 'STAFF' THEN 5 WHEN 'SCYTHE' THEN 6 WHEN 'PILLAR' THEN 7 WHEN 'BLASTER' THEN 8 WHEN 'BOW' THEN 9 WHEN 'CROSSGUN' THEN 10 WHEN 'DUALBLADE' THEN 11 WHEN 'GREATSWORD' THEN 12 WHEN 'BATTLEGLAIVE' THEN 13 WHEN 'LONGBLADE' THEN 14 WHEN 'PHANTOMDAGGER' THEN 15 ELSE 16 END AS CategoryOrder,
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
						UNION
						SELECT DISTINCT e.MaterialClass1
						FROM EnhanceInfo AS e
						UNION
						SELECT DISTINCT e.MaterialClass2
						FROM EnhanceInfo AS e
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
								i.ItemClass IN ('elculous_wing_test_rear') OR
								i.ItemClass LIKE '%_limitless' OR
								i.ItemClass LIKE '%_comback' OR
								i.ItemClass LIKE '%_comback2' OR
								i.ItemClass LIKE '%_newserver' OR
								i.ItemClass LIKE '%_provide' OR
								i.ItemClass LIKE '%_fruitgift' OR
								i.ItemClass LIKE '%_gift' OR
								i.ItemClass LIKE '%_7days' OR
								i.ItemClass LIKE '%_5th_rentweapon' OR
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

		public void SetQualityTypes() {
			Debug.WriteLine("SetQualityTypes() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_QualityTypes;
					CREATE TABLE HDB_QualityTypes (
						Key NVARCHAR NOT NULL,
						Level INT NOT NULL,
						Property NVARCHAR NOT NULL,
						Improvement DOUBLE NOT NULL,
						CONSTRAINT [unique] UNIQUE(Key, Level, Property)
					);
					INSERT INTO HDB_QualityTypes
					SELECT
						LOWER(qs.ItemType) AS Key,
						qs.Quality AS Level,
						CASE qs.Stat WHEN 'ATK' THEN 'patk' ELSE LOWER(qs.Stat) END AS Property,
						qs.Value AS Improvement
					FROM QualityStatInfo AS qs
					INNER JOIN (
						SELECT e.QualityType
						FROM EquipItemInfo AS e
						INNER JOIN HDB_Classification AS c ON
							c.ObjectType = 'equip' AND
							c.ObjectID = e._ROWID_
						UNION
						SELECT ccp.QualityType
						FROM CombineCraftInfo AS cc
						INNER JOIN CombineCraftPartsInfo AS ccp ON ccp.PartsGroup IN (cc.PartsGroup1, cc.PartsGroup2, cc.PartsGroup3, cc.PartsGroup4, cc.PartsGroup5)
						INNER JOIN EquipItemInfo AS e ON e.ItemClass = cc.ItemClass
						INNER JOIN HDB_Classification AS c ON
							c.ObjectType = 'equip' AND
							c.ObjectID = e._ROWID_
					) AS e ON e.QualityType = qs.ItemType
					WHERE qs.Quality IN (1, 3, 4, 5);
				";
				command.ExecuteNonQuery();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetEnhanceTypes() {
			Debug.WriteLine("SetEnhanceTypes() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_EnhanceTypes;
					CREATE TABLE HDB_EnhanceTypes (
						Key NVARCHAR NOT NULL,
						Level INT NOT NULL,
						Chance DOUBLE NOT NULL,
						Risk NVARCHAR NOT NULL,
						Mat1Key NVARCHAR NOT NULL,
						Mat1IconKey NVARCHAR NOT NULL,
						Mat1Name NVARCHAR NOT NULL,
						Mat1Rarity INT NOT NULL,
						Mat1Count INT NOT NULL,
						Mat2Key NVARCHAR NOT NULL,
						Mat2IconKey NVARCHAR NOT NULL,
						Mat2Name NVARCHAR NOT NULL,
						Mat2Rarity INT NOT NULL,
						Mat2Count INT NOT NULL,
						Mat3Key NVARCHAR NOT NULL,
						Mat3IconKey NVARCHAR NOT NULL,
						Mat3Name NVARCHAR NOT NULL,
						Mat3Rarity INT NOT NULL,
						Mat3Count INT NOT NULL,
						Property NVARCHAR NOT NULL,
						Improvement DOUBLE NOT NULL,
						CONSTRAINT [unique] UNIQUE(Key, Level, Property)
					);
					INSERT INTO HDB_EnhanceTypes
					SELECT
						LOWER(ens.EnhanceType) AS Key,
						ens.EnhanceLevel AS Level,
						en.SuccessRatio AS Chance,
						CASE en.OnFail WHEN 'NoPenalty' THEN 'none' WHEN 'RankDown' THEN 'downgrade' WHEN 'RankReset' THEN 'reset' WHEN 'Destroy' THEN 'break' END AS Risk,
						m1.Key AS Mat1Key,
						m1.IconKey AS Mat1IconKey,
						m1.Name AS Mat1Name,
						m1.Rarity AS Mat1Rarity,
						en.Gold AS Mat1Count,
						m2.Key AS Mat2Key,
						m2.IconKey AS Mat2IconKey,
						m2.Name AS Mat2Name,
						m2.Rarity AS Mat2Rarity,
						en.MaterialNum1 AS Mat2Count,
						m3.Key AS Mat3Key,
						m3.IconKey AS Mat3IconKey,
						m3.Name AS Mat3Name,
						m3.Rarity AS Mat3Rarity,
						en.MaterialNum2 AS Mat3Count,
						CASE ens.Stat WHEN 'ATK' THEN 'patk' WHEN 'ATK_Absolute' THEN 'aatk' WHEN 'ATK_Speed' THEN 'speed' WHEN 'MaxDurability' THEN 'durability' ELSE LOWER(ens.Stat) END AS Property,
						CASE WHEN ens.Stat = 'MaxDurability' THEN ens.Value / 100 ELSE ens.Value END AS Improvement
					FROM EnhanceStatInfo AS ens
					INNER JOIN EnhanceInfo AS en ON
						en.EnhanceType = ens.EnhanceType AND
						en.EnhanceLevel = ens.EnhanceLevel
					INNER JOIN (
						SELECT e.EnhanceType
						FROM EquipItemInfo AS e
						INNER JOIN HDB_Classification AS c ON
							c.ObjectType = 'equip' AND
							c.ObjectID = e._ROWID_
						UNION
						SELECT ccp.EnhanceType
						FROM CombineCraftInfo AS cc
						INNER JOIN CombineCraftPartsInfo AS ccp ON ccp.PartsGroup IN (cc.PartsGroup1, cc.PartsGroup2, cc.PartsGroup3, cc.PartsGroup4, cc.PartsGroup5)
						INNER JOIN EquipItemInfo AS e ON e.ItemClass = cc.ItemClass
						INNER JOIN HDB_Classification AS c ON
							c.ObjectType = 'equip' AND
							c.ObjectID = e._ROWID_
					) AS e ON e.EnhanceType = ens.EnhanceType
					LEFT JOIN HDB_Mats AS m1 ON m1.Key = 'gold'
					LEFT JOIN HDB_Mats AS m2 ON m2.Key = en.MaterialClass1
					LEFT JOIN HDB_Mats AS m3 ON m3.Key = en.MaterialClass2
					WHERE ens.Stat != 'DEF_Destroyed';
				";
				command.ExecuteNonQuery();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetEnchants() {
			Debug.WriteLine("SetEnchants() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_Enchants;
					CREATE TABLE HDB_Enchants (
						ID INT PRIMARY KEY,
						Key NVARCHAR NOT NULL,
						Name NVARCHAR NOT NULL,
						Prefix INT NOT NULL,
						Level INT NOT NULL,
						Rank NVARCHAR NOT NULL,
						RestrictionsText NVARCHAR NOT NULL,
						MinSuccessChance INT NOT NULL,
						MaxSuccessChance INT NOT NULL,
						BreakChance INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(Key, Prefix)
					);
					INSERT INTO HDB_Enchants
					SELECT
						ei._ROWID_ AS ID,
						ei.EnchantClass AS Key,
						tn.Text AS Name,
						CASE WHEN ei.IsPrefix = 'True' THEN 1 ELSE 0 END AS Prefix,
						ei.EnchantLevel AS Level,
						CASE WHEN ei.EnchantLevel > 6 THEN CAST(9 - (ei.[EnchantLevel] - 7) AS NVARCHAR) ELSE CHAR(71 - ei.[EnchantLevel]) END AS Rank,
						tr.Text AS RestrictionsText,
						ei.MinSuccessRatio AS MinSuccessChance,
						ei.MaxSuccessRatio AS MaxSuccessChance,
						ei.DestructionRatio AS BreakChance
					FROM EnchantInfo AS ei
					INNER JOIN HDB_Text AS tn ON tn.Key = 'GAMEUI_HEROES_ATTRIBUTE_' || CASE WHEN ei.IsPrefix = 'True' THEN 'PREFIX' ELSE 'SUFFIX' END || '_' || UPPER(ei.EnchantClass)
					LEFT JOIN (
						SELECT
							t.EnchantClass,
							CASE WHEN t.Text LIKE 'For %' THEN t.Text ELSE 'For ' || t.Text END AS Text
						FROM (
							SELECT
								t.EnchantClass,
								CASE WHEN t.Text LIKE '%.' THEN t.Text ELSE t.Text || '.' END AS Text
							FROM (
								SELECT
									ei.EnchantClass,
									CASE
										WHEN t.Text IN ('For all Weapons', 'Can enchant to a Weapon') THEN 'For Weapons.'
										WHEN t.Text = 'For all Armor and Shields.' THEN 'For Armor and Shields.'
										WHEN t.Text = 'For Cloth Armor and Light Armor' THEN 'For Cloth and Light Armor.'
										WHEN t.Text = 'For Heavy Armor and Plate Armor' THEN 'For Heavy and Plate Armor.'
										WHEN t.Text = 'For Chest Armor' THEN 'For Tunics.'
										WHEN t.Text LIKE '% for enchanting' THEN REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(t.Text, ' Armor are available for enchanting', '.'), 'Head', 'Helms'), 'Leg', 'Pants'), 'Hand', 'Gloves'), 'Feet', 'Boots'), 'Armor,', 'Armor''s ')
										ELSE REPLACE(REPLACE(REPLACE(t.Text, 'Can enchant to a', 'For'), 'Can enchant to', 'For'), 'Can be enchanted with', 'For')
									END AS Text
								FROM EnchantInfo AS ei
								INNER JOIN HDB_Text AS t ON t.Key = 'GAMEUI_HEROES_ITEMCONSTRAINT_' || UPPER(ei.EnchantClass)
							) AS t
						) AS t
					) AS tr ON tr.EnchantClass = ei.EnchantClass
					WHERE
						ei.EnchantClass != 'test' AND
						ei.EnchantClass NOT LIKE 'pvp_%' AND
						ei.ItemConstraint != 'INNERARMOR' AND
						ei.Duration = 0 AND
						ei.MinSuccessRatio != 100 AND
						ei.EnchantClass NOT LIKE '%_100' AND
						tn.Text GLOB '*[A-z]*';
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_EnchantRestrictions;
					CREATE TABLE HDB_EnchantRestrictions (
						EnchantKey NVARCHAR NOT NULL,
						GroupKey NVARCHAR NOT NULL,
						TypeKey NVARCHAR,
						CategoryKey NVARCHAR,
						EquipKey NVARCHAR,
						CONSTRAINT [unique] UNIQUE(EnchantKey, GroupKey, TypeKey, CategoryKey, EquipKey)
					);
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					SELECT
						e.Key AS EnchantKey,
						ei.ItemConstraint AS Restrictions
					FROM HDB_Enchants AS e
					INNER JOIN EnchantInfo AS ei ON ei._ROWID_ = e.ID
				";
				var reader = command.ExecuteReader();
				var insertCommand = connection.CreateCommand();
				insertCommand.Transaction = transaction;
				insertCommand.CommandText = "INSERT INTO HDB_EnchantRestrictions VALUES (@EnchantKey, @GroupKey, @TypeKey, @CategoryKey, @EquipKey);";
				insertCommand.Parameters.Add("@EnchantKey", DbType.String);
				insertCommand.Parameters.Add("@GroupKey", DbType.String);
				insertCommand.Parameters.Add("@TypeKey", DbType.String);
				insertCommand.Parameters.Add("@CategoryKey", DbType.String);
				insertCommand.Parameters.Add("@EquipKey", DbType.String);
				var equipCommand = connection.CreateCommand();
				equipCommand.Transaction = transaction;
				equipCommand.CommandText = @"
					SELECT
						e.Key,
						e.GroupKey,
						e.TypeKey,
						e.CategoryKey
					FROM HDB_Equips AS e
					WHERE e.Key = @Key;
				";
				equipCommand.Parameters.Add("@Key", DbType.String);
				var mapping = new Dictionary<String, String[][]>() {
					{ "CLOTH",       new String[][] { new [] { "armor", "cloth", null } } },
					{ "LIGHT_ARMOR", new String[][] { new [] { "armor", "light", null } } },
					{ "HEAVY_ARMOR", new String[][] { new [] { "armor", "heavy", null } } },
					{ "PLATE_ARMOR", new String[][] { new [] { "armor", "plate", null } } },
					{ "HELM",        new String[][] { new [] { "armor", null, "helm" } } },
					{ "TUNIC",       new String[][] { new [] { "armor", null, "tunic" } } },
					{ "PANTS",       new String[][] { new [] { "armor", null, "pants" } } },
					{ "GLOVES",      new String[][] { new [] { "armor", null, "gloves" } } },
					{ "BOOTS",       new String[][] { new [] { "armor", null, "boots" } } },
					{ "SHIELD",      new String[][] { new [] { "offhand", "shield", "shield" } } },
					{ "LARGESHIELD", new String[][] { new [] { "offhand", "shield", "largeshield" } } },
					{ "SPELLBOOK",   new String[][] { new [] { "offhand", "other", "spellbook" } } },
					{ "ACCESSORY",   new String[][] { new [] { "accessory", "ordinary", null }, new [] { "accessory", "special", "artifact" } } },
					{ "WEAPON",      new String[][] { new [] { "weapon", null, null } } },
					{ "LONGSWORD",   new String[][] { new [] { "weapon", "all", "longsword" } } },
					{ "HAMMER",      new String[][] { new [] { "weapon", "all", "hammer" } } },
					{ "DUALSWORD",   new String[][] { new [] { "weapon", "all", "dualsword" } } },
					{ "DUALSPEAR",   new String[][] { new [] { "weapon", "all", "dualspear" } } },
					{ "STAFF",       new String[][] { new [] { "weapon", "all", "staff" } } },
					{ "SCYTHE",      new String[][] { new [] { "weapon", "all", "scythe" } } }
				};
				while (reader.Read()) {
					insertCommand.Parameters["@EnchantKey"].Value = reader["EnchantKey"];
					var restrictions = Convert.ToString(reader["Restrictions"]);
					// HACK: if they add more restrictions like this, then a real parser should be added
					if (restrictions.Contains("&")) {
						var restrictionParts =  Convert.ToString(reader["Restrictions"]).Split('&');
						restrictionParts[0] = restrictionParts[0].Substring(1, restrictionParts[0].Length - 2);
						restrictionParts[1] = restrictionParts[1].Substring(1, restrictionParts[1].Length - 2);
						var restrictionPart0Parts = restrictionParts[0].Split(',');
						var restrictionPart1Parts = restrictionParts[1].Split(',');
						insertCommand.Parameters["@EquipKey"].Value = null;
						foreach (var restriction1 in restrictionPart0Parts) {
							insertCommand.Parameters["@GroupKey"].Value = mapping[restriction1][0][0];
							insertCommand.Parameters["@TypeKey"].Value = mapping[restriction1][0][1];
							var filter = mapping[restriction1][0].Clone();
							foreach (var restriction2 in restrictionPart1Parts) {
								insertCommand.Parameters["@CategoryKey"].Value = mapping[restriction2][0][2];
								insertCommand.ExecuteNonQuery();
							}
						}
					}
					else {
						foreach (var restriction in restrictions.Split(',')) {
							if (mapping.ContainsKey(restriction)) {
								foreach (var filter in mapping[restriction]) {
									insertCommand.Parameters["@GroupKey"].Value = filter[0];
									insertCommand.Parameters["@TypeKey"].Value = filter[1];
									insertCommand.Parameters["@CategoryKey"].Value = filter[2];
									insertCommand.Parameters["@EquipKey"].Value = null;
									insertCommand.ExecuteNonQuery();
								}
							}
							else {
								equipCommand.Parameters["@Key"].Value = restriction.ToLower();
								var equipReader = equipCommand.ExecuteReader();
								if (!equipReader.Read()) {
									Debug.WriteLine(String.Concat("Ignored restriction: ", restriction));
								}
								else {
									insertCommand.Parameters["@GroupKey"].Value = equipReader["GroupKey"];
									insertCommand.Parameters["@TypeKey"].Value = equipReader["TypeKey"];
									insertCommand.Parameters["@CategoryKey"].Value = equipReader["CategoryKey"];
									insertCommand.Parameters["@EquipKey"].Value = equipReader["Key"];
									insertCommand.ExecuteNonQuery();
								}
								equipReader.Close();
							}
						}
					}
				}
				reader.Close();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_EnchantProperties;
					CREATE TABLE HDB_EnchantProperties (
						EnchantKey NVARCHAR NOT NULL,
						Property NVARCHAR,
						Value INT,
						Condition NVARCHAR,
						[Order] INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(EnchantKey, Property, Condition)
					);
					INSERT INTO HDB_EnchantProperties
					SELECT
						e.Key AS EnchantKey,
						CASE
							WHEN esi.Stat GLOB 'ATK[+-]*' THEN 'patk'
							WHEN esi.Stat GLOB 'MATK[+-]*' THEN 'matk'
							WHEN esi.Stat GLOB 'ATK_Speed[+-]*' THEN 'speed'
							WHEN esi.Stat GLOB 'Critical[+-]*' THEN 'crit'
							WHEN esi.Stat GLOB 'Balance[+-]*' THEN 'bal'
							WHEN esi.Stat GLOB 'HP[+-]*' THEN 'hp'
							WHEN esi.Stat GLOB 'DEF[+-]*' THEN 'def'
							WHEN esi.Stat GLOB 'Res_Critical[+-]*' THEN 'critres'
							WHEN esi.Stat GLOB 'STR[+-]*' THEN 'str'
							WHEN esi.Stat GLOB 'INT[+-]*' THEN 'int'
							WHEN esi.Stat GLOB 'DEX[+-]*' THEN 'dex'
							WHEN esi.Stat GLOB 'WILL[+-]*' THEN 'will'
							WHEN esi.Stat GLOB 'STAMINA[+-]*' THEN 'stamina'
							ELSE ts.Text
						END AS Property,
						CASE
							WHEN esi.Stat GLOB 'ATK[+-]*' THEN CAST(SUBSTR(esi.Stat, LENGTH('ATK+')) AS INT)
							WHEN esi.Stat GLOB 'MATK[+-]*' THEN CAST(SUBSTR(esi.Stat, LENGTH('MATK+')) AS INT)
							WHEN esi.Stat GLOB 'ATK_Speed[+-]*' THEN CAST(SUBSTR(esi.Stat, LENGTH('ATK_Speed+')) AS INT)
							WHEN esi.Stat GLOB 'Critical[+-]*' THEN CAST(SUBSTR(esi.Stat, LENGTH('Critical+')) AS INT)
							WHEN esi.Stat GLOB 'Balance[+-]*' THEN CAST(SUBSTR(esi.Stat, LENGTH('Balance+')) AS INT)
							WHEN esi.Stat GLOB 'HP[+-]*' THEN CAST(SUBSTR(esi.Stat, LENGTH('HP+')) AS INT)
							WHEN esi.Stat GLOB 'DEF[+-]*' THEN CAST(SUBSTR(esi.Stat, LENGTH('DEF+')) AS INT)
							WHEN esi.Stat GLOB 'Res_Critical[+-]*' THEN CAST(SUBSTR(esi.Stat, LENGTH('Res_Critical+')) AS INT)
							WHEN esi.Stat GLOB 'STR[+-]*' THEN CAST(SUBSTR(esi.Stat, LENGTH('STR+')) AS INT)
							WHEN esi.Stat GLOB 'INT[+-]*' THEN CAST(SUBSTR(esi.Stat, LENGTH('INT+')) AS INT)
							WHEN esi.Stat GLOB 'DEX[+-]*' THEN CAST(SUBSTR(esi.Stat, LENGTH('DEX+')) AS INT)
							WHEN esi.Stat GLOB 'WILL[+-]*' THEN CAST(SUBSTR(esi.Stat, LENGTH('WILL+')) AS INT)
							WHEN esi.Stat GLOB 'STAMINA[+-]*' THEN CAST(SUBSTR(esi.Stat, LENGTH('STAMINA+')) AS INT)
							ELSE NULL
						END AS Value,
						CASE WHEN LENGTH(RTRIM(tc.Text)) = 0 THEN NULL ELSE tc.Text END AS Condition,
						esi.[Order]
					FROM EnchantStatInfo AS esi
					INNER JOIN HDB_Enchants AS e ON e.Key = esi.EnchantClass
					LEFT JOIN HDB_Text AS tc ON tc.Key = UPPER(esi.ConditionDesc)
					LEFT JOIN HDB_Text AS ts ON ts.Key = UPPER(esi.StatDesc);
				";
				command.ExecuteNonQuery();
				transaction.Commit();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetIcons() {
			Debug.WriteLine("SetIcons() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_Icons;
					CREATE TABLE HDB_Icons (
						Key NVARCHAR,
						Icon NVARCHAR NOT NULL,
						IconBG NVARCHAR,
						Material1 NVARCHAR,
						Material2 NVARCHAR,
						Material3 NVARCHAR,
						CONSTRAINT [unique] UNIQUE(Key)
					);
					DROP TABLE IF EXISTS HDB_tempt;
					CREATE TABLE HDB_tempt (
						Icon NVARCHAR NOT NULL,
						IconBG NVARCHAR,
						Material1 NVARCHAR,
						Material2 NVARCHAR,
						Material3 NVARCHAR
					);
					INSERT INTO HDB_tempt
					SELECT DISTINCT
						i.Icon,
						i.IconBG,
						e.Material1,
						e.Material2,
						e.Material3
					FROM ItemClassInfo AS i
					LEFT JOIN EquipItemInfo AS e ON e.ItemClass = i.ItemClass;
					INSERT INTO HDB_Icons
					SELECT
						t.Icon || CASE WHEN t.RN == 0 THEN '' ELSE '_' || t.RN END AS Key,
						t.Icon,
						t.IconBG,
						t.Material1,
						t.Material2,
						t.Material3
					FROM (
						SELECT
							(SELECT COUNT(*) FROM HDB_tempt AS tc WHERE tc.Icon = t.Icon AND tc._ROWID_ < t._ROWID_) AS RN,
							t.*
						FROM HDB_tempt AS t
					) AS t;
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
					var iconFile = Path.Combine(this.config.IconImportPath, iconFileName);
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
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_Mats;
					CREATE TABLE HDB_Mats (
						ID INT PRIMARY KEY,
						Key NVARCHAR NOT NULL,
						IconKey NVARCHAR,
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
						ico.Key AS IconKey,
						tn.Text AS Name,
						c.Name AS Classification,
						td.Text AS Description,
						i.Rarity,
						CASE
							WHEN i.ItemClass = 'gold' THEN 4
							WHEN i.ItemClass LIKE 'cloth_lvl_' OR i.ItemClass LIKE 'skin_lvl_' OR i.ItemClass LIKE 'iron_ore_lvl_' THEN 3
							WHEN i.ItemClass LIKE 'combine_%part1_%' THEN 1
							ELSE 2
						END AS [Order]
					FROM HDB_Classification AS c
					INNER JOIN ItemClassInfo AS i ON i._ROWID_ = c.ObjectID
					INNER JOIN HDB_Text AS tn ON tn.Key = 'HEROES_ITEM_NAME_' || UPPER(i.ItemClass)
					LEFT JOIN HDB_Text AS td ON
						td.Key = 'HEROES_ITEM_DESC_' || UPPER(i.ItemClass) AND
						LENGTH(td.Text) > 0
					LEFT JOIN (
						SELECT
							MIN(ico.Key) AS Key,
							ico.Icon,
							ico.IconBG
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
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
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
						IconKey NVARCHAR,
						Name NVARCHAR NOT NULL,
						Classification NVARCHAR NOT NULL,
						Description NVARCHAR,
						Rarity INT NOT NULL,
						QualityTypeKey NVARCHAR,
						EnhanceTypeKey NVARCHAR,
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
						DURABILITY INT NOT NULL,
						WEIGHT INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(Key)
					);
					DROP TABLE IF EXISTS HDB_tempt;
					CREATE TABLE HDB_tempt (
						PartsGroup NVARCHAR NOT NULL,
						ItemClass NVARCHAR NOT NULL
					);
					INSERT INTO HDB_tempt
					SELECT
						ccp.PartsGroup,
						ccp.ItemClass
					FROM CombineCraftPartsInfo AS ccp
					INNER JOIN (
						SELECT
							ccp.PartsGroup,
							p.grade,
							MAX(SUBSTR(ccp.ItemClass, LENGTH(ccp.ItemClass), 1)) AS quality
						FROM CombineCraftPartsInfo AS ccp
						INNER JOIN (
							SELECT
								ccp.PartsGroup,
								MAX(SUBSTR(ccp.ItemClass, LENGTH(ccp.ItemClass) - 2, 1)) AS grade
							FROM CombineCraftPartsInfo AS ccp
							GROUP BY ccp.PartsGroup
						) AS p ON
							p.PartsGroup = ccp.PartsGroup AND
							p.grade = SUBSTR(ccp.ItemClass, LENGTH(ccp.ItemClass) - 2, 1)
						GROUP BY
							ccp.PartsGroup,
							p.grade
					) AS p ON
						p.PartsGroup = ccp.PartsGroup AND
						p.grade = SUBSTR(ccp.ItemClass, LENGTH(ccp.ItemClass) - 2, 1) AND
						p.quality = SUBSTR(ccp.ItemClass, LENGTH(ccp.ItemClass), 1);
					INSERT INTO HDB_Equips
					SELECT
						c.ObjectID AS ID,
						LOWER(e.ItemClass) AS Key,
						c.GroupKey,
						c.TypeKey,
						c.CategoryKey,
						ico.Key AS IconKey,
						tn.Text AS Name,
						c.Name AS classification,
						td.Text AS Description,
						COALESCE(e.Rarity, i.Rarity) AS Rarity,
						LOWER(e.QualityType) AS QualityTypeKey,
						LOWER(e.EnhanceType) AS EnhanceTypeKey,
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
						e.STAMINA,
						e.MaxDurability / 100 AS DURABILITY,
						e.Weight AS WEIGHT
					FROM HDB_Classification AS c
					INNER JOIN (
						SELECT
							e._ROWID_ AS ID,
							e.ItemClass,
							ce.Rarity,
							COALESCE(ce.QualityType, e.QualityType) AS QualityType,
							COALESCE(ce.EnhanceType, e.EnhanceType) AS EnhanceType,
							COALESCE(ce.ATK, e.ATK) AS ATK,
							COALESCE(ce.MATK, e.MATK) AS MATK,
							COALESCE(ce.ATK_Speed, e.ATK_Speed) AS ATK_Speed,
							COALESCE(ce.Critical, e.Critical) AS Critical,
							COALESCE(ce.Balance, e.Balance) AS Balance,
							COALESCE(ce.HP, e.HP) AS HP,
							COALESCE(ce.DEF, e.DEF) AS DEF,
							COALESCE(ce.Res_Critical, e.Res_Critical) AS Res_Critical,
							COALESCE(ce.STR, e.STR) AS STR,
							COALESCE(ce.INT, e.INT) AS INT,
							COALESCE(ce.DEX, e.DEX) AS DEX,
							COALESCE(ce.WILL, e.WILL) AS WILL,
							COALESCE(ce.STAMINA, e.STAMINA) AS STAMINA,
							e.MaxDurability,
							e.Weight,
							e.Material1,
							e.Material2,
							e.Material3
						FROM EquipItemInfo AS e
						LEFT JOIN (
							SELECT
								ce.*,
								ccp.QualityType,
								ccp.EnhanceType,
								ccp.EnchantMaxLevel
							FROM (
								SELECT
									cc.ItemClass,
									MAX(i.Rarity) AS Rarity,
									SUM(ccp.ATK) AS ATK,
									SUM(ccp.MATK) AS MATK,
									SUM(ccp.ATK_Speed) AS ATK_Speed,
									SUM(ccp.Critical) AS Critical,
									SUM(ccp.Balance) AS Balance,
									SUM(ccp.HP) AS HP,
									SUM(ccp.DEF) AS DEF,
									SUM(ccp.Res_Critical) AS Res_Critical,
									SUM(ccp.STR) AS STR,
									SUM(ccp.DEX) AS DEX,
									SUM(ccp.INT) AS INT,
									SUM(ccp.WILL) AS WILL,
									SUM(ccp.STAMINA) AS STAMINA
								FROM CombineCraftInfo AS cc
								INNER JOIN HDB_tempt AS bp ON bp.PartsGroup IN (cc.PartsGroup1, cc.PartsGroup2, cc.PartsGroup3, cc.PartsGroup4, cc.PartsGroup5)
								INNER JOIN CombineCraftPartsInfo AS ccp ON ccp.ItemClass = bp.ItemClass
								INNER JOIN ItemClassInfo AS i ON i.ItemClass = ccp.ItemClass
								GROUP BY cc.ItemClass
							) AS ce
							INNER JOIN CombineCraftInfo AS cc ON cc.ItemClass = ce.ItemClass
							INNER JOIN HDB_tempt AS bp ON bp.PartsGroup = cc.PartsGroup1
							INNER JOIN CombineCraftPartsInfo AS ccp ON ccp.ItemClass = bp.ItemClass
						) AS ce ON ce.ItemClass = e.ItemClass
					) AS e ON e.ID = c.ObjectID
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
						c.ObjectID != 3313 -- event_charm_dreamer_zhtw_2;
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
						CONSTRAINT [unique] UNIQUE(EquipKey, Type, MatKey)
					);
					INSERT INTO HDB_EquipRecipes
					SELECT
						e.Key AS EquipKey,
						fr.RecipeType AS Type,
						m.Key AS MatKey,
						rm.Num AS MatCount,
						t.Text AS AppearQuestName
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
						t.EquipKey,
						'npc' AS Type,
						t.MatKey,
						1 AS MatCount,
						tq.Text AS AppearQuestName
					FROM (
						SELECT DISTINCT
							e.Key AS EquipKey,
							ici.ItemClass AS MatKey
						FROM HDB_Equips AS e
						INNER JOIN (
							SELECT
								cci.ItemClass AS EquipKey,
								cci.PartsGroup1 AS PartGroup
							FROM CombineCraftInfo AS cci
							WHERE cci.PartsGroup1 IS NOT NULL
							UNION ALL
							SELECT
								cci.ItemClass,
								cci.PartsGroup2
							FROM CombineCraftInfo AS cci
							WHERE cci.PartsGroup2 IS NOT NULL
							UNION ALL
							SELECT
								cci.ItemClass,
								cci.PartsGroup3
							FROM CombineCraftInfo AS cci
							WHERE cci.PartsGroup3 IS NOT NULL
							UNION ALL
							SELECT
								cci.ItemClass,
								cci.PartsGroup4
							FROM CombineCraftInfo AS cci
							WHERE cci.PartsGroup4 IS NOT NULL
							UNION ALL
							SELECT
								cci.ItemClass,
								cci.PartsGroup5
							FROM CombineCraftInfo AS cci
							WHERE cci.PartsGroup5 IS NOT NULL
						) AS cci ON cci.EquipKey = e.Key
						INNER JOIN CombineCraftPartsInfo AS ccpi ON ccpi.PartsGroup = cci.PartGroup
						INNER JOIN ItemClassInfo AS ici ON ici.ItemClass = SUBSTR(ccpi.ItemClass, 1, LENGTH(ccpi.ItemClass) - 4)
					) AS t
					INNER JOIN CombineCraftInfo AS cci ON cci.ItemClass = t.EquipKey
					LEFT JOIN HDB_Text AS tq ON tq.Key = 'HEROES_QUEST_TITLE_' || UPPER(cci.AppearQuestID)
					UNION ALL
					SELECT
						cci.ItemClass AS EquipKey,
						'npc' AS Type,
						'gold' AS MatKey,
						350000 AS MatCount,
						tq.Text AS AppearQuestName
					FROM CombineCraftInfo AS cci
					LEFT JOIN HDB_Text AS tq ON tq.Key = 'HEROES_QUEST_TITLE_' || UPPER(cci.AppearQuestID);
					DROP TABLE IF EXISTS HDB_tempt;
					CREATE TABLE HDB_tempt (
						EquipKey NVARCHAR NOT NULL,
						Type NVARCHAR NOT NULL,
						MatKey NVARCHAR NOT NULL,
						MatCount INT NOT NULL,
						AppearQuestName NVARCHAR
					);
					INSERT INTO HDB_tempt
					SELECT 
						e.Key AS EquipKey,
						fr.RecipeType AS Type,
						m.Key AS MatKey,
						mm.Num AS MatCount,
						NULL AS AppearQuestName
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
					LEFT JOIN HDB_Mats AS m ON m.Key = LOWER(mm.ItemClass);
					INSERT INTO HDB_EquipRecipes
					SELECT DISTINCT t.*
					FROM HDB_tempt AS t;
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_EquipRecipeExpertises;
					CREATE TABLE HDB_EquipRecipeExpertises (
						EquipKey NVARCHAR NOT NULL,
						Name NVARCHAR,
						ExperienceRequired INT,
						CONSTRAINT [unique] UNIQUE(EquipKey, Name)
					);
					INSERT INTO HDB_EquipRecipeExpertises
					SELECT
						er.EquipKey,
						t.Text AS ExpertiseName,
						mr.ExperienceRequired AS ExperienceRequired
					FROM (
						SELECT DISTINCT er.EquipKey
						FROM HDB_EquipRecipes AS er
					) AS er
					INNER JOIN ManufactureMaterialInfo AS mmr ON
						mmr.Type = 0 AND
						LOWER(mmr.ItemClass) = er.EquipKey AND
						mmr.RecipeID != 'sewing_ingkara_lower'
					INNER JOIN ManufactureRecipeInfo AS mr ON mr.RecipeID = mmr.RecipeID
					INNER JOIN HDB_FeaturedRecipes AS fr ON
						fr.RecipeType = 'pc' AND
						fr.RecipeKey = LOWER(mr.RecipeID) AND (
							fr.RecipeFeature = mr.Feature OR
							COALESCE(fr.RecipeFeature, mr.Feature) IS NULL
						)
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
					INSERT INTO HDB_EquipRecipeShops
					SELECT
						cci.ItemClass AS EquipKey,
						'dianann' AS ShopKey,
						tn.Text AS ShopName
					FROM CombineCraftInfo AS cci
					LEFT JOIN HDB_Text AS tn ON tn.Key = 'HEROES_NPC_DIANANN';
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_EquipPartGroups;
					CREATE TABLE HDB_EquipPartGroups (
						EquipKey NVARCHAR NOT NULL,
						Key NVARCHAR NOT NULL,
						IconKey NVARCHAR NOT NULL,
						[Order] INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(EquipKey, Key)
					);
					INSERT INTO HDB_EquipPartGroups
					SELECT
						e.Key AS EquipKey,
						cci.PartGroup AS Key,
						cci.PartGroupIconKey AS IconKey,
						cci.PartGroupOrder AS [Order]
					FROM HDB_Equips AS e
					INNER JOIN (
						SELECT
							cci.ItemClass AS EquipKey,
							cci.PartsGroup1 AS PartGroup,
							cci.PartsGroupIcon1 AS PartGroupIconKey,
							1 AS PartGroupOrder
						FROM CombineCraftInfo AS cci
						WHERE cci.PartsGroup1 IS NOT NULL
						UNION ALL
						SELECT
							cci.ItemClass,
							cci.PartsGroup2,
							cci.PartsGroupIcon2,
							2 AS PartsGroupOrder
						FROM CombineCraftInfo AS cci
						WHERE cci.PartsGroup2 IS NOT NULL
						UNION ALL
						SELECT
							cci.ItemClass,
							cci.PartsGroup3,
							cci.PartsGroupIcon3,
							3 AS PartsGroupOrder
						FROM CombineCraftInfo AS cci
						WHERE cci.PartsGroup3 IS NOT NULL
						UNION ALL
						SELECT
							cci.ItemClass,
							cci.PartsGroup4,
							cci.PartsGroupIcon4,
							4 AS PartsGroupOrder
						FROM CombineCraftInfo AS cci
						WHERE cci.PartsGroup4 IS NOT NULL
						UNION ALL
						SELECT
							cci.ItemClass,
							cci.PartsGroup5,
							cci.PartsGroupIcon5,
							5 AS PartsGroupOrder
						FROM CombineCraftInfo AS cci
						WHERE cci.PartsGroup5 IS NOT NULL
					) AS cci ON cci.EquipKey = e.Key;
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_EquipScreenshots;
					CREATE TABLE HDB_EquipScreenshots (
						EquipKey NVARCHAR NOT NULL,
						EquipCostumeKey INT,
						EquipCostumeModel NVARCHAR,
						CharacterID INT NOT NULL,
						Camera NVARCHAR NOT NULL,
						Ready INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(EquipKey, CharacterID, Camera)
					);
					INSERT INTO HDB_EquipScreenshots
					SELECT
						e.Key AS EquipKey,
						NULL AS EquipCostumeKey,
						NULL AS EquipCostumeModel,
						c.ID AS CharacterID,
						CASE
							WHEN e.CategoryKey = 'dualsword' AND c.ID = 1 THEN '3blocf'
							WHEN e.CategoryKey = 'dualspear' THEN '3blocf'
							WHEN e.CategoryKey = 'longsword' THEN '3blocf'
							WHEN e.CategoryKey = 'hammer' THEN '3blocf'
							WHEN e.CategoryKey = 'staff' THEN '1frohf'
							WHEN e.CategoryKey = 'scythe' THEN '1frahf'
							WHEN e.CategoryKey = 'pillar' THEN '2frocf'
							WHEN e.CategoryKey = 'blaster' THEN '1dlbcf'
							WHEN e.CategoryKey = 'bow' THEN '4drocf'
							WHEN e.CategoryKey = 'crossgun' THEN '1rbocf'
							WHEN e.CategoryKey = 'dualsword' AND c.ID = 32 THEN '2dbocf'
							WHEN e.CategoryKey = 'dualblade' THEN '3dfocf'
							WHEN e.CategoryKey = 'greatsword' THEN '2lfacf'
							WHEN e.CategoryKey = 'battleglaive' THEN '1dfohf'
							WHEN e.CategoryKey = 'longblade' THEN '2blocf'
							WHEN e.CategoryKey = 'longblade' THEN '2blocf'
							WHEN e.CategoryKey = 'phantomdagger' THEN '5dbocf'
							WHEN e.CategoryKey = 'shield' THEN '3dbocf'
							WHEN e.CategoryKey = 'largeshield' THEN '3dbocf'
							ELSE '1frocf'
						END Camera,
						0 AS Ready
					FROM HDB_Equips AS e
					INNER JOIN EquipItemInfo AS ei ON ei._ROWID_ = e.ID
					INNER JOIN HDB_Characters AS c ON e.ClassRestriction | c.ID = e.ClassRestriction
					WHERE
						e.GroupKey = 'weapon' OR (
							e.GroupKey = 'offhand' AND
							e.TypeKey = 'shield'
						);
				";
				command.ExecuteNonQuery();
				transaction.Commit();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetEquipParts() {
			Debug.WriteLine("SetEquipParts() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_EquipParts;
					CREATE TABLE HDB_EquipParts (
						Key NVARCHAR NOT NULL,
						EquipPartGroupKey NVARCHAR NOT NULL,
						IconKey NVARCHAR,
						Name NVARCHAR NOT NULL,
						Description NVARCHAR,
						Rarity INT NOT NULL,
						QualityTypeKey NVARCHAR,
						EnhanceTypeKey NVARCHAR,
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
						MaxEnchantLevel INT,
						CONSTRAINT [unique] UNIQUE(Key)
					);
					INSERT INTO HDB_EquipParts
					SELECT
						ccpi.ItemClass AS Key,
						epg.Key AS EquipPartGroupKey,
						ico.Key AS IconKey,
						tn.Text AS Name,
						td.Text AS Description,
						ici.Rarity,
						LOWER(ccpi.QualityType) AS QualityTypeKey,
						LOWER(ccpi.EnhanceType) AS EnhanceTypeKey,
						CASE WHEN ccpi.MATK > 0 THEN ccpi.MATK ELSE ccpi.ATK END AS ATK,
						ccpi.ATK AS PATK,
						ccpi.MATK,
						ccpi.ATK_Speed AS SPEED,
						ccpi.Critical AS CRIT,
						ccpi.Balance AS BAL,
						ccpi.HP,
						ccpi.DEF,
						ccpi.Res_Critical AS CRITRES,
						ccpi.STR,
						ccpi.INT,
						ccpi.DEX,
						ccpi.WILL,
						ccpi.EnchantMaxLevel AS MaxEnchantLevel
					FROM (
						SELECT DISTINCT epg.Key
						FROM HDB_EquipPartGroups AS epg
					) AS epg
					INNER JOIN CombineCraftPartsInfo AS ccpi ON ccpi.PartsGroup = epg.Key
					INNER JOIN ItemClassInfo AS ici ON ici.ItemClass = ccpi.ItemClass
					INNER JOIN HDB_FeaturedItems AS fi ON fi.ItemID = ici._ROWID_
					LEFT JOIN HDB_Icons AS ico ON ico.Icon = ici.Icon
					INNER JOIN HDB_Text AS tn ON tn.Key = 'HEROES_ITEM_NAME_' || UPPER(ici.ItemClass)
					LEFT JOIN HDB_Text AS td ON td.Key = 'HEROES_ITEM_DESC_' || UPPER(ici.ItemClass)
					WHERE
						ccpi.ItemClass IN (
							SELECT MAX(ccpi.ItemClass)
							FROM CombineCraftPartsInfo AS ccpi
							INNER JOIN ItemClassInfo AS ici ON ici.ItemClass = ccpi.ItemClass
							GROUP BY
								ccpi.PartsGroup,
								ici.rarity
						);
				";
				command.ExecuteNonQuery();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void SetBaseEquips() {
			Debug.WriteLine("SetBaseEquips() {");
			Debug.Indent();
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_BaseEquips;
					CREATE TABLE HDB_BaseEquips (
						ID INT PRIMARY KEY,
						Key NVARCHAR NOT NULL,
						IconKey NVARCHAR,
						Name NVARCHAR NOT NULL,
						Description NVARCHAR,
						CONSTRAINT [unique] UNIQUE(Key)
					);
					INSERT INTO HDB_BaseEquips
					SELECT 
						i._ROWID_ AS ID,
						LOWER(i.ItemClass) AS Key,
						ico.Key AS IconKey,
						tn.Text AS Name,
						td.Text AS Description
					FROM (
						SELECT DISTINCT si.BaseItemClass
						FROM SetItemInfo AS si
						WHERE si.BaseItemClass != si.ItemClass
					) AS be
					LEFT JOIN ItemClassInfo i ON i.ItemClass = be.BaseItemClass
					LEFT JOIN HDB_Text AS tn ON tn.Key = 'HEROES_ITEM_NAME_' || UPPER(i.ItemClass)
					LEFT JOIN HDB_Text AS td ON
						td.Key = 'HEROES_ITEM_DESC_' || UPPER(i.ItemClass) AND
						td.Text != tn.Text
					LEFT JOIN (
						SELECT
							MIN(ico.Key) AS Key,
							ico.Icon,
							ico.IconBG
						FROM HDB_Icons AS ico
						GROUP BY
							ico.Icon,
							ico.IconBG
					) ico ON
						ico.Icon = i.Icon AND (
							ico.IconBG = i.IconBG OR
							COALESCE(ico.IconBG, i.IconBG) IS NULL
						)
					ORDER BY i._ROWID_;
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_BaseEquipEquips;
					CREATE TABLE HDB_BaseEquipEquips (
						BaseEquipKey NVARCHAR NOT NULL,
						EquipKey NVARCHAR NOT NULL,
						EquipIconKey NVARCHAR,
						EquipName NVARCHAR NOT NULL,
						EquipClassRestriction INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(BaseEquipKey, EquipKey)
					);
					INSERT INTO HDB_BaseEquipEquips
					SELECT
						be.Key AS BaseEquipKey,
						e.Key AS EquipKey,
						e.IconKey AS EquipIconKey,
						e.Name AS EquipName,
						e.ClassRestriction AS EquipClassRestriction
					FROM HDB_BaseEquips AS be
					INNER JOIN SetItemInfo AS si ON LOWER(si.BaseItemClass) = be.Key
					INNER JOIN HDB_Equips AS e ON e.key = LOWER(si.ItemClass);
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
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
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
						IconKey NVARCHAR,
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
						WEIGHT INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(Key)
					);
					INSERT INTO HDB_Sets
					SELECT
						c.ObjectID AS ID,
						LOWER(s.SetID) AS Key,
						c.GroupKey,
						c.TypeKey,
						ss.IconKey,
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
						ss.STAMINA,
						ss.Weight AS WEIGHT
					FROM (
						SELECT DISTINCT
							c.ObjectID,
							c.GroupKey,
							c.TypeKey
						FROM HDB_Classification AS c
						WHERE
							c.ObjectType = 'set' AND
							c.ObjectID NOT IN (76, 176)
					) AS c
					INNER JOIN SetInfo AS s ON s._ROWID_ = c.ObjectID
					INNER JOIN HDB_Text AS tn ON tn.Key = 'HEROES_ITEM_SETITEM_NAME_' || UPPER(s.SetID)
					INNER JOIN (
						SELECT
							s.SetID,
							MAX(CASE WHEN e.CategoryKey = 'helm' THEN e.IconKey ELSE NULL END) AS IconKey,
							MAX(e.Rarity) AS Rarity,
							MAX(e.RequiredLevel) AS RequiredLevel,
							MAX(si.ClassRestriction) AS ClassRestriction,
							SUM(e.ATK) AS ATK,
							SUM(e.ATK) AS PATK,
							SUM(e.MATK) AS MATK,
							SUM(e.SPEED) AS SPEED,
							SUM(e.CRIT) AS CRIT,
							SUM(e.BAL) AS BAL,
							SUM(e.HP) AS HP,
							SUM(e.DEF) AS DEF,
							SUM(e.CRITRES) AS CRITRES,
							SUM(e.STR) AS STR,
							SUM(e.INT) AS INT,
							SUM(e.DEX) AS DEX,
							SUM(e.WILL) AS WILL,
							SUM(e.STAMINA) AS STAMINA,
							SUM(e.WEIGHT) AS WEIGHT
						FROM SetInfo AS s
						INNER JOIN (
							SELECT
								si.SetID AS SetKey,
								MAX(e.Key) AS EquipKey,
								SUM(DISTINCT c.ID) AS ClassRestriction
							FROM SetItemInfo AS si
							LEFT JOIN HDB_BaseEquipEquips AS bee ON bee.BaseEquipKey = LOWER(si.BaseItemClass)
							INNER JOIN HDB_Equips AS e ON e.Key = COALESCE(bee.EquipKey, LOWER(si.BaseItemClass))
							INNER JOIN HDB_Characters AS c ON e.ClassRestriction | c.ID = e.ClassRestriction
							GROUP BY
								si.SetID,
								si.BaseItemClass
						) AS si ON si.SetKey = s.SetID
						INNER JOIN HDB_Equips AS e ON
							e.Key = si.EquipKey AND
							e.GroupKey = 'armor'
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
						/*
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
						*/
						INNER JOIN (
							SELECT
								si.SetID,
								COUNT(*) AS SetCount
							FROM (
								SELECT
									si.SetID,
									MAX(e.Key) AS EquipKey
								FROM SetItemInfo AS si
								LEFT JOIN HDB_BaseEquipEquips AS bee ON bee.BaseEquipKey = LOWER(si.BaseItemClass)
								INNER JOIN HDB_Equips AS e ON e.Key = COALESCE(bee.EquipKey, LOWER(si.BaseItemClass))
								GROUP BY
									si.SetID,
									si.BaseItemClass
							) AS si
							INNER JOIN HDB_Equips AS e ON
								e.Key = si.EquipKey AND
								e.GroupKey = 'armor'
							GROUP BY si.SetID
						) AS sem ON
							sem.SetID = se.SetID AND
							sem.SetCount = se.SetCount
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
						EquipKey NVARCHAR NOT NULL,
						EquipName NVARCHAR NOT NULL,
						Base INT NOT NULL,
						[Order] INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(SetKey, EquipKey, EquipName)
					);
					INSERT INTO HDB_SetParts
					SELECT
						s.Key AS SetKey,
						COALESCE(be.Key, e.Key) AS EquipKey,
						COALESCE(be.Name, e.Name) AS EquipName,
						CASE WHEN be.ID IS NOT NULL THEN 1 ELSE 0 END AS Base,
						CASE
							WHEN em.GroupKey = 'armor' THEN
								CASE em.CategoryKey WHEN 'helm' THEN 10 WHEN 'tunic' THEN 11 WHEN 'pants' THEN 12 WHEN 'gloves' THEN 13 WHEN 'boots' THEN 14 ELSE 15 END
							WHEN em.GroupKey = 'weapon' THEN 20
							ELSE 30
						END AS [Order]
					FROM HDB_Sets AS s
					INNER JOIN (
						SELECT DISTINCT
							si.SetID,
							si.BaseItemClass
						FROM SetItemInfo AS si
					) AS si ON LOWER(si.SetID) = s.Key
					LEFT JOIN HDB_Equips AS e ON e.Key = LOWER(si.BaseItemClass)
					LEFT JOIN HDB_BaseEquips AS be ON be.Key = LOWER(si.BaseItemClass)
					INNER JOIN (
						SELECT
							LOWER(si.SetID) AS SetKey,
							LOWER(si.BaseItemClass) AS BaseEquipKey,
							MAX(e.Key) AS EquipKey
						FROM SetItemInfo AS si
						LEFT JOIN HDB_BaseEquipEquips AS bee ON bee.BaseEquipKey = LOWER(si.BaseItemClass)
						INNER JOIN HDB_Equips AS e ON e.Key = COALESCE(bee.EquipKey, LOWER(si.BaseItemClass))
						GROUP BY
							LOWER(si.SetID),
							LOWER(si.BaseItemClass)
					) AS sim ON
						sim.SetKey = s.Key AND
						sim.BaseEquipKey = LOWER(si.BaseItemClass)
					LEFT JOIN HDB_Equips AS em ON em.Key = sim.EquipKey;
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
					DROP TABLE IF EXISTS HDB_SetScreenshots;
					CREATE TABLE HDB_SetScreenshots (
						SetKey NVARCHAR NOT NULL,
						CharacterID INT NOT NULL,
						Camera NVARCHAR NOT NULL,
						Ready INT NOT NULL,
						CONSTRAINT [unique] UNIQUE(SetKey, CharacterID, Camera)
					);
					INSERT INTO HDB_SetScreenshots
					SELECT
						s.Key AS SetKey,
						c.ID AS CharacterID,
						sc.Camera,
						0 AS Ready
					FROM HDB_Sets AS s
					INNER JOIN HDB_Characters AS c ON s.ClassRestriction | c.ID = s.ClassRestriction
					INNER JOIN (
						SELECT
							c.ID AS CharacterID,
							CASE c.ID
								WHEN 256 THEN '2dfocf'
								ELSE '1dfocf'
							END AS Camera
						FROM HDB_Characters AS c
						UNION ALL
						SELECT
							c.ID AS CharacterID,
							CASE c.ID
								WHEN 256 THEN '2dbocf'
								ELSE '1dbocf'
							END AS Camera
						FROM HDB_Characters AS c
					) AS sc ON sc.CharacterID = c.ID
					WHERE s.GroupKey = 'armor';
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_SetScreenshotParts;
					CREATE TABLE HDB_SetScreenshotParts (
						SetKey NVARCHAR NOT NULL,
						CharacterID INT NOT NULL,
						EquipKey NVARCHAR NOT NULL,
						EquipCostumeKey INT,
						CONSTRAINT [unique] UNIQUE(SetKey, CharacterID, EquipKey)
					);
					INSERT INTO HDB_SetScreenshotParts
					SELECT
						ss.SetKey,
						ss.CharacterID,
						e.Key AS EquipKey,
						NULL AS EquipCostumeKey
					FROM (
						SELECT DISTINCT
							ss.SetKey,
							ss.CharacterID
						FROM HDB_SetScreenshots AS ss
					) AS ss
					INNER JOIN HDB_SetParts AS sp ON sp.SetKey = ss.SetKey
					LEFT JOIN HDB_BaseEquipEquips AS bee ON bee.BaseEquipKey = sp.EquipKey
					INNER JOIN HDB_Equips AS e ON
						e.Key = COALESCE(bee.EquipKey, sp.EquipKey) AND (
							e.GroupKey = 'armor' OR (
								e.GroupKey = 'weapon' AND
								e.CategoryKey IN ('battleglaive', 'bow', 'dualsword', 'greatsword', 'longblade', 'longsword', 'pillar', 'staff', 'phantomdagger')
							)
						) AND
						e.ClassRestriction = e.ClassRestriction | ss.CharacterID;
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

		public void SetScreenshots() {
			Debug.WriteLine("SetScreenshots() {");
			Debug.Indent();
			var block = new Regex(@"^\s*""(?<key>\w+)""\s*$");
			var property = new Regex(@"^\s*""(?<key>\w+)""\s*""(?<value>[\/\.\w]*)""\s*$");
			Func<String[], Int32, Tuple<Int32, Dictionary<String, Object>>> parseNode = null;
			parseNode = (lines, lineIndex) => {
				var node = new Dictionary<String, Object>();
				for (var li = lineIndex; li < lines.Length; li += 1) {
					var line = lines[li];
					var comment = line.IndexOf("//");
					if (comment != -1) {
						line = line.Substring(0, comment);
					}
					var match = block.Match(line);
					if (match.Success) {
						for (; li < lines.Length; li += 1) {
							if (lines[li].Trim() == "{") {
								var result = parseNode(lines, li);
								li = result.Item1;
								node.Add(match.Groups["key"].Value, result.Item2);
								break;
							}
						}
					}
					else {
						match = property.Match(line);
						if (match.Success) {
							node.Add(match.Groups["key"].Value, match.Groups["value"].Value);
						}
						else if (line.Trim() == "}") {
							return Tuple.Create(li, node);
						}
					}
				}
				return Tuple.Create(lines.Length, node);
			};
			var linesTmp = File.ReadAllLines(Path.Combine(this.config.HfsPath, "player_costume.txt.comp"));
			var document = parseNode(linesTmp, 0).Item2;
			var costumes = new Dictionary<String, Dictionary<String, Dictionary<String, String>>>();
			var costumeSectionCategories = new Dictionary<String, String> {
				{ "upper_body", "tunic" },
				{ "lower_body", "pants" },
				{ "head", "helm" },
				{ "foot", "boots" },
				{ "hand", "gloves" }
			};
			foreach (var costumeSectionCategory in costumeSectionCategories) {
				var costumeSection = costumeSectionCategory.Key;
				var category = costumeSectionCategory.Value;
				var costumeSectionNode = (Dictionary<String, Object>)document[costumeSection];
				foreach (var costume in costumeSectionNode.Keys) {
					var costumeNode = (Dictionary<String, Object>)costumeSectionNode[costume];
					var costumeCategory = costumeNode.ContainsKey("category") ? Convert.ToString(costumeNode["category"]) : "";
					var costumeLabel = Convert.ToString(costumeNode["label"]);
					if (!costumes.ContainsKey(category)) {
						costumes.Add(category, new Dictionary<String, Dictionary<String, String>>());
					}
					if (!costumes[category].ContainsKey(costumeCategory)) {
						costumes[category].Add(costumeCategory, new Dictionary<String, String>());
					}
					if (costumes[category][costumeCategory].ContainsKey(costumeLabel)) {
						Debug.WriteLine("duplicate costume: {0}, {1}, {2}, {3}", category, costumeCategory, costumeLabel, costume);
					}
					else {
						costumes[category][costumeCategory].Add(costumeLabel, costume);
					}
				}
			}
			linesTmp = File.ReadAllLines(Path.Combine(this.config.HfsPath, "shields.txt.comp"));
			document = parseNode(linesTmp, 0).Item2;
			var shields = new Dictionary<String, String>();
			var shieldSectionNode = (Dictionary<String, Object>)document["Fiona"];
			foreach (var shield in shieldSectionNode.Keys) {
				if (shieldSectionNode[shield] is Dictionary<String, Object>) {
					var shieldNode = (Dictionary<String, Object>)shieldSectionNode[shield];
					shields.Add(shield, Convert.ToString(shieldNode["normal"]));
				}
			}
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					UPDATE HDB_EquipScreenshots
					SET
						EquipCostumeKey = NULL,
						Ready = 0;
				";
				command.ExecuteNonQuery();
				var updateCommand = connection.CreateCommand();
				updateCommand.Transaction = transaction;
				updateCommand.CommandText = @"
					UPDATE HDB_EquipScreenshots
					SET
						EquipCostumeKey = @EquipCostumeKey,
						EquipCostumeModel = @EquipCostumeModel,
						Ready = @Ready
					WHERE
						HDB_EquipScreenshots.EquipKey = @EquipKey AND
						HDB_EquipScreenshots.CharacterID = @CharacterID AND
						HDB_EquipScreenshots.Camera = @Camera;
				";
				updateCommand.Parameters.Add("@EquipCostumeKey", DbType.Int32);
				updateCommand.Parameters.Add("@EquipCostumeModel", DbType.String);
				updateCommand.Parameters.Add("@Ready", DbType.Int32);
				updateCommand.Parameters.Add("@EquipKey", DbType.String);
				updateCommand.Parameters.Add("@CharacterID", DbType.Int32);
				updateCommand.Parameters.Add("@Camera", DbType.String);
				command.CommandText = @"
					SELECT
						es.EquipKey,
						es.CharacterID,
						es.Camera,
						e.GroupKey AS EquipGroupKey,
						e.CategoryKey AS EquipCategoryKey,
						ei.CostumeType AS EquipCostumeLabel,
						CASE
							WHEN es.CharacterID IN (1, 16, 512) THEN 'male'
							WHEN es.CharacterID IN (2, 4, 32, 128, 256) THEN 'female'
							WHEN es.CharacterID IN (8) THEN 'giant'
							WHEN es.CharacterID IN (64) THEN 'tall'
						END AS EquipCostumeCategory
					FROM HDB_EquipScreenshots AS es
					INNER JOIN HDB_Equips AS e ON e.Key = es.EquipKey
					INNER JOIN EquipItemInfo AS ei ON ei._ROWID_ = e.ID;
				";
				var reader = command.ExecuteReader();
				var screenshotPath = Path.Combine(this.config.RootPath, "screenshots", "equips");
				var usedScreenshotFiles = new List<String>();
				while (reader.Read()) {
					String costume = null;
					String model = null;
					var group = Convert.ToString(reader["EquipGroupKey"]);
					var category = Convert.ToString(reader["EquipCategoryKey"]);
					var costumeCategory = Convert.ToString(reader["EquipCostumeCategory"]);
					var costumeLabel = Convert.ToString(reader["EquipCostumeLabel"]);
					if (group == "armor") {
						if (costumes[category].ContainsKey(costumeCategory) && costumes[category][costumeCategory].ContainsKey(costumeLabel)) {
							costume = costumes[category][costumeCategory][costumeLabel];
						}
						else if (costumes[category].ContainsKey("") && costumes[category][""].ContainsKey(costumeLabel)) {
							costume = costumes[category][""][costumeLabel];
						}
						else {
							Debug.WriteLine("missing costume: {0}, {1}, {2}, {3}", category, costumeCategory, costumeLabel, costume);
						}
					}
					else if (group == "offhand") {
						model = shields[costumeLabel];
					}
					else {
						costume = costumeLabel;
					}
					var ready = 0;
					var screenshotFilename = String.Concat(reader["EquipKey"], "_", reader["CharacterID"], reader["Camera"], ".jpeg");
					var screenshotFile = Path.Combine(screenshotPath, screenshotFilename);
					if (File.Exists(screenshotFile)) {
						ready = 1;
						usedScreenshotFiles.Add(screenshotFile);
					}
					updateCommand.Parameters["@EquipCostumeKey"].Value = costume;
					updateCommand.Parameters["@EquipCostumeModel"].Value = model;
					updateCommand.Parameters["@Ready"].Value = ready;
					updateCommand.Parameters["@EquipKey"].Value = reader["EquipKey"];
					updateCommand.Parameters["@CharacterID"].Value = reader["CharacterID"];
					updateCommand.Parameters["@Camera"].Value = reader["Camera"];
					updateCommand.ExecuteNonQuery();
				}
				reader.Close();
				if (Directory.Exists(screenshotPath)) {
					foreach (var screenshotFile in Directory.GetFiles(screenshotPath)) {
						if (!usedScreenshotFiles.Contains(screenshotFile)) {
							File.Delete(screenshotFile);
						}
					}
				}
				command.CommandText = @"
					UPDATE HDB_SetScreenshots
					SET Ready = 0;
				";
				command.ExecuteNonQuery();
				updateCommand = connection.CreateCommand();
				updateCommand.Transaction = transaction;
				updateCommand.CommandText = @"
					UPDATE HDB_SetScreenshots
					SET Ready = 1
					WHERE
						HDB_SetScreenshots.SetKey = @SetKey AND
						HDB_SetScreenshots.CharacterID = @CharacterID AND
						HDB_SetScreenshots.Camera = @Camera;
				";
				updateCommand.Parameters.Add("@SetKey", DbType.String);
				updateCommand.Parameters.Add("@CharacterID", DbType.Int32);
				updateCommand.Parameters.Add("@Camera", DbType.String);
				command.CommandText = @"
					SELECT
						ss.SetKey,
						ss.CharacterID,
						ss.Camera
					FROM HDB_SetScreenshots AS ss;
				";
				reader = command.ExecuteReader();
				screenshotPath = Path.Combine(this.config.RootPath, "screenshots", "sets");
				usedScreenshotFiles.Clear();
				while (reader.Read()) {
					var screenshotFilename = String.Concat(reader["SetKey"], "_", reader["CharacterID"], reader["Camera"], ".jpeg");
					var screenshotFile = Path.Combine(screenshotPath, screenshotFilename);
					if (File.Exists(screenshotFile)) {
						usedScreenshotFiles.Add(screenshotFile);
						updateCommand.Parameters["@SetKey"].Value = reader["SetKey"];
						updateCommand.Parameters["@CharacterID"].Value = reader["CharacterID"];
						updateCommand.Parameters["@Camera"].Value = reader["Camera"];
						updateCommand.ExecuteNonQuery();
					}
				}
				reader.Close();
				if (Directory.Exists(screenshotPath)) {
					foreach (var screenshotFile in Directory.GetFiles(screenshotPath)) {
						if (!usedScreenshotFiles.Contains(screenshotFile)) {
							File.Delete(screenshotFile);
						}
					}
				}
				command.CommandText = @"
					UPDATE HDB_SetScreenshotParts
					SET EquipCostumeKey = NULL;
				";
				command.ExecuteNonQuery();
				updateCommand = connection.CreateCommand();
				updateCommand.Transaction = transaction;
				updateCommand.CommandText = @"
					UPDATE HDB_SetScreenshotParts
					SET EquipCostumeKey = @EquipCostumeKey
					WHERE
						HDB_SetScreenshotParts.EquipKey = @EquipKey AND
						HDB_SetScreenshotParts.CharacterID = @CharacterID;
				";
				updateCommand.Parameters.Add("@EquipCostumeKey", DbType.Int32);
				updateCommand.Parameters.Add("@EquipKey", DbType.String);
				updateCommand.Parameters.Add("@CharacterID", DbType.Int32);
				command.CommandText = @"
					SELECT
						ssp.EquipKey,
						ss.CharacterID,
						e.GroupKey AS EquipGroupKey,
						e.CategoryKey AS EquipCategoryKey,
						ei.CostumeType AS EquipCostumeLabel,
						CASE
							WHEN ss.CharacterID IN (1, 16, 512) THEN 'male'
							WHEN ss.CharacterID IN (2, 4, 32, 128, 256) THEN 'female'
							WHEN ss.CharacterID IN (8) THEN 'giant'
							WHEN ss.CharacterID IN (64) THEN 'tall'
						END AS EquipCostumeCategory
					FROM (
						SELECT DISTINCT
							ss.SetKey,
							ss.CharacterID
						FROM HDB_SetScreenshots AS ss
					) AS ss
					INNER JOIN HDB_SetScreenshotParts AS ssp ON
						ssp.SetKey = ss.SetKey AND
						ssp.CharacterID = ss.CharacterID
					INNER JOIN HDB_Equips AS e ON e.Key = ssp.EquipKey
					INNER JOIN EquipItemInfo AS ei ON ei._ROWID_ = e.ID;
				";
				reader = command.ExecuteReader();
				while (reader.Read()) {
					String costume = null;
					var group = Convert.ToString(reader["EquipGroupKey"]);
					var category = Convert.ToString(reader["EquipCategoryKey"]);
					var costumeCategory = Convert.ToString(reader["EquipCostumeCategory"]);
					var costumeLabel = Convert.ToString(reader["EquipCostumeLabel"]);
					if (group != "armor") {
						costume = costumeLabel;
					}
					else {
						if (costumes[category].ContainsKey(costumeCategory) && costumes[category][costumeCategory].ContainsKey(costumeLabel)) {
							costume = costumes[category][costumeCategory][costumeLabel];
						}
						else if (costumes[category].ContainsKey("") && costumes[category][""].ContainsKey(costumeLabel)) {
							costume = costumes[category][""][costumeLabel];
						}
						else {
							Debug.WriteLine("missing costume: {0}, {1}, {2}, {3}", category, costumeCategory, costumeLabel, costume);
						}
					}
					updateCommand.Parameters["@EquipCostumeKey"].Value = costume;
					updateCommand.Parameters["@EquipKey"].Value = reader["EquipKey"];
					updateCommand.Parameters["@CharacterID"].Value = reader["CharacterID"];
					updateCommand.ExecuteNonQuery();
				}
				transaction.Commit();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

	}

}
