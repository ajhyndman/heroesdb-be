HeroesDB.net backend
=====================
This is the backend for [heroesdb.net](http://www.heroesdb.net/), which is a Vindictus item database website. This application extracts data from game files and generates JSON files for equipment, sets, enchants, crafting recipes and few other things, also icons and screenshots for everything. Requires [frontend](https://gitlab.com/niaru/heroesdb-fe/) to view the generated data in a meaningful way. Everything is written in C#, with a tiny bit of C. To compile this, you will need a [SQLite](http://system.data.sqlite.org) and [TargaImage](https://www.codeproject.com/Articles/31702/NET-Targa-Image-Reader) libraries.

This isn't pretty, this was never ment to be used by anyone but me.

Development
-----------
### Files and Paths
First thing you have to do, is find and extract these game files:

	heroes.db3:
		286FE9924483F382029EF68BA6C260B3C2563BF9.hfs
	heroes_text_english_eu.txt:
		5A7E0CE7BC7F94F5E5205A84D700A5A04DADA8BB.hfs
	icons:
		37C0910CA69EA06694C5E62A2D6034BCBCB20A7B.hfs
		8325A482843F9E040B27192EA94C894695F6EAF7.hfs
		6AA8EBA1AC8E354032250F4D316B928ACCA3BBA0.hfs
		903E6E6F87E1760EA9E2CDE6523D340DD0C352E3.hfs
	player_costume.txt and shields.txt:
		763FD8F12D3D013194261D98B4AEF0FB0F6FBA1B.hfs
	NPC images:
		D9B2C8F8022700BF17C02DE21F952A9264662C32.hfs
		84A9C242FE66C164D3BEB587B18E157A057BC66C.hfs
	QuestResultPanel_I16.dds:
		336A97B847BB6CCA869D56A4F9B4460E45C46F5B.hfs

They're in the `hfs` directory of the game and can be extracted with VZipFlip (search the web, or ask someone you trust, I don't think there's an official website for it). `heroes.db3` is the main file from where all data comes from, it's a SQLite database. `heroes_text_english_eu.txt` is where from names and discriptions come, I'm not sure if there are still EU and NA versions of this or just NA now. For icons you should create a new directory and extract everything from the mentioned files there. `player_costume.txt` and `shields.txt` are required only for screenshots. NPC images and `QuestResultPanel_I16.dds` are what's in the [frontend's](https://gitlab.com/niaru/heroesdb-fe/) `assets` directory. That should be everything, or at least that's how it was on the EU version, could be different now, could need to find more files later.

Once you're done with that, edit `Config` to match your paths.

### Setup

Once you have the required files ready, the next step is to run all `Set` methods, who will try to parse the relevant data and move it to a more sane structure (`HDB_` tables) for exporting later. You might want to change Region in the `app.config` file, that's used to determine which features are live - which items and characters should be exported (doesn't work for everything though). New characters and weapon types have to be added manually (see commit history for how Delia was added), all other game updates ideally won't require any changes, but realistically something will fail now and then, hopefully a constraint will trigger and let you know. There's quite a bit of mess with these SQL statements, SQLite was the wrong tool for this job.

### Export

Once all `Set` methods successfully complete, you're ready to `Export` everything from those new `HDB_` tables to JSON files and PNG icons. This should work reliably, only thing that sometimes fails is `Exporter.ExportIcons()`, when there's a new material color added to the game, in that case add the color to `Config.Materials`.

### Screenshots

This is the most involved process and requires some explanations. Screenshots are generated almost completely automatically, by using a mod that injects custom preview costumes for the avatar shop and controlling mouse and keyboard to position the character and take screenshots. You will need an Vindictus account(s) and every character you wish to take screenshots for, you will have to login and enter the avatar shop, that's what the application is asking from you, when it says "Initialize {0} for screenshots". Although I never got banned for this, I strongly recommend using Vindictus CN (doesn't have region restrictions) or at least a VPN.

First `Setuper.SetEquips()`, `Setuper.SetSets()`, `Setuper.SetScreenshots()` will determine which screenshots can be and need be taken. Then `Screenshoter.Screenshot()` using the mod will inject custom costumes based on `Config.GetCostumeTemplateDefaultValues()` and `Config.Materials` and will control mouse and keyboard to automate the process. For this to work, you have to run the application with administrator rights. Mouse is moved to absolute screen positions, so if the buttons get moved around in the game, you will have to adjust the values in `Screenshoter`. When all screenshots are taken, `Exporter.ExportScreenshots()` will resize and tag them using [ImageMagick](https://www.imagemagick.org/) and [ExifTool](http://www.sno.phy.queensu.ca/~phil/exiftool/). Move `overlay.png` to your `Config.RootPath` + `screenshots` directory, else resizing and tagging will fail.

The mod is a modified `zlib1.dll`, that can give the game your file, instead of the file the game asked for. The way you can (and `Screenshot()` does) interact with the mod, is by creating files and directories on your `d:\`. If you create a `d:\zlib_trace.txt` file, the mod will write a lot of debug information there. If you create a `d:\zlib_dump\` directory, the mod will write there all files the game is asking for. Finally and most importantly, a `d:\zlib_replace\` directory will instruct the mod to compare each game's requested file with `d:\zlib_replace\old_%d.bin` files and on a match, to give the equivalent `d:\zlib_replace\new_%d.bin` file instead.

So, download [zlib](http://www.zlib.net/) source, patch `inflate.c` there to be similar to `zlib_inject\inflate.c`, compile and replace the game's `zlib1.dll` with your. Then try taking screenshots with `Screenshoter.Screenshot()`, if nothing happens, then you probably didn't run the application with admin rights, if screenshots are being taken, but they all are of the same default costume, then you have to update the `zlib_inject_source` directory with current files, which need to be replaced, use `d:\zlib_dump\` to figure out what the game is currently asking for. Remember to create those control files/directories on `d:\` only when you need to and delete any that the application leaves (if it does), it's slow and creates a lot of data.

Deployment
----------
Nothing to do here, besides making sure everything is being written in the [frontend's](https://gitlab.com/niaru/heroesdb-fe/) `data` directory. A good idea is to have a separate Git repository for that `data` directory, so you can see what's changing after every update.