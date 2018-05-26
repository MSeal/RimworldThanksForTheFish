﻿using RimWorld;
using System;
using System.Linq;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using UnityEngine;

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

    public class PlaceWorker_DeepWaterFishing : PlaceWorker {
        public static int MIN_FISHING_RADIUS = 10;
        public static TerrainDef Shallow = TerrainDef.Named("WaterShallow");
        public static TerrainDef ShallowOcean = TerrainDef.Named("WaterOceanShallow");
        public static TerrainDef ShallowMoving = TerrainDef.Named("WaterMovingShallow");
        public static TerrainDef Deep = TerrainDef.Named("WaterDeep");
        public static TerrainDef DeepOcean = TerrainDef.Named("WaterOceanDeep");
        public static TerrainDef DeepMoving = TerrainDef.Named("WaterMovingDeep");

        public static bool ShallowWaterTerrain(TerrainDef terrainDef) {
            return terrainDef == Shallow || terrainDef == ShallowOcean || terrainDef == ShallowMoving;
        }

        public static bool DeepWaterTerrain(TerrainDef terrainDef) {
            return terrainDef == Deep || terrainDef == DeepOcean || terrainDef == DeepMoving;
        }

        public static bool WaterTerrain(TerrainDef terrainDef) {
            return ShallowWaterTerrain(terrainDef) || DeepWaterTerrain(terrainDef);
        }

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
                    WaterTerrain(map.terrainGrid.TerrainAt(cell))) {
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

        protected bool FishingSpotConnectedByWater(IntVec3 loc, int radius, Map map) {
            HashSet<IntVec3> explored = new HashSet<IntVec3>();
            HashSet<IntVec3> fringe = new HashSet<IntVec3>();
            CellRect bounds = CellRect.CenteredOn(loc, radius);

            fringe.Add(loc);
            while (fringe.Any()) {
                IntVec3 nextCell = fringe.Pop();
                if (CellHasFishingSpot(nextCell, map)) {
                    return true;
                }
                explored.Add(nextCell);
                AddAdjacentFringeWaterCells(nextCell, fringe, explored, bounds, map);
            }

            return false;
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null) {
            if (!DeepWaterTerrain(map.terrainGrid.TerrainAt(loc))) {
                return new AcceptanceReport("AllTheFish.DeepWaterFishing".Translate());
            }
            if (FishingSpotConnectedByWater(loc, MIN_FISHING_RADIUS, map)) {
                return new AcceptanceReport("AllTheFish.CloseFishing".Translate());
            }
            return true;
        }
    }

    public class WorkGiver_CatchFish : WorkGiver_DoBill {
        // Thanks for making the more advanced version of this private... :/
        protected bool ThingIsUsableBillGiver(Thing thing) {
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
