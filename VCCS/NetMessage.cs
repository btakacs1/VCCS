using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VCCS
{
	abstract class NetworkMessage
	{
		public const byte HeaderConnect = 0x01;
		public const byte HeaderSendRing = 0x02;
		public const byte HeaderAcceptCall = 0x03;
		public const byte HeaderEndCall = 0x04;
		public const byte HeaderVoice = 0x20;

		private byte[] payload = null;
		private int index = 0;

		public byte Header { get; private set; }
		public byte[] Payload { get { return payload; } }

		public NetworkMessage(byte header)
		{
			Header = header;
		}

		protected void WriteBytes(byte[] bytes)
		{
			if (payload == null)
			{
				// No payload exists, so we need to write the header to the beginning
				// Size + 1 for header
				payload = new byte[bytes.Length + 1];
				payload[index++] = Header;
			}
			else
			{
				// Resize the payload to fit the new bytes
				Console.WriteLine("Resizing to " + payload.Length + bytes.Length);
				Array.Resize(ref payload, payload.Length + bytes.Length);
			}

			// Put the new bytes into the payload
			for (int i = 0; i < bytes.Length; i++)
			{
				payload[index++] = bytes[i];
			}
		}

		protected void WriteString(string s)
		{
			WriteBytes(Client.StringEncoding.GetBytes(s));
		}

		protected void End()
		{
			WriteBytes(new byte[] { 0 });
		}
	}

	class NetworkMessage_HeaderOnly : NetworkMessage
	{
		public NetworkMessage_HeaderOnly(byte header) : base(header)
		{
			End();
		}
	}

	class NetworkMessage_Callsign : NetworkMessage
	{
		public NetworkMessage_Callsign(byte header, string callsign) : base(header)
		{
			WriteString(callsign);
			End();
		}
	}

	class NetworkMessage_Voice : NetworkMessage
	{
		public NetworkMessage_Voice(byte[] data, int length) : base(HeaderVoice)
		{
			byte[] lengthBytes = BitConverter.GetBytes(length);
			//if (BitConverter.IsLittleEndian)
			//	Array.Reverse(lengthBytes);
			WriteBytes(lengthBytes);
			WriteBytes(data);
			End();
		}
	}
}