using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using SpectesGaze.Content.Systems;

namespace SpectesGaze.Content.Items
{
	internal sealed class GrabbedWorldItem : GlobalItem
	{
		public override void OnSpawn(Item item, IEntitySource source) =>
			ModContent.GetInstance<DomainGrab>().ReleaseIfHolding(item);

		public override bool CanPickup(Item item, Player player) => !ModContent.GetInstance<DomainGrab>().Holds(item);

		public override bool CanStackInWorld(Item destination, Item source)
		{
			DomainGrab grab = ModContent.GetInstance<DomainGrab>();
			return !grab.Holds(destination) && !grab.Holds(source);
		}

		public override void Update(Item item, ref float gravity, ref float maxFallSpeed)
		{
			if (!ModContent.GetInstance<DomainGrab>().Holds(item)) return;

			gravity = 0f;
			item.velocity = Vector2.Zero;
		}
	}
}
