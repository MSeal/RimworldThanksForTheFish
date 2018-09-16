using RimWorld;
using System;
using System.Linq;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using UnityEngine;
using HugsLib;
using HugsLib.Settings;

namespace AllTheFish {
    static class Extensions {
        public static T Pop<T>(this HashSet<T> items) {
            T item = items.FirstOrDefault();
            if (item != null) {
                items.Remove(item);
            }
            return item;
        }
    }

    public class AllTheFishLoader : ModBase {
        public DolphinAway dolphinLeaving;

        public override string ModIdentifier {
            get { return "AllTheFish"; }
        }
        
        const int DEFAULT_MIN_FISHING_RADIUS = 10;
        const int DEFAULT_MIN_FISHING_SIZE = 5;
        const int DEFAULT_FISHING_WORK_COST = 1350;

        public static SettingHandle<int> minFishingRadius;
        public static SettingHandle<int> minFishingSize;
        public static SettingHandle<int> fishingWorkCost;

        public static List<TerrainDef> DefaultFishableWaters = new List<TerrainDef> {
            TerrainDef.Named("WaterDeep"),
            TerrainDef.Named("WaterOceanDeep"),
            TerrainDef.Named("WaterMovingChestDeep")
        };

        public static int MIN_FISHING_RADIUS = 10;
        public static int MIN_FISHING_SIZE = 5;

        protected void LoadHandles() {
            minFishingRadius = Settings.GetHandle<int>(
              "minFishingRadius",
              "AllTheFish.MinFishingRadiusSetting".Translate(),
              "AllTheFish.MinFishingRadiusSettingDescription".Translate(),
              DEFAULT_MIN_FISHING_RADIUS,
              Validators.IntRangeValidator(0, 255 * 255));

            minFishingSize = Settings.GetHandle<int>(
               "minFishingSize",
               "AllTheFish.MinFishingSizeSetting".Translate(),
               "AllTheFish.MinFishingSizeSettingDescription".Translate(),
               DEFAULT_MIN_FISHING_SIZE,
               Validators.IntRangeValidator(0, 255 * 255));

            fishingWorkCost = Settings.GetHandle<int>(
               "fishingWorkCost",
               "AllTheFish.FishingWorkCostSetting".Translate(),
               "AllTheFish.FishingWorkCostSettingDescription".Translate(),
               DEFAULT_FISHING_WORK_COST,
               Validators.IntRangeValidator(1, 32767));
            fishingWorkCost.OnValueChanged = newValue => { ApplyFishingCostSetting(); };
        }

        public void ApplyFishingCostSetting() {
            RecipeDef fishing = DefDatabase<RecipeDef>.GetNamed("CatchFish", true);
            fishing.workAmount = fishingWorkCost;

            Logger.Message(String.Format("Set fishingWorkCost: {0}", fishing.workAmount));
        }

        protected void InjectModSupports()
        {
            RecipeDef CleanFish = DefDatabase<RecipeDef>.GetNamed("CleanFish", true);
            RecipeDef CleanFishBulk = DefDatabase<RecipeDef>.GetNamed("CleanFishBulk", true);
            // Just in-case tribal essentials reuses this name 
            ThingDef ButcheringSpot = DefDatabase<ThingDef>.GetNamed("ButcheringSpot", false);
            ThingDef ButcherBlock = DefDatabase<ThingDef>.GetNamed("MedTimes_ButcherBlock", false);

            // Support for Tribal Essentials
            if (ButcheringSpot != null)
            {
                CleanFish.recipeUsers.Add(ButcheringSpot);
                CleanFishBulk.recipeUsers.Add(ButcheringSpot);
            }
            // Support for Medieval Times
            if (ButcherBlock != null)
            {
                CleanFish.recipeUsers.Add(ButcherBlock);
                CleanFishBulk.recipeUsers.Add(ButcherBlock);
            }
        }

        public override void DefsLoaded() {
            LoadHandles();
            ApplyFishingCostSetting();
            InjectModSupports();
        }

        public static List<TerrainDef> FetchFishingTagged()
        {
            List<TerrainDef> fishables = new List<TerrainDef>();
            foreach (TerrainDef t in DefDatabase<TerrainDef>.AllDefs)
            {
                if (t.HasTag("Fishable"))
                {
                    fishables.Add(t);
                }
            }
            return fishables;
        }

