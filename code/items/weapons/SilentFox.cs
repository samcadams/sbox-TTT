using System;
using Sandbox;

using SWB_Base;
using TTT.Player;
using TTT.Roles;

namespace TTT.Items
{
	[Library( "ttt_weapon_silentfox", Title = "Silent Fox" )]
	[Shop( SlotType.Primary, 100, new Type[] { typeof( TraitorRole ) } )]
	[Precached( "models/weapons/v_mp7-silenced.vmdl", "models/weapons/w_mp7-silenced.vmdl" )]
	[Hammer.EditorModel( "models/weapons/w_mp7-silenced.vmdl" )]
	public class SilentFox : MP7
	{
		public override string TextOnTick => WeaponGenerics.PickupText( _data.Library.Title );
		public override ItemData GetItemData() { return _data; }
		private readonly ItemData _data = new( typeof( SilentFox ) );

		public override string ViewModelPath => "models/weapons/v_mp7-silenced.vmdl";
		public override string WorldModelPath => "models/weapons/w_mp7-silenced.vmdl";

		public SilentFox() : base()
		{
			Primary = new ClipInfo
			{
				Ammo = 25,
				AmmoType = AmmoType.SMG,
				ClipSize = 25,

				BulletSize = 4f,
				Damage = 20f,
				Force = 3f,
				Spread = 0.08f,
				Recoil = 0.35f,
				RPM = 600,
				FiringType = FiringType.auto,
				ScreenShake = new ScreenShake
				{
					Length = 0.4f,
					Speed = 3.0f,
					Size = 0.35f,
					Rotation = 0.35f
				},

				DryFireSound = "dryfire_pistol-1",
				ShootSound = "mp7_fire_silenced-1",

				BulletEjectParticle = "particles/pistol_ejectbrass.vpcf",
				MuzzleFlashParticle = "particles/swb/muzzle/flash_medium.vpcf",

				InfiniteAmmo = 0
			};
		}
	}
}

