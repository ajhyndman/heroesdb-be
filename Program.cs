
using System;

namespace HeroesDB {

	class Program {

		public static void Main(string[] args) {
			var databaseFile = @"D:\HeroesDB\hfs\heroes.db3.comp";
			var ext = new Extractor();
			var stp = new Setuper(databaseFile);
			//Console.WriteLine("ImportText");
			//stp.ImportText(@"D:\HeroesDB\hfs\heroes_text_english_eu.txt");
			//Console.WriteLine("SetCharacters");
			//stp.SetCharacters();
			//Console.WriteLine("SetFeaturedItemClasses");
			//stp.SetFeaturedItemClasses();
			//Console.WriteLine("SetFeaturedEquipItems");
			//stp.SetFeaturedEquipItems();
			//Console.WriteLine("SetItemStats");
			//stp.SetItemStats();
			Console.WriteLine("SetClassification");
			stp.SetClassification();
			//Console.WriteLine("SetIcons");
			//stp.SetIcons();
			Console.WriteLine("SetItems");
			stp.SetItems();
			var exp = new Exporter(databaseFile, @"D:\HeroesDB\www\data\");
			//Console.WriteLine("ExportCharacters");
			//exp.ExportCharacters();
			//Console.WriteLine("ExportItemStats");
			//exp.ExportItemStats();
			//Console.WriteLine("ExportItemGroups");
			//exp.ExportItemGroups();
			Console.WriteLine("ExportItems");
			exp.ExportItems();
			//Console.WriteLine("ExportIcons");
			//exp.ExportIcons(@"D:\HeroesDB\hfs\icons");
			Console.WriteLine("DONE!");
			Console.ReadKey(true);
		}

	}

}
