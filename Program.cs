
using System.Diagnostics;
using System;

namespace HeroesDB {

	class Program {

		public static void Main(String[] args) {
			Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
			Debug.WriteLine("Main() {");
			Debug.Indent();
			try {
				var cfg = new Config();
				var ext = new Extractor();
				var stp = new Setuper(cfg);
				stp.ImportText();
				stp.SetFeaturedItems();
				stp.SetFeaturedEquips();
				stp.SetFeaturedRecipes();
				stp.SetClassification();
				stp.SetIcons();
				stp.SetCharacters();
				stp.SetMats();
				stp.SetQualityTypes();
				stp.SetEnhanceTypes();
				stp.SetEquips();
				stp.SetSets();
				var exp = new Exporter(cfg);
				exp.ExportClassification();
				exp.ExportIcons();
				exp.ExportCharacters();
				exp.ExportMats();
				exp.ExportQualityTypes();
				exp.ExportEnhanceTypes();
				exp.ExportEquips();
				exp.ExportSets();
				exp.ExportSitemap();
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
