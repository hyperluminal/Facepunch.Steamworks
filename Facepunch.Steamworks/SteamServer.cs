﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Steamworks.Data;

namespace Steamworks
{
	/// <summary>
	/// Provides the core of the Steam Game Servers API
	/// </summary>
	public static partial class SteamServer
	{
		static bool initialized;

		static ISteamGameServer _internal;
		internal static ISteamGameServer Internal
		{
			get
			{
				if ( _internal == null )
				{
					_internal = new ISteamGameServer( );
					_internal.InitServer();
				}

				return _internal;
			}
		}

		public static bool IsValid => initialized;


		public static Action<Exception> OnCallbackException;

		internal static void InstallEvents()
		{
			SteamInventory.InstallEvents();
            SteamNetworkingSockets.InstallEvents(true);

            ValidateAuthTicketResponse_t.Install( x => OnValidateAuthTicketResponse?.Invoke( x.SteamID, x.OwnerSteamID, x.AuthSessionResponse ), true );
			SteamServersConnected_t.Install( x => OnSteamServersConnected?.Invoke(), true );
			SteamServerConnectFailure_t.Install( x => OnSteamServerConnectFailure?.Invoke( x.Result, x.StillRetrying ), true );
			SteamServersDisconnected_t.Install( x => OnSteamServersDisconnected?.Invoke( x.Result ), true );
		}

		/// <summary>
		/// User has been authed or rejected
		/// </summary>
		public static event Action<SteamId, SteamId, AuthResponse> OnValidateAuthTicketResponse;

		/// <summary>
		/// Called when a connections to the Steam back-end has been established.
		/// This means the server now is logged on and has a working connection to the Steam master server.
		/// </summary>
		public static event Action OnSteamServersConnected;

		/// <summary>
		/// This will occur periodically if the Steam client is not connected, and has failed when retrying to establish a connection (result, stilltrying)
		/// </summary>
		public static event Action<Result, bool> OnSteamServerConnectFailure;

		/// <summary>
		/// Disconnected from Steam
		/// </summary>
		public static event Action<Result> OnSteamServersDisconnected;


		/// <summary>
		/// Initialize the steam server.
		/// If asyncCallbacks is false you need to call RunCallbacks manually every frame.
		/// </summary>
		public static void Init( AppId appid, SteamServerInit init, bool asyncCallbacks = true )
		{
			uint ipaddress = 0; // Any Port

			if ( init.SteamPort == 0 )
				init = init.WithRandomSteamPort();

			if ( init.IpAddress != null )
				ipaddress = Utility.IpToInt32( init.IpAddress );

			System.Environment.SetEnvironmentVariable( "SteamAppId", appid.ToString() );
			System.Environment.SetEnvironmentVariable( "SteamGameId", appid.ToString() );
			var secure = (int)(init.Secure ? 3 : 2);

			//
			// Get other interfaces
			//
			if ( !SteamInternal.GameServer_Init( ipaddress, init.SteamPort, init.GamePort, init.QueryPort, secure, init.VersionString ) )
			{
				throw new System.Exception( $"InitGameServer returned false ({ipaddress},{init.SteamPort},{init.GamePort},{init.QueryPort},{secure},\"{init.VersionString}\")" );
			}

			initialized = true;

			//
			// Initial settings
			//
			AutomaticHeartbeats = true;
			MaxPlayers = 32;
			BotCount = 0;
			Product = $"{appid.Value}";
			ModDir = init.ModDir;
			GameDescription = init.GameDescription;
			Passworded = false;
			DedicatedServer = init.DedicatedServer;

			InstallEvents();

			if ( asyncCallbacks )
			{
				RunCallbacksAsync();
			}
		}

		static List<SteamInterface> openIterfaces = new List<SteamInterface>();

		internal static void WatchInterface( SteamInterface steamInterface )
		{
			if ( openIterfaces.Contains( steamInterface ) )
				throw new System.Exception( "openIterfaces already contains interface!" );

			openIterfaces.Add( steamInterface );
		}

		internal static void ShutdownInterfaces()
		{
			foreach ( var e in openIterfaces )
			{
				e.Shutdown();
			}

			openIterfaces.Clear();
		}

		public static void Shutdown()
		{
			Event.DisposeAllServer();

			initialized = false;

			_internal = null;

			ShutdownInterfaces();
			SteamNetworkingUtils.Shutdown();
			SteamNetworkingSockets.Shutdown();
			SteamInventory.Shutdown();

			SteamGameServer.Shutdown();
		}

		internal static async void RunCallbacksAsync()
		{
			while ( IsValid )
			{
				try
				{
					RunCallbacks();
				}
				catch ( System.Exception e )
				{
					OnCallbackException?.Invoke( e );
				}

				await Task.Delay( 16 );
			}
		}

		/// <summary>
		/// Run the callbacks. This is also called in Async callbacks.
		/// </summary>
		public static void RunCallbacks()
		{
			SteamGameServer.RunCallbacks();
		}

		/// <summary>
		/// Sets whether this should be marked as a dedicated server.
		/// If not, it is assumed to be a listen server.
		/// </summary>
		public static bool DedicatedServer
		{
			get => _dedicatedServer;
			set { if ( _dedicatedServer == value ) return; Internal.SetDedicatedServer( value ); _dedicatedServer = value; }
		}
		private static bool _dedicatedServer;

		/// <summary>
		/// Gets or sets the current MaxPlayers. 
		/// This doesn't enforce any kind of limit, it just updates the master server.
		/// </summary>
		public static int MaxPlayers
		{
			get => _maxplayers;
			set { if ( _maxplayers == value ) return; Internal.SetMaxPlayerCount( value ); _maxplayers = value; }
		}
		private static int _maxplayers = 0;

		/// <summary>
		/// Gets or sets the current BotCount. 
		/// This doesn't enforce any kind of limit, it just updates the master server.
		/// </summary>
		public static int BotCount
		{
			get => _botcount;
			set { if ( _botcount == value ) return; Internal.SetBotPlayerCount( value ); _botcount = value; }
		}
		private static int _botcount = 0;

		/// <summary>
		/// Gets or sets the current Map Name. 
		/// </summary>
		public static string MapName
		{
			get => _mapname;
			set { if ( _mapname == value ) return; Internal.SetMapName( value ); _mapname = value; }
		}
		private static string _mapname;

		/// <summary>
		/// Gets or sets the current ModDir
		/// </summary>
		public static string ModDir
		{
			get => _modDir; 
			internal set { if ( _modDir == value ) return; Internal.SetModDir( value ); _modDir = value; }
		}
		private static string _modDir = "";

		/// <summary>
		/// Gets the current product
		/// </summary>
		public static string Product
		{
			get => _product;
			internal set { if ( _product == value ) return; Internal.SetProduct( value ); _product = value; }
		}
		private static string _product = "";

		/// <summary>
		/// Gets or sets the current Product
		/// </summary>
		public static string GameDescription
		{
			get => _gameDescription;
			internal set { if ( _gameDescription == value ) return; Internal.SetGameDescription( value ); _gameDescription = value; }
		}
		private static string _gameDescription = "";

		/// <summary>
		/// Gets or sets the current ServerName
		/// </summary>
		public static string ServerName
		{
			get => _serverName;
			set { if ( _serverName == value ) return; Internal.SetServerName( value ); _serverName = value; }
		}
		private static string _serverName = "";

		/// <summary>
		/// Set whether the server should report itself as passworded
		/// </summary>
		public static bool Passworded
		{
			get => _passworded;
			set { if ( _passworded == value ) return; Internal.SetPasswordProtected( value ); _passworded = value; }
		}
		private static bool _passworded;

		/// <summary>
		/// Gets or sets the current GameTags. This is a comma seperated list of tags for this server.
		/// When querying the server list you can filter by these tags.
		/// </summary>
		public static string GameTags
		{
			get => _gametags;
			set
			{
				if ( _gametags == value ) return;
				Internal.SetGameTags( value );
				_gametags = value;
			}
		}
		private static string _gametags = "";

		/// <summary>
		/// Log onto Steam anonymously.
		/// </summary>
		public static void LogOnAnonymous()
		{
			Internal.LogOnAnonymous();
			ForceHeartbeat();
		}

		/// <summary>
		/// Log onto Steam anonymously.
		/// </summary>
		public static void LogOff()
		{
			Internal.LogOff();
		}

		/// <summary>
		/// Returns true if the server is connected and registered with the Steam master server
		/// You should have called LogOnAnonymous etc on startup.
		/// </summary>
		public static bool LoggedOn => Internal.BLoggedOn();

		/// <summary>
		/// To the best of its ability this tries to get the server's
		/// current public ip address. Be aware that this is likely to return
		/// null for the first few seconds after initialization.
		/// </summary>
		public static System.Net.IPAddress PublicIp
		{
			get
			{
				var ip = Internal.GetPublicIP();
				if ( ip == 0 ) return null;

				return Utility.Int32ToIp( ip );
			}
		}

		/// <summary>
		/// Enable or disable heartbeats, which are sent regularly to the master server.
		/// Enabled by default.
		/// </summary>
		public static bool AutomaticHeartbeats
		{
			set { Internal.EnableHeartbeats( value ); }
		}

		/// <summary>
		/// Set heartbeat interval, if automatic heartbeats are enabled.
		/// You can leave this at the default.
		/// </summary>
		public static int AutomaticHeartbeatRate
		{
			set { Internal.SetHeartbeatInterval( value ); }
		}

		/// <summary>
		/// Force send a heartbeat to the master server instead of waiting
		/// for the next automatic update (if you've left them enabled)
		/// </summary>
		public static void ForceHeartbeat()
		{
			Internal.ForceHeartbeat();
		}

		/// <summary>
		/// Update this connected player's information. You should really call this
		/// any time a player's name or score changes. This keeps the information shown
		/// to server queries up to date.
		/// </summary>
		public static void UpdatePlayer( SteamId steamid, string name, int score )
		{
			Internal.BUpdateUserData( steamid, name, (uint)score );
		}

		static Dictionary<string, string> KeyValue = new Dictionary<string, string>();

		/// <summary>
		/// Sets a Key Value. These can be anything you like, and are accessible
		/// when querying servers from the server list.
		/// 
		/// Information describing gamemodes are common here.
		/// </summary>
		public static void SetKey( string Key, string Value )
		{
			if ( KeyValue.ContainsKey( Key ) )
			{
				if ( KeyValue[Key] == Value )
					return;

				KeyValue[Key] = Value;
			}
			else
			{
				KeyValue.Add( Key, Value );
			}

			Internal.SetKeyValue( Key, Value );
		}

		/// <summary>
		/// Remove all key values
		/// </summary>
		public static void ClearKeys()
		{
			KeyValue.Clear();
			Internal.ClearAllKeyValues();
		}

		/// <summary>
		/// Start authorizing a ticket. This user isn't authorized yet. Wait for a call to OnAuthChange.
		/// </summary>
		public static unsafe bool BeginAuthSession( byte[] data, SteamId steamid )
		{
			fixed ( byte* p = data )
			{
				var result = Internal.BeginAuthSession( (IntPtr)p, data.Length, steamid );

				if ( result == BeginAuthResult.OK )
					return true;

				return false;
			}
		}

		/// <summary>
		/// Forget this guy. They're no longer in the game.
		/// </summary>
		public static void EndSession( SteamId steamid )
		{
			Internal.EndAuthSession( steamid );
		}

		/// <summary>
		/// If true, Steam wants to send a packet. You should respond by sending
		/// this packet in an unconnected way to the returned Address and Port.
		/// </summary>
		/// <param name="packet">Packet to send. The Data passed is pooled - so use it immediately.</param>
		/// <returns>True if we want to send a packet</returns>
		public static unsafe bool GetOutgoingPacket( out OutgoingPacket packet )
		{
			var buffer = Helpers.TakeBuffer( 1024 * 32 );
			packet = new OutgoingPacket();

			fixed ( byte* ptr = buffer )
			{
				uint addr = 0;
				ushort port = 0;

				var size = Internal.GetNextOutgoingPacket( (IntPtr)ptr, buffer.Length, ref addr, ref port );
				if ( size == 0 )
					return false;

				packet.Size = size;
				packet.Data = buffer;
				packet.Address = addr;
				packet.Port = port;
				return true;
			}
		}

		/// <summary>
		/// We have received a server query on our game port. Pass it to Steam to handle.
		/// </summary>
		public static unsafe void HandleIncomingPacket( byte[] data, int size, uint address, ushort port )
		{
			fixed ( byte* ptr = data )
			{
				HandleIncomingPacket( (IntPtr)ptr, size, address, port );
			}
		}
		
		/// <summary>
		/// We have received a server query on our game port. Pass it to Steam to handle.
		/// </summary>
		public static unsafe void HandleIncomingPacket( IntPtr ptr, int size, uint address, ushort port )
		{
			Internal.HandleIncomingPacket( ptr, size, address, port );
		}
	}
}