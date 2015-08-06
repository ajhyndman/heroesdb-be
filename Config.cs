
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System;

namespace HeroesDB {

	public class Config {

		public readonly String RootPath = @"D:\Home\Code\HeroesDB";

		public readonly String AppPath;

		public readonly String ExifTool = @"D:\Home\Code\HeroesDB\tools\exiftool.exe";

		public readonly String ImageMagickConvert = @"C:\Program Files\ImageMagick-6.9.0-Q16\convert";

		public readonly String HfsPath;

		public readonly String WwwPath;

		public readonly String ExportPath;

		public readonly String DatabaseFile;

		public readonly String ConnectionString;

		public readonly String TextImportFile;

		public readonly String IconImportPath;

		public readonly String InjectSourcePath;

		public readonly String InjectDestinationPath = @"D:\zlib_replace";

		public readonly String ScreenshotSourcePath = @"C:\Users\crt\Documents\洛奇英雄传\游戏截图";

		/*
		DROP VIEW IF EXISTS HDB_tempv;
		CREATE VIEW HDB_tempv AS
			SELECT e.Key
			FROM EquipItemInfo AS ei
			INNER JOIN HDB_Equips AS e ON e.ID = ei._ROWID_
			WHERE
				e.Key IN (
					SELECT es.EquipKey
					FROM HDB_EquipScreenshots AS es
					UNION
					SELECT ssp.EquipKey
					FROM HDB_SetScreenshotParts AS ssp
				) AND (
					ei.Material1 = 'metal_gold' OR
					ei.Material2 = 'metal_gold' OR
					ei.Material3 = 'metal_gold'
				);
		UPDATE HDB_EquipScreenshots
		SET Ready = 0
		WHERE
			HDB_EquipScreenshots.EquipKey IN (
				SELECT t.Key FROM HDB_tempv AS t
			);
		UPDATE HDB_SetScreenshots
		SET Ready = 0
		WHERE
			HDB_SetScreenshots.SetKey IN (
				SELECT ssp.SetKey 
				FROM HDB_SetScreenshotParts AS ssp
				INNER JOIN HDB_tempv AS t ON t.Key = ssp.EquipKey
			);
		*/
		public readonly Dictionary<String, Color[]> Materials = new Dictionary<String, Color[]> {
			{ "beard", new [] { Color.FromArgb(0, 222, 219, 217) } },
			{ "black", new [] { Color.FromArgb(0, 41, 41, 41), Color.FromArgb(0, 51, 56, 68) } },
			{ "bloodlord", new [] { Color.FromArgb(0, 247, 90, 66) } },
			{ "brown", new [] { Color.FromArgb(0, 96, 72, 48) } },
			{ "cash", new [] { Color.FromArgb(0, 115, 122, 88), Color.FromArgb(0, 124, 110, 72) } },
			{ "cash_angelring", new [] { Color.FromArgb(0, 255, 234, 232) } },
			{ "champagne_gold", new [] { Color.FromArgb(0, 246, 236, 177) } },
			{ "chinese", new [] { Color.FromArgb(0, 255, 89, 0), Color.FromArgb(0, 242, 18, 1) } },
			{ "cloth", new [] { Color.FromArgb(0, 234, 228, 213), Color.FromArgb(0, 213, 219, 234), Color.FromArgb(0, 234, 213, 219) } },
			{ "cloth_bright", new [] { Color.FromArgb(0, 217, 233, 244), Color.FromArgb(0, 233, 244, 217) } },
			{ "cloth_bw", new [] { Color.FromArgb(0, 186, 183, 183), Color.FromArgb(0, 243, 240, 240) } },
			{ "cloth_dark", new [] { Color.FromArgb(0, 66, 60, 59), Color.FromArgb(0, 67, 56, 55) } },
			{ "cloth_santa", new [] { Color.FromArgb(0, 230, 31, 45) } },
			{ "crystal", new [] { Color.FromArgb(0, 192, 255, 255) } },
			{ "crystal_blue", new [] { Color.FromArgb(0, 66, 171, 236) } },
			{ "darkblue", new [] { Color.FromArgb(0, 8, 55, 105) } },
			{ "event_euro2012_lower", new [] { Color.FromArgb(0, 233, 255, 255) } },
			{ "event_euro2012_upper", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_metalic", new [] { Color.FromArgb(0, 249, 250, 198) } },
			{ "event_rabbit_moon_C1", new [] { Color.FromArgb(0, 249, 250, 198) } },
			{ "event_rabbit_moon_head_C2", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_rabbit_moon_head_C3", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_rabbit_moon_upper_C2", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_rabbit_moon_upper_C3", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_black_head_C1", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_black_head_C2", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_brown_head_C1", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_brown_head_C2", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_green_head_C1", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_green_head_C2", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_red_head_C1", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_red_head_C2", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_twinkle_head_C1", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_twinkle_head_C2", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_yellow_head_C1", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_yellow_head_C2", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_yellowgreen_head_C1", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_sprout_yellowgreen_head_C2", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_wingB", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_wingG", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_wingR_gold", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "event_wingR_silver", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "fix_bluechannel", new [] { Color.FromArgb(0, 2, 93, 140) } },
			{ "fix_greenchannel", new [] { Color.FromArgb(0, 27, 175, 27) } },
			{ "fix_redchannel", new [] { Color.FromArgb(0, 240, 35, 17) } },
			{ "flat_bandage", new [] { Color.FromArgb(0, 218, 191, 180) } },
			{ "flat_black", new [] { Color.FromArgb(0, 50, 48, 41) } },
			{ "flat_bloodyred", new [] { Color.FromArgb(0, 178, 55, 7) } },
			{ "flat_camelbrown", new [] { Color.FromArgb(0, 206, 175, 138) } },
			{ "flat_cobaltblue", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "flat_devcatorange", new [] { Color.FromArgb(0, 255, 153, 0) } },
			{ "flat_gray", new [] { Color.FromArgb(0, 182, 177, 175) } },
			{ "flat_leatherbrown", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "flat_milkypink", new [] { Color.FromArgb(0, 235, 167, 174) } },
			{ "flat_pumpkin", new [] { Color.FromArgb(0, 255, 155, 0) } },
			{ "flat_pumpkinlumi", new [] { Color.FromArgb(0, 233, 128, 66) } },
			{ "flat_red", new [] { Color.FromArgb(0, 146, 1, 7) } },
			{ "flat_smoketurquoise", new [] { Color.FromArgb(0, 114, 165, 169) } },
			{ "flat_volcanicred", new [] { Color.FromArgb(0, 150, 27, 22) } },
			{ "flat_white", new [] { Color.FromArgb(0, 239, 232, 215) } },
			{ "flat_worldcupred", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "frame_red", new [] { Color.FromArgb(0, 244, 30, 8) } },
			{ "fur_mono", new [] { Color.FromArgb(0, 255, 250, 227) } },
			{ "giantspider", new [] { Color.FromArgb(0, 118, 19, 5) } },
			{ "gray", new [] { Color.FromArgb(0, 153, 153, 153), Color.FromArgb(0, 96, 96, 120) } },
			{ "gray_brown", new [] { Color.FromArgb(0, 120, 111, 106) } },
			{ "innerarmor", new [] { Color.FromArgb(0, 252, 245, 198), Color.FromArgb(0, 214, 235, 255), Color.FromArgb(0, 247, 251, 255) } },
			{ "korean", new [] { Color.FromArgb(0, 214, 30, 0), Color.FromArgb(0, 41, 105, 74) } },
			{ "laghodessa", new [] { Color.FromArgb(0, 129, 149, 4) } },
			{ "leaf_green", new [] { Color.FromArgb(0, 90, 117, 66) } },
			{ "leaf_green2", new [] { Color.FromArgb(0, 213, 220, 220) } },
			{ "leather", new [] { Color.FromArgb(0, 113, 80, 49), Color.FromArgb(0, 101, 84, 66), Color.FromArgb(0, 145, 70, 3) } },
			{ "leather_brown", new [] { Color.FromArgb(0, 95, 53, 23 ), Color.FromArgb(0, 181, 97, 35 ) } },
			{ "leather_colorful", new [] { Color.FromArgb(0, 124, 67, 64) } },
			{ "leather_crimson", new [] { Color.FromArgb(0, 124, 29, 32), Color.FromArgb(0, 128, 51, 40) } },
			{ "leather_dark", new [] { Color.FromArgb(0, 59, 54, 46), Color.FromArgb(0, 73, 0, 2), Color.FromArgb(0, 75, 37, 14) } },
			{ "leather_enamel", new [] { Color.FromArgb(0, 239, 240, 241), Color.FromArgb(0, 100, 112, 120) } },
			{ "leather_flatbrown", new [] { Color.FromArgb(0, 117, 59, 48), Color.FromArgb(0, 130, 102, 95) } },
			{ "leather_glasgavelen", new [] { Color.FromArgb(0, 68, 70, 69) } },
			{ "leather_red", new [] { Color.FromArgb(0, 124, 29, 32), Color.FromArgb(0, 199, 98, 105) } },
			{ "leather_wonderland", new [] { Color.FromArgb(0, 159, 18, 34) } },
			{ "metal", new [] { Color.FromArgb(0, 183, 183, 183), Color.FromArgb(0, 117, 113, 87), Color.FromArgb(0, 102, 102, 102) } },
			{ "metal_bronze", new [] { Color.FromArgb(0, 138, 112, 79), Color.FromArgb(0, 105, 57, 9) } },
			{ "metal_colorful", new [] { Color.FromArgb(0, 93,98,94), Color.FromArgb(0, 152, 146, 151) } },
			{ "metal_dark", new [] { Color.FromArgb(0, 39, 42, 43), Color.FromArgb(0, 66, 70, 73) } },
			{ "metal_gold", new [] { Color.FromArgb(0, 135, 115, 56), Color.FromArgb(0, 170, 136, 8) } },
			{ "metal_gold_ex", new [] { Color.FromArgb(0, 162, 157, 153), Color.FromArgb(0, 153, 122, 7) } },
			{ "metal_luxury", new [] { Color.FromArgb(0, 101, 96, 102), Color.FromArgb(0, 42, 33, 40), Color.FromArgb(0, 230, 226, 231) } },
			{ "metal_red", new [] { Color.FromArgb(0, 109, 13, 15) } },
			{ "metal_silver_ex", new [] { Color.FromArgb(0, 175, 175, 175), Color.FromArgb(0, 146, 148, 148) } },
			{ "military", new [] { Color.FromArgb(0, 128, 128, 93), Color.FromArgb(0, 125, 121, 111), Color.FromArgb(0, 24, 69, 44) } },
			{ "nightmare", new [] { Color.FromArgb(0, 124, 58, 73) } },
			{ "rainbow", new [] { Color.FromArgb(0, 157, 28, 83), Color.FromArgb(0, 87, 217, 183) } },
			{ "red_strawberry", new [] { Color.FromArgb(0, 161, 36, 47) } },
			{ "silk", new [] { Color.FromArgb(0, 252, 253, 244) } },
			{ "skull", new [] { Color.FromArgb(0, 226, 220, 196), Color.FromArgb(0, 203, 198, 176) } },
			{ "weapon_edge", new [] { Color.FromArgb(0, 230, 229, 232) } },
			{ "white", new [] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 229, 229, 229) } },
			{ "wood", new [] { Color.FromArgb(0, 187, 163, 125),  Color.FromArgb(0, 87, 70, 50),  Color.FromArgb(0, 119, 104, 80) } }
		};

