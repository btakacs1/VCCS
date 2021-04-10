using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;
using System.Windows.Threading;
using System.Net;

namespace VCCS
{
	static class Client
	{
		private static string callsign = "ERR";
		private static TcpClient tcpClient;
		private static UdpClient udpClient;
		private static string serverIP;
		
		private static IPEndPoint addressUDP = new IPEndPoint(IPAddress.Any, 25596);

		private static bool isSendingCall = false;
		private static string sendingCallTo = "";
		private static bool isInCall = false;
		private static string inCallWith = "";
		private static List<string> incomingCalls = new List<string>();

		public static Dispatcher Dispatcher { get; private set; }
		public static string Callsign { get => callsign; }
		public static bool RingOff { get; set; }
		public static bool Mute { get; set; }
		public static bool IsInCall { get => isInCall; }
		public static string InCallWith { get => inCallWith; }

		public static Encoding StringEncoding = Encoding.ASCII;

		public static event Action OnConnectionSucceeded;
		public static event Action OnConnectionFailed;
		public static event Action<string> OnControllerConnected;
		public static event Action<string> OnControllerDisconnected;
		public static event Action<string> OnIncomingCallAdded;
		public static event Action<string> OnIncomingCallRemoved;
		public static event Action<string> OnCallStarted;
		public static event Action<string> OnCallEnded;
		public static event Action<byte[]> OnVoiceReceived;

		public static Dictionary<string, string> CallsignAliases = new Dictionary<string, string>
		{
			{ "CYYZ_TWR", "CYYZ_N_TWR" }, { "CYYZ_DEP", "CYYZ_S_DEP" }, { "CYYZ_GND", "CYYZ_N_GND" },
			{ "CYYZ_APP", "CYYZ_1_APP" }, { "TOR_CTR", "TOR_CE_CTR" }
		};

		public static void Connect()
		{
			Dispatcher = Dispatcher.CurrentDispatcher;
			tcpClient = new TcpClient();
			serverIP = File.ReadAllText("ServerIP.txt");
			try
			{
				tcpClient.Connect(serverIP, 25596);
			}
			catch (SocketException)
			{
				OnConnectionFailed?.Invoke();
				return;
			}
			callsign =
#if DEBUG
				"CYYZ_S_DEP";
#else
				"CYYZ_N_TWR";
#endif
			SendNetworkMessage(new NetworkMessage_Callsign(NetworkMessage.HeaderConnect, callsign));

			OnConnectionSucceeded?.Invoke();

			udpClient = new UdpClient();
			udpClient.Connect(serverIP, 25596);

			ReadNetworkStream();
			ReadNetworkStreamUDP();
		}

		public static void ControllerConnected(string callsign)
		{
			OnControllerConnected?.Invoke(callsign);
			if (callsign == Callsign)
			{
				// We connected

				// Register with UDP so that the server which callsign belongs to this address
				// TODO: Ensure that this message will always be received correctly by the server
				byte[] data = new byte[callsign.Length + 3];
				data[0] = data[1] = data[2] = 100;
				for (int i = 0; i < callsign.Length; i++)
				{
					data[i + 3] = (byte)callsign[i];
				}
				udpClient.Send(data, callsign.Length + 3);
			}
		}

		public static void ControllerDisconnected(string callsign)
		{
			OnControllerDisconnected?.Invoke(callsign);
		}

		public static void AddIncomingCall(string callsign)
		{
			incomingCalls.Add(callsign);
			OnIncomingCallAdded?.Invoke(callsign);
		}

		public static void RemoveIncomingCall(string callsign)
		{
			incomingCalls.Remove(callsign);
			OnIncomingCallRemoved?.Invoke(callsign);
		}

		public static void StartCall(string callsign)
		{
			isInCall = true;
			inCallWith = callsign;
			isSendingCall = false;
			OnCallStarted?.Invoke(callsign);
		}

		public static void EndCall()
		{
			if (!isInCall)
				return;
			isInCall = false;
			OnCallEnded?.Invoke(inCallWith);
			inCallWith = "";
			isSendingCall = false;
			SendNetworkMessage(new NetworkMessage_HeaderOnly(NetworkMessage.HeaderEndCall));
		}

		public static void SendVoice(byte[] data, int length)
		{
			udpClient.Send(data, length);
		}

		public static void ReceiveVoice(byte[] data, int length)
		{
			OnVoiceReceived?.Invoke(data);
		}

		public static void ControllerButtonClicked(string callsign)
		{
			if (isInCall || callsign == Callsign)
				return;

			if (incomingCalls.Contains(callsign))
			{
				// Accept call
				Console.WriteLine("Accepting call");
				SendNetworkMessage(new NetworkMessage_Callsign(NetworkMessage.HeaderAcceptCall, callsign));
				StartCall(callsign);
				RemoveIncomingCall(callsign);
			}
			else if (!isSendingCall)
			{
				// Send call
				Console.WriteLine("Sending call");
				isSendingCall = true;
				sendingCallTo = callsign;
				SendNetworkMessage(new NetworkMessage_Callsign(NetworkMessage.HeaderSendRing, callsign));
			}
		}

		public static void SendNetworkMessage(NetworkMessage netMsg)
		{
			tcpClient.GetStream().Write(netMsg.Payload, 0, netMsg.Payload.Length);
		}

		private static void ReadNetworkStream()
		{
			byte[] buffer = new byte[4096];
			tcpClient.GetStream().BeginRead(buffer, 0, 4096, NetworkStreamDataReceived, buffer);
		}

		private static void ReadNetworkStreamUDP()
		{
			udpClient.BeginReceive(NetworkStreamDataReceivedUDP, null);
		}

		private static void NetworkStreamDataReceived(IAsyncResult r)
		{
			Console.WriteLine("Data received");
			byte[] buffer = (byte[])r.AsyncState;
			while (buffer[0] != 0x00)
			{
				// Count the length of the message by incrementing length until we reach a 0 byte
				int length = 0;
				while (buffer[++length] != 0x00) ;

				// Process data
				ClientReader.ReadData(buffer, length);

				// Erase data
				for (int i = 0; i < length; i++)
				{
					buffer[i] = 0x00;
				}

				// Check for additional messages after this
				if (buffer[length + 1] != 0x00)
				{
					// There is more
					for (int i = 0; length + 1 + i < 4096; i++)
					{
						// Shift it down to the start
						buffer[i] = buffer[length + 1 + i];
						buffer[length + 1 + i] = 0x00;
					}
				}
			}
			ReadNetworkStream();
		}

		private static void NetworkStreamDataReceivedUDP(IAsyncResult r)
		{
			byte[] buffer = udpClient.EndReceive(r, ref addressUDP);
			ReceiveVoice(buffer, buffer.Length);

			ReadNetworkStreamUDP();
		}
	}
}