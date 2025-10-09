using Mash_Tubs.src.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using static System.Reflection.Metadata.BlobBuilder;

namespace Mash_Tubs.src.Blocks
{
    internal class BlockMultiblockAdvanced : BlockMultiblock
    {
        private T Handle<T, K>(IBlockAccessor ba, int x, int y, int z, BlockCallDelegateInterface<T, K> onImplementsInterface, BlockCallDelegateBlock<T> onIsMultiblock, BlockCallDelegateBlock<T> onOtherwise) where K : class
        {
            Block block = ba.GetBlock(x, y, z);
            K val = block as K;
            if (val == null)
            {
                val = block.GetBehavior(typeof(K), withInheritance: true) as K;
            }

            if (val != null)
            {
                return onImplementsInterface(val);
            }

            if (block is BlockMultiblock)
            {
                return onIsMultiblock(block);
            }

            return onOtherwise(block);
        }

        private void Handle<K>(IBlockAccessor ba, int x, int y, int z, BlockCallDelegateInterface<K> onImplementsInterface, BlockCallDelegateBlock onIsMultiblock, BlockCallDelegateBlock onOtherwise) where K : class
        {
            Block block = ba.GetBlock(x, y, z);
            K val = block as K;
            if (val == null)
            {
                val = block.GetBehavior(typeof(K), withInheritance: true) as K;
            }

            if (val != null)
            {
                onImplementsInterface(val);
            }
            else if (block is BlockMultiblock)
            {
                onIsMultiblock(block);
            }
            else
            {
                onOtherwise(block);
            }
        }

        public override void OnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos)
             //(Block block) => base.GetColorWithoutTint(capi, pos), (Block block) => block.GetColorWithoutTint(capi, pos)
        {
            Handle(world.BlockAccessor,pos.X+OffsetInv.X,pos.InternalY+OffsetInv.Y,pos.Z+OffsetInv.Z,(IMultiBlockCollisions inf) => inf.MBOnEntityInside(world,entity,pos,OffsetInv),(Block block) => base.OnEntityInside(world,entity,pos), (Block block) => block.OnEntityInside(world, entity, pos));
        }
       
        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            Handle(world.BlockAccessor, pos.X + OffsetInv.X, pos.InternalY + OffsetInv.Y, pos.Z + OffsetInv.Z, (IMultiBlockCollisions inf) => inf.MBOnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact, OffsetInv), (Block block) => base.OnEntityCollide(world, entity, pos,facing,collideSpeed,isImpact), (Block block) => block.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact));
        }
        public override bool OnFallOnto(IWorldAccessor world, BlockPos pos, Block block, TreeAttribute blockEntityAttributes)
        {
            return Handle(world.BlockAccessor, pos.X + OffsetInv.X, pos.InternalY + OffsetInv.Y, pos.Z + OffsetInv.Z, (IMultiBlockFallOnto inf) => inf.MBOnFallOnto(world, pos, block, blockEntityAttributes, OffsetInv), (Block nblock) => base.OnFallOnto(world, pos, block, blockEntityAttributes), (Block nblock) => nblock.OnFallOnto(world, pos,block,blockEntityAttributes));
        }
        public override bool CanAcceptFallOnto(IWorldAccessor world, BlockPos pos, Block fallingBlock, TreeAttribute blockEntityAttributes)
        {
            return Handle(world.BlockAccessor, pos.X + OffsetInv.X, pos.InternalY + OffsetInv.Y, pos.Z + OffsetInv.Z, (IMultiBlockFallOnto inf) => inf.MBCanAcceptFallOnto(world, pos, fallingBlock, blockEntityAttributes, OffsetInv), (Block block) => base.CanAcceptFallOnto(world, pos, fallingBlock, blockEntityAttributes), (Block block) => block.CanAcceptFallOnto(world, pos, fallingBlock, blockEntityAttributes));
        }
    }
}
