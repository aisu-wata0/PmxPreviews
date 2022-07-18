using PEPlugin;
// using PXCPlugin;
using SlimDX.Direct3D9;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Numerics;
using System.Linq;

public class Entrypoint : PEPluginClass
{
	public override IPEPluginOption Option => new PEPluginOption(true, false);

	public override void Run(IPERunArgs args)
	{
		if (Environment.GetEnvironmentVariable("PMX_PREVIEW_GEN") != "1") return;
		var regenerate = Environment.GetEnvironmentVariable("REGENERATE") == "1";
		bool camera_fit = Environment.GetEnvironmentVariable("CAMERA_FIT") == "1";

		var shot_angle_sides_string = Environment.GetEnvironmentVariable("PMX_PREVIEW_shot_angle_sides");
		// example: var shot_angle_sides_string = "0  45  140 ";
		var shot_angle_sides = Array.ConvertAll(
			shot_angle_sides_string.Split(new[] { ' ', },
			StringSplitOptions.RemoveEmptyEntries), float.Parse);

		var shot_angle_ups_string = Environment.GetEnvironmentVariable("PMX_PREVIEW_shot_angle_ups");
		// example: var shot_angle_ups_string = "0  50  -50  -90 ";
		var shot_angle_ups = Array.ConvertAll(
			shot_angle_ups_string.Split(new[] { ' ', },
			StringSplitOptions.RemoveEmptyEntries), float.Parse);

		var connector = args.Host.Connector;
		var path = connector.Pmx?.CurrentPath;
		if (string.IsNullOrWhiteSpace(path)) return;
		var view = connector.View.PmxView as Form;
		view.Shown += (s, e) =>
		{
			var previewDirname = "prev";
			var previewDirpath = Path.Combine(Path.GetDirectoryName(path), previewDirname);
			var previewPath = Path.Combine(previewDirpath, $"{Path.GetFileName(path)}" + "_{0}, {1}.png");
			var previewsExist = true;
			if (!regenerate)
			{
				foreach (float shot_angle_side in shot_angle_sides)
				{
					foreach (float shot_angle_up in shot_angle_ups)
					{
						if (!File.Exists(String.Format(previewPath, shot_angle_up, shot_angle_side)))
						{
							previewsExist = false;
							break;
						}
					}
					if (!previewsExist)
					{
						break;
					}
				}
			}

			if (regenerate || !previewsExist)
			{
				Directory.CreateDirectory(previewDirpath);
				WritePreview(view, previewPath, camera_fit, shot_angle_ups, shot_angle_sides, connector);
			}

			var metaPath = Path.Combine(Path.GetDirectoryName(path), $"{Path.GetFileName(path)}.meta.txt");
			if (regenerate || !File.Exists(metaPath))
				WriteMetaInfo(metaPath);

			Environment.Exit(0);
		};
	}

