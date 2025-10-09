using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Mash_Tubs.src.Blocks
{
    internal class BlockEntityMashTub : BlockEntityLiquidContainer
    {
        private static SimpleParticleProperties liquidParticles;
    
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => Block?.Code.FirstCodePart();
        //"mashtub"

        private MeshData currentMesh;
        private ICoreClientAPI capi;
        
        private BlockTubGeneric ownBlock;
        public ItemSlot MashSlot => inventory[0];
        public ItemSlot LiquidSlot => inventory[1];
        private TextureAtlasPosition juiceTexPos;
        private int dryStackSize;
        private ItemStack mashStack => MashSlot.Itemstack;
        
        private double lastLiquidTransferTotalHours;

        private bool squeezeSoundPlayed;


        private long listenerId;

        private float percentageTubMashedPerStomp = 0.25f;

        public bool Mashing;//is the tub currently being mashed
        private float MashingVolume;//volume of the container which is being mashed 0 - 1 
        private double tubSqueezeRel;//how much the tub is being squeezed 0-1
        private double mashPercent;//current mash tubSqueezeRel
        private double squeezedLitresLeft;//litres left to squeeze out of the mash

        private double mashTransitionSpeed = 1.5;//how fast the mash transition is

        private bool spillingOver;
        private bool serverListenerActive;
        public bool CanFillRemoveItems => !Mashing;

        public int CapacityLitres { get; set; } = 10;
        public int CapacityItems { get; set; } = 64;
        private double juiceableLitresLeft
        {
            get
            {
                return (mashStack?.Attributes?.GetDouble("juiceableLitresLeft")).GetValueOrDefault() * MashSlot.Itemstack.StackSize;
            }
            set
            {
                mashStack.Attributes.SetDouble("juiceableLitresLeft", value / mashStack.StackSize);
            }
        }
        private double juiceableLitresTransfered
        {
            get
            {
                return (mashStack?.Attributes?.GetDouble("juiceableLitresTransfered")).GetValueOrDefault() * MashSlot.Itemstack.StackSize;
            }
            set
            {
                mashStack.Attributes.SetDouble("juiceableLitresTransfered", value / mashStack.StackSize);
            }
        }
        static BlockEntityMashTub()
        {
            liquidParticles = new SimpleParticleProperties
            {
                MinVelocity = new Vec3f(-0.04f, 0f, -0.04f),
                AddVelocity = new Vec3f(0.08f, 0f, 0.08f),
                addLifeLength = 0.5f,
                LifeLength = 0.5f,
                MinQuantity = 0.25f,
                GravityEffect = 0.5f,
                SelfPropelled = true,
                MinSize = 0.1f,
                MaxSize = 0.2f
            };
        }

        public BlockEntityMashTub()
        {
            inventory = new InventoryGeneric(2, null, null, (int id, InventoryGeneric self) => (id == 0) ? (new ItemSlot(self)) : (new ItemSlotLiquidOnly(self, 50f)));
            inventory.BaseWeight = 1f;
            inventory.OnGetSuitability = GetSuitability;
            inventory.SlotModified += Inventory_SlotModified;
            inventory.OnAcquireTransitionSpeed += Inventory_OnAcquireTransitionSpeed1;
        }
        private float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            if (targetSlot == inventory[1] && inventory[0].StackSize > 0)
            {
                ItemStack itemstack = inventory[0].Itemstack;
                ItemStack itemstack2 = sourceSlot.Itemstack;
                if (itemstack.Collectible.Equals(itemstack, itemstack2, GlobalConstants.IgnoredStackAttributes))
                {
                    return -1f;
                }
            }

            return (isMerge ? (inventory.BaseWeight + 3f) : (inventory.BaseWeight + 1f)) + (float)((sourceSlot.Inventory is InventoryBasePlayer) ? 1 : 0);
        }
        private void Inventory_SlotModified(int slotId)
        {
            if (slotId == 0 || slotId == 1)
            {
                
                ICoreAPI api = Api;
                if (api != null && api.Side == EnumAppSide.Client)
                {
                    currentMesh = GenMesh();
                }

                MarkDirty(redrawOnClient: true);
            }
        }

        private float Inventory_OnAcquireTransitionSpeed1(EnumTransitionType transType, ItemStack stack, float mul)
        {
            if (Mashing && tubSqueezeRel > 0.0)
            {
                return 0f;
            }

            return mul;
        }
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            ownBlock = base.Block as BlockTubGeneric;
            //BlockTubGeneric blockTub = ownBlock;
            if (ownBlock != null && (ownBlock.Attributes?["capacityLitres"].Exists).GetValueOrDefault())
            {
                CapacityLitres = ownBlock.Attributes["capacityLitres"].AsInt(50);
                (inventory[1] as ItemSlotLiquidOnly).CapacityLitres = CapacityLitres;
            }
            
            if (api.Side == EnumAppSide.Client && currentMesh == null)
            {
                currentMesh = GenMesh();
                MarkDirty(redrawOnClient: true);
            }
            else if (serverListenerActive)
            {
                if (listenerId == 0L)
                {
                    listenerId = RegisterGameTickListener(onTick100msServer, 25);
                }
            }
        }

        internal void OnEntityStomp(ICoreAPI api, Entity entity)
        {
            if (api.Side == EnumAppSide.Client && currentMesh == null) 
            {
                squeezeSoundPlayed = false;
                lastLiquidTransferTotalHours = Api.World.Calendar.TotalHours;
                if (listenerId == 0L)
                {
                    listenerId = RegisterGameTickListener(onTick25msClient, 25);
                }
            }


            if (api.Side == EnumAppSide.Server && !MashSlot.Empty)
            {
                squeezeSoundPlayed = false;
                lastLiquidTransferTotalHours = Api.World.Calendar.TotalHours;
                updateSqueezeRel(percentageTubMashedPerStomp);
                api.Logger.Event("serverside stomp");
                if (listenerId == 0L)
                {
                    Mashing = true;
                    listenerId = RegisterGameTickListener(onTick100msServer, 25);
                }
                if (MashSlot.Empty && listenerId != 0L)
                {
                    UnregisterGameTickListener(listenerId);
                    listenerId = 0L;
                }
            }
        }
        private void onTick25msClient(float dt)
        {
            double num = mashStack?.Attributes.GetDouble("squeezeRel", 1.0) ?? 1.0;
            float num2 = MashingVolume;
            if (MashSlot.Empty || num >= 1.0 || tubSqueezeRel > num || squeezedLitresLeft < 0.01)
            {
                return;
            }

            Random rand = Api.World.Rand;
            liquidParticles.MinQuantity = (float)juiceableLitresLeft / 10f;
            for (int i = 0; i < 4; i++)
            {
                BlockFacing blockFacing = BlockFacing.HORIZONTALS[i];
                //liquidParticles.Color = capi.BlockTextureAtlas.GetRandomColor(renderer.juiceTexPos, rand.Next(30));
                Vec3d vec3d = blockFacing.Plane.Startd.Add(-0.5, 0.0, -0.5);
                Vec3d vec3d2 = blockFacing.Plane.Endd.Add(-0.5, 0.0, -0.5);
                vec3d.Mul(0.5);
                vec3d2.Mul(0.5);
                vec3d2.Y = 0.3125 - (1.0 - num + (double)Math.Max(0f, 0.9f - num2)) * 0.5;
                vec3d.Add(blockFacing.Normalf.X * 1.2f / 16f, 0.0, blockFacing.Normalf.Z * 1.2f / 16f);
                vec3d2.Add(blockFacing.Normalf.X * 1.2f / 16f, 0.0, blockFacing.Normalf.Z * 1.2f / 16f);
                liquidParticles.MinPos = vec3d;
                liquidParticles.AddPos = vec3d2.Sub(vec3d);
                liquidParticles.MinPos.Add(Pos).Add(0.5, 1.0, 0.5);
                Api.World.SpawnParticles(liquidParticles);
            }

            if (num < 0.89999997615814209)
            {
                liquidParticles.MinPos = Pos.ToVec3d().Add(0.375, 0.699999988079071, 0.375);
                liquidParticles.AddPos.Set(0.25, 0.0, 0.25);
                for (int j = 0; j < 3; j++)
                {
                  //  liquidParticles.Color = capi.BlockTextureAtlas.GetRandomColor(renderer.juiceTexPos, rand.Next(30));
                    Api.World.SpawnParticles(liquidParticles);
                }
            }
        }

        private void onTick100msServer(float dt)
        {
            //if serverlistener was active when tub was unloaded
            if (serverListenerActive)
            {
                updateSqueezeRel(0);
                serverListenerActive = false;
                return;
            }
            //if mashslot is empty return
            if (MashSlot.Empty)
            {
                return;
            }
            JuiceableProperties juiceableProps = getJuiceableProps(mashStack);
            double totalHours = Api.World.Calendar.TotalHours;
            double num = mashStack.Attributes.GetDouble("squeezeRel", 1.0);
            double amountToRemove = 0.0;
            if (num > tubSqueezeRel)
            {

                num = GameMath.Clamp(Math.Min(mashStack.Attributes.GetDouble("squeezeRel", 1.0), num), 0.0, 1.0);
                mashStack.Attributes.SetDouble("squeezeRel", num);
                mashPercent = MashingVolume/num;

            }
            if (Api.Side == EnumAppSide.Server && Mashing && num < 1.0 && tubSqueezeRel <= num && juiceableLitresLeft > 0.0)
            {
                
                    /*
                    Mashing;//is the tub currently being mashed
                    MashingVolume;//volume of mash being mashed
                    tubSqueezeRel;//how much the tub is being squeezed 0-1
                    mashPercent;//current mash tubSqueezeRel
                    squeezedLitresLeft;//litres left to squeeze out of the mash
                    */
                    squeezedLitresLeft = Math.Max(Math.Max(0.0, squeezedLitresLeft), juiceableLitresLeft - (juiceableLitresLeft + juiceableLitresTransfered)  * mashPercent);
                    amountToRemove = Math.Min(squeezedLitresLeft, Math.Round((totalHours - lastLiquidTransferTotalHours) * GameMath.Clamp(squeezedLitresLeft * (1.0 - num) * 500.0, 25.0, 100.0), 2));

                    if (!squeezeSoundPlayed)
                    {
                        Api.World.PlaySoundAt(new AssetLocation("sounds/player/wetclothsqueeze.ogg"), Pos, 0.0, null, randomizePitch: false);
                        squeezeSoundPlayed = true;
                    }
            }
            
            if (Api.Side == EnumAppSide.Server)
            {

                //apply crushing values to liquid and item stacks
                if (juiceableProps != null && squeezedLitresLeft > 0.0)
                {

                    ItemStack resolvedItemstack = juiceableProps.LiquidStack.ResolvedItemstack;
                    resolvedItemstack.StackSize = 999999;
                    float num3;
                    if (ownBlock != null && !ownBlock.IsFull(LiquidSlot.Itemstack))
                    {
                        float currentLitres = ownBlock.GetCurrentLitres(LiquidSlot.Itemstack);
                        if (amountToRemove > 0.0)
                        {
                            ownBlock.TryPutLiquid(Pos, resolvedItemstack, (float)amountToRemove);
                        }
                        num3 = (float)amountToRemove - currentLitres;
                    }
                    else
                    {
                        spillingOver = true;
                        num3 = (float)amountToRemove;
                    }

                    juiceableLitresLeft -= num3;
                    squeezedLitresLeft -= ((tubSqueezeRel <= num) ? num3 : (num3 * 100f));
                    juiceableLitresTransfered += num3;
                    lastLiquidTransferTotalHours = totalHours;
                    MarkDirty(redrawOnClient: true);
                }

                else if (!Mashing || juiceableLitresLeft <= 0.0)
                {
                    spillingOver = false;
                    Mashing = false;
                    UnregisterGameTickListener(listenerId);
                    listenerId = 0L;
                    MarkDirty(redrawOnClient: true);
                }
            }
        }

        private void updateSqueezeRel(float tubCrushValue)
        {
            if (tubCrushValue != null && mashStack != null)
            {
                var num = Math.Clamp((tubSqueezeRel <= 0 ? 1.0 : tubSqueezeRel) - tubCrushValue,0.0,1.0);
                MashingVolume = (float)(juiceableLitresLeft + juiceableLitresTransfered) / CapacityLitres;
                tubSqueezeRel = num;
                Api.Logger.Event("new tubSqueezeRel :" + tubSqueezeRel);
            }
        }

        internal bool ItemInventoryInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemStack itemstack = activeHotbarSlot.Itemstack;
            ItemStack returnStack;

            if (Mashing)
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "compressing", Lang.Get("Finish Treading to add/remove fruit"));
                return false;
            }

            if (!activeHotbarSlot.Empty)
            {
                JuiceableProperties juiceableProps = getJuiceableProps(itemstack);
                if (juiceableProps == null)
                {
                    return false;
                }
                if (!juiceableProps.LitresPerItem.HasValue && !itemstack.Attributes.HasAttribute("juiceableLitresLeft")) 
                {
                        return false;
                }
                ItemStack mashTypeStack = (juiceableProps.LitresPerItem.HasValue ? juiceableProps.PressedStack.ResolvedItemstack.Clone() : itemstack.GetEmptyClone());
                if (!LiquidSlot.Empty)
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "tubnotempty", Lang.Get("Cannot Add mash to partially filled tub. Remove liquids first."));
                    return false;
                }


                if (MashSlot.Empty)
                {
                    MashSlot.Itemstack = mashTypeStack;
                    //if already a mash item, add as the mashStack
                    if (!juiceableProps.LitresPerItem.HasValue)
                    {
                        mashStack.StackSize = 1;
                        dryStackSize = GameMath.RoundRandom(Api.World.Rand, ((float)juiceableLitresLeft + (float)juiceableLitresTransfered) * getJuiceableProps(mashStack).PressedDryRatio);
                        MashingVolume = (float)(juiceableLitresLeft + juiceableLitresTransfered) / CapacityLitres;
                        activeHotbarSlot.TakeOut(1);
                        MarkDirty(redrawOnClient: true);
                  
                        (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                        return true;
                    }
                }
                else if (juiceableLitresLeft + juiceableLitresTransfered >= CapacityLitres)
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "fullcontainer", Lang.Get("Container is full, press out juice and remove the mash before adding more"));
                    return false;
                }
                if (!mashStack.Equals(Api.World, mashTypeStack, GlobalConstants.IgnoredStackAttributes.Append("juiceableLitresLeft", "juiceableLitresTransfered", "squeezeRel")))
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "fullcontainer", Lang.Get("Cannot mix fruit"));
                    return false;
                }
                float heldLitersLeft = (float)itemstack.Attributes.GetDecimal("juiceableLitresLeft");
                float heldLitersTransfered = (float)itemstack.Attributes.GetDecimal("juiceableLitresTransfered");
                
                bool heldStackIsMash = !juiceableProps.LitresPerItem.HasValue;

                int inputStackSize = Math.Min(itemstack.StackSize, byPlayer.Entity.Controls.ShiftKey ? 1 : (byPlayer.Entity.Controls.CtrlKey ? itemstack.Item.MaxStackSize : 4));

                float itemLiters = heldStackIsMash ? heldLitersLeft + heldLitersTransfered : juiceableProps.LitresPerItem.Value;

                while ((inputStackSize * itemLiters) + juiceableLitresLeft + juiceableLitresTransfered > CapacityLitres)
                {
                    inputStackSize--;
                }
                if (inputStackSize <= 0)
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "fullcontainer", Lang.Get("Container is full, press out juice and remove the mash before adding more"));
                    return false;
                }
                if (heldStackIsMash)
                {
                    TransitionState[] array = itemstack.Collectible.UpdateAndGetTransitionStates(Api.World, activeHotbarSlot);
                    TransitionState[] array2 = mashStack.Collectible.UpdateAndGetTransitionStates(Api.World, MashSlot);
                    if (array != null && array2 != null)
                    {
                        Dictionary<EnumTransitionType, TransitionState> dictionary = null;
                        dictionary = new Dictionary<EnumTransitionType, TransitionState>();
                        TransitionState[] array3 = array2;
                        foreach (TransitionState transitionState in array3)
                        {
                            dictionary[transitionState.Props.Type] = transitionState;
                        }

                        float num3 = (heldLitersLeft + heldLitersTransfered) / (heldLitersLeft + heldLitersTransfered + (float)juiceableLitresLeft + (float)juiceableLitresTransfered);
                        array3 = array;
                        foreach (TransitionState transitionState2 in array3)
                        {
                            TransitionState transitionState3 = dictionary[transitionState2.Props.Type];
                            mashStack.Collectible.SetTransitionState(mashStack, transitionState2.Props.Type, transitionState2.TransitionedHours * num3 + transitionState3.TransitionedHours * (1f - num3));
                        }
                    }
                    
                }

                heldLitersLeft = (float)inputStackSize * itemLiters;
                if (inputStackSize > 0)
                {
                    AssetLocation code = activeHotbarSlot.Itemstack.Collectible.Code;
                    activeHotbarSlot.TakeOut(inputStackSize);
                    Api.World.Logger.Audit("{0} Put {1}x{2} into Treading Vat at {3}.", byPlayer.PlayerName, inputStackSize, code, blockSel.Position);

                    double oldjuiceableLitresLeft = juiceableLitresLeft;
                    double oldjuiceableLitersTransfered = juiceableLitresTransfered;
                    mashStack.StackSize = (int)Math.Ceiling(Math.Max(1, (juiceableLitresLeft + heldLitersLeft + juiceableLitresTransfered + heldLitersTransfered) * 0.1));
                    juiceableLitresLeft = heldLitersLeft + oldjuiceableLitresLeft;
                    juiceableLitresTransfered = heldLitersTransfered + oldjuiceableLitersTransfered;
                    MashingVolume = (float)(juiceableLitresLeft + juiceableLitresTransfered) / CapacityLitres;

                    //mashStack.Attributes.SetDouble("juiceableLitresTransfered", (juiceableLitresTransfered += heldLitersTransfered)/newStackSize);


                    dryStackSize = GameMath.RoundRandom(Api.World.Rand, ((float)juiceableLitresLeft + (float)juiceableLitresTransfered) * getJuiceableProps(mashStack).PressedDryRatio);
                    activeHotbarSlot.MarkDirty();
                    MarkDirty(redrawOnClient: true);
                    
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                }
                return true;
            }

            if (MashSlot.Empty ||mashStack == null)
            {
                return false;
            }

            convertDryMash();

            if (!LiquidSlot.Empty && !getJuiceableProps(mashStack).LitresPerItem.HasValue) {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "fullcontainer", Lang.Get("Tub is still full, drain the tub before removing items"));
                return false;
            }
            //retrieve mash stacks
            if (!byPlayer.InventoryManager.TryGiveItemstack(mashStack, slotNotifyEffect: true))
            {
                Api.World.SpawnItemEntity(mashStack, Pos);
            }

            Api.World.Logger.Audit("{0} Took 1x{1} from Fruitpress at {2}.", byPlayer.PlayerName, mashStack.Collectible.Code);
            MashSlot.Itemstack = null;
      //      renderer?.reloadMeshes(null, mustReload: true);
            if (Api.Side == EnumAppSide.Server)
            {
                MarkDirty(redrawOnClient: true);
            }

            return true;
        }

        public bool HeldItemHasPropsOrEmpty(IPlayer byPlayer)
        {
            var activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeHotbarSlot.Empty) {
                return true;
            }
            if (getJuiceableProps(activeHotbarSlot.Itemstack) != null)
            {
                return true;
            }
            return false;
        }
        public JuiceableProperties getJuiceableProps(ItemStack stack)
        {
            JuiceableProperties obj = ((stack != null && (stack.ItemAttributes?["juiceableProperties"].Exists).GetValueOrDefault()) ? stack.ItemAttributes["juiceableProperties"].AsObject<JuiceableProperties>(null, stack.Collectible.Code.Domain) : null);
            obj?.LiquidStack?.Resolve(Api.World, "juiceable properties liquidstack", stack.Collectible.Code);
            obj?.PressedStack?.Resolve(Api.World, "juiceable properties pressedstack", stack.Collectible.Code);
            if (obj != null)
            {
                JsonItemStack returnStack = obj.ReturnStack;
                if (returnStack != null)
                {
                    returnStack.Resolve(Api.World, "juiceable properties returnstack", stack.Collectible.Code);
                    return obj;
                }

                return obj;
            }

            return obj;
        }
        private void convertDryMash()
        {
            if (!(juiceableLitresLeft < 0.01))
            {
                return;
            }

            JuiceableProperties juiceableProps = getJuiceableProps(mashStack);
            if (juiceableProps?.ReturnStack?.ResolvedItemstack != null && mashStack != null)
            {
                double num = Math.Round(juiceableLitresTransfered, 2, MidpointRounding.AwayFromZero);
                MashSlot.Itemstack = juiceableProps.ReturnStack.ResolvedItemstack.Clone();
                mashStack.StackSize = (int)((double)mashStack.StackSize * num);
            }
            else
            {
                mashStack?.Attributes?.RemoveAttribute("juiceableLitresTransfered");
                mashStack?.Attributes?.RemoveAttribute("juiceableLitresLeft");
                mashStack?.Attributes?.RemoveAttribute("squeezeRel");
                if (mashStack?.Collectible.Code.Path != "rot")
                {
                    mashStack.StackSize = dryStackSize;
                }
            }

            dryStackSize = 0;
        }
        internal MeshData GenMesh()
        {
            if (ownBlock == null)
            {
                return null;
            }

            MeshData meshData = ownBlock.GenMesh(inventory[0].Itemstack, inventory[1].Itemstack, Mashing, Pos);
            if (meshData.CustomInts != null)
            {
                for (int i = 0; i < meshData.CustomInts.Count; i++)
                {
                    meshData.CustomInts.Values[i] |= 536870912;
                    meshData.CustomInts.Values[i] |= 268435456;
                }
            }

            return meshData;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            ItemSlot itemSlot = Inventory[0];
            ItemSlot itemSlot2 = Inventory[1];
            if (!itemSlot.Empty && itemSlot2.Empty && BlockLiquidContainerBase.GetContainableProps(itemSlot.Itemstack) != null)
            {
                Inventory.TryFlipItems(1, itemSlot);
            }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            if (!Mashing)
            {
                base.OnBlockBroken(byPlayer);
            }
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            bool empty = Inventory.Empty;
            ItemStack itemStack = mashStack;
            base.FromTreeAttributes(tree, worldForResolving);
            Mashing = tree.GetBool("mashing");
            squeezedLitresLeft = tree.GetDouble("squeezedLitresLeft");
            squeezeSoundPlayed = tree.GetBool("squeezeSoundPlayed");
            dryStackSize = tree.GetInt("dryStackSize");
            lastLiquidTransferTotalHours = tree.GetDouble("lastLiquidTransferTotalHours");

            ICoreAPI api = Api;
            if (api != null && api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(redrawOnClient: true);
                if (listenerId > 0 && juiceableLitresLeft <= 0.0)
                {
                    UnregisterGameTickListener(listenerId);
                    listenerId = 0L;
                }
            }
            if (listenerId == 0L)
            {
                serverListenerActive = tree.GetBool("ServerListenerActive");
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("mashing", Mashing);
            tree.SetBool("squeezeSoundPlayed", squeezeSoundPlayed);
            tree.SetInt("dryStackSize", dryStackSize);
            tree.SetDouble("lastLiquidTransferTotalHours", lastLiquidTransferTotalHours);
            if (Api.Side == EnumAppSide.Server)
            {
                if (listenerId != 0L)
                {
                    tree.SetBool("ServerListenerActive", value: true);
                }
            }
        }
        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
        }
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh);
            return true;
        }
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (MashSlot.Empty)
            {
                return;
            }

            JuiceableProperties juiceableProps = getJuiceableProps(mashStack);
            if (juiceableLitresLeft >= 0.01 && mashStack.Collectible.Code.Path != "rot")
            {
                string text = juiceableProps.LiquidStack.ResolvedItemstack.GetName().ToLowerInvariant();
                dsc.AppendLine(Lang.Get("Mash produces {0:0.##} X {1} litres of {2} juice when squeezed", juiceableLitresLeft/mashStack.StackSize, mashStack.StackSize, text));
                return;
            }

            int num = ((mashStack.Collectible.Code.Path != "rot") ? dryStackSize : MashSlot.StackSize);
            string text2 = MashSlot.GetStackName().ToLowerInvariant();
            if (juiceableProps?.ReturnStack?.ResolvedItemstack != null)
            {
                num = (int)((double)juiceableProps.ReturnStack.ResolvedItemstack.StackSize * Math.Round(juiceableLitresTransfered, 2, MidpointRounding.AwayFromZero));
                text2 = juiceableProps.ReturnStack.ResolvedItemstack.GetName().ToLowerInvariant();
            }

            dsc.AppendLine(Lang.Get("{0}x {1}", num, text2));
        }

    }
}