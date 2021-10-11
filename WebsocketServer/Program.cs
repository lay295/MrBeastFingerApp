using Fleck;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace MrBeastFingerApp
{
    class Program
    {
        private static ConcurrentDictionary<User, IWebSocketConnection> allSockets = new ConcurrentDictionary<User, IWebSocketConnection>();
		private static List<User> userList = new List<User>();
		static double screenX = 0, screenY = 0;
		static void Main(string[] args)
        {
            WebSocketServer server = new WebSocketServer("ws://0.0.0.0:8080");


			SetHome();

			server.Start(socket =>
			{
				socket.OnOpen = () =>
				{
					User newUser = new User() { Id = socket.ConnectionInfo.Id };
					userList.Add(newUser);
					if (allSockets.TryAdd(newUser, socket))
					{
						Console.WriteLine($"{socket.ConnectionInfo.Id} added");
						Console.WriteLine($"{allSockets.Count} connections");

						JObject res = new JObject();
						res["type"] = "position";
						res["data"] = new JObject();
						res["data"]["sender"] = "Server";
						res["data"]["position"] = screenX + "," + screenY;
						if (socket.IsAvailable)
							socket.Send(res.ToString());
					}
					else
						Console.WriteLine($"{socket.ConnectionInfo.Id} add failed");
				};

				socket.OnClose = () =>
				{
					if (allSockets.TryRemove(userList.Where(x => x.Id == socket.ConnectionInfo.Id).First(), out IWebSocketConnection removedSocket))
					{
						Console.WriteLine($"{removedSocket.ConnectionInfo.Id} removed");
						Console.WriteLine($"{allSockets.Count} connections");
					}
					else
						Console.WriteLine($"{socket.ConnectionInfo.Id} remove failed");
				};

				socket.OnMessage = message =>
				{
					JObject msg = JObject.Parse(message);

					if (msg["type"].ToString() == "username")
                    {
						userList.Where(x => x.Id == socket.ConnectionInfo.Id).First().Username = msg["data"].ToString();
                    }
					if (msg["type"].ToString() == "touch")
					{
						Touch();
					}
					if (msg["type"].ToString() == "setz")
                    {
						SetZ(msg["data"].ToObject<double>());
                    }
					if (msg["type"].ToString() == "jog")
					{
						Jog(msg["data"].ToObject<double>());
					}
					if (msg["type"].ToString() == "position")
					{
						string[] data = msg["data"].ToString().Split(',');
						double x = double.Parse(data[0]);
						double y = double.Parse(data[1]);
						if (Math.Sqrt(Math.Pow(screenX - x, 2) + Math.Pow(screenY - y, 2)) > 20)
						{
							SetScreenPos(x, y);

							string user = userList.Where(x => x.Id == socket.ConnectionInfo.Id).First().Username;
							JObject res = new JObject();
							res["type"] = "position";
							res["data"] = new JObject();
							res["data"]["sender"] = user;
							res["data"]["position"] = screenX + "," + screenY;

							foreach (var currentSocket in allSockets)
							{
								if (currentSocket.Value.ConnectionInfo.Id != socket.ConnectionInfo.Id && currentSocket.Value.IsAvailable)
									currentSocket.Value.Send(res.ToString());
							}
						}
					}
					if (msg["type"].ToString() == "disable")
					{
						DisableSteppers();
					}
					if (msg["type"].ToString() == "enable")
					{
						EnableSteppers();
					}
				};

				socket.OnError = exception =>
				{
					Console.WriteLine($"{socket.ConnectionInfo.Id} OnError() {exception.Message}");
				};
			});

			string latestData = "";
			var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
			var encParams = new EncoderParameters(1);
			encParams.Param[0] = new EncoderParameter(Encoder.Quality, 90L);
			while (true)
            {
					using (MemoryStream ms = new MemoryStream())
					{
						CaptureApplication("scrcpy").Save(ms, encoder, encParams);
						string data = Convert.ToBase64String(ms.ToArray());

						if (data != latestData)
						{
							JObject msg = new JObject();
							msg["type"] = "image";
							msg["data"] = data;
							string responseString = msg.ToString();
							foreach (var socket in allSockets)
							{
								if (socket.Value.IsAvailable)
									socket.Value.Send(responseString);
							}
						}
						latestData = data;
					}
			}
		}

        private static void SetHome()
        {
			var client = new RestClient("http://octopi.local/api/printer/command");
			var request = new RestRequest(Method.POST);
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("X-Api-Key", "B07CA976D66F4D0AB5527C82F79E21DD");
			request.AddParameter("application/json", "{\n\t\"command\": \"G92 X137.0 Y358.0 Z21.0\"\n}", ParameterType.RequestBody);
			IRestResponse response = client.Execute(request);
		}

        private static void Touch()
        {
			var client = new RestClient("http://octopi.local/api/printer/command");
			var request = new RestRequest(Method.POST);
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("X-Api-Key", "B07CA976D66F4D0AB5527C82F79E21DD");
			request.AddParameter("application/json", "{\n\t\"commands\": [\n\t\t\"G91\",\n\t\t\"G1 Z5.0 F1200\",\n\t\t\"G1 Z-5.0 F1200\",\n\t\t\"G1 Z5.0 F1200\",\n\t\t\"G1 Z-5.0 F1200\",\n\t\t\"G90\"\n\t]\n}", ParameterType.RequestBody);
			IRestResponse response = client.Execute(request);
		}

        public static Bitmap CaptureApplication(string procName)
		{
			var proc = Process.GetProcessesByName(procName)[0];
			var rect = new User32.Rect();
			User32.GetWindowRect(proc.MainWindowHandle, ref rect);

			int width = rect.right - rect.left;
			int height = rect.bottom - rect.top;

			var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
			using (Graphics graphics = Graphics.FromImage(bmp))
			{
				graphics.CopyFromScreen(rect.left, rect.top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
			}

			Bitmap final = new Bitmap(422, 912);
			using (Graphics graphics = Graphics.FromImage(final))
			{
				graphics.DrawImage(bmp, new Rectangle(0, 0, 422, 912), new Rectangle(8, 31, 422, 912), GraphicsUnit.Pixel);
			}

			return final;
		}

		private class User32
		{
			[StructLayout(LayoutKind.Sequential)]
			public struct Rect
			{
				public int left;
				public int top;
				public int right;
				public int bottom;
			}

			[DllImport("user32.dll")]
			public static extern IntPtr GetWindowRect(IntPtr hWnd, ref Rect rect);
		}

		static void Jog(double amount)
        {
			var client = new RestClient("http://octopi.local/api/printer/printhead");
			var request = new RestRequest(Method.POST);
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("X-Api-Key", "B07CA976D66F4D0AB5527C82F79E21DD");
			request.AddParameter("application/json", "{\n  \"command\": \"jog\",\n  \"z\": " + amount + ",\n\t\"speed\": 1200\n}", ParameterType.RequestBody);
			IRestResponse response = client.Execute(request);
		}

		static void Home()
		{
			var client = new RestClient("http://192.168.1.127/api/printer/command");
			var request = new RestRequest(Method.POST);
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("X-Api-Key", "B07CA976D66F4D0AB5527C82F79E21DD");
			request.AddParameter("application/json", "{\n\t\"command\": \"G28\"\n}", ParameterType.RequestBody);
			IRestResponse response = client.Execute(request);
		}

		static void SetZ(double pos)
        {
			var client = new RestClient("http://192.168.1.127/api/printer/command");
			var request = new RestRequest(Method.POST);
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("X-Api-Key", "B07CA976D66F4D0AB5527C82F79E21DD");
			request.AddParameter("application/json", "{\n\t\"command\": \"G1 Z" + pos+ "\"\n}", ParameterType.RequestBody);
			IRestResponse response = client.Execute(request);
		}

		static void EnableSteppers()
		{
			var client = new RestClient("http://octopi.local/api/printer/command");
			var request = new RestRequest(Method.POST);
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("X-Api-Key", "B07CA976D66F4D0AB5527C82F79E21DD");
			request.AddParameter("application/json", "{\n\t\"command\": \"M17 X Y Z\"\n}", ParameterType.RequestBody);
			IRestResponse response = client.Execute(request);
		}

		static void DisableSteppers()
		{
			var client = new RestClient("http://octopi.local/api/printer/command");
			var request = new RestRequest(Method.POST);
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("X-Api-Key", "B07CA976D66F4D0AB5527C82F79E21DD");
			request.AddParameter("application/json", "{\n\t\"command\": \"M18\"\n}", ParameterType.RequestBody);
			IRestResponse response = client.Execute(request);
		}

		static void SetPos(double x, double y)
		{
			var client = new RestClient("http://192.168.1.127/api/printer/command");
			var request = new RestRequest(Method.POST);
			request.AddHeader("Content-Type", "application/json");
			request.AddHeader("X-Api-Key", "B07CA976D66F4D0AB5527C82F79E21DD");
			request.AddParameter("application/json", "{\n\t\"command\": \"G1 X" + x + " Y" + y + " F12000\"\n}", ParameterType.RequestBody);
			IRestResponse response = client.Execute(request);
		}
		static void SetScreenPos(double x, double y)
        {
			x = x - 20;
			if (y < 0)
				y = 0;
			if (y > 912)
				y = 912;
			if (x < 0)
				x = 0;
			if (x > 422)
				x = 422;

			try
			{
				screenX = x;
				screenY = y;
			}
			catch { }
			SetPos(100.0 + (x * 0.165), 280.0 - (y * 0.165));
        }
	}

	class User
    {
		public Guid Id { get; set; }
		public string Username { get; set; }
	}
}
