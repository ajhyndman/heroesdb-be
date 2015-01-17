
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System;

namespace HeroesDB {

	public class Config {

		public readonly String RootPath = @"D:\HeroesDB\";

		public readonly String AppPath;

		public readonly String HfsPath;

		public readonly String WwwPath;

		public readonly String ExportPath;

		public readonly String DatabaseFile;

		public readonly String ConnectionString;

		public readonly String TextImportFile;

		public readonly String IconImportPath;

		public readonly String InjectSource;

		public readonly String InjectDestination = @"D:\zlib_replace";

		public readonly Dictionary<String, Color> Materials = new Dictionary<String, Color> {
			{ "metal", Color.FromArgb(1, 204, 204, 204) },
			{ "metal_dark", Color.FromArgb(1, 148, 152, 155) },
			{ "metal_silver_ex", Color.FromArgb(1, 243, 241, 238) },
			{ "leather_dark", Color.FromArgb(1, 73, 0, 2) },
			{ "leather", Color.FromArgb(1, 162, 78, 4) },
			{ "metal_gold_ex", Color.FromArgb(1, 218, 179, 0) },
			{ "metal_luxury", Color.FromArgb(1, 221, 205, 204) },
			{ "metal_gold", Color.FromArgb(1, 212, 174, 55) },
			{ "cloth_dark", Color.FromArgb(1, 186, 171, 170) },
			{ "cloth_bw", Color.FromArgb(1, 243, 240, 240) },
			{ "cloth", Color.FromArgb(1, 222, 213, 213) },
			{ "metal_colorful", Color.FromArgb(1, 188, 198, 204) },
			{ "leather_colorful", Color.FromArgb(1, 212, 134, 134) },
			{ "rainbow", Color.FromArgb(1, 229, 6, 102) },
			{ "metal_bronze", Color.FromArgb(1, 166, 152, 131) },
			{ "skull", Color.FromArgb(1, 247, 249, 243) },
			{ "leather_brown", Color.FromArgb(1, 202, 172, 140) },
			{ "white", Color.FromArgb(1, 255, 255, 255) },
			{ "leather_enamel", Color.FromArgb(1, 246, 246, 247) },
			{ "cloth_bright", Color.FromArgb(1, 217, 233, 244) },
			{ "black", Color.FromArgb(1, 0, 0, 0) },
			{ "leather_crimson", Color.FromArgb(1, 211, 8, 8) },
			{ "weapon_edge", Color.FromArgb(1, 230, 229, 232) },
			{ "silk", Color.FromArgb(1, 252, 253, 244) },
			{ "wood", Color.FromArgb(1, 187, 163, 125) },
			{ "gray_brown", Color.FromArgb(1, 120, 111, 106) },
			{ "metal_red", Color.FromArgb(1, 109, 13, 15) },
			{ "innerarmor", Color.FromArgb(1, 252, 245, 198) },
			{ "gray", Color.FromArgb(1, 153, 153, 153) },
			{ "leather_red", Color.FromArgb(1, 124, 29, 32) },
			{ "frame_red", Color.FromArgb(1, 244, 30, 8) },
			{ "nightmare", Color.FromArgb(1, 124, 58, 73) },
			{ "leather_glasgavelen", Color.FromArgb(1, 102, 99, 51) },
			{ "chinese", Color.FromArgb(1, 255, 89, 0) },
			{ "fur_mono", Color.FromArgb(1, 255, 250, 227) },
			{ "leaf_green", Color.FromArgb(1, 168, 210, 110) },
			{ "leather_flatbrown", Color.FromArgb(1, 166, 144, 121) },
			{ "flat_red", Color.FromArgb(1, 255, 70, 24) },
			{ "laghodessa", Color.FromArgb(1, 144, 166, 5) },
			{ "cloth_santa", Color.FromArgb(1, 230, 31, 45) },
			{ "giantspider", Color.FromArgb(1, 141, 134, 108) },
			{ "beard", Color.FromArgb(1, 180, 153, 127) },
			{ "fix_bluechannel", Color.FromArgb(1, 2, 93, 140) },
			{ "fix_greenchannel", Color.FromArgb(1, 27, 175, 27) },
			{ "fix_redchannel", Color.FromArgb(1, 240, 35, 17) },
			{ "red_strawberry", Color.FromArgb(1, 242, 58, 101) },
			{ "flat_white", Color.FromArgb(1, 239, 232, 215) },
			{ "leather_wonderland", Color.FromArgb(1, 238, 12, 73) },
			{ "flat_black", Color.FromArgb(1, 50, 48, 41) },
			{ "flat_pumpkin", Color.FromArgb(1, 255, 155, 0) },
			{ "brown", Color.FromArgb(1, 96, 72, 48) },
			{ "champagne_gold", Color.FromArgb(1, 246, 236, 177) },
			{ "military", Color.FromArgb(1, 128, 128, 93) },
			{ "flat_devcatorange", Color.FromArgb(1, 255, 153, 0) },
			{ "flat_gray", Color.FromArgb(1, 182, 177, 175) },
			{ "korean", Color.FromArgb(1, 214, 30, 0) },
			{ "crystal", Color.FromArgb(1, 192, 255, 255) },
			{ "crystal_blue", Color.FromArgb(1, 66, 171, 236) },
			{ "event_rabbit_moon_C1", Color.FromArgb(1, 249, 250, 198) },
			{ "flat_pumpkinlumi", Color.FromArgb(1, 233, 128, 66) },
			{ "flat_volcanicred", Color.FromArgb(1, 150, 27, 22) },
			{ "bloodlord", Color.FromArgb(1, 247, 90, 66) },
			{ "cash_angelring", Color.FromArgb(1, 255, 234, 232) },
			{ "darkblue", Color.FromArgb(1, 8, 55, 105) },
			{ "event_euro2012_lower", Color.FromArgb(1, 233, 255, 255) },
			{ "flat_bandage", Color.FromArgb(1, 218, 191, 180) },
			{ "flat_bloodyred", Color.FromArgb(1, 178, 55, 7) },
			{ "flat_camelbrown", Color.FromArgb(1, 206, 175, 138) },
			{ "flat_milkypink", Color.FromArgb(1, 235, 167, 174) },
			{ "flat_smoketurquoise", Color.FromArgb(1, 114, 165, 169) }
		};

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
			this.InjectSource = Path.Combine(this.AppPath, "zlib_inject_source");
			Debug.Unindent();
			Debug.WriteLine("}");
		}
	}

}
