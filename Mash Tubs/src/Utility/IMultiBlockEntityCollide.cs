﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Mash_Tubs.src.Utility
{
    internal interface IMultiBlockCollisions
    {
        void MBOnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos, Vec3i offsetInv);

        void MBOnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact, Vec3i offsetInv);

        bool MBOnFallOnto(IWorldAccessor world, BlockPos pos, Block block, TreeAttribute blockEntityAttributes, Vec3i offsetInv);

        bool MBCanAcceptFallOnto(IWorldAccessor world, BlockPos pos, Block fallingBlock, TreeAttribute blockEntityAttributes, Vec3i offsetInv);
    }
}
