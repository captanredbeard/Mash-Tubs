﻿using HarmonyLib;
using Mash_Tubs.src.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;
using static System.Reflection.Metadata.BlobBuilder;

namespace Mash_Tubs.src.Blocks
{
    internal class BlockTubGeneric : BlockLiquidContainerBase, IMultiBlockColSelBoxes, IMultiBlockCollisions
    {
        public override bool AllowHeldLiquidTransfer => false;

        public AssetLocation emptyShape { get; protected set; } = AssetLocation.Create("block/wood/tub/empty", "mashtubs");

        public AssetLocation contentsShape { get; protected set; } = AssetLocation.Create("block/wood/tub/contents", "mashtubs");

        public AssetLocation opaqueLiquidContentsShape { get; protected set; } = AssetLocation.Create("block/wood/tub/opaqueliquidcontents", "mashtubs");

        public AssetLocation liquidContentsShape { get; protected set; } = AssetLocation.Create("block/wood/tub/liquidcontents", "mashtubs");
        #region MBselection
        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return false;
        }

        public ValuesByMultiblockOffset ValuesByMultiblockOffset { get; set; } = new();

        Cuboidf[] IMultiBlockColSelBoxes.MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            if (ValuesByMultiblockOffset.CollisionBoxesByOffset.TryGetValue(offset, out Cuboidf[] collisionBoxes))
            {
                return collisionBoxes;
            }
            Block originaBlock = blockAccessor.GetBlock(pos.AddCopy(offset.X, offset.Y, offset.Z));
            return originaBlock.GetCollisionBoxes(blockAccessor, pos);
        }

        Cuboidf[] IMultiBlockColSelBoxes.MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            if (ValuesByMultiblockOffset.SelectionBoxesByOffset.TryGetValue(offset, out Cuboidf[] selectionBoxes))
            {
                return selectionBoxes;
            }
            Block originaBlock = blockAccessor.GetBlock(pos.AddCopy(offset.X, offset.Y, offset.Z));
            return this.GetSelectionBoxes(blockAccessor, pos);
        }

        bool IMultiBlockCollisions.MBCanAcceptFallOnto(IWorldAccessor world, BlockPos pos, Block fallingBlock, TreeAttribute blockEntityAttributes, Vec3i offsetInv)
        {
            return base.CanAcceptFallOnto(world, pos, fallingBlock, blockEntityAttributes);
        }
        bool IMultiBlockCollisions.MBOnFallOnto(IWorldAccessor world, BlockPos pos, Block block, TreeAttribute blockEntityAttributes, Vec3i offsetInv)
        {
            return base.OnFallOnto(world, pos, block, blockEntityAttributes);
        }
        void IMultiBlockCollisions.MBOnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos, Vec3i offsetInv)
        {
            base.OnEntityInside(world, entity, pos);
        }
        void IMultiBlockCollisions.MBOnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact, Vec3i offsetInv)
        {
            OnEntityCollide(world, entity, pos.AddCopy(offsetInv), facing, collideSpeed, isImpact);
        }
        #endregion MBselection

        public override int GetContainerSlotId(BlockPos pos)
        {
            return 1;
        }
        public override int GetContainerSlotId(ItemStack containerStack)
        {
            return 1;
        }
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            object value;
            Dictionary<string, MultiTextureMeshRef> dictionary2 = (Dictionary<string, MultiTextureMeshRef>)(capi.ObjectCache.TryGetValue("mashTubMeshRefs" + Code, out value) ? (value as Dictionary<string, MultiTextureMeshRef>) : (capi.ObjectCache["mashTubMeshRefs" + Code] = new Dictionary<string, MultiTextureMeshRef>()));
            ItemStack[] contents = GetContents(capi.World, itemstack);
            if (contents != null && contents.Length != 0)
            {
                bool @bool = itemstack.Attributes.GetBool("mashing");
                string barrelMeshkey = GetTubMeshkey(contents[0], (contents.Length > 1) ? contents[1] : null);
                if (!dictionary2.TryGetValue(barrelMeshkey, out var value2))
                {
                    MeshData data = GenMesh(contents[0], (contents.Length > 1) ? contents[1] : null, @bool);
                    value2 = (dictionary2[barrelMeshkey] = capi.Render.UploadMultiTextureMesh(data));
                }

                renderinfo.ModelRef = value2;
            }
        }
        public override void OnUnloaded(ICoreAPI api)
        {
            if (!(api is ICoreClientAPI coreClientAPI) || !coreClientAPI.ObjectCache.TryGetValue("mashTubMeshRefs", out var value))
            {
                return;
            }

            foreach (KeyValuePair<int, MultiTextureMeshRef> item in value as Dictionary<int, MultiTextureMeshRef>)
            {
                item.Value.Dispose();
            }

            coreClientAPI.ObjectCache.Remove("mashTubMeshRefs");
        }
        public string GetTubMeshkey(ItemStack contentStack, ItemStack liquidStack)
        {
            return string.Concat(contentStack?.StackSize + "x" + contentStack?.GetHashCode(), (liquidStack?.StackSize).ToString(), "x", (liquidStack?.GetHashCode()).ToString());
        }
    
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            bool flag = false;
            BlockBehavior[] blockBehaviors = BlockBehaviors;
            foreach (BlockBehavior obj in blockBehaviors)
            {
                EnumHandling handling = EnumHandling.PassThrough;
                obj.OnBlockBroken(world, pos, byPlayer, ref handling);
                if (handling == EnumHandling.PreventDefault)
                {
                    flag = true;
                }

                if (handling == EnumHandling.PreventSubsequent)
                {
                    return;
                }
            }

            if (flag)
            {
                return;
            }

            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] array = new ItemStack[1]
                {
                new ItemStack(this)
                };
                for (int j = 0; j < array.Length; j++)
                {
                    world.SpawnItemEntity(array[j], pos);
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos, 0.0, byPlayer);
            }

            if (EntityClass != null)
            {
                world.BlockAccessor.GetBlockEntity(pos)?.OnBlockBroken(byPlayer);
            }

            world.BlockAccessor.SetBlock(0, pos);
        }
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
        }
        public override int TryPutLiquid(BlockPos pos, ItemStack liquidStack, float desiredLitres)
        {
            return base.TryPutLiquid(pos, liquidStack, desiredLitres);
        }
        public override int TryPutLiquid(ItemStack containerStack, ItemStack liquidStack, float desiredLitres)
        {
            return base.TryPutLiquid(containerStack, liquidStack, desiredLitres);
        }
        
        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }
        
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }
            
            BlockEntityMashTub blockEntityTub = null;
            if (blockSel.Position != null)
            {
                blockEntityTub = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMashTub;
            }
            if (blockEntityTub != null) {
                if (blockEntityTub.Mashing) 
                { 
                    return true;
                }
                if (blockEntityTub.HeldItemHasPropsOrEmpty(byPlayer) && blockSel.Position != null)
                {
                        blockEntityTub?.ItemInventoryInteract(byPlayer, blockSel);
                        return true;
                }
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            StringBuilder StringBuilder = new StringBuilder();
            string text = base.GetPlacedBlockInfo(world, pos, forPlayer);
            string text2 = "";
            int num = text.IndexOfOrdinal(Environment.NewLine + Environment.NewLine);
            if (num > 0)
            {
                text2 = text.Substring(num);
                text = text.Substring(0, num);
            }

            if (GetCurrentLitres(pos) <= 0f)
            {
                text = "";
            }

            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMashTub blockEntityTub)
            {
                ItemSlot itemSlot = blockEntityTub.Inventory[0];
                if (!itemSlot.Empty)
                {
                    text = ((text.Length <= 0) ? (text + Lang.Get("Contents:") + "\n ") : (text + " "));
                    text += Lang.Get("{0}x {1}", itemSlot.Itemstack.StackSize, itemSlot.Itemstack.GetName());
                    text += BlockLiquidContainerBase.PerishableInfoCompact(api, itemSlot, 0f, withStackName: false);
                    blockEntityTub.GetBlockInfo(forPlayer,StringBuilder);
                    text += StringBuilder.ToString();
                }   
            }

            return text + text2;
        }

        public override void TryFillFromBlock(EntityItem byEntityItem, BlockPos pos)
        {}

        public MeshData GenMesh(ItemStack contentStack, ItemStack liquidContentStack, bool issealed, BlockPos forBlockPos = null)
        {
            ICoreClientAPI obj = api as ICoreClientAPI;
            Shape shape = Vintagestory.API.Common.Shape.TryGet(obj,emptyShape);
            obj.Tesselator.TesselateShape(this, shape, out var modeldata,this.Shape.RotateXYZCopy??new Vec3f(0,0,0));
            
                JsonObject containerProps = liquidContentStack?.ItemAttributes?["waterTightContainerProps"];
                MeshData meshData = getContentMeshFromAttributes(contentStack, liquidContentStack, forBlockPos) ?? getContentMesh(contentStack, forBlockPos, contentsShape);
                MeshData meshData2 = getContentMeshFromAttributes(contentStack, liquidContentStack, forBlockPos) ?? getContentMeshLiquids(contentStack, liquidContentStack, forBlockPos, containerProps);
                if (meshData != null)
                {
                    modeldata.AddMeshData(meshData);
                }
                if (meshData2 !=null) {
                    modeldata.AddMeshData(meshData2);
                }
                if (forBlockPos != null)
                {
                    modeldata.CustomInts = new CustomMeshDataPartInt(modeldata.FlagsCount);
                    modeldata.CustomInts.Values.Fill(268435456);
                    modeldata.CustomInts.Count = modeldata.FlagsCount;
                    modeldata.CustomFloats = new CustomMeshDataPartFloat(modeldata.FlagsCount * 2);
                    modeldata.CustomFloats.Count = modeldata.FlagsCount * 2;
                }
            

            return modeldata;
        }
        private MeshData getContentMeshLiquids(ItemStack contentStack, ItemStack liquidContentStack, BlockPos forBlockPos, JsonObject containerProps)
        {
            bool flag = containerProps?["isopaque"].AsBool() ?? false;
            bool flag2 = containerProps?.Exists ?? false;
            if (liquidContentStack != null && (flag2 || contentStack == null))
            {
                AssetLocation shapefilepath = contentsShape;
                if (flag2)
                {
                    shapefilepath = (flag ? opaqueLiquidContentsShape : liquidContentsShape);
                }

                return getContentMesh(liquidContentStack, forBlockPos, shapefilepath);
            }

            return null;
        }

        private MeshData getContentMeshFromAttributes(ItemStack contentStack, ItemStack liquidContentStack, BlockPos forBlockPos)
        {
            if (liquidContentStack != null && (liquidContentStack.ItemAttributes?["inTubShape"].Exists).GetValueOrDefault())
            {
                AssetLocation shapefilepath = AssetLocation.Create(liquidContentStack.ItemAttributes?["inTubShape"].AsString(), contentStack.Collectible.Code.Domain).WithPathPrefixOnce("shapes").WithPathAppendixOnce(".json");
                return getContentMesh(contentStack, forBlockPos, shapefilepath);
            }
            return null;
        }

        protected MeshData getContentMesh(ItemStack stack, BlockPos forBlockPos, AssetLocation shapefilepath)
        {
            ICoreClientAPI coreClientAPI = api as ICoreClientAPI;
            WaterTightContainableProps containableProps = BlockLiquidContainerBase.GetContainableProps(stack);
            ITexPositionSource texPositionSource;
            float fillHeight;
            if (containableProps != null)
            {
                if (containableProps.Texture == null)
                {
                    return null;
                }

                texPositionSource = new ContainerTextureSource(coreClientAPI, stack, containableProps.Texture);
                fillHeight = GameMath.Min(1f, (float)stack.StackSize / containableProps.ItemsPerLitre / (float)Math.Max(100, containableProps.MaxStackSize)) * 5f / 10f;
            }
            else
            {
                texPositionSource = getContentTexture(coreClientAPI, stack, out fillHeight);
            }

            if (stack != null && texPositionSource != null)
            {
                Shape shape = Vintagestory.API.Common.Shape.TryGet(coreClientAPI, shapefilepath);
                if (shape == null)
                {
                    api.Logger.Warning($"tub block '{Code}': Content shape {shapefilepath} not found. Will try to default to another one.");
                    return null;
                }

                coreClientAPI.Tesselator.TesselateShape("treading tub", shape, out var modeldata, texPositionSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ), containableProps?.GlowLevel ?? 0, 0, 0);
                modeldata.Translate(0f, fillHeight, 0f);
                if (containableProps != null && containableProps.ClimateColorMap != null)
                {
                    int color = coreClientAPI.World.ApplyColorMapOnRgba(containableProps.ClimateColorMap, null, -1, 196, 128, flipRb: false);
                    if (forBlockPos != null)
                    {
                        color = coreClientAPI.World.ApplyColorMapOnRgba(containableProps.ClimateColorMap, null, -1, forBlockPos.X, forBlockPos.Y, forBlockPos.Z, flipRb: false);
                    }

                    byte[] array = ColorUtil.ToBGRABytes(color);
                    for (int i = 0; i < modeldata.Rgba.Length; i++)
                    {
                        modeldata.Rgba[i] = (byte)(modeldata.Rgba[i] * array[i % 4] / 255);
                    }
                }

                return modeldata;
            }

            return null;
        }

        public static ITexPositionSource getContentTexture(ICoreClientAPI capi, ItemStack stack, out float fillHeight)
        {
            ITexPositionSource result = null;
            fillHeight = 0f;
            JsonObject jsonObject = stack?.ItemAttributes?["inContainerTexture"];
            if (jsonObject != null && jsonObject.Exists)
            {
                result = new ContainerTextureSource(capi, stack, jsonObject.AsObject<CompositeTexture>());
                fillHeight = GameMath.Min(0.75f, 0.7f * (float)stack.StackSize / (float)stack.Collectible.MaxStackSize);
            }
            else if (stack?.Block != null && (stack.Block.DrawType == EnumDrawType.Cube || stack.Block.Shape.Base.Path.Contains("basic/cube")) && capi.BlockTextureAtlas.GetPosition(stack.Block, "up", returnNullWhenMissing: true) != null)
            {
                result = new BlockTopTextureSource(capi, stack.Block);
                fillHeight = GameMath.Min(0.75f, 0.7f * (float)stack.StackSize / (float)stack.Collectible.MaxStackSize);
            }
            else if (stack != null)
            {
                if (stack.Class == EnumItemClass.Block)
                {
                    if (stack.Block.Textures.Count > 1)
                    {
                        return null;
                    }

                    result = new ContainerTextureSource(capi, stack, stack.Block.Textures.FirstOrDefault().Value);
                }
                else
                {
                    if (stack.Item.Textures.Count > 1)
                    {
                        return null;
                    }

                    result = new ContainerTextureSource(capi, stack, stack.Item.FirstTexture);
                }

                fillHeight = GameMath.Min(0.75f, 0.7f * (float)stack.StackSize / (float)stack.Collectible.MaxStackSize);
            }

            return result;
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            ValuesByMultiblockOffset = ValuesByMultiblockOffset.FromAttributes(this);
            if (Attributes != null)
            {
                capacityLitresFromAttributes = Attributes["capacityLitres"].AsInt(50);
                emptyShape = AssetLocation.Create(Attributes["emptyShape"].AsString(emptyShape), Code.Domain);
                contentsShape = AssetLocation.Create(Attributes["contentsShape"].AsString(contentsShape), Code.Domain);
                opaqueLiquidContentsShape = AssetLocation.Create(Attributes["opaqueLiquidContentsShape"].AsString(opaqueLiquidContentsShape), Code.Domain);
                liquidContentsShape = AssetLocation.Create(Attributes["liquidContentsShape"].AsString(liquidContentsShape), Code.Domain);
            }
            emptyShape.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            contentsShape.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            opaqueLiquidContentsShape.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            liquidContentsShape.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            if (api.Side != EnumAppSide.Client)
            {
                return;
            }

            ICoreClientAPI capi = api as ICoreClientAPI;
            base.interactions = ObjectCacheUtil.GetOrCreate(api, "liquidContainerBase", delegate
            {
                List<ItemStack> list = new List<ItemStack>();
                foreach (CollectibleObject collectible in api.World.Collectibles)
                {
                    if (collectible is ILiquidSource || collectible is ILiquidSink || collectible is BlockWateringCan)
                    {
                        List<ItemStack> handBookStacks = collectible.GetHandBookStacks(capi);
                        if (handBookStacks != null)
                        {
                            list.AddRange(handBookStacks);
                        }
                    }
                }

                ItemStack[] lstacks = list.ToArray();
                ItemStack[] linenStack = new ItemStack[1]
                {
                new ItemStack(api.World.GetBlock(new AssetLocation("linen-normal-down")))
                };
                return new WorldInteraction[1]
                {
                new WorldInteraction
                {
                    ActionLangCode = "blockhelp-bucket-rightclick",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = lstacks,
                    GetMatchingStacks = delegate(WorldInteraction wi, BlockSelection bs, EntitySelection ws)
                    {
                        BlockEntityMashTub obj = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityMashTub;
                        return (obj == null || obj.Mashing) ? null : lstacks;
                    }
                }
                };
            });
        }
        
        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMashTub blockEntityMashTub) {
                if (isImpact && facing.IsVertical && collideSpeed.Y < -0.2 && (entity.Pos.Y - pos.Y) < 0.1)
                {
                    blockEntityMashTub.OnEntityStomp(world.Api,entity);
                }
            }
            base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);
        }
    }
}
