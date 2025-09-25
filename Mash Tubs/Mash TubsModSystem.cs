using Mash_Tubs.src.Behaviors;
using Mash_Tubs.src.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

namespace Mash_Tubs
{
    public class Mash_TubsModSystem : ModSystem
    {
        internal string ModID = "mashtubs";
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            ModID = this.Mod.Info.ModID;
            api.RegisterBlockClass("BlockMultiblockAdvanced", typeof(BlockMultiblockAdvanced));
            api.RegisterBlockBehaviorClass("AdvancedMultiBlock", typeof(BlockBehaviorAdvMultiblock));

            api.RegisterBlockClass(ModID + ":BlockTubGeneric", typeof(src.Blocks.BlockTubGeneric));
            api.RegisterBlockEntityClass(ModID + ":BlockEntityTreadingTub", typeof(src.Blocks.BlockEntityMashTub));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Mod.Logger.Notification("Hello from template mod server side: " + Lang.Get("mash tubs:hello"));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Mod.Logger.Notification("Hello from template mod client side: " + Lang.Get("mash tubs:hello"));
        }
    }
}
