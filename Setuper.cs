
using System.Collections.Generic;
using System.Configuration;
using System.Data.SQLite;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System;

namespace HeroesDB {

	public class Setuper {

		private String databaseFile;

		private List<String> enabledFeatures;

		public Setuper(string databaseFile) {
			this.databaseFile = databaseFile;
		}

		private void loadFeatures() {
			var region = ConfigurationManager.AppSettings["Region"];
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
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

		private Boolean isFeatured(String itemFeatures) {
			if (this.enabledFeatures == null) {
				this.loadFeatures();
			}
			var test = new Regex(@"^(?<item>((!)?\w+)|(\|\|)|(&&))+$");
			var match = test.Match(itemFeatures);
			var captures = match.Groups["item"].Captures;
			var featuredItem = true;
			for (var i = 0; i < captures.Count; i++) {
				if (captures[i].Value == "&&" || captures[i].Value == "||") {
					continue;
				}
				var disabledFeature = captures[i].Value.StartsWith("!", StringComparison.InvariantCulture);
				var existingFeature = this.enabledFeatures.Contains(captures[i].Value.Replace("!", ""));
				var enabledFeature = (!disabledFeature && existingFeature) || (disabledFeature && !existingFeature) ? true : false;
				if (i == 0) {
					featuredItem = enabledFeature;
				}
				else {
					if (captures[i - 1].Value == "||") {
						featuredItem = featuredItem || enabledFeature;
					}
					else if (captures[i - 1].Value == "&&") {
						featuredItem = featuredItem && enabledFeature;
					}
				}
			}
			return featuredItem;
		}

		public void ImportText(String textFile) {
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_Text;
					CREATE TABLE HDB_Text (
						[Key] NVARCHAR(250) PRIMARY KEY NOT NULL UNIQUE,
						Text NVARCHAR(5000) NOT NULL
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
		}

		public void SetCharacters() {
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_Characters;
					CREATE TABLE HDB_Characters (
						ID INT PRIMARY KEY NOT NULL UNIQUE,
						Name NVARCHAR(50) NOT NULL,
						Description NVARCHAR(50) NOT NULL
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
		}

		public void SetFeaturedEquipItems() {
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_FeaturedEquipItem;
					CREATE TABLE HDB_FeaturedEquipItem (
						ItemID INT NOT NULL UNIQUE
					);
					INSERT INTO HDB_FeaturedEquipItem
					SELECT i._ROWID_
					FROM EquipItemInfo AS i
					WHERE i.Feature IS NULL;
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					SELECT
						i._ROWID_ AS ID,
						i.Feature
					FROM EquipItemInfo AS i
					WHERE i.Feature IS NOT NULL;
				";
				var reader = command.ExecuteReader();
				var featuredItems = new List<Int32>();
				while (reader.Read()) {
					if (this.isFeatured(Convert.ToString(reader["Feature"]))) {
						featuredItems.Add(Convert.ToInt32(reader["ID"]));
					}
				}
				reader.Close();
				command.CommandText = "INSERT INTO HDB_FeaturedEquipItem VALUES (@ID);";
				command.Parameters.Add("@ID", DbType.Int32);
				foreach (var id in featuredItems) {
					command.Parameters["@ID"].Value = id;
					command.ExecuteNonQuery();
				}
				transaction.Commit();
			}
		}

		public void SetFeaturedItemClasses() {
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_FeaturedItemClass;
					CREATE TABLE HDB_FeaturedItemClass (
						ItemID INT NOT NULL UNIQUE
					);
					INSERT INTO HDB_FeaturedItemClass
					SELECT i._ROWID_
					FROM ItemClassInfo AS i
					WHERE i.Feature IS NULL;
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					SELECT
						i._ROWID_ AS ID,
						i.Feature
					FROM ItemClassInfo AS i
					WHERE i.Feature IS NOT NULL;
				";
				var reader = command.ExecuteReader();
				var featuredItems = new List<Int32>();
				while (reader.Read()) {
					if (this.isFeatured(Convert.ToString(reader["Feature"]))) {
						featuredItems.Add(Convert.ToInt32(reader["ID"]));
					}
				}
				reader.Close();
				command.CommandText = "INSERT INTO HDB_FeaturedItemClass VALUES (@ID);";
				command.Parameters.Add("@ID", DbType.Int32);
				foreach (var id in featuredItems) {
					command.Parameters["@ID"].Value = id;
					command.ExecuteNonQuery();
				}
				transaction.Commit();
			}
		}

		public void SetItemStats() {
			var region = ConfigurationManager.AppSettings["Region"];
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = String.Format(@"
					DROP TABLE IF EXISTS HDB_ItemStat;
					CREATE TABLE HDB_ItemStat (
						[Key] NVARCHAR(50) PRIMARY KEY NOT NULL,
						Type NVARCHAR(50) NOT NULL,
						Name NVARCHAR(50) NOT NULL,
						ShortName NVARCHAR(50) NOT NULL,
						Description NVARCHAR(100),
						[Order] INT
					);
					INSERT INTO HDB_ItemStat
					SELECT
						ist.StatName AS [Key],
						'Base' AS Type,
						tn.Text AS Name,
						tsn.Text AS ShortName,
						td.Text AS Description,
						ist.[Order]
					FROM ItemStatInfo AS ist
					LEFT JOIN FeatureMatrix AS f ON
						f.Feature = 'ExtremeMode' AND
						f.[{0}] IS NOT NULL
					LEFT JOIN HDB_Text AS tn ON tn.Key = 'GAMEUI_HEROES_ITEMSTAT_' || UPPER(ist.StatName)
					LEFT JOIN HDB_Text AS tsn ON tsn.Key = 'GAMEUI_HEROES_CHARACTER_DIALOG_' || UPPER(ist.StatName)
					LEFT JOIN HDB_Text AS td ON td.Key = 'GAMEUI_HEROES_STATTOOLTIP_' || CASE WHEN f.Feature IS NOT NULL THEN 'EX_' ELSE '' END || UPPER(ist.StatName)
					WHERE ist.StatName IN ('ATK', 'MATK', 'ATK_Speed', 'Critical', 'Balance', 'DEF', 'Res_Critical', 'STR', 'DEX', 'INT', 'WILL', 'STAMINA')
					UNION ALL
					SELECT 'HDBATK', 'HDBATK', 'ATT', 'ATT', NULL, -100
					UNION ALL
					SELECT 'RequiredLevel', 'RequiredLevel', 'Required Level', 'Level', NULL, 101
					UNION ALL
					SELECT 'ClassRestriction', 'ClassRestriction', 'Character Restriction', 'Character', NULL, 100;
				", region);
				command.ExecuteNonQuery();
			}
		}

		public void SetClassification() {
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var transaction = connection.BeginTransaction();
				var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_ItemClassification;
					CREATE TABLE HDB_ItemClassification (
						ItemID INT NOT NULL,
						EquipID INT NOT NULL,
						SetID INT,
						GroupKey NVARCHAR(50),
						GroupName NVARCHAR(100),
						GroupOrder INT NOT NULL,
						TypeKey NVARCHAR(50),
						TypeName NVARCHAR(100),
						TypeOrder INT NOT NULL,
						TypePrimaryStats NVARCHAR(250),
						CategoryKey NVARCHAR(50),
						CategoryName NVARCHAR(100),
						CategoryOrder INT NOT NULL
					);

					INSERT INTO HDB_ItemClassification
					SELECT
						i._ROWID_ AS ItemID,
						e._ROWID_ AS EquipID,
						NULL AS SetID,
						i.Category AS GroupKey,
						tgn.Text AS GroupName,
						1 AS GroupOrder,
						'ALL' AS TypeKey,
						'All' AS TypeName,
						1 AS TypeOrder,
						'RequiredLevel, HDBATK, Balance, Critical, ATK_Speed' AS TypePrimaryStats,
						e.EquipClass AS CategoryKey,
						tcn.Text AS CategoryName,
						CASE e.EquipClass WHEN 'DUALSWORD' THEN 1 WHEN 'DUALSPEAR' THEN 2 WHEN 'LONGSWORD' THEN 3 WHEN 'HAMMER' THEN 4 WHEN 'STAFF' THEN 5 WHEN 'SCYTHE' THEN 6 WHEN 'PILLAR' THEN 7 WHEN 'BLASTER' THEN 8 WHEN 'BOW' THEN 9 WHEN 'CROSSGUN' THEN 10 WHEN 'DUALBLADE' THEN 11 WHEN 'GREATSWORD' THEN 12 WHEN 'BATTLEGLAIVE' THEN 13 WHEN 'LONGBLADE' THEN 14 ELSE 15 END AS CategoryOrder 
					FROM ItemClassInfo AS i
					INNER JOIN HDB_FeaturedItemClass AS fi ON fi.ItemID = i._ROWID_
					INNER JOIN EquipItemInfo AS e ON e.ItemClass = i.ItemClass
					INNER JOIN HDB_FeaturedEquipItem AS fe ON fe.ItemID = e._ROWID_
					INNER JOIN HDB_Text AS tgn ON tgn.[Key] = 'HEROES_ITEM_EQUIP_PART_NAME_' || i.Category
					INNER JOIN HDB_Text AS tcn ON tcn.[Key] = 'HEROES_ITEM_EQUIP_CLASS_NAME_' || e.EquipClass
					WHERE i.Category = 'WEAPON';

					INSERT INTO HDB_ItemClassification
					SELECT
						i._ROWID_ AS ItemID,
						e._ROWID_ AS EquipID,
						NULL AS SetID,
						'ARMOR' AS GroupKey,
						tgn.Text AS GroupName,
						2 AS GroupOrder,
						e.EquipClass AS TypeKey,
						REPLACE(ttn.Text, ' Armor', '') AS TypeName,
						CASE e.EquipClass WHEN 'CLOTH' THEN 2 WHEN 'LIGHT_ARMOR' THEN 3 WHEN 'HEAVY_ARMOR' THEN 4 WHEN 'PLATE_ARMOR' THEN 5 END AS TypeOrder,
						'ClassRestriction, RequiredLevel, DEF, STR, INT' AS TypePrimaryStats,
						i.Category AS CategoryKey,
						tcn.Text AS CategoryName,
						CASE i.Category WHEN 'HELM' THEN 1 WHEN 'TUNIC' THEN 2 WHEN 'PANTS' THEN 3 WHEN 'GLOVES' THEN 4 WHEN 'BOOTS' THEN 5 END AS CategoryOrder
					FROM ItemClassInfo AS i
					INNER JOIN HDB_FeaturedItemClass AS fi ON fi.ItemID = i._ROWID_
					INNER JOIN EquipItemInfo AS e ON e.ItemClass = i.ItemClass
					INNER JOIN HDB_FeaturedEquipItem AS fe ON fe.ItemID = e._ROWID_
					INNER JOIN HDB_Text AS tgn ON tgn.[Key] = 'HEROES_ITEM_TRADECATEGORY_NAME_ARMOR'
					INNER JOIN HDB_Text AS ttn ON ttn.[Key] = 'HEROES_ITEM_EQUIP_CLASS_NAME_' || e.EquipClass
					INNER JOIN HDB_Text AS tcn ON tcn.[Key] = 'HEROES_ITEM_EQUIP_PART_NAME_' || i.Category
					WHERE e.EquipClass IN ('CLOTH', 'LIGHT_ARMOR', 'HEAVY_ARMOR', 'PLATE_ARMOR');

					INSERT INTO HDB_ItemClassification
					SELECT
						i._ROWID_ AS ItemID,
						e._ROWID_ AS EquipID,
						s._ROWID_ AS SetID,
						'ARMOR' AS GroupKey,
						tgn.Text AS GroupName,
						2 AS GroupOrder,
						'SET' AS TypeKey,
						'Set' AS TypeName,
						1 AS TypeOrder,
						'RequiredLevel, DEF, STR, INT, DEX, WILL' AS TypePrimaryStats,
						UPPER(ec.Name) AS CategoryKey,
						ec.Name AS CategoryName,
						ec.ID AS CategoryOrder
					FROM SetInfo AS s
					INNER JOIN SetItemInfo AS si ON si.SetID = s.SetID
					INNER JOIN ItemClassInfo AS i ON i.ItemClass = si.ItemClass
					INNER JOIN EquipItemInfo AS e ON e.ItemClass = si.ItemClass 
					INNER JOIN (
						SELECT
							si.SetID,
							COUNT(*) AS c
						FROM SetItemInfo AS si
						WHERE si.ItemClass = si.BaseItemClass
						GROUP BY si.SetID
					) AS sic ON sic.SetID = s.SetID
					INNER JOIN (
						SELECT
							si.SetID,
							c.ID,
							c.Name,
							COUNT(*) AS c
						FROM SetItemInfo AS si
						INNER JOIN ItemClassInfo AS i ON i.ItemClass = si.ItemClass
						INNER JOIN HDB_FeaturedItemClass AS fi ON fi.ItemID = i._ROWID_
						INNER JOIN EquipItemInfo AS e ON e.ItemClass = i.ItemClass
						INNER JOIN HDB_FeaturedEquipItem AS fe ON fe.ItemID = e._ROWID_
						INNER JOIN HDB_Characters AS c ON i.ClassRestriction & c.ID = c.ID
						WHERE e.EquipClass IN ('CLOTH', 'LIGHT_ARMOR', 'HEAVY_ARMOR', 'PLATE_ARMOR')
						GROUP BY
							si.SetID,
							c.ID,
							c.Name
					) AS ec ON
						ec.SetID = s.SetId AND
						ec.c = sic.c
					INNER JOIN HDB_Text AS tgn ON tgn.[Key] = 'HEROES_ITEM_TRADECATEGORY_NAME_ARMOR';

					INSERT INTO HDB_ItemClassification
					SELECT
						i._ROWID_ AS ItemID,
						e._ROWID_ AS EquipID,
						NULL AS SetID,
						e.EquipClass AS GroupKey,
						tgn.Text AS GroupName,
						3 AS GroupOrder,
						CASE WHEN i.Category IN ('ARTIFACT', 'BRACELET') THEN 'SPECIAL' ELSE 'ORDINARY' END AS TypeKey,
						CASE WHEN i.Category IN ('ARTIFACT', 'BRACELET') THEN 'Special' ELSE 'Ordinary' END AS TypeName,
						CASE WHEN i.Category IN ('ARTIFACT', 'BRACELET') THEN 2 ELSE 1 END AS TypeOrder,
						CASE WHEN i.Category IN ('ARTIFACT', 'BRACELET') THEN NULL ELSE 'ClassRestriction, RequiredLevel, STR, INT, DEF' END AS TypePrimaryStats,
						i.Category AS CategoryKey,
						tcn.Text AS CategoryName,
						1 AS CategoryOrder
					FROM ItemClassInfo AS i
					INNER JOIN HDB_FeaturedItemClass AS fi ON fi.ItemID = i._ROWID_
					INNER JOIN EquipItemInfo AS e ON e.ItemClass = i.ItemClass
					INNER JOIN HDB_FeaturedEquipItem AS fe ON fe.ItemID = e._ROWID_
					INNER JOIN HDB_Text AS tgn ON tgn.[Key] = 'HEROES_ITEM_EQUIP_CLASS_NAME_' || e.EquipClass
					INNER JOIN HDB_Text AS tcn ON tcn.[Key] = 'HEROES_ITEM_EQUIP_PART_NAME_' || i.Category
					WHERE
						e.EquipClass = 'ACCESSORY' AND
						i.Category IN ('ARTIFACT', 'BELT', 'BRACELET', 'CHARM', 'EARRING', 'RING');

					INSERT INTO HDB_ItemClassification
					SELECT
						i._ROWID_ AS ItemID,
						e._ROWID_ AS EquipID,
						NULL AS SetID,
						'OFFHAND' AS GroupKey,
						'Off-hand' AS GroupName,
						4 AS GroupOrder,
						CASE WHEN e.EquipClass IN ('LARGESHIELD', 'SHIELD') THEN 'SHIELDS' ELSE 'OTHER' END AS TypeKey,
						CASE WHEN e.EquipClass IN ('LARGESHIELD', 'SHIELD') THEN 'Shields' ELSE 'Other' END AS TypeName,
						CASE WHEN e.EquipClass IN ('LARGESHIELD', 'SHIELD') THEN 1 ELSE 2 END AS TypeOrder,
						CASE WHEN e.EquipClass IN ('LARGESHIELD', 'SHIELD') THEN 'RequiredLevel, DEF, STR, DEX, WILL' ELSE 'RequiredLevel, INT, HDBATK, Critical, DEF' END AS TypePrimaryStats,
						e.EquipClass AS CategoryKey,
						tcn.Text AS CategoryName,
						1 AS CategoryOrder
					FROM ItemClassInfo AS i
					INNER JOIN HDB_FeaturedItemClass AS fi ON fi.ItemID = i._ROWID_
					INNER JOIN EquipItemInfo AS e ON e.ItemClass = i.ItemClass
					INNER JOIN HDB_FeaturedEquipItem AS fe ON fe.ItemID = e._ROWID_
					INNER JOIN HDB_Text AS tcn ON tcn.[Key] = 'HEROES_ITEM_EQUIP_CLASS_NAME_' || e.EquipClass
					WHERE e.EquipClass IN ('LARGESHIELD', 'SHIELD', 'SPELLBOOK', 'CASTLET');

					DELETE FROM HDB_ItemClassification
					WHERE HDB_ItemClassification.EquipID IN (
						SELECT e._ROWID_
						FROM EquipItemInfo AS e
						WHERE e.MaxDurability = 999999
					);

					DELETE FROM HDB_ItemClassification
					WHERE HDB_ItemClassification.ItemID IN (
						SELECT i._ROWID_
						FROM ItemClassInfo AS i
						LEFT JOIN HDB_Text AS tn ON tn.[Key] = 'HEROES_ITEM_NAME_' || UPPER(i.ItemClass)
						LEFT JOIN ItemClassInfo AS ai ON ai.ItemClass = 'avatar_' || i.ItemClass
						WHERE
							i.ExpireIn IS NOT NULL OR
							i.ItemClass LIKE '%_limitless' OR
							i.ItemClass LIKE '%_comback' OR
							i.ItemClass LIKE '%_provide' OR
							i.ItemClass LIKE '%_fruitgift' OR
							i.ItemClass LIKE '%_gift' OR
							i.ItemClass LIKE '%_7days' OR
							tn.Text GLOB '*[^A-z0-9 ()''.:-]*' OR
							tn.Text LIKE '%(Event)' OR
							ai.ItemClass IS NOT NULL
					);
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_ItemGroup;
					CREATE TABLE HDB_ItemGroup (
						[Key] NVARCHAR(50) PRIMARY KEY NOT NULL UNIQUE,
						Name NVARCHAR(100) NOT NULL,
						[Order] INT NOT NULL
					);
					INSERT INTO HDB_ItemGroup
					SELECT DISTINCT
						ic.GroupKey,
						ic.GroupName,
						ic.GroupOrder
					FROM HDB_ItemClassification AS ic;
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_ItemType;
					CREATE TABLE HDB_ItemType (
						[Key] NVARCHAR(50) NOT NULL,
						GroupKey NVARCHAR(50) NOT NULL,
						Name NVARCHAR(100) NOT NULL,
						[Order] INT NOT NULL,
						PrimaryStats NVARCHAR(250)
					);
					INSERT INTO HDB_ItemType
					SELECT DISTINCT
						ic.TypeKey,
						ic.GroupKey,
						ic.TypeName,
						ic.TypeOrder,
						ic.TypePrimaryStats
					FROM HDB_ItemClassification AS ic;
				";
				command.ExecuteNonQuery();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_ItemCategory;
					CREATE TABLE HDB_ItemCategory (
						[Key] NVARCHAR(50) NOT NULL,
						GroupKey NVARCHAR(50) NOT NULL,
						TypeKey NVARCHAR(50) NOT NULL,
						Name NVARCHAR(100) NOT NULL,
						[Order] INT NOT NULL
					);
					INSERT INTO HDB_ItemCategory
					SELECT DISTINCT
						ic.CategoryKey,
						ic.GroupKey,
						ic.TypeKey,
						ic.CategoryName,
						ic.CategoryOrder
					FROM HDB_ItemClassification AS ic;
				";
				command.ExecuteNonQuery();
				transaction.Commit();
			}
		}

		public void SetIcons() {
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					DROP TABLE IF EXISTS HDB_ItemIcon;
					CREATE TABLE HDB_ItemIcon (
						Icon NVARCHAR(50) NOT NULL,
						IconBG NVARCHAR(50),
						Material1 NVARCHAR(50),
						Material2 NVARCHAR(50),
						Material3 NVARCHAR(50)
					);
					INSERT INTO HDB_ItemIcon
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
			}
		}

		public void SetItems() {
			var connectionString = String.Format("Data Source={0}; Version=3;", this.databaseFile);
			using (var connection = new SQLiteConnection(connectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					DROP VIEW IF EXISTS HDB_Item;
					CREATE VIEW HDB_Item AS
						SELECT
							CAST(icf.ItemID AS NVARCHAR) AS ID,
							i.ItemClass AS [Key],
							ig._ROWID_ AS GroupID,
							it._ROWID_ AS TypeID,
							ic._ROWID_ AS CategoryID,
							ico._ROWID_ AS IconID,
							tn.Text AS Name,
							td.Text AS Description,
							i.Rarity,
							trs.Text AS RequiredSkill,
							sr.Rank_String AS RequiredSkillRank,
							i.RequiredLevel,
							i.ClassRestriction,
							CASE WHEN e.MATK > 0 THEN e.MATK ELSE e.ATK END AS HDBATK,
							e.ATK,
							e.MATK,
							e.ATK_Speed,
							e.Critical,
							e.Balance,
							e.HP,
							e.DEF,
							e.Res_Critical,
							e.STR,
							e.INT,
							e.DEX,
							e.WILL,
							e.STAMINA
						FROM (
							SELECT icf.*
							FROM HDB_ItemClassification AS icf
							INNER JOIN (
								SELECT
									icf.ItemID,
									MIN(icf.EquipID) AS EquipID
								FROM HDB_ItemClassification AS icf
								GROUP BY icf.ItemID
							) AS t ON
								t.ItemID = icf.ItemID AND
								t.EquipID = icf.EquipID
							WHERE icf.SetID IS NULL
						) AS icf
						INNER JOIN HDB_ItemGroup AS ig ON ig.[Key] = icf.GroupKey
						INNER JOIN HDB_ItemType AS it ON
							it.[Key] = icf.TypeKey AND
							it.GroupKey = icf.GroupKey
						INNER JOIN HDB_ItemCategory AS ic ON
							ic.[Key] = icf.CategoryKey AND
							ic.TypeKey = icf.TypeKey AND
							ic.GroupKey = icf.GroupKey
						INNER JOIN ItemClassInfo AS i ON i._ROWID_ = icf.ItemID
						LEFT JOIN EquipItemInfo AS e ON e._ROWID_ = icf.EquipID
						INNER JOIN HDB_Text AS tn ON tn.[Key] = 'HEROES_ITEM_NAME_' || UPPER(i.ItemClass)
						LEFT JOIN HDB_Text AS td ON
							td.[Key] = 'HEROES_ITEM_DESC_' || UPPER(i.ItemClass) AND
							LENGTH(td.Text) > 0 AND
							td.Text != '0' AND
							td.Text != 'Effective equipment for PVP.' AND
							td.Text != 'pvp\nEquipment' AND
							td.Text != tn.Text
						INNER JOIN HDB_ItemIcon AS ico ON
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
						LEFT JOIN HDB_Text AS trs ON trs.[Key] = 'HEROES_SKILL_NAME_' || UPPER(i.RequiredSkill)
						LEFT JOIN SkillRankEnum AS sr ON sr.Rank_Num = i.RequiredSkillRank
						UNION ALL
						SELECT
							's' || s._ROWID_ AS ID,
							s.SetID AS [Key],
							ss.GroupID,
							ss.TypeID,
							ss.CategoryID,
							ss.IconID,
							tn.Text AS Name,
							NULL AS Description,
							ss.Rarity,
							NULL AS RequiredSkill,
							NULL AS RequiredSkillRank,
							ss.RequiredLevel,
							ss.ClassRestriction,
							ss.HDBATK + COALESCE(se.HDBATK, 0) AS HDBATK,
							ss.ATK + COALESCE(se.ATK, 0) AS ATK,
							ss.MATK + COALESCE(se.MATK, 0) AS MATK,
							ss.ATK_Speed,
							ss.Critical,
							ss.Balance,
							ss.HP + COALESCE(se.HP, 0) AS HP,
							ss.DEF + COALESCE(se.DEF, 0) AS DEF,
							ss.Res_Critical,
							ss.STR + COALESCE(se.STR, 0) AS STR,
							ss.INT + COALESCE(se.INT, 0) AS INT,
							ss.DEX + COALESCE(se.DEX, 0) AS DEX,
							ss.WILL + COALESCE(se.WILL, 0) AS WILL,
							ss.STAMINA
						FROM SetInfo AS s 
						INNER JOIN (
							SELECT
								icf.SetID,
								ig._ROWID_ AS GroupID,
								it._ROWID_ AS TypeID,
								ic._ROWID_ AS CategoryID,
								MAX(CASE WHEN i.Category = 'HELM' THEN ico._ROWID_ ELSE NULL END) AS IconID,
								MAX(i.Rarity) AS Rarity,
								MAX(i.RequiredLevel) AS RequiredLevel,
								MIN(i.ClassRestriction) AS ClassRestriction,
								SUM(CASE WHEN e.MATK > 0 THEN e.MATK ELSE e.ATK END) AS HDBATK,
								SUM(e.ATK) AS ATK,
								SUM(e.MATK) AS MATK,
								SUM(e.ATK_Speed) AS ATK_Speed,
								SUM(e.Critical) AS Critical,
								SUM(e.Balance) AS Balance,
								SUM(e.HP) AS HP,
								SUM(e.DEF) AS DEF,
								SUM(e.Res_Critical) AS Res_Critical,
								SUM(e.STR) AS STR,
								SUM(e.INT) AS INT,
								SUM(e.DEX) AS DEX,
								SUM(e.WILL) AS WILL,
								SUM(e.STAMINA) AS STAMINA
							FROM (
								SELECT icf.*
								FROM HDB_ItemClassification AS icf
								INNER JOIN (
									SELECT
										icf.ItemID,
										MIN(icf.EquipID) AS EquipID
									FROM HDB_ItemClassification AS icf
									GROUP BY icf.ItemID
								) AS t ON
									t.ItemID = icf.ItemID AND
									t.EquipID = icf.EquipID
								WHERE icf.SetID IS NOT NULL
							) AS icf
							INNER JOIN HDB_ItemGroup AS ig ON ig.[Key] = icf.GroupKey
							INNER JOIN HDB_ItemType AS it ON
								it.[Key] = icf.TypeKey AND
								it.GroupKey = icf.GroupKey
							INNER JOIN HDB_ItemCategory AS ic ON
								ic.[Key] = icf.CategoryKey AND
								ic.TypeKey = icf.TypeKey AND
								ic.GroupKey = icf.GroupKey
							INNER JOIN ItemClassInfo AS i ON i._ROWID_ = icf.ItemID
							INNER JOIN EquipItemInfo AS e ON e._ROWID_ = icf.EquipID
							INNER JOIN HDB_ItemIcon AS ico ON
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
							GROUP BY
								icf.SetID,
								ig._ROWID_,
								it._ROWID_,
								ic._ROWID_
						) AS ss ON ss.SetID = s._ROWID_
						LEFT JOIN (
							SELECT
								se.SetID,
								SUM(CASE WHEN se.EffectTarget IN ('ATK', 'MATK') THEN se.Amount ELSE 0 END) AS HDBATK,
								SUM(CASE WHEN se.EffectTarget = 'ATK' THEN se.Amount ELSE 0 END) AS ATK,
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
						) AS se ON se.SetID = s.SetID
						INNER JOIN HDB_Text AS tn ON tn.[Key] = 'HEROES_ITEM_SETITEM_NAME_' || UPPER(s.SetID);
				";
				command.ExecuteNonQuery();
			}
		}

	}

}
