
using System.Diagnostics;
using System;

namespace HeroesDB {

	class Program {

		const String DatabaseFile = @"D:\HeroesDB\hfs\heroes.db3.comp";

		public static void Main(String[] args) {
			Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
			Debug.WriteLine("Main() {");
			Debug.Indent();
			try {
				var ext = new Extractor();
				var stp = new Setuper(DatabaseFile);
				stp.ImportText(@"D:\HeroesDB\hfs\heroes_text_english_eu.txt");
				stp.SetFeaturedItems();
				stp.SetFeaturedEquips();
				stp.SetFeaturedRecipes();
				stp.SetClassification();
				stp.SetIcons(@"D:\HeroesDB\hfs\icons");
				stp.SetCharacters();
				stp.SetMats();
				stp.SetQualityTypes();
				stp.SetEnhanceTypes();
				stp.SetEquips();
				stp.SetSets();
				var exp = new Exporter(DatabaseFile, @"D:\HeroesDB\www\data\");
				exp.ExportClassification();
				exp.ExportIcons(@"D:\HeroesDB\hfs\icons");
				exp.ExportCharacters();
				exp.ExportMats();
				exp.ExportQualityTypes();
				exp.ExportEnhanceTypes();
				exp.ExportEquips();
				exp.ExportSets();
				exp.ExportSitemap(@"D:\HeroesDB\www\");
			}
			catch (Exception exception) {
				Debug.WriteLine(exception.Source);
				Debug.WriteLine(exception.Message);
				Debug.WriteLine(exception.StackTrace);
				throw exception;
			}
			finally {
				Debug.Unindent();
				Debug.WriteLine("}");
				Console.ReadKey(true);
			}
		}

	}

}
