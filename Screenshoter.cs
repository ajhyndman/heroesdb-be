
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System;

namespace HeroesDB {

	static class WinUser {

		public const UInt32 INPUT_MOUSE = 0;
		public const UInt32 INPUT_KEYBOARD = 1;
		public const UInt32 INPUT_HARDWARE = 2;

		public const UInt32 KEYEVENTF_EXTENDEDKEY = 0x0001;
		public const UInt32 KEYEVENTF_KEYUP = 0x0002;
		public const UInt32 KEYEVENTF_UNICODE = 0x0004;
		public const UInt32 KEYEVENTF_SCANCODE = 0x0008;

		public const UInt32 MOUSEEVENTF_MOVE = 0x0001;
		public const UInt32 MOUSEEVENTF_LEFTDOWN = 0x0002;
		public const UInt32 MOUSEEVENTF_LEFTUP = 0x0004;
		public const UInt32 MOUSEEVENTF_RIGHTDOWN = 0x0008;
		public const UInt32 MOUSEEVENTF_RIGHTUP = 0x0010;
		public const UInt32 MOUSEEVENTF_MIDDLEDOWN = 0x0020;
		public const UInt32 MOUSEEVENTF_MIDDLEUP = 0x0040;
		public const UInt32 MOUSEEVENTF_XDOWN = 0x0080;
		public const UInt32 MOUSEEVENTF_XUP = 0x0100;
		public const UInt32 MOUSEEVENTF_WHEEL = 0x0800;
		public const UInt32 MOUSEEVENTF_VIRTUALDESK = 0x4000;
		public const UInt32 MOUSEEVENTF_ABSOLUTE = 0x8000;

		public const Int32 SW_HIDE = 0;
		public const Int32 SW_SHOWNORMAL = 1;
		public const Int32 SW_SHOWMINIMIZED = 2;
		public const Int32 SW_SHOWMAXIMIZED = 3;
		public const Int32 SW_SHOWNOACTIVATE = 4;
		public const Int32 SW_SHOW = 5;
		public const Int32 SW_MINIMIZE = 6;
		public const Int32 SW_SHOWMINNOACTIVE = 7;
		public const Int32 SW_SHOWNA = 8;
		public const Int32 SW_RESTORE = 9;
		public const Int32 SW_SHOWDEFAULT = 10;
		public const Int32 SW_MAX = 10;

		// http://msdn.microsoft.com/en-us/library/windows/desktop/ms646270.aspx
		[StructLayout(LayoutKind.Sequential)]
		public struct INPUT {
			public UInt32 type;
			public MOUSEKEYBDHARDWAREINPUT data;
		}

		// http://social.msdn.microsoft.com/Forums/en/csharplanguage/thread/f0e82d6e-4999-4d22-b3d3-32b25f61fb2a
		[StructLayout(LayoutKind.Explicit)]
		public struct MOUSEKEYBDHARDWAREINPUT {
			[FieldOffset(0)]
			public HARDWAREINPUT hi;
			[FieldOffset(0)]
			public KEYBDINPUT ki;
			[FieldOffset(0)]
			public MOUSEINPUT mi;
		}

		// http://msdn.microsoft.com/en-us/library/windows/desktop/ms646269.aspx
		[StructLayout(LayoutKind.Sequential)]
		public struct HARDWAREINPUT {
			public UInt32 uMsg;
			public UInt16 wParamL;
			public UInt16 wParamH;
		}

		// http://msdn.microsoft.com/en-us/library/windows/desktop/ms646271.aspx
		[StructLayout(LayoutKind.Sequential)]
		public struct KEYBDINPUT {
			public UInt16 wVk;
			public UInt16 wScan;
			public UInt32 dwFlags;
			public UInt32 time;
			public IntPtr dwExtraInfo;
		}

