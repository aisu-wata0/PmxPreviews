using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

internal class Program
{
	static void Main(string[] args)
	{
		if (args.Length != 1)
		{
			Console.WriteLine("Usage: PmxReportGen path");
			return;
		}
		var dir = args[0];
		if (!Directory.Exists(dir))
		{
			Console.WriteLine("Path does not exist");
			return;
		}
		var allowedExtensions = new[] { ".pmx", ".pmd" };
		var modelFiles = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
			.Where(x => allowedExtensions.Contains(Path.GetExtension(x).ToLower())).ToArray();
		var dirLen = dir.Length;

		var sb = new StringBuilder();
		sb.AppendLine("<html>");
		sb.AppendLine("<head>");
		sb.AppendLine(@"
			<style>
				table {
					font-family: arial, sans-serif;
					border-collapse: collapse;
				}
				table tr:nth-child(n) {
				  background-color: #bbbbbb;
				}
				table tr:nth-child(2n) {
				  background-color: #dddddd;
				}
				table td, table th {
				  border: 1px solid #dddddd;
				  text-align: left;
				  padding: 8px;
				}
				table td {
					font-size:50px;
				}
			</style>");
		sb.AppendLine("</head>");
		sb.AppendLine("<body>");
		sb.AppendLine("<table><tr><th>Preview</th><th>Name</th><th>File</th></tr>");
		//sb.AppendLine("<table><tr><th>Preview</th><th>Name</th><th>Path</th></tr>");
		foreach (var modelFile in modelFiles)
		{
			string name = null;
			var metaPath = modelFile + ".meta.txt";
			if(File.Exists(metaPath))
			{
				var metadata = File.ReadAllLines(metaPath)
					.Select(x => x.Split('='))
					.Where(x => x.Length == 2)
					.ToDictionary(x => x[0].Trim(), x => x[1].Trim());
				if (metadata.TryGetValue("NAME_JP", out string nameJP))
					name = nameJP;
				if (metadata.TryGetValue("NAME_EN", out string nameEN) && !string.IsNullOrWhiteSpace(nameEN))
					name = nameEN;
			}

			var previewDirname = "prev";
			var dirPath = Path.GetDirectoryName(modelFile);
			if (dirPath == null)
			{
				Console.WriteLine($"# FAIL: directory doesn't exist: \"{dirPath}\", this shouldn't happen, since its the parent of the pmxs path");
				return false;
			}
			var previewDirpath = Path.Combine(dirPath, previewDirname);
			var previewPath = Path.Combine(previewDirpath, $"{Path.GetFileName(modelFile)}" + "_0, 0.png");
			string imageData = null;
			if (File.Exists(previewPath))
			{
				using (var image = Image.FromFile(previewPath))
				{
					const float maxWidth = 400f;
					var imageHeight = (int)(image.Height * (maxWidth / image.Width));
					using (var resized = new Bitmap(image, new Size((int)maxWidth, imageHeight)))
					using (var ms = new MemoryStream())
					{
						resized.Save(ms, ImageFormat.Png);
						imageData = $"<img src='data:image/png;base64, {Convert.ToBase64String(ms.ToArray())}'/>";
					}
				}
			}
			sb.AppendLine($"<tr><td>{imageData}</td><td>{name}</td><td>{Path.GetFileName(modelFile)}</td></tr>");
			//var relativePath = modelFile.Substring(dir.Length);
			//sb.AppendLine($"<tr><td>{imageData}</td><td>{name}</td><td>{relativePath}</td></tr>");
		}

		sb.AppendLine("</table>");
		sb.AppendLine("</body>");
		sb.AppendLine("</html>");
		File.WriteAllText("out.html", sb.ToString());
	}
}