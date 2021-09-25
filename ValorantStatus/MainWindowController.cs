using DiscordRPC;
using DiscordRPC.Message;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ValAPINet;
using ValorantRPC;
using static ValAPINet.UserPresence;
using static ValorantStatus.Constants;

namespace ValorantStatus {
	internal class MainWindowController : IDisposable {
		private const string GameProcessName = "VALORANT-Win64-Shipping";

		private static string RiotPath => Path.Combine("Riot Client", "RiotClientServices.exe");
		private static string RiotClientSettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "\\Riot Games\\Metadata\\valorant.live\\valorant.live.product_settings.yaml");
		
		private DiscordRpcClient RpcClient;
		private Auth Authentication;
		private Process Process;
		private readonly MainWindow MainWindow;
		private readonly CancellationTokenSource LoopTokenSource;
		private readonly SemaphoreSlim LoopSync;
		private readonly Thread LoopThread;
		private string CurrentMapName;
		private string CurrentGameMode;
		private CurrentStatus LastUserStatus = CurrentStatus.Unknown;

		private static TextInfo CustomTextInfo => new CultureInfo("en-US", false).TextInfo;

		public MainWindowController(MainWindow window) {
			MainWindow = window;
			LoopSync = new SemaphoreSlim(1, 1);
			LoopTokenSource = new CancellationTokenSource();

			ThreadStart threadStart = new(async () => await PresenceLoop().ConfigureAwait(false));
			LoopThread = new Thread(threadStart) {
				IsBackground = true,
				Name = "PresenceLoop"
			};

			window.Loaded += OnLoaded;
			window.Dispatcher.UnhandledException += OnUnhandledExceptionOccured;
		}

		private async void OnLoaded(object sender, EventArgs e) => await InitAsync().ConfigureAwait(false);

		private void OnUnhandledExceptionOccured(object sender, DispatcherUnhandledExceptionEventArgs e) {
			if (e.Exception == null) {
				Application.Current.Shutdown();
				return;
			}

			string errorMessage = $"An application error occurred. If this error occurs again there seems to be a serious bug in the application, and you better close it.\n\nError:{e.Exception.Message}\n\nDo you want to continue?\n(if you click Yes you will continue with your work, if you click No the application will close)";

			if (MessageBox.Show(errorMessage, "Application User Interface Error", MessageBoxButton.YesNoCancel, MessageBoxImage.Error) == MessageBoxResult.No) {
				if (MessageBox.Show("WARNING: The application will close. Any changes will not be saved!\nDo you really want to close it?", "Close the application!", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) == MessageBoxResult.Yes) {
					Application.Current.Shutdown();
				}
			}

			e.Handled = true;
		}

		private void StartValorantProcess(string settingsPath) {
			if (string.IsNullOrEmpty(settingsPath)) {
				return;
			}

			Process = new Process();
			Process.StartInfo.FileName = Path.Combine(settingsPath, RiotPath);
			Process.StartInfo.Arguments = "--launch-product=valorant --launch-patchline=live";
			Process.Start();
		}

		private static async Task<string> GetRiotClientPath() {
			try {
				string settings = await File.ReadAllTextAsync(RiotClientSettingsPath).ConfigureAwait(false);
				return settings.Split("product_install_root: \"")[1].Split("\"")[0];
			}
			catch {
				// add default path for exception handling
				return "";
			}
		}

		private void InitDiscordRpcClient() {
			RpcClient = new DiscordRpcClient(ConfigurationManager.AppSettings.Get("DiscordKey")) {
				SkipIdenticalPresence = true
			};

			RpcClient.RegisterUriScheme();
			RpcClient.OnJoin += RpcOnJoin;
			RpcClient.OnJoinRequested += RpcOnJoinRequested;
			RpcClient.Initialize();
			RpcClient.SetPresence(new RichPresence() {
				Details = "Logging into Valorant",
				State = $"Using ValorantStatus",
				Assets = new Assets() {
					LargeImageKey = "logo"
				},
				Buttons = new Button[] { new Button() { Label = "ValorantStatus", Url = "https://github.com/brianbaldner/ValorantStatus" } }
			});
		}

		private void HandleWhileUserInMenu(Presence presence) {
			LastUserStatus = CurrentStatus.InMenu;

			//If game closed, stop the program					
			if (!IsGameRunning()) {
				LoopTokenSource.Cancel();
				return;
			}

			if (!RpcClient.IsInitialized || presence == null) {
				return;
			}

			switch (presence.privinfo.partyAccessibility) {
				case "OPEN":
					switch (presence.privinfo.partyState) {
						case "MATCHMAKING":
							//If looking for match
							RpcClient.SetPresence(new RichPresence() {
								Details = "Menus",
								State = $"Searching ({CustomTextInfo.ToTitleCase(presence.privinfo.queueId)})",
								Assets = new Assets() {
									LargeImageKey = "logo"
								},
								Party = new Party {
									ID = presence.privinfo.partyId,
									Max = presence.privinfo.maxPartySize,
									Privacy = Party.PrivacySetting.Public,
									Size = presence.privinfo.partySize
								},
								Secrets = new Secrets() {
									JoinSecret = Crypto.Encrypt(presence.privinfo.partyId),
									SpectateSecret = "1dfdfgdfgsfdgdfgsdf"
								}
							});
							break;
						default:
							//If not waiting in match
							RpcClient.SetPresence(new RichPresence() {
								Details = "Menus",
								State = $"Waiting ({CustomTextInfo.ToTitleCase(presence.privinfo.queueId)})",
								Assets = new Assets() {
									LargeImageKey = "logo"
								},
								Party = new Party {
									ID = presence.privinfo.partyId,
									Max = presence.privinfo.maxPartySize,
									Privacy = Party.PrivacySetting.Private,
									Size = presence.privinfo.partySize
								},
								Secrets = new Secrets() {
									JoinSecret = Crypto.Encrypt(presence.privinfo.partyId),
									SpectateSecret = "1dfdfgdfgsfdgdfgsdf"
								}
							});
							break;
					}
					break;

			}

		}

		private void HandleIfUserInRange(Presence presence) {
			if (presence.privinfo.provisioningFlow != "ShootingRange") {
				return;
			}

			LastUserStatus = CurrentStatus.InRange;
			CurrentGameMode = "Shooting Range";
			CurrentMapName = "/Game/Maps/Poveglia/Range";
		}

		private async Task PresenceLoop() {
			Authentication = Websocket.GetAuthLocal();

			do {
				await LoopSync.WaitAsync().ConfigureAwait(false);
				try {
					Presence presence = GetPresence(Authentication.subject);
					RpcClient.Invoke();

					if (presence != null) {
						if (!string.IsNullOrEmpty(presence.privinfo.matchMap)) {
							CurrentMapName = presence.privinfo.matchMap;
						}

						CurrentGameMode = presence.privinfo.queueId;
					}

					if (presence == null || presence.privinfo.sessionLoopState == "MENUS") {
						HandleWhileUserInMenu(presence);
						continue;
					}

					HandleIfUserInRange(presence);

					//One size fits all in game presence
					RpcClient.SetPresence(new RichPresence() {
						Details = $"Playing {CustomTextInfo.ToTitleCase(CurrentGameMode)} on {GetMapName(CurrentMapName)}",
						State = $"{presence.privinfo.partyOwnerMatchScoreAllyTeam}-{presence.privinfo.partyOwnerMatchScoreEnemyTeam}",
						Assets = new Assets() {
							LargeImageKey = GetMapName(CurrentMapName).ToLower().Replace(" ", "_"),
							LargeImageText = GetMapName(CurrentMapName)
						},
						Party = new Party {
							ID = presence.privinfo.partyId,
							Max = presence.privinfo.maxPartySize,
							Privacy = Party.PrivacySetting.Private,
							Size = presence.privinfo.partySize
						},
						Timestamps = new Timestamps() {
							Start = null
						},
						Secrets = new Secrets() {
							JoinSecret = null,
							SpectateSecret = null
						}
					});
				}
				catch (Exception e) {
					Debug.WriteLine(e);
				}
				finally {
					switch (LastUserStatus) {
						case CurrentStatus.InMatch:
							await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
							break;
						case CurrentStatus.InMenu:
						case CurrentStatus.InRange:
						case CurrentStatus.Unknown:
						default:
							await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
							break;
					}


					LoopSync.Release();
				}

			} while (!LoopTokenSource.IsCancellationRequested);
		}

		private async Task InitAsync() {

			MainWindow.Hide();
			StartValorantProcess(await GetRiotClientPath().ConfigureAwait(false));
			InitDiscordRpcClient();
			await WaitForGameStartAsync().ConfigureAwait(false);
			LoopThread.Start();
		}

		private bool IsGameRunning() => Process.GetProcessesByName(GameProcessName)?.Length > 0;

		private async Task WaitForGameStartAsync() => _ = await GetGameProcessWhenStartedAsync().ConfigureAwait(false);

		private static async Task<Process> GetGameProcessWhenStartedAsync() {
			while (true) {
				Process[] process = Process.GetProcessesByName(GameProcessName);

				if (process == null || process.Length == 0) {
					await Task.Delay(TimeSpan.FromSeconds(3));
					continue;
				}

				break;
			}

			return Process.GetProcessesByName(GameProcessName).First();
		}

		private void RpcOnJoinRequested(object sender, JoinRequestMessage args) {
			DiscordRpcClient client = sender as DiscordRpcClient;
			MessageBoxResult result = MessageBox.Show($"{args.User.Username} would like to join your party.", "ValorantStatus", MessageBoxButton.YesNo);
			switch (result) {
				case MessageBoxResult.Yes:
					client.Respond(args, true);
					break;
				case MessageBoxResult.No:
					client.Respond(args, false);
					break;
			}
		}

		private async void RpcOnJoin(object sender, JoinMessage args) {
			string partyid = Crypto.Decrypt(args.Secret);

			using (HttpClient client = new HttpClient()) {
				using (HttpRequestMessage request = new(HttpMethod.Post, $"https://glz-{Authentication.region}-1.{Authentication.region}.a.pvp.net/parties/v1/players/{Authentication.subject}/joinparty/{partyid}")) {
					request.Headers.Add("Authorization", $"Bearer {Authentication.AccessToken}");
					request.Headers.Add("X-Riot-Entitlements-JWT", Authentication.EntitlementToken);
					request.Headers.Add("X-Riot-ClientPlatform", "ew0KCSJwbGF0Zm9ybVR5cGUiOiAiUEMiLA0KCSJwbGF0Zm9ybU9TIjogIldpbmRvd3MiLA0KCSJwbGF0Zm9ybU9TVmVyc2lvbiI6ICIxMC4wLjE5MDQyLjEuMjU2LjY0Yml0IiwNCgkicGxhdGZvcm1DaGlwc2V0IjogIlVua25vd24iDQp9");
					request.Headers.Add("X-Riot-ClientVersion", Authentication.version);

					using (HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false)) {
						if (response.StatusCode != HttpStatusCode.OK) {
							// Log error
							Debug.WriteLine($"{nameof(RpcOnJoin)} Request failed.");
						}
					}
				}
			}
		}

		public void Dispose() {
			LoopTokenSource?.Cancel();
			Process?.Close();
			Process?.Dispose();
			RpcClient.Deinitialize();
			RpcClient?.Dispose();
			MainWindow.icon.Visibility = Visibility.Collapsed;
		}
	}
}
