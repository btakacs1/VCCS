using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace VCCS
{
	static class ClientReader
	{
		private static Dictionary<byte, Action<byte[], int>> netMsgHandlers = new Dictionary<byte, Action<byte[], int>>()
		{
			{ 0x90, ReadConnect },
			{ 0x91, ReadDisconnect },
			{ 0xA0, ReadRing },
			{ 0xA1, ReadStartCall },
			{ 0xA2, ReadEndCall },
		};

		public static void ReadData(byte[] data, int length)
		{
			Console.WriteLine("Received message");
			byte header = data[0];
			if (netMsgHandlers.ContainsKey(header))
			{
				// Call the message handler from the main thread
				Client.Dispatcher.Invoke(() =>
				{
					netMsgHandlers[header](data, length);
				});
			}
			else
			{
				MessageBox.Show($"[ERROR] Failed to match network message header {header.ToString()} to a function");
			}
		}

		public static void ReadConnect(byte[] data, int length)
		{
			string callsign = Client.StringEncoding.GetString(data, 1, length - 1);
			Client.ControllerConnected(callsign);
			Console.WriteLine("Received connect from " + callsign);
		}

		public static void ReadDisconnect(byte[] data, int length)
		{
			string callsign = Client.StringEncoding.GetString(data, 1, length - 1);
			Client.ControllerDisconnected(callsign);
			Console.WriteLine("Received disconnect from " + callsign);
		}

		public static void ReadRing(byte[] data, int length)
		{
			string callsign = Client.StringEncoding.GetString(data, 1, length - 1);
			Client.AddIncomingCall(callsign);
			Console.WriteLine("Received ring from " + callsign);
		}

		public static void ReadStartCall(byte[] data, int length)
		{
			string callsign = Client.StringEncoding.GetString(data, 1, length - 1);
			Client.StartCall(callsign);
			Console.WriteLine("Started call with " + callsign);
		}

		public static void ReadEndCall(byte[] data, int length)
		{
			Client.EndCall();
			Console.WriteLine("Ended call");
		}
	}
}