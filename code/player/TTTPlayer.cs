using Sandbox;

using TTT.Events;
using TTT.Player.Camera;
using TTT.Roles;

namespace TTT.Player
{
	public partial class TTTPlayer : SWB_Base.PlayerBase
	{
		private static int CarriableDropVelocity { get; set; } = 300;

		[Net, Local]
		public int Credits { get; set; } = 0;

		[Net]
		public bool IsForcedSpectator { get; set; } = false;

		// Handled by Disguiser.cs perk.
		[Net]
		public bool IsDisguised { get; set; } = false;

		public bool IsInitialSpawning { get; set; } = false;

		public new Inventory Inventory
		{
			get => (Inventory)base.Inventory;
			private init => base.Inventory = value;
		}

		public new DefaultWalkController Controller
		{
			get => (DefaultWalkController)base.Controller;
			private set => base.Controller = value;
		}

		private DamageInfo _lastDamageInfo;

		public TTTPlayer()
		{
			Inventory = new Inventory( this );
		}

		// Important: Server-side only
		public void InitialSpawn()
		{
			bool isPostRound = Gamemode.Game.Instance.Round is Rounds.PostRound;

			IsInitialSpawning = true;
			IsForcedSpectator = isPostRound || Gamemode.Game.Instance.Round is Rounds.InProgressRound;

			Respawn();

			// sync roles
			using ( Prediction.Off() )
			{
				foreach ( TTTPlayer player in Utils.GetPlayers() )
				{
					if ( isPostRound || player.IsConfirmed )
					{
						player.SendClientRole( To.Single( this ) );
					}
				}

				Client.SetValue( "forcedspectator", IsForcedSpectator );

				Event.Run( TTTEvent.Player.InitialSpawn, Client );

				ClientInitialSpawn();
			}

			IsInitialSpawning = false;
			IsForcedSpectator = false;
		}

		public override void Respawn()
		{
			SetModel( "models/citizen/citizen.vmdl" );

			Animator = new StandardPlayerAnimator();

			EnableHideInFirstPerson = true;
			EnableShadowInFirstPerson = true;
			EnableDrawing = true;

			Credits = 0;

			SetRole( new NoneRole() );

			IsMissingInAction = false;

			using ( Prediction.Off() )
			{
				Event.Run( TTTEvent.Player.Spawned, this );

				RPCs.ClientOnPlayerSpawned( this );
				SendClientRole();
			}

			base.Respawn();

			if ( !IsForcedSpectator )
			{
				Controller = new DefaultWalkController();
				Camera = new FirstPersonCamera();

				EnableAllCollisions = true;
				EnableDrawing = true;
			}
			else
			{
				MakeSpectator( false );
			}

			// RemovePlayerCorpse();
			Inventory.DeleteContents();
			Gamemode.Game.Instance.Round.OnPlayerSpawn( this );

			switch ( Gamemode.Game.Instance.Round )
			{
				// hacky
				// TODO use a spectator flag, otherwise, no player can respawn during round with an item etc.
				// TODO spawn player as spectator instantly
				case Rounds.PreRound:
					IsConfirmed = false;
					CorpseConfirmer = null;

					Client.SetValue( "forcedspectator", false );

					break;
			}
		}

		public override void OnKilled()
		{
			base.OnKilled();

			BecomePlayerCorpseOnServer( _lastDamageInfo.Force, GetHitboxBone( _lastDamageInfo.HitboxIndex ) );

			Inventory.DropAll();
			Inventory.DeleteContents();

			ShowFlashlight( false, false );

			IsMissingInAction = true;

			using ( Prediction.Off() )
			{
				RPCs.ClientOnPlayerDied( this );

				if ( Gamemode.Game.Instance.Round is Rounds.InProgressRound )
				{
					SyncMIA();
				}
				else if ( Gamemode.Game.Instance.Round is Rounds.PostRound && PlayerCorpse != null && !PlayerCorpse.IsIdentified )
				{
					PlayerCorpse.IsIdentified = true;

					RPCs.ClientConfirmPlayer( null, PlayerCorpse, this, PlayerCorpse.DeadPlayerClientData.Name, PlayerCorpse.DeadPlayerClientData.PlayerId, Role.Name, Team.Name, PlayerCorpse.GetConfirmationData(), PlayerCorpse.KillerWeapon, PlayerCorpse.Perks );
				}
			}
		}

		public override void Simulate( Client client )
		{
			if ( IsClient )
			{
				TickPlayerVoiceChat();
			}
			else
			{
				TickAFKSystem();
			}

			TickEntityHints();

			if ( LifeState != LifeState.Alive )
			{
				TickPlayerChangeSpectateCamera();

				return;
			}

			// Input requested a carriable entity switch
			if ( Input.ActiveChild != null )
			{
				ActiveChild = Input.ActiveChild;
			}

			SimulateActiveChild( client, ActiveChild );

			TickItemSimulate();
			TickPlayerUse();
			TickPlayerDropCarriable();
			TickPlayerFlashlight();
			TickPlayerShop();
			TickLogicButtonActivate();

			PawnController controller = GetActiveController();
			controller?.Simulate( client, this, GetActiveAnimator() );
		}

		protected override void UseFail()
		{
			// Do nothing. By default this plays a sound that we don't want.
		}

		public override void StartTouch( Entity other )
		{
			if ( IsClient )
			{
				return;
			}

			if ( other is PickupTrigger )
			{
				StartTouch( other.Parent );
			}
		}

		private void TickPlayerDropCarriable()
		{
			if ( Input.Pressed( InputButton.Drop ) && !Input.Down( InputButton.Run ) && ActiveChild != null && Inventory != null )
			{
				Entity droppedEntity = Inventory.DropActive();

				if ( droppedEntity != null )
				{
					if ( droppedEntity.PhysicsGroup != null )
					{
						droppedEntity.PhysicsGroup.Velocity = Velocity + (EyeRot.Forward + EyeRot.Up) * CarriableDropVelocity;
					}
				}
			}
		}

		private void TickPlayerChangeSpectateCamera()
		{
			if ( !Input.Pressed( InputButton.Jump ) || !IsServer )
			{
				return;
			}

			using ( Prediction.Off() )
			{
				Camera = Camera switch
				{
					RagdollSpectateCamera => new FreeSpectateCamera(),
					FreeSpectateCamera => new ThirdPersonSpectateCamera(),
					ThirdPersonSpectateCamera => new FirstPersonSpectatorCamera(),
					FirstPersonSpectatorCamera => new FreeSpectateCamera(),
					_ => Camera
				};
			}
		}

		private void TickItemSimulate()
		{
			if ( Client == null )
			{
				return;
			}

			PerksInventory perks = Inventory.Perks;

			for ( int i = 0; i < perks.Count(); i++ )
			{
				perks.Get( i ).Simulate( Client );
			}
		}

		protected override void OnDestroy()
		{
			RemovePlayerCorpse();

			base.OnDestroy();
		}
	}
}