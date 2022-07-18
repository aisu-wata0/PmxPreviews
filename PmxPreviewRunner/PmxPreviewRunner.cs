using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

internal class Program
{
	const string PMXE_PATH = "PmxEditor_x64.exe";
	static void Main(string[] args)
	{
		Console.WriteLine(Environment.CurrentDirectory);
		Console.WriteLine(args.Length);

		if (!File.Exists(PMXE_PATH))
		{
			Console.WriteLine($"Could not find \"{PMXE_PATH}\" in the execution folder (\"{Environment.CurrentDirectory}\")");
			return;
		}

		if (args.Length < 1)
		{
			Console.WriteLine("Usage: PmxPreviewRunner \"folder_to_create_previews\"");
			return;
		}

		var dir = args[0];
		if (!Directory.Exists(dir))
		{
			Console.WriteLine("Path: \"" + dir  + "\" does't exist");
			return;
		}

		var parallel = false;
		var patch_pmx_header = false;
		var parallelism = 4;
		var regenerate = false;
		var camera_fit = false;
		float[] shot_angle_sides = { };
		float[] shot_angle_ups = { };
		const string CONFIG_PATH = "config.ini";
		try
		{
			if (File.Exists(CONFIG_PATH))
			{
				var config = File.ReadAllLines(CONFIG_PATH)
					.Select(x => x.Trim())
					.Where(x => !x.StartsWith("//") && !x.StartsWith(";"))
					.Select(x => x.Split('='))
					.Where(x => x.Length == 2)
					.Select(x => x.Select(y => y.Trim()).ToArray());

				foreach (var configEntry in config)
				{
					var val = configEntry[1];
					switch (configEntry[0])
					{
						case "PARALLEL":
							parallel = val == "1";
							break;
						case "PARALLELISM":
							if (int.TryParse(val, out int v) && (v == -1 || v > 0))
								parallelism = v;
							break;
						case "PATCH_PMX_HEADER":
							patch_pmx_header = val == "1";
							break;
						case "REGENERATE":
							regenerate = val == "1";
							break;
						case "CAMERA_FIT":
							camera_fit = val == "1";
							break;
						case "PMX_PREVIEW_shot_angle_sides":
							// example: var val = "0, 45, 140,";
							if (val != null && val.Length > 0)
							{
								shot_angle_sides = Array.ConvertAll(
									val.Split(new[] { ',', },
									StringSplitOptions.RemoveEmptyEntries), float.Parse);
							}
							break;
						case "PMX_PREVIEW_shot_angle_ups":
							// example: var val = "0, -50";
							if (val != null && val.Length > 0)
							{
								shot_angle_ups = Array.ConvertAll(
									val.Split(new[] { ',', },
									StringSplitOptions.RemoveEmptyEntries), float.Parse);
							}
							break;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.ToString());
			Console.WriteLine("Failed to parse config.ini, fix your configuration file.");
			System.Environment.Exit(1);
		}
		var shot_angle_sides_string = Environment.GetEnvironmentVariable("PMX_PREVIEW_shot_angle_sides");
		if (shot_angle_sides.Length <= 0)
		{
			float[] _ = { 0, 45, 140, };
			shot_angle_sides = _;
		}

		var shot_angle_ups_string = Environment.GetEnvironmentVariable("PMX_PREVIEW_shot_angle_ups");
		if (shot_angle_ups.Length <= 0)
		{
			float[] _ = { 0, -50 };
			shot_angle_ups = _;
		}

		var allowedExtensions = new[] { ".pmx", ".pmd" };
		var modelFiles = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
			.Where(x => allowedExtensions.Contains(Path.GetExtension(x).ToLower())).ToArray();

		Console.WriteLine($"Found {modelFiles.Length} files.");
		var sw = new Stopwatch();
		sw.Start();

		if (parallel)
		{
			var i = -1;
			Parallel.ForEach(modelFiles, new ParallelOptions() { MaxDegreeOfParallelism = parallelism }, model =>
			{
				Interlocked.Increment(ref i);
				Console.WriteLine($"{i + 1} - {model}");
				GeneratePreviewForModel(model, patch_pmx_header, regenerate, camera_fit, shot_angle_sides, shot_angle_ups);
			});
		}
		else
		{
			for (var i = 0; i < modelFiles.Length; i++)
			{
				var model = modelFiles[i];
				Console.WriteLine($"{i + 1} - {model}");
				GeneratePreviewForModel(model, patch_pmx_header, regenerate, camera_fit, shot_angle_sides, shot_angle_ups);
			}
		}

		sw.Stop();
		Console.WriteLine($"Done - {sw.ElapsedMilliseconds}ms");
		Console.ReadLine();
	}

	static bool checkFilesExist(string path, float[] shot_angle_sides, float[] shot_angle_ups)
	{
		var previewDirname = "prev";
		var dirPath = Path.GetDirectoryName(path);
		if (dirPath == null){
			Console.WriteLine($"# FAIL: directory doesn't exist: \"{dirPath}\", this shouldn't happen, since its the parent of the pmxs path");
			return false;
		}
		var previewDirpath = Path.Combine(dirPath, previewDirname);
		var previewPath = Path.Combine(previewDirpath, $"{Path.GetFileName(path)}" + "_{0}, {1}.png");
		var previewsExist = true;
		var tooLong = "";
		if (previewDirpath.Length > 246)
		{
			Console.WriteLine($"# FAIL: filename toolong (dir): \"{previewDirpath}\"");
			throw new Exception("FAIL: filename toolong (dir): \"{previewDirpath}\"");
		}
		try
		{
			foreach (float shot_angle_side in shot_angle_sides)
			{
				foreach (float shot_angle_up in shot_angle_ups)
				{
					var fname = String.Format(previewPath, shot_angle_up, shot_angle_side);
					if (fname.Length > 259)
					{
						tooLong = $"# FAIL: filename toolong: \"{fname}\"";
					}
					if (!File.Exists(fname))
					{
						previewsExist = false;
					}
				}
			}

		}
		catch
		{
			if (tooLong.Length > 0)
			{
				throw new Exception(tooLong);
			}
			return false;
		}
		if (tooLong.Length > 0)
		{
			throw new Exception(tooLong);
		}
		return previewsExist;
	}

	static void GeneratePreviewForModel(string path, bool patchPMXHeader, bool regenerate, bool camera_fit, float[] shot_angle_sides, float[] shot_angle_ups)
	{
		var previewsExist = true;
		try
		{
			previewsExist = checkFilesExist(path, shot_angle_sides, shot_angle_ups);
		}
		catch (Exception exception)
		{
			Console.WriteLine(exception.Message);
			return;
		}

		if (!regenerate && previewsExist && File.Exists(path + ".meta.txt"))
		{
			Console.WriteLine($"Skipping, preview + meta already exist. {!regenerate}   {previewsExist}   {File.Exists(path + ".meta.txt")}");
			return;
		}

		if (Path.GetExtension(path).ToLower() == ".pmx" && patchPMXHeader)
			PatchPMXHeader(path);

		var proc = new Process()
		{
			StartInfo = new ProcessStartInfo()
			{
				FileName = PMXE_PATH,
				Arguments = $"\"{path}\"",
				UseShellExecute = false,
				WindowStyle = ProcessWindowStyle.Minimized
			}
		};
		proc.StartInfo.EnvironmentVariables.Add("PMX_PREVIEW_GEN", "1");
		proc.StartInfo.EnvironmentVariables.Add("REGENERATE", regenerate ? "1" : "0");
		proc.StartInfo.EnvironmentVariables.Add("CAMERA_FIT", camera_fit ? "1" : "0");
		proc.StartInfo.EnvironmentVariables.Add("PMX_PREVIEW_shot_angle_sides", string.Join(" ", shot_angle_sides));
		proc.StartInfo.EnvironmentVariables.Add("PMX_PREVIEW_shot_angle_ups", string.Join(" ", shot_angle_ups));

		proc.Start();
		var exited = proc.WaitForExit(1000 * 120);
		if (!exited)
		{
			Console.WriteLine($"Failed: \"{path}\"");
		}
		try
		{
			proc.Kill();
		}
		catch { }
		try
		{
			proc.Close();
		}
		catch { }
		try
		{
			proc.Dispose();
		}
		catch { }
		if (!checkFilesExist(path, shot_angle_sides, shot_angle_ups))
		{
			Console.WriteLine($"# FAIL: previews not created: \"{path}\"");
		}
	}

	static void PatchPMXHeader(string path)
	{
		var allBytes = File.ReadAllBytes(path);
		if (allBytes.Length < 4) return;
		var header = System.Text.Encoding.ASCII.GetString(allBytes.Take(4).ToArray());
		if (new[] { "Pmx ", "PMX " }.Contains(header)) return;
		var backupPath = path + ".bak";
		if (!File.Exists(backupPath))
			File.Move(path, backupPath);
		var patchBytes = new byte[] { 0x50, 0x4D, 0x58, 0x20 };
		for (var i = 0; i < patchBytes.Length; i++)
			allBytes[i] = patchBytes[i];
		File.WriteAllBytes(path, allBytes);
	}
}
