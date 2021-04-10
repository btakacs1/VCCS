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
using System.Windows.Shapes;
using AdonisUI.Controls;

namespace VCCS
{
	/// <summary>
	/// Interaction logic for ConnectDialog.xaml
	/// </summary>
	public partial class ConnectDialog : AdonisWindow
	{
		public ConnectDialog()
		{
			InitializeComponent();
			CallsignField.Focus();
		}

		private void Connect()
		{
			string callsign = CallsignField.Text;
			if (Client.CallsignAliases.ContainsKey(callsign))
			{
				callsign = Client.CallsignAliases[callsign];
			}
			Client.Connect(callsign);
			Close();
		}

		private void ConnectButton_Click(object sender, RoutedEventArgs e)
		{
			Connect();
		}

		private void CallsignField_TextChanged(object sender, TextChangedEventArgs e)
		{
			int c = CallsignField.CaretIndex;
			CallsignField.Text = CallsignField.Text.ToUpper().Replace(" ", "_").Replace("-", "_");
			CallsignField.CaretIndex = c;
		}

		private void CallsignField_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				Connect();
			}
		}
	}
}
