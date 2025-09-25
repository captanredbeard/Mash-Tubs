using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Mash_Tubs.src.Behaviors
{
    [DocumentAsJson]
    internal class BlockBehaviorAdvMultiblock : BlockBehaviorMultiblock
    {
        public BlockBehaviorAdvMultiblock(Block block)
        : base(block)
        {
        }
        private string type;

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            type = properties["type"].AsString("monolithic");
            
        }
        public override void OnBlockPlaced(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            handling = EnumHandling.PassThrough;
            IterateOverEach(pos, delegate (BlockPos mpos)
            {
                if (mpos == pos)
                {
                    return true;
                }

                int num = mpos.X - pos.X;
                int num2 = mpos.Y - pos.Y;
                int num3 = mpos.Z - pos.Z;
                string text = ((num < 0) ? "n" : ((num > 0) ? "p" : "")) + Math.Abs(num);
                string text2 = ((num2 < 0) ? "n" : ((num2 > 0) ? "p" : "")) + Math.Abs(num2);
                string text3 = ((num3 < 0) ? "n" : ((num3 > 0) ? "p" : "")) + Math.Abs(num3);
                AssetLocation assetLocation = new AssetLocation("advmultiblock-" + type + "-" + text + "-" + text2 + "-" + text3);
                Block block = world.GetBlock(assetLocation);
                if (block == null)
                {
                    throw new IndexOutOfRangeException("Advanced Multiblocks are currently limited to 5x5x5 with the controller being in the middle of it, yours likely exceeds the limit because I could not find block with code " + assetLocation.Path);
                }

                world.BlockAccessor.SetBlock(block.Id, mpos);
                return true;
            });
        }
    }
}