        // The mods references haven't been ported, but just in case they do later and forget the Water tag, we'll update them on the fly
        public static void CompatabilityAddMissingWaterTags()
        {
            // Add tracking water distances under bridges from RF - Basic Bridges mod
            List<TerrainDef> oldBridges = new List<TerrainDef> {
                DefDatabase<TerrainDef>.GetNamed("Bridge", false),
                DefDatabase<TerrainDef>.GetNamed("BridgeWaterDeep", false),
                DefDatabase<TerrainDef>.GetNamed("BridgeWaterOceanDeep", false),
                DefDatabase<TerrainDef>.GetNamed("BridgeWaterMovingDeep", false),
                DefDatabase<TerrainDef>.GetNamed("BridgeWaterMovingChestDeep", false),
                DefDatabase<TerrainDef>.GetNamed("BridgeWaterShallow", false),
                DefDatabase<TerrainDef>.GetNamed("BridgeWaterOceanShallow", false),
                DefDatabase<TerrainDef>.GetNamed("BridgeWaterMovingShallow", false),
                DefDatabase<TerrainDef>.GetNamed("StoneBridgeWaterDeep", false),
                DefDatabase<TerrainDef>.GetNamed("StoneBridgeWaterOceanDeep", false),
                DefDatabase<TerrainDef>.GetNamed("StoneBridgeWaterMovingDeep", false),
                DefDatabase<TerrainDef>.GetNamed("StoneBridgeWaterMovingChestDeep", false),
                DefDatabase<TerrainDef>.GetNamed("StoneBridgeWaterShallow", false),
                DefDatabase<TerrainDef>.GetNamed("StoneBridgeWaterOceanShallow", false),
                DefDatabase<TerrainDef>.GetNamed("StoneBridgeWaterMovingShallow", false),
                DefDatabase<TerrainDef>.GetNamed("BridgeTKKN_SpringsWater", false)
            };

            // Add tracking water distances through new water tiles in Nature's Pretty Sweet mod
            List<TerrainDef> oldNaturesSweetWater = new List<TerrainDef> {
                DefDatabase<TerrainDef>.GetNamed("TKKN_ColdSpringsWater", false),
                DefDatabase<TerrainDef>.GetNamed("TKKN_HotSpringsWater", false),
                DefDatabase<TerrainDef>.GetNamed("TKKN_ColdSprings", false),
                DefDatabase<TerrainDef>.GetNamed("TKKN_HotSprings", false)
            };

            foreach (TerrainDef t in oldBridges.Concat(oldNaturesSweetWater))
            {
                if (t != null && !t.HasTag("Water"))
                {
                    if (t.tags == null)
                    {
                        t.tags = new List<string>();
                    }
                    t.tags.Add("Water");
                }
            }
        }

        public static void AddDefaultFishableTags()
        {
            foreach (TerrainDef t in DefaultFishableWaters)
            {
                if (!t.HasTag("NonFishable") && !t.HasTag("Fishable"))
                {
                    if (t.tags == null)
                    {
                        t.tags = new List<string>();
                    }
                    t.tags.Add("Fishable");
                }
            }
        }

        public static void AddDefaultWaterAffordances()
        {
            TerrainAffordanceDef waterAffordance = DefDatabase<TerrainAffordanceDef>.GetNamed("Water");
            foreach (TerrainDef t in DefDatabase<TerrainDef>.AllDefs)
            {
                if (t.HasTag("Fishable") && !t.affordances.Contains(waterAffordance))
                {
                    t.affordances.Add(waterAffordance);
                    t.changeable = true;
                }
            }
        }

        public override void MapLoaded(Map map) {
            base.MapLoaded(map);

            CompatabilityAddMissingWaterTags();
            AddDefaultFishableTags();
            AddDefaultWaterAffordances();
            Logger.Message("MapLoaded: Fishable terrain options: " + string.Join(", ", FetchFishingTagged().ConvertAll<string>(t => t.ToString()).ToArray()));
            
            IntVec3? launchPoint = RandomWater(map);
            if (launchPoint.HasValue)
            {
                Logger.Message("Humans are here... so long and thanks for all the fish @ (" + launchPoint + ")");
                DolphinAway dolphin = (DolphinAway)ThingMaker.MakeThing(ThingDef.Named("DolphinAway"), null);
                // Needs his fish before leaving...
                Thing fish = ThingMaker.MakeThing(ThingDef.Named("DeadFish"));
                dolphin.innerContainer.TryAdd(fish);
                this.dolphinLeaving = (DolphinAway)SkyfallerMaker.MakeSkyfaller(ThingDef.Named("DolphinAway"), dolphin);
                GenSpawn.Spawn(this.dolphinLeaving, launchPoint.GetValueOrDefault(), map);
            }
        }

        public override void Tick(int currentTick) {
            base.Tick(currentTick);
            if (this.dolphinLeaving != null) {
                if (this.dolphinLeaving.Destroyed) {
                    this.dolphinLeaving = null;
                } else {
                    this.dolphinLeaving.Tick();
                }
            }
        }

