
using System.Diagnostics;
using System;

namespace HeroesDB {

	class Program {

		public static void Color(Config cfg) {
			while (true) {
				var arg = Console.ReadLine();
				System.Drawing.Color? colour = null;
				var rgb = arg.Split(new [] { ',' });
				if (rgb.Length == 3) {
					colour = System.Drawing.Color.FromArgb(0, Convert.ToInt32(rgb[0]), Convert.ToInt32(rgb[1]), Convert.ToInt32(rgb[2]));
				}
				else if (System.Text.RegularExpressions.Regex.IsMatch(arg, @"^\d+$")) {
					colour = System.Drawing.Color.FromArgb(Convert.ToInt32(arg));
				}
				else if (cfg.Materials.ContainsKey(arg)) {
					colour = cfg.Materials[arg][0];
				}
				if (colour != null) {
					var color = (System.Drawing.Color)colour;
					Console.WriteLine(String.Format("{0}, {1}, {2}\n{3}", color.R, color.G, color.B, color.ToArgb()));
				}
			}
		}

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
				stp.SetCharacters();
				stp.SetClassification();
				stp.SetIcons();
				stp.SetMats();
				stp.SetQualityTypes();
				stp.SetEnhanceTypes();
				stp.SetEnchants();
				stp.SetEquips();
				stp.SetSets();
				stp.SetScreenshots();
				var ssr = new Screenshoter(cfg);
				ssr.Screenshot();
				var exp = new Exporter(cfg);
				exp.ExportClassification();
				exp.ExportIcons();
				exp.ExportCharacters();
				exp.ExportMats();
				exp.ExportQualityTypes();
				exp.ExportEnhanceTypes();
				exp.ExportEnchants();
				exp.ExportScreenshots();
				exp.ExportEquips();
				exp.ExportSets();
				exp.ExportSitemap();
//				Color(cfg);
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
