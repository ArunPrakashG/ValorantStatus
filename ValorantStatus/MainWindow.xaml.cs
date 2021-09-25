using DiscordRPC;
using RestSharp;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ValAPINet;
using ValorantStatus;

namespace ValorantRPC {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, IDisposable {
		private readonly MainWindowController Controller;

		public MainWindow() {
			InitializeComponent();
			icon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().ManifestModule.Name);
			QuitButton.Click += OnQuitButtonClicked;
			Controller = new MainWindowController(this);
		}

		private void OnQuitButtonClicked(object sender, RoutedEventArgs e) {
			Controller.Dispose();
			Environment.Exit(0);
		}

		public void Dispose() {
			Controller?.Dispose();
		}
	}
}