        public HashSet<IntVec3> AllDeepWater(Map map) {
            HashSet<IntVec3> water = new HashSet<IntVec3>();
            foreach (var cell in map.AllCells) {
                if (map.terrainGrid.TerrainAt(cell).HasTag("DeepWater")) {
                    water.Add(cell);
                }
            }
            return water;
        }

        public IntVec3? RandomWater(Map map) {
            System.Random randomizer = new System.Random();
            HashSet<IntVec3> water = AllDeepWater(map);
            if (water.Count == 0) {
                return null;
            } else {
                return water.ElementAt(randomizer.Next(water.Count));
            }
        }
    }

    public class DolphinAway : Skyfaller {
        public int rotationAngle = 0;

        public override void Tick() {
            this.rotationAngle += 3;
            base.Tick();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad) {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad) {
                this.angle = 30;
            }
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false) {
            this.Graphic.Draw(drawLoc, (!flip) ? this.Rotation : this.Rotation.Opposite, this, this.rotationAngle);
        }
    }

    public class PlaceWorker_DeepWaterFishing : PlaceWorker {
        public static TerrainDef Shallow = TerrainDef.Named("WaterShallow");
        public static TerrainDef ShallowOcean = TerrainDef.Named("WaterOceanShallow");
        public static TerrainDef ShallowMoving = TerrainDef.Named("WaterMovingShallow");

        

        public static IEnumerable<IntVec3> DirectlyConnectedCells(IntVec3 center) {
            yield return new IntVec3(center.x + 1, 0, center.z);
            yield return new IntVec3(center.x, 0, center.z + 1);
            yield return new IntVec3(center.x - 1, 0, center.z);
            yield return new IntVec3(center.x, 0, center.z -1);
        }

        protected void AddAdjacentFringeWaterCells(IntVec3 center, HashSet<IntVec3> fringe, HashSet<IntVec3> explored, CellRect bounds, Map map) {
            foreach (var cell in DirectlyConnectedCells(center)) {
                if (!explored.Contains(cell) &&
                    bounds.Contains(cell) &&
                    cell.InBounds(map) &&
                    map.terrainGrid.TerrainAt(cell).HasTag("Water")) {
                    fringe.Add(cell);
                }
            }
        }

        protected bool CellHasFishingSpot(IntVec3 cell, Map map) {
            foreach (var thing in map.thingGrid.ThingsListAt(cell)) {
                if (thing.def.defName.Contains("FishingSpot")) {
                    return true;
                }
            }
            return false;
        }

        protected bool FishingSpotMinSize(IntVec3 loc, int minSize, Map map) {
            HashSet<IntVec3> explored = new HashSet<IntVec3>();
            HashSet<IntVec3> fringe = new HashSet<IntVec3>();
            CellRect bounds = CellRect.CenteredOn(loc, minSize);

            fringe.Add(loc);
            while (fringe.Any())
            {
                IntVec3 nextCell = fringe.Pop();
                explored.Add(nextCell);
                if (explored.Count >= minSize)
                {
                    return true;
                }
                AddAdjacentFringeWaterCells(nextCell, fringe, explored, bounds, map);
            }

            return false;
        }

        protected bool FishingSpotConnectedByWater(IntVec3 loc, int radius, Map map) {
            HashSet<IntVec3> explored = new HashSet<IntVec3>();
            HashSet<IntVec3> fringe = new HashSet<IntVec3>();
            CellRect bounds = CellRect.CenteredOn(loc, radius);

            fringe.Add(loc);
            while (fringe.Any()) {
                IntVec3 nextCell = fringe.Pop();
                if (CellHasFishingSpot(nextCell, map)) {
                    return false;
                }
                explored.Add(nextCell);
                AddAdjacentFringeWaterCells(nextCell, fringe, explored, bounds, map);
            }

            return true;
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null) {
            if (!map.terrainGrid.TerrainAt(loc).HasTag("Fishable")) {
                return new AcceptanceReport("AllTheFish.DeepWaterFishing".Translate());
            }
            if (map.terrainGrid.TerrainAt(loc).HasTag("NoFishingRules"))
            {
                return true;
            }
            if (!FishingSpotMinSize(loc, AllTheFishLoader.minFishingSize, map)) {
                return new AcceptanceReport("AllTheFish.TooSmallFishing".Translate());
            }
            if (!FishingSpotConnectedByWater(loc, AllTheFishLoader.minFishingRadius, map)) {
                return new AcceptanceReport("AllTheFish.CloseFishing".Translate());
            }
            return true;
        }
    }

    public class WorkGiver_CatchFish : WorkGiver_DoBill {
        // Thanks for making the more advanced version of this private... :/
        public new bool ThingIsUsableBillGiver(Thing thing) {
            Pawn pawn = thing as Pawn;
            if (this.def.fixedBillGiverDefs != null && this.def.fixedBillGiverDefs.Contains(thing.def)) {
                return true;
            }
            if (pawn != null) {
                return (this.def.billGiversAllHumanlikes && pawn.RaceProps.Humanlike) ||
                    (this.def.billGiversAllMechanoids && pawn.RaceProps.IsMechanoid) ||
                    (this.def.billGiversAllAnimals && pawn.RaceProps.Animal);
            }
            return false;
        }

        protected Job AssignBill(Job job, Pawn pawn, IBillGiver giver) {
            for (int i = 0; i < giver.BillStack.Count; i++) {
                Bill bill = giver.BillStack[i];
                if (bill.recipe.requiredGiverWorkType == null || bill.recipe.requiredGiverWorkType == this.def.workType) {
                    if (bill.ShouldDoNow() && bill.PawnAllowedToStartAnew(pawn)) {
                        if (!bill.recipe.PawnSatisfiesSkillRequirements(pawn)) {
                            JobFailReason.Is("MissingSkill".Translate());
                        }
                        job.bill = bill;
                        return job;
                    }
                }
            }
            return null;
        }

        public override Job JobOnThing(Pawn pawn, Thing fishingSpot, bool forced = false) {
            IBillGiver billGiver = fishingSpot as IBillGiver;
            if (billGiver != null &&
                    this.ThingIsUsableBillGiver(fishingSpot) &&
                    billGiver.BillStack.AnyShouldDoNow &&
                    billGiver.CurrentlyUsableForBills()) {
                LocalTargetInfo target = fishingSpot;
                if (pawn.CanReserve(target, 1, -1, null, forced) && !fishingSpot.IsBurning() && !fishingSpot.IsForbidden(pawn)) {
                    IntVec3 fishingSpotCell = fishingSpot.Position + new IntVec3(0, 0, -1).RotatedBy(fishingSpot.Rotation);
                    billGiver.BillStack.RemoveIncompletableBills();
                    Job job = new Job(DefDatabase<JobDef>.GetNamed("CatchFish"), fishingSpot, fishingSpotCell);
                    if (this.AssignBill(job, pawn, billGiver) != null) {
                        return job;
                    }
                }
            }
            return null;
        }
    }

    public class JobDriver_CatchFish : JobDriver_DoBill {
        public Mote fishingRod;

        protected ThingDef GetRodMote(Rot4 rotation) {
            if (rotation == Rot4.North) {
                return ThingDef.Named("FishingRodNorth");
            }
            if (rotation == Rot4.East) {
                return ThingDef.Named("FishingRodEast");
            }
            if (rotation == Rot4.South) {
                return ThingDef.Named("FishingRodSouth");
            }
            return ThingDef.Named("FishingRodWest");
        }

        protected override IEnumerable<Toil> MakeNewToils() {
            var i = 0;
            foreach (var toil in base.MakeNewToils()) {
                // Hack Alert: The 13th toil is Toils_Recipe.DoRecipeWork
                // No, there's really not an easier way to know in C#... that I know of
                // blame anonymous functions!
                if (i == 12) {
                    toil.AddPreInitAction(() => {
                        Thing fishingSpot = this.TargetA.Thing;
                        IntVec3 fishingSpotCell = this.TargetB.Cell;
                        ThingDef mote = GetRodMote(fishingSpot.Rotation);
                        this.fishingRod = (Mote)ThingMaker.MakeThing(mote, null);
                        Vector3 adjustment = new Vector3(0.0f, 0.0f, -0.25f).RotatedBy(fishingSpot.Rotation);
                        this.fishingRod.exactPosition = fishingSpot.Position.ToVector3Shifted() + adjustment;
                        this.fishingRod.Scale = 1f;
                        GenSpawn.Spawn(this.fishingRod, fishingSpotCell, this.Map);
                    });
                    toil.AddFinishAction(() => {
                        Thing fishingSpot = this.TargetA.Thing;
                        IntVec3 fishingSpotCell = this.TargetB.Cell;
                        if (this.fishingRod != null) {
                            this.fishingRod.Destroy();
                        }
                        foreach (var thing in base.Map.thingGrid.ThingsListAt(fishingSpotCell)) {
                            if (thing.def.defName.Contains("FishingRod")) {
                                thing.DeSpawn();
                            }
                        }
                    });
                }
                yield return toil;
                i++;
            }
        }
    }
}