		// http://msdn.microsoft.com/en-us/library/windows/desktop/ms646273.aspx
		[StructLayout(LayoutKind.Sequential)]
		public struct MOUSEINPUT {
			public Int32 dx;
			public Int32 dy;
			public Int32 mouseData;
			public UInt32 dwFlags;
			public Int32 time;
			public IntPtr dwExtraInfo;
		}

		[DllImport("user32.dll")]
		public static extern IntPtr FindWindow(String lpClassName, String lpWindowName);

		[DllImport("user32.dll")]
		public static extern Boolean ShowWindow(IntPtr hWnd, Int32 nCmdShow);

		[DllImport("user32.dll")]
		public static extern Boolean SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern UInt32 SendInput(UInt32 nInputs, [MarshalAs(UnmanagedType.LPArray, SizeConst = 1)] INPUT[] pInputs, Int32 cbSize);

	}

	public class Screenshoter {

		private Config config;

		public Screenshoter(Config config) {
			Debug.WriteLine("Screenshoter() {");
			Debug.Indent();
			this.config = config;
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		private void sendInput(WinUser.INPUT input, Int32 delay = 50) {
			System.Threading.Thread.Sleep(delay);
			WinUser.SendInput(1, new [] { input }, Marshal.SizeOf(typeof(WinUser.INPUT)));
		}

		/*
		private void type() {
			var input = new WinUser.INPUT {
				type = WinUser.INPUT_KEYBOARD,
				data = new WinUser.MOUSEKEYBDHARDWAREINPUT {
					ki = new WinUser.KEYBDINPUT()
				}
			};
			input.data.ki.wVk = 0x41;
			this.sendInput(input);
			input.data.ki.dwFlags = WinUser.KEYEVENTF_KEYUP;
			this.sendInput(input);
			input.data.ki.dwFlags = 0;
			input.data.ki.wVk = 0x42;
			this.sendInput(input, 1000);
			input.data.ki.dwFlags = WinUser.KEYEVENTF_KEYUP;
			this.sendInput(input);
		}
		*/

		private void click(Int32 x, Int32 y) {
			var bounds = Screen.PrimaryScreen.Bounds;
			x = x * 65535 / bounds.Width;
			y = y * 65535 / bounds.Height;
			var input = new WinUser.INPUT {
				type = WinUser.INPUT_MOUSE,
				data = new WinUser.MOUSEKEYBDHARDWAREINPUT {
					mi = new WinUser.MOUSEINPUT {
						dx = x,
						dy = y,
						dwFlags = WinUser.MOUSEEVENTF_ABSOLUTE | WinUser.MOUSEEVENTF_MOVE
					}
				}
			};
			this.sendInput(input);
			input.data.mi.dwFlags = WinUser.MOUSEEVENTF_LEFTDOWN;
			this.sendInput(input);
			input.data.mi.dwFlags = WinUser.MOUSEEVENTF_LEFTUP;
			this.sendInput(input);
		}

		private void drag(Int32 fromX, Int32 fromY, Int32 toX, Int32 toY, Double speed) {
			var bounds = Screen.PrimaryScreen.Bounds;
			fromX *= 65535 / bounds.Width;
			fromY *= 65535 / bounds.Height;
			toX *= 65535 / bounds.Width;
			toY *= 65535 / bounds.Height;
			speed = speed > 1 ? 1 : speed;
			speed = speed < 0.001 ? 0.001 : speed;
			var stepX = (toX - fromX) * speed;
			var stepY = (toY - fromY) * speed;
			var input = new WinUser.INPUT {
				type = WinUser.INPUT_MOUSE,
				data = new WinUser.MOUSEKEYBDHARDWAREINPUT {
					mi = new WinUser.MOUSEINPUT {
						dx = fromX,
						dy = fromY,
						dwFlags = WinUser.MOUSEEVENTF_ABSOLUTE | WinUser.MOUSEEVENTF_MOVE
					}
				}
			};
			this.sendInput(input);
			input.data.mi.dwFlags = WinUser.MOUSEEVENTF_LEFTDOWN;
			this.sendInput(input);
			input.data.mi.dwFlags = WinUser.MOUSEEVENTF_ABSOLUTE | WinUser.MOUSEEVENTF_MOVE;
			for (var i = 1; i <= 1 / speed; i += 1) {
				input.data.mi.dx = Convert.ToInt32(fromX + (stepX * i));
				input.data.mi.dy = Convert.ToInt32(fromY + (stepY * i));
				this.sendInput(input);
			}
			input.data.mi.dwFlags = WinUser.MOUSEEVENTF_LEFTUP;
			this.sendInput(input);
		}

		private void scroll(Int32 x) {
			for (var i = 0; i != x; i += (x < 0 ? -1 : 1)) {
				var input = new WinUser.INPUT {
					type = WinUser.INPUT_MOUSE,
					data = new WinUser.MOUSEKEYBDHARDWAREINPUT {
						mi = new WinUser.MOUSEINPUT {
							dwFlags = WinUser.MOUSEEVENTF_WHEEL,
							mouseData = (x < 0 ? -120 : 120)
						}
					}
				};
				this.sendInput(input);
			}
		}

		private void setInjectOldFiles(Int32 characterID) {
			foreach (var file in Directory.GetFiles(this.config.InjectDestinationPath, "old_*.bin")) {
				File.Delete(file);
			}
			foreach (var i in new [] { 0, 1 }) {
				var sourceFilename = String.Concat(characterID, "_old_", i, ".bin");
				var sourceFile = Path.Combine(this.config.InjectSourcePath, sourceFilename);
				var destinationFilename = String.Concat("old_", i, ".bin");
				var destinationFile = Path.Combine(this.config.InjectDestinationPath, destinationFilename);
				if (File.Exists(sourceFile)) {
					File.Copy(sourceFile, destinationFile, true);
				}
			}
		}

		private void setInjectNewFile(Int32 characterID, String type, Dictionary<String, String> values) {
			var sourceFilename = String.Concat(characterID, "_new.bin");
			if (type == "shield") {
				sourceFilename = String.Concat(characterID, "_", type, "_new.bin");
			}
			var sourceFile = Path.Combine(this.config.InjectSourcePath, sourceFilename);
			var template = File.ReadAllText(sourceFile);
			foreach (var key in values.Keys) {
				template = template.Replace(String.Concat("{{", key, "}}"), values[key]);
			}
			var destinationFile0 = Path.Combine(this.config.InjectDestinationPath, "new_0.bin");
			File.WriteAllText(destinationFile0, template);
			if (File.Exists(Path.Combine(this.config.InjectDestinationPath, "old_1.bin"))) {
				var destinationFile1 = Path.Combine(this.config.InjectDestinationPath, "new_1.bin");
				File.Copy(destinationFile0, destinationFile1, true);
			}
		}

		private Boolean saveScreenshot(String objectTypeDirectory, String objectKey, Int32 characterID, String camera) {
			var sourceFiles = Directory.GetFiles(this.config.ScreenshotSourcePath);
			if (sourceFiles.Length == 0) {
				return false;
			}
			var sourceFile = sourceFiles[0];
			var destinationFilename = String.Concat(objectKey, "_", characterID, camera, ".jpeg");
			var destinationFile = Path.Combine(this.config.RootPath, "screenshots", objectTypeDirectory, destinationFilename);
			if (File.Exists(destinationFile)) {
				File.Delete(destinationFile);
			}
			File.Move(sourceFile, destinationFile);
			return true;
		}

		private void setInitialCamera() {
			this.scroll(-5);
			this.drag(1000, 700, 1000, 520, 0.5);
		}

		private void setPose(String pose, Boolean reset = false) {
			var poses = new Dictionary<String, Int32> {
				{ "1", 30},
				{ "2", 75},
				{ "3", 120},
				{ "4", 160},
				{ "5", 210}
			};
			if (reset) {
				this.click(poses["1"], 185);
			}
			else {
				this.click(poses[pose], 185);
			}
		}

		private void setCamera(String camera, Boolean reset = false) {
			var pose = camera.Substring(0, 1);
			var xPosition = camera.Substring(1, 2);
			var xPositions = new Dictionary<String, Int32> {
				{ "df", 0 },
				{ "fr", -10 },
				{ "dr", -20 },
				{ "rb", -30 },
				{ "db", -40 },
				{ "bl", 30 },
				{ "dl", 20 },
				{ "lf", 10 }
			};
			var xDistance = xPositions[xPosition] * 15;
			var yPosition = camera.Substring(3, 2);
			var yPositions = new Dictionary<String, Int32> {
				{ "ah", 15 },
				{ "oh", 10 },
				{ "ac", 5 },
				{ "oc", 0 },
				{ "bc", -5 },
				{ "of", -10 },
				{ "bf", -15 }
			};
			var yDistance = yPositions[yPosition] * 15;
			var zPosition = camera.Substring(5, 1);
			var zPositions = new Dictionary<String, Int32> {
				{ "f", 0 },
				{ "b", 1 },
				{ "c", 2 }
			};
			var zDistance = zPositions[zPosition];
			if (reset) {
				this.setPose(pose, true);
				this.click(200, 25);
				xDistance = -xDistance;
				yDistance = -yDistance;
				zDistance = -zDistance;
			}
			else {
				this.setPose(pose);
				if (xPosition == "rb" || xPosition == "db" || xPosition == "bl") {
					this.drag(118, 48, 128, 48, 0.5);
					this.drag(128, 48, 118, 48, 0.5);
				}
			}
			if (xDistance != 0 || yDistance != 0) {
				this.drag(1000, 700, 1000 + xDistance, 700 + yDistance, 0.5);
			}
			if (zDistance != 0) {
				this.scroll(zDistance);
			}
			System.Threading.Thread.Sleep(2000);
		}

		private void clickLoad() {
			this.click(200, 330);
			System.Threading.Thread.Sleep(2500);
		}

		private void clickScreenshot() {
			this.click(275, 75);
			System.Threading.Thread.Sleep(2000);
		}

		private void ScreenshotEquips(Int32 characterID) {
			Debug.WriteLine("ScreenshotEquips({0}) {{", characterID);
			Debug.Indent();
			var exportPath = Path.Combine(this.config.ExportPath, "screenshots", "equips");
			if (!Directory.Exists(exportPath)) {
				Directory.CreateDirectory(exportPath);
			}
			this.setInjectOldFiles(characterID);
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var updateCommand = connection.CreateCommand();
				updateCommand.CommandText = @"
					UPDATE HDB_EquipScreenshots
					SET Ready = 1
					WHERE
						HDB_EquipScreenshots.EquipKey = @EquipKey AND
						HDB_EquipScreenshots.CharacterID = @CharacterID AND
						HDB_EquipScreenshots.Camera = @Camera;
				";
				updateCommand.Parameters.Add("@EquipKey", DbType.String);
				updateCommand.Parameters.Add("@CharacterID", DbType.Int32).Value = characterID;
				updateCommand.Parameters.Add("@Camera", DbType.String);
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						es.EquipKey,
						e.GroupKey AS EquipGroupKey,
						e.CategoryKey AS EquipCategoryKey,
						es.EquipCostumeKey,
						es.EquipCostumeModel,
						ei.Material1 AS EquipMaterial1,
						ei.Material2 AS EquipMaterial2,
						ei.Material3 AS EquipMaterial3,
						es.Camera
					FROM HDB_EquipScreenshots AS es
					INNER JOIN HDB_Equips AS e ON e.Key = es.EquipKey
					INNER JOIN EquipItemInfo AS ei ON ei._ROWID_ = e.ID
					WHERE
						es.Ready = 0 AND
						es.CharacterID = @CharacterID
					ORDER BY
						e.CategoryKey,
						es.Camera,
						es.EquipKey;
				";
				command.Parameters.Add("@CharacterID", DbType.String).Value = characterID;
				var reader = command.ExecuteReader();
				String currentCamera = null;
				while (reader.Read()) {
					Debug.Write(".");
					var categoryKey = Convert.ToString(reader["EquipCategoryKey"]);
					var groupKey = Convert.ToString(reader["EquipGroupKey"]);
					var values = this.config.GetCostumeTemplateDefaultValues("equip", characterID);
					String valueKey = null;
					if (groupKey == "armor") {
						valueKey = categoryKey;
					}
					else if (groupKey == "offhand") {
						valueKey = "shield";
					}
					else if (groupKey == "weapon") {
						valueKey = groupKey;
						values[String.Concat(valueKey, "_type")] = String.Concat("weapon_", categoryKey);
					}
					else {
						Debug.WriteLine("unsupported EquipGroupKey: {0}", groupKey);
					}
					if (valueKey != null) {
						values[valueKey] = Convert.ToString(reader["EquipCostumeKey"] != DBNull.Value ? reader["EquipCostumeKey"] : reader["EquipCostumeModel"]);
						var material1Key = Convert.ToString(reader["EquipMaterial1"]);
						var material2Key = Convert.ToString(reader["EquipMaterial2"]);
						var material3Key = Convert.ToString(reader["EquipMaterial3"]);
						var material1Index = 0;
						var material2Index = material2Key == material1Key ? 1 : 0;
						var material3Index = material3Key == material1Key && material3Key == material2Key ? 2 : (material3Key == material1Key || material3Key == material2Key ? 1 : 0);
						if (reader["EquipMaterial1"] != DBNull.Value) {
							values[String.Concat(valueKey, "_color1")] = Convert.ToString(this.config.Materials[material1Key][material1Index].ToArgb());
						}
						if (reader["EquipMaterial2"] != DBNull.Value) {
							values[String.Concat(valueKey, "_color2")] = Convert.ToString(this.config.Materials[material2Key][material2Index].ToArgb());
						}
						if (reader["EquipMaterial3"] != DBNull.Value) {
							values[String.Concat(valueKey, "_color3")] = Convert.ToString(this.config.Materials[material3Key][material3Index].ToArgb());
						}
						this.setInjectNewFile(characterID, valueKey, values);
						var equipKey = Convert.ToString(reader["EquipKey"]);
						var camera = Convert.ToString(reader["Camera"]);
						this.clickLoad();
						if (camera != currentCamera) {
							if (currentCamera != null) {
								this.setCamera(currentCamera, true);
							}
							this.setCamera(camera);
							currentCamera = camera;
						}
						else if (characterID == 32) {
							this.setPose(camera.Substring(0, 1));
							System.Threading.Thread.Sleep(1000);
						}
						this.clickScreenshot();
						var i = 0;
						while (!this.saveScreenshot("equips", equipKey, characterID, camera)) {
							Debug.Write("r");
							if (i++ > 10) {
								throw new Exception("Failing to take take/save screenshots.");
							}
							System.Threading.Thread.Sleep(5000);
							this.clickScreenshot();
						}
						updateCommand.Parameters["@EquipKey"].Value = reader["EquipKey"];
						updateCommand.Parameters["@CharacterID"].Value = characterID;
						updateCommand.Parameters["@Camera"].Value = reader["Camera"];
						updateCommand.ExecuteNonQuery();
					}
				}
				if (currentCamera != null) {
					this.setCamera(currentCamera, true);
				}
				Debug.WriteLine("");
				reader.Close();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		private void ScreenshotSets(Int32 characterID) {
			Debug.WriteLine("ScreenshotSets({0}) {{", characterID);
			Debug.Indent();
			var exportPath = Path.Combine(this.config.ExportPath, "screenshots", "sets");
			if (!Directory.Exists(exportPath)) {
				Directory.CreateDirectory(exportPath);
			}
			this.setInjectOldFiles(characterID);
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var updateCommand = connection.CreateCommand();
				updateCommand.CommandText = @"
					UPDATE HDB_SetScreenshots
					SET Ready = 1
					WHERE
						HDB_SetScreenshots.SetKey = @SetKey AND
						HDB_SetScreenshots.CharacterID = @CharacterID AND
						HDB_SetScreenshots.Camera = @Camera;
				";
				updateCommand.Parameters.Add("@SetKey", DbType.String);
				updateCommand.Parameters.Add("@CharacterID", DbType.Int32).Value = characterID;
				updateCommand.Parameters.Add("@Camera", DbType.String);
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						ss.SetKey,
						e.GroupKey AS EquipGroupKey,
						e.CategoryKey AS EquipCategoryKey,
						ssp.EquipCostumeKey,
						ei.Material1 AS EquipMaterial1,
						ei.Material2 AS EquipMaterial2,
						ei.Material3 AS EquipMaterial3,
						ss.Camera
					FROM HDB_SetScreenshots AS ss
					INNER JOIN HDB_SetScreenshotParts AS ssp ON
						ssp.SetKey = ss.SetKey AND
						ssp.CharacterID = ss.CharacterID
					INNER JOIN HDB_Equips AS e ON e.Key = ssp.EquipKey
					INNER JOIN EquipItemInfo AS ei ON ei._ROWID_ = e.ID
					WHERE
						ss.Ready = 0 AND
						ss.CharacterID = @CharacterID
					ORDER BY
						ss.Camera DESC,
						ss.SetKey;
				";
				command.Parameters.Add("@CharacterID", DbType.String).Value = characterID;
				var reader = command.ExecuteReader();
				var values = this.config.GetCostumeTemplateDefaultValues("set", characterID);
				String currentCamera = null;
				Action<String, String> screenshot = (setKey, camera) => {
					this.setInjectNewFile(characterID, "set", values);
					this.clickLoad();
					if (camera != currentCamera) {
						if (currentCamera != null) {
							this.setCamera(currentCamera, true);
						}
						this.setCamera(camera);
						currentCamera = camera;
					}
					else if (characterID == 32) {
						this.setPose(camera.Substring(0, 1));
						System.Threading.Thread.Sleep(1000);
					}
					this.clickScreenshot();
					var i = 0;
					while (!this.saveScreenshot("sets", setKey, characterID, camera)) {
						Debug.WriteLine("r");
						if (i++ > 10) {
							throw new Exception("Failing to take take/save screenshots.");
						}
						System.Threading.Thread.Sleep(5000);
						this.clickScreenshot();
					}
					updateCommand.Parameters["@SetKey"].Value = setKey;
					updateCommand.Parameters["@CharacterID"].Value = characterID;
					updateCommand.Parameters["@Camera"].Value = camera;
					updateCommand.ExecuteNonQuery();
				};
				String previousSetKey = null;
				String previousCamera = null;
				while (reader.Read()) {
					var setKey = Convert.ToString(reader["SetKey"]);
					var camera = Convert.ToString(reader["Camera"]);
					var categoryKey = Convert.ToString(reader["EquipCategoryKey"]);
					var groupKey = Convert.ToString(reader["EquipGroupKey"]);
					if ((setKey != previousSetKey || camera != previousCamera) && previousSetKey != null) {
						Debug.Write(".");
						screenshot(previousSetKey, previousCamera);
						values = this.config.GetCostumeTemplateDefaultValues("set", characterID);
					}
					String valueKey = null;
					if (groupKey == "armor") {
						valueKey = categoryKey;
					}
					else if (groupKey == "weapon") {
						valueKey = groupKey;
						values[String.Concat(valueKey, "_type")] = String.Concat("weapon_", categoryKey);
					}
					else {
						Debug.WriteLine("unsupported EquipGroupKey: {0}", groupKey);
					}
					if (valueKey != null) {
						values[valueKey] = Convert.ToString(reader["EquipCostumeKey"]);
						var material1Key = Convert.ToString(reader["EquipMaterial1"]);
						var material2Key = Convert.ToString(reader["EquipMaterial2"]);
						var material3Key = Convert.ToString(reader["EquipMaterial3"]);
						var material1Index = 0;
						var material2Index = material2Key == material1Key ? 1 : 0;
						var material3Index = material3Key == material1Key && material3Key == material2Key ? 2 : (material3Key == material1Key || material3Key == material2Key ? 1 : 0);
						if (reader["EquipMaterial1"] != DBNull.Value) {
							values[String.Concat(valueKey, "_color1")] = Convert.ToString(this.config.Materials[material1Key][material1Index].ToArgb());
						}
						if (reader["EquipMaterial2"] != DBNull.Value) {
							values[String.Concat(valueKey, "_color2")] = Convert.ToString(this.config.Materials[material2Key][material2Index].ToArgb());
						}
						if (reader["EquipMaterial3"] != DBNull.Value) {
							values[String.Concat(valueKey, "_color3")] = Convert.ToString(this.config.Materials[material3Key][material3Index].ToArgb());
						}
					}
					previousSetKey = setKey;
					previousCamera = camera;
				}
				if (previousSetKey != null) {
					screenshot(previousSetKey, previousCamera);
				}
				if (currentCamera != null) {
					this.setCamera(currentCamera, true);
				}
				Debug.WriteLine("");
				reader.Close();
			}
			Debug.Unindent();
			Debug.WriteLine("}");
		}

		public void Screenshot() {
			Debug.WriteLine("Screenshot() {");
			Debug.Indent();
			var window = WinUser.FindWindow("Valve001", null);
			if (window == IntPtr.Zero) {
				Debug.WriteLine("window not found");
				return;
			}
			if (!Directory.Exists(this.config.InjectDestinationPath)) {
				Directory.CreateDirectory(this.config.InjectDestinationPath);
			}
			if (Directory.Exists(this.config.ScreenshotSourcePath)) {
				foreach (var file in Directory.GetFiles(this.config.ScreenshotSourcePath)) {
					File.Delete(file);
				}
			}
			var characters = new DataTable();
			using (var connection = new SQLiteConnection(this.config.ConnectionString)) {
				connection.Open();
				var command = connection.CreateCommand();
				command.CommandText = @"
					SELECT
						c.ID,
						c.Name
					FROM HDB_Characters AS c
					WHERE
						c.ID IN (
							SELECT es.CharacterID
							FROM HDB_EquipScreenshots AS es
							WHERE es.Ready = 0
						) OR
						c.ID IN (
							SELECT ss.CharacterID
							FROM HDB_SetScreenshots AS ss
							WHERE ss.Ready = 0
						)
					ORDER BY
						CASE WHEN c.ID IN (4, 8, 64, 256) THEN 1 WHEN c.ID IN (2, 16, 32, 128) THEN 2 WHEN c.ID IN (1, 512) THEN 3 ELSE 4 END,
						c.ID;
				";
				var reader = command.ExecuteReader();
				characters.Load(reader);
			}
			foreach (DataRow character in characters.Rows) {
				Console.WriteLine("Initialize {0} for screenshots and press [Enter] or any other key to skip.", character["Name"]);
				Console.Beep();
				var key = Console.ReadKey();
				if (key.Key == ConsoleKey.Enter) {
					WinUser.ShowWindow(window, WinUser.SW_RESTORE);
					WinUser.SetForegroundWindow(window);
					this.setInitialCamera();
					this.ScreenshotSets(Convert.ToInt32(character["ID"]));
					this.ScreenshotEquips(Convert.ToInt32(character["ID"]));
				}
			}
			Directory.Delete(this.config.InjectDestinationPath, true);
			Debug.Unindent();
			Debug.WriteLine("}");
		}

	}

}