		public Dictionary<String, String> GetCostumeTemplateDefaultValues(String objectType, Int32 characterID) {
			var values = new Dictionary<String, String> {
				{ "helm", "-1" },
				{ "helm_color1", "-1" },
				{ "helm_color2", "-1" },
				{ "helm_color3", "-1" },
				{ "tunic", "-1" },
				{ "tunic_color1", "-1" },
				{ "tunic_color2", "-1" },
				{ "tunic_color3", "-1" },
				{ "pants", "-1" },
				{ "pants_color1", "-1" },
				{ "pants_color2", "-1" },
				{ "pants_color3", "-1" },
				{ "gloves", "-1" },
				{ "gloves_color1", "-1" },
				{ "gloves_color2", "-1" },
				{ "gloves_color3", "-1" },
				{ "boots", "-1" },
				{ "boots_color1", "-1" },
				{ "boots_color2", "-1" },
				{ "boots_color3", "-1" },
				{ "weapon_type", "weapon_barehand" },
				{ "weapon", "-1" },
				{ "weapon_color1", "-1" },
				{ "weapon_color2", "-1" },
				{ "weapon_color3", "-1" }
			};
			if (objectType == "set") {
				return values;
			}
			else {
				switch (characterID) {
					case 1: // Lann
						values["tunic"] = "0";
						values["tunic_color1"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						values["tunic_color2"] = Convert.ToString(this.Materials["metal_dark"][0].ToArgb());
						values["tunic_color3"] = Convert.ToString(this.Materials["leather_colorful"][0].ToArgb());
						values["pants"] = "0";
						values["pants_color1"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						values["pants_color2"] = Convert.ToString(this.Materials["metal_dark"][0].ToArgb());
						values["pants_color3"] = Convert.ToString(this.Materials["cloth_dark"][0].ToArgb());
						values["gloves"] = "0";
						values["gloves_color1"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						values["gloves_color2"] = Convert.ToString(this.Materials["leather_colorful"][0].ToArgb());
						values["boots"] = "0";
						values["boots_color1"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						return values;
					case 2: // Fiona
						values["tunic"] = "65";
						values["tunic_color1"] = Convert.ToString(this.Materials["metal_luxury"][0].ToArgb());
						values["tunic_color2"] = Convert.ToString(this.Materials["cloth_bw"][0].ToArgb());
						values["tunic_color3"] = Convert.ToString(this.Materials["leather_dark"][0].ToArgb());
						values["pants"] = "65";
						values["pants_color1"] = Convert.ToString(this.Materials["cloth_bw"][0].ToArgb());
						values["pants_color2"] = Convert.ToString(this.Materials["cloth"][0].ToArgb());
						values["pants_color3"] = Convert.ToString(this.Materials["leather"][0].ToArgb());
						values["boots"] = "65";
						values["boots_color1"] = Convert.ToString(this.Materials["metal_luxury"][0].ToArgb());
						values["boots_color2"] = Convert.ToString(this.Materials["cloth_bw"][0].ToArgb());
						return values;
					case 4: // Evie
						values["tunic"] = "177";
						values["tunic_color1"] = Convert.ToString(this.Materials["metal_dark"][0].ToArgb());
						values["tunic_color2"] = Convert.ToString(this.Materials["cloth_bw"][0].ToArgb());
						values["tunic_color3"] = Convert.ToString(this.Materials["leather_dark"][0].ToArgb());
						values["pants"] = "177";
						values["pants_color1"] = Convert.ToString(this.Materials["metal_dark"][0].ToArgb());
						values["pants_color3"] = Convert.ToString(this.Materials["leather_dark"][0].ToArgb());
						values["boots"] = "177";
						values["boots_color1"] = Convert.ToString(this.Materials["cloth_bw"][0].ToArgb());
						values["boots_color2"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						values["boots_color3"] = Convert.ToString(this.Materials["cloth_dark"][0].ToArgb());
						return values;
					case 8: // Karok
						values["tunic"] = "170";
						values["tunic_color1"] = Convert.ToString(this.Materials["metal_gold"][0].ToArgb());
						values["tunic_color2"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						values["tunic_color3"] = Convert.ToString(this.Materials["leather"][0].ToArgb());
						values["pants"] = "170";
						values["pants_color1"] = Convert.ToString(this.Materials["metal_gold"][0].ToArgb());
						values["pants_color2"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						values["pants_color3"] = Convert.ToString(this.Materials["leather"][0].ToArgb());
						values["boots"] = "170";
						values["boots_color1"] = Convert.ToString(this.Materials["metal_gold"][0].ToArgb());
						values["boots_color2"] = Convert.ToString(this.Materials["metal_gold_ex"][0].ToArgb());
						values["boots_color3"] = Convert.ToString(this.Materials["leather"][0].ToArgb());
						return values;
					case 16: // Kai
						values["tunic"] = "34";
						values["tunic_color1"] = Convert.ToString(this.Materials["leather_dark"][0].ToArgb());
						values["tunic_color2"] = Convert.ToString(this.Materials["leaf_green"][0].ToArgb());
						values["tunic_color3"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						values["pants"] = "34";
						values["pants_color1"] = Convert.ToString(this.Materials["leather_dark"][0].ToArgb());
						values["pants_color2"] = Convert.ToString(this.Materials["leaf_green"][0].ToArgb());
						values["pants_color3"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						values["boots"] = "34";
						values["boots_color1"] = Convert.ToString(this.Materials["leather"][0].ToArgb());
						values["boots_color2"] = Convert.ToString(this.Materials["leather_dark"][0].ToArgb());
						values["boots_color3"] = Convert.ToString(this.Materials["leather_dark"][1].ToArgb());
						return values;
					case 32: // Vella
						values["tunic"] = "227";
						values["tunic_color1"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						values["tunic_color2"] = Convert.ToString(this.Materials["metal_dark"][0].ToArgb());
						values["tunic_color3"] = Convert.ToString(this.Materials["leather_colorful"][0].ToArgb());
						values["pants"] = "227";
						values["pants_color1"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						values["pants_color2"] = Convert.ToString(this.Materials["metal_dark"][0].ToArgb());
						values["pants_color3"] = Convert.ToString(this.Materials["cloth_dark"][0].ToArgb());
						values["gloves"] = "227";
						values["gloves_color1"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						values["gloves_color2"] = Convert.ToString(this.Materials["leather_colorful"][0].ToArgb());
						values["boots"] = "227";
						values["boots_color1"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						return values;
					case 64: // Hurk
						values["tunic"] = "389";
						values["tunic_color1"] = Convert.ToString(this.Materials["leather"][0].ToArgb());
						values["tunic_color2"] = Convert.ToString(this.Materials["metal_dark"][0].ToArgb());
						values["tunic_color3"] = Convert.ToString(this.Materials["leather_dark"][0].ToArgb());
						values["pants"] = "389";
						values["pants_color1"] = Convert.ToString(this.Materials["leather"][0].ToArgb());
						values["pants_color2"] = Convert.ToString(this.Materials["metal_dark"][0].ToArgb());
						values["pants_color3"] = Convert.ToString(this.Materials["leather_dark"][0].ToArgb());
						values["gloves"] = "389";
						values["gloves_color1"] = Convert.ToString(this.Materials["metal_dark"][0].ToArgb());
						values["gloves_color2"] = Convert.ToString(this.Materials["leather"][0].ToArgb());
						values["gloves_color3"] = Convert.ToString(this.Materials["metal_dark"][1].ToArgb());
						values["boots"] = "389";
						values["boots_color1"] = Convert.ToString(this.Materials["leather"][0].ToArgb());
						values["boots_color2"] = Convert.ToString(this.Materials["metal_dark"][0].ToArgb());
						values["boots_color3"] = Convert.ToString(this.Materials["metal_dark"][1].ToArgb());
						return values;
					case 128: // Lynn
						values["tunic"] = "511";
						values["tunic_color1"] = Convert.ToString(this.Materials["red_strawberry"][0].ToArgb());
						values["tunic_color2"] = Convert.ToString(this.Materials["cloth"][0].ToArgb());
						values["tunic_color3"] = Convert.ToString(this.Materials["leather_dark"][0].ToArgb());
						values["pants"] = "511";
						values["pants_color1"] = Convert.ToString(this.Materials["red_strawberry"][0].ToArgb());
						values["pants_color2"] = Convert.ToString(this.Materials["cloth"][0].ToArgb());
						values["pants_color3"] = Convert.ToString(this.Materials["leather_dark"][0].ToArgb());
						values["gloves"] = "511";
						values["gloves_color1"] = Convert.ToString(this.Materials["leather_dark"][0].ToArgb());
						values["gloves_color2"] = Convert.ToString(this.Materials["metal_silver_ex"][0].ToArgb());
						values["boots"] = "511";
						values["boots_color1"] = Convert.ToString(this.Materials["cloth"][0].ToArgb());
						values["boots_color2"] = Convert.ToString(this.Materials["red_strawberry"][0].ToArgb());
						values["boots_color3"] = Convert.ToString(this.Materials["leather_dark"][0].ToArgb());
						return values;
					case 256: // Arisha
						values["tunic"] = "580";
						values["tunic_color1"] = Convert.ToString(this.Materials["gray"][0].ToArgb());
						values["tunic_color2"] = Convert.ToString(this.Materials["metal_dark"][0].ToArgb());
						values["tunic_color3"] = Convert.ToString(this.Materials["metal_gold"][0].ToArgb());
						values["pants"] = "580";
						values["pants_color1"] = Convert.ToString(this.Materials["metal_dark"][0].ToArgb());
						values["pants_color2"] = Convert.ToString(this.Materials["cloth_dark"][0].ToArgb());
						values["pants_color3"] = Convert.ToString(this.Materials["metal_dark"][1].ToArgb());
						values["gloves"] = "580";
						values["gloves_color1"] = Convert.ToString(this.Materials["red_strawberry"][0].ToArgb());
						values["gloves_color2"] = Convert.ToString(this.Materials["metal_dark"][0].ToArgb());
						values["gloves_color3"] = Convert.ToString(this.Materials["leather_dark"][0].ToArgb());
						values["boots"] = "580";
						values["boots_color1"] = Convert.ToString(this.Materials["gray"][0].ToArgb());
						values["boots_color2"] = Convert.ToString(this.Materials["red_strawberry"][0].ToArgb());
						values["boots_color3"] = Convert.ToString(this.Materials["metal"][0].ToArgb());
						return values;
					default:
						return values;
				}
			}
		}

		public Config() {
			Debug.WriteLine("Config() {");
			Debug.Indent();
			this.AppPath = Path.Combine(this.RootPath, "export");
			this.HfsPath = Path.Combine(this.RootPath, "hfs");
			this.WwwPath = Path.Combine(this.RootPath, "www");
			this.ExportPath = Path.Combine(this.WwwPath, "data");
			this.DatabaseFile = Path.Combine(HfsPath, "heroes.db3.comp");
			this.ConnectionString = String.Format("Data Source={0}; Version=3;", this.DatabaseFile);
			this.TextImportFile = Path.Combine(this.HfsPath, "heroes_text_english_eu.txt");
			this.IconImportPath = Path.Combine(this.HfsPath, "icons");
			this.InjectSourcePath = Path.Combine(this.AppPath, "zlib_inject_source");
			Debug.Unindent();
			Debug.WriteLine("}");
		}
	}

}
