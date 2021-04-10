using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using AdonisUI.Controls;
using NAudio;
using NAudio.Wave;
using System.Windows.Threading;

namespace VCCS
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : AdonisWindow
	{
		private WaveIn sourceStream;
		private DirectSoundOut waveOut;
		private BufferedWaveProvider waveBuffer;
		private WaveStream waveStream;
		private WaveFormat waveFormat;

		private TcpClient tcpClient;

		private DispatcherTimer buttonFlashTimer = new DispatcherTimer();
		private bool buttonFlashState = false;
		private List<Button> flashingButtons = new List<Button>();
		private Brush defaultButtonBrush;

		public MainWindow()
		{
			InitializeComponent();
			
			//Console.WriteLine("Connecting");
			//Client.Connect();
			//Console.WriteLine("Connected");

			waveFormat = new WaveFormat(20000, WaveIn.GetCapabilities(0).Channels);

			sourceStream = new WaveIn();
			sourceStream.DeviceNumber = 0;
			sourceStream.BufferMilliseconds = 10;
			sourceStream.WaveFormat = waveFormat;
			sourceStream.DataAvailable += sourceStream_DataAvailable;

			waveBuffer = new BufferedWaveProvider(waveFormat);

			waveOut = new DirectSoundOut();
			waveOut.Init(waveBuffer);

			sourceStream.StartRecording();

			Client.OnVoiceReceived += (byte[] data) =>
			{
				//waveStream.Write(data, 0, data.Length);
				waveBuffer.AddSamples(data, 0, data.Length);
				waveOut.Play();
			};

			Client.OnConnected += () =>
			{
				Console.WriteLine("RECEIVED CONNECT");
				ConnectButton.Content = Client.Callsign;
				ConnectButton.SetResourceReference(BackgroundProperty, AdonisUI.Brushes.Layer1InteractionBrush);
			};

			Client.OnDisconnected += () =>
			{
				ConnectButton.Content = "CONNECT";
				ConnectButton.SetResourceReference(BackgroundProperty, AdonisUI.Brushes.Layer1BackgroundBrush);
				GetControllerButton(Client.Callsign).SetResourceReference(BackgroundProperty, AdonisUI.Brushes.Layer0BackgroundBrush);
				ForEachControllerButton((Button button) =>
				{
					button.IsEnabled = false;
					//button.SetResourceReference(BackgroundProperty, AdonisUI.Brushes.Layer0BackgroundBrush);
					return false;
				});
			};

			Client.OnControllerConnected += (string callsign) =>
			{
				GetControllerButton(callsign).IsEnabled = true;
				if (callsign == Client.Callsign)
					GetControllerButton(callsign).SetResourceReference(BackgroundProperty, AdonisUI.Brushes.Layer1HighlightBrush);
			};

			Client.OnControllerDisconnected += (string callsign) =>
			{
				GetControllerButton(callsign).IsEnabled = false;
			};

			ForEachControllerButton((Button button) =>
			{
				button.Click += (object sender, RoutedEventArgs e) =>
				{
					Client.ControllerButtonClicked(button.Name.Substring(2));
				};
				return false;
			});

			defaultButtonBrush = ButtonRingOff.Background;

			buttonFlashTimer.Interval = new TimeSpan(0, 0, 0, 0, 250);
			buttonFlashTimer.Tick += (object o, EventArgs e) =>
			{
				buttonFlashState = !buttonFlashState;
				foreach (Button button in flashingButtons)
				{
					if (buttonFlashState)
						button.Background = Brushes.Gray;
					else
						button.Background = defaultButtonBrush;
				}
			};
			buttonFlashTimer.Start();

			Client.OnIncomingCallAdded += (string callsign) =>
			{
				flashingButtons.Add(GetControllerButton(callsign));
				Console.WriteLine("Added flashing button " + flashingButtons[0].Name);
			};

			Client.OnIncomingCallRemoved += (string callsign) =>
			{
				flashingButtons.Remove(GetControllerButton(callsign));
			};

			Client.OnCallStarted += (string callsign) =>
			{
				ButtonEnd.IsEnabled = true;
				GetControllerButton(callsign).Background = Brushes.Gray;
			};

			Client.OnCallEnded += (string callsign) =>
			{
				ButtonEnd.IsEnabled = false;
				GetControllerButton(callsign).Background = defaultButtonBrush;
			};
		}

		private Button GetControllerButton(string callsign)
		{
			if (Client.CallsignAliases.ContainsKey(callsign))
			{
				callsign = Client.CallsignAliases[callsign];
			}

			Button result = null;
			ForEachControllerButton((Button button) =>
			{
				if (button.Name == "C_" + callsign)
				{
					result = button;
					return true;
				}
				return false;
			});
			return result;
		}

		private void ForEachControllerButton(Func<Button, bool> func)
		{
			foreach (UIElement stackPanelElement in StackVertical.Children)
			{
				StackPanel stackPanel = stackPanelElement as StackPanel;
				if (stackPanel != null)
				{
					foreach (UIElement element in stackPanel.Children)
					{
						if (element is Button b)
						{
							if (b.Name.StartsWith("C_"))
							{
								bool res = func.Invoke(b);
								if (res)
								{
									// Break out of the loop if true is returned
									return;
								}
							}
						}
					}
				}
			}
		}

		private void sourceStream_DataAvailable(object sender, WaveInEventArgs e)
		{
			if (Client.IsInCall)
			{
				Client.SendVoice(e.Buffer, e.BytesRecorded);
			}
		}

		private void ButtonEnd_Click(object sender, RoutedEventArgs e)
		{
			Client.EndCall();
		}

		private void ButtonRingOff_Click(object sender, RoutedEventArgs e)
		{
			Client.RingOff = !Client.RingOff;
			if (Client.RingOff)
				ButtonRingOff.SetResourceReference(BackgroundProperty, AdonisUI.Brushes.Layer1InteractionBrush);
			else
				ButtonRingOff.SetResourceReference(BackgroundProperty, AdonisUI.Brushes.Layer1BackgroundBrush);
		}

		private void ButtonMute_Click(object sender, RoutedEventArgs e)
		{
			Client.Mute = !Client.Mute;
			if (Client.Mute)
			{
				ButtonMute.SetResourceReference(BackgroundProperty, AdonisUI.Brushes.Layer1InteractionBrush);
				sourceStream.StopRecording();
				Console.WriteLine("Muted");
			}
			else
			{
				ButtonMute.SetResourceReference(BackgroundProperty, AdonisUI.Brushes.Layer1BackgroundBrush);
				sourceStream.StartRecording();
				Console.WriteLine("Unmuted");
			}
		}

		private void ConnectButton_Click(object sender, RoutedEventArgs e)
		{
			if (Client.IsConnected)
			{
				Client.Disconnect();
			}
			else
			{
				ConnectDialog connectDialog = new ConnectDialog();
				connectDialog.Show();
			}
		}
	}
}