	void WritePreview(Form viewForm, string path, bool camera_fit, float[] shot_angle_ups, float[] shot_angle_sides, IPEConnector connector)
	{
		const BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Instance;
		var vtCtrl = viewForm.Controls["vtCtrl"];
		var vtType = vtCtrl.GetType();
		foreach (var cb in new[] { "tb1_Bone", "tb3_VertexS", "tb19_Axis", "tb17_FloorShadow" })
			(vtType.GetField(cb, bf).GetValue(vtCtrl) as ToolStripButton).Checked = false;

		Application.DoEvents();

		var cmCtrl1 = viewForm.GetType().GetField("cmCtrl1", bf).GetValue(viewForm);
		var cc = cmCtrl1.GetType().GetProperty("CC").GetValue(cmCtrl1);

		if (camera_fit){
			var pmx = connector.Pmx.GetCurrentState();

			if (pmx.Vertex.Count > 0)
			{
				Vector2 _minMaxX;
				Vector2 _minMaxY;
				// Vector2 _minMaxZ;
				float _minZ;

				var POS = pmx.Vertex.Select(v => v.Position);
				_minMaxX = new Vector2(POS.Min(v => v.X), POS.Max(v => v.X));
				_minMaxY = new Vector2(POS.Min(v => v.Y), POS.Max(v => v.Y));
				// _minMaxZ = new Vector2(POS.Min(v => v.Z), POS.Max(v => v.Z));
				_minZ = POS.Min(v => v.Z);

				//https://stackoverflow.com/a/64571830
				var cam = cc.GetType().GetProperty("Camera").GetValue(cc);
				var FOV = (float)cam.GetType().GetField("FOV").GetValue(cam);
				var AspectRatio = (float)cam.GetType().GetField("AspectRatio").GetValue(cam);
				var distanceX = (float)Math.Abs(_minMaxX.Y - _minMaxX.X) / (float)Math.Sin(Math.PI / 180.0 * FOV * AspectRatio);

				var distanceY = (float)Math.Abs(_minMaxY.Y - _minMaxY.X) / (float)Math.Sin(Math.PI / 180.0 * FOV);
				var distance = Math.Max(distanceX, distanceY);
				SlimDX.Vector3 CameraPosition = (SlimDX.Vector3)cam.GetType().GetField("CameraPosition").GetValue(cam);

				CameraPosition.Y = (_minMaxY.X + _minMaxY.Y) / 2;
				CameraPosition.Z = -distance - Math.Abs(_minZ);

				cam.GetType().GetField("CameraPosition").SetValue(cam, CameraPosition);

				var CameraTarget = new SlimDX.Vector3(0f, CameraPosition.Y, 0f);


				cam.GetType().GetField("CameraTarget").SetValue(cam, CameraTarget);
				cc.GetType().GetMethod("Update").Invoke(cc, new object[] { true });
			}
		}


		float shot_angle_side_prev = 0f;
		foreach (float shot_angle_side in shot_angle_sides)
		{
			cc.GetType().GetMethod("TurnRight").Invoke(cc, new object[] { shot_angle_side - shot_angle_side_prev });
			float shot_angle_up_prev = 0f;
			foreach (float shot_angle_up in shot_angle_ups)
			{
				cc.GetType().GetMethod("TurnUp").Invoke(cc, new object[] { shot_angle_up - shot_angle_up_prev });

				cc.GetType().GetMethod("Update").Invoke(cc, new object[] { true });
				var draw = viewForm.GetType().GetProperty("PmxDraw").GetValue(viewForm);
				var d3d = draw.GetType().BaseType.GetField("m_manager", bf).GetValue(draw);
				var backBuffer = d3d.GetType().GetProperty("RenderTargetBuffer").GetValue(d3d) as Surface;
				using (var stream = Surface.ToStream(backBuffer, ImageFileFormat.Bmp))
				using (var bitmap = new Bitmap(stream))
					bitmap.Save(String.Format(path, shot_angle_up, shot_angle_side), ImageFormat.Png);

				shot_angle_up_prev = shot_angle_up;
			}
			cc.GetType().GetMethod("TurnUp").Invoke(cc, new object[] { -shot_angle_up_prev });
			shot_angle_up_prev = 0f;
			shot_angle_side_prev = shot_angle_side;
		}

	}

	void WriteMetaInfo(string path)
	{
		var meta = new StringBuilder();

		foreach (Form f in Application.OpenForms)
		{
			if (f.GetType().Name != "PmxForm") continue;
			var gb5 = f.Controls["tabPmxData"].Controls["tabPage1"].Controls["groupBox5"];
			var modelNameBox = gb5.Controls["txtInfo_ModelName"] as TextBox;
			meta.AppendLine($"NAME_JP={modelNameBox.Text}");
			(gb5.Controls["jeModelInfo"].Controls["rdEN"] as RadioButton).Checked = true;
			meta.AppendLine($"NAME_EN={modelNameBox.Text}");
			break;
		}

		File.WriteAllText(path, meta.ToString());
	}

	//void DumpControls(Control root)
	//{
	//    var sb = new StringBuilder();
	//    var q = new Stack<(Control, int)>();
	//    q.Push((root, 0));
	//    while (q.Count > 0)
	//    {
	//        var c = q.Pop();
	//        var tabulation = new string('\t', c.Item2);
	//        sb.AppendLine($"{tabulation}{c.Item1.Name} - {c.Item1.GetType()}");
	//        foreach (Control s in c.Item1.Controls)
	//            q.Push((s, c.Item2 + 1));
	//    }
	//    File.WriteAllText("out.txt", sb.ToString());
	//}
}
