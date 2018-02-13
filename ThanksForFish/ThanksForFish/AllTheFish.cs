using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using UnityEngine;

namespace AllTheFish {
    public class PlaceWorker_DeepWaterFishing : PlaceWorker {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null) {
            TerrainDef terrainDef = map.terrainGrid.TerrainAt(loc);
            if (terrainDef == TerrainDef.Named("WaterDeep") || terrainDef == TerrainDef.Named("WaterOceanDeep") || terrainDef == TerrainDef.Named("WaterMovingDeep")) {
                return true;
            }
            //TODO too close to other fishing spot
            return new AcceptanceReport("AllTheFish.DeepWaterFishing".Translate());
        }
    }

    //TODO check expanded work tab for subtask

    public class WorkGiver_CatchFish : WorkGiver_Scanner {
        public override ThingRequest PotentialWorkThingRequest {
            get { return ThingRequest.ForDef(ThingDef.Named("FishingSpot")); }
        }

        public override PathEndMode PathEndMode {
            get { return PathEndMode.InteractionCell; }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing fishingSpot, bool forced = false) {
            if (fishingSpot.def.defName != "FishingSpot") { return false; }
            if (fishingSpot.IsBurning()) { return false; }
            if (pawn.Dead || pawn.Downed || pawn.IsBurning()) { return false; }
            if (pawn.CanReserveAndReach(fishingSpot, this.PathEndMode, Danger.Some) == false) { return false; }
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing fishingSpot, bool forced = false) {
            IntVec3 fishingSpotCell = fishingSpot.Position + new IntVec3(0, 0, -1).RotatedBy(fishingSpot.Rotation);
            return new Job(DefDatabase<JobDef>.GetNamed("CatchFish"), fishingSpot, fishingSpotCell);
        }

        public override bool AllowUnreachable {
            get { return true; }
        }
    }

    public class JobDriver_CatchFish : JobDriver_DoBill {
        public Mote fishingRod;

        protected ThingDef GetRodMote(Rot4 rotation) {
            if (rotation == Rot4.North) {
                Log.Message("Found North Rod: " + ThingDef.Named("FishingRodNorth"));
                return ThingDef.Named("FishingRodNorth");
            }
            if (rotation == Rot4.East) {
                Log.Message("Found East Rod: " + ThingDef.Named("FishingRodEast"));
                return ThingDef.Named("FishingRodEast");
            }
            if (rotation == Rot4.South) {
                Log.Message("Found South Rod: " + ThingDef.Named("FishingRodSouth"));
                return ThingDef.Named("FishingRodSouth");
            }
            if (rotation == Rot4.West) {
                Log.Message("Found West Rod: " + ThingDef.Named("FishingRodWest"));
                return ThingDef.Named("FishingRodWest");
            }
            return ThingDef.Named("FishingRodNorth");
        }

        protected override IEnumerable<Toil> MakeNewToils() {
            var i = 0;
            Log.Message("START!");
            foreach (var toil in base.MakeNewToils()) {
                Log.Message("Toil: " + i + " => " + toil);
                // Hack Alert: The 13th toil is Toils_Recipe.DoRecipeWork
                if (i == 12) {
                    toil.AddPreInitAction(() => {
                        Thing fishingSpot = this.TargetThingA;
                        IntVec3 fishingSpotCell = fishingSpot.Position + new IntVec3(0, 0, -1).RotatedBy(fishingSpot.Rotation);
                        ThingDef mote = GetRodMote(fishingSpot.Rotation);
                        this.fishingRod = (Mote)ThingMaker.MakeThing(mote, null);
                        this.fishingRod.exactPosition = fishingSpotCell.ToVector3Shifted();
                        this.fishingRod.Scale = 1f;
                        GenSpawn.Spawn(this.fishingRod, fishingSpotCell, this.Map);
                    });
                    toil.AddFinishAction(() => {
                        Thing fishingSpot = this.TargetThingA;
                        IntVec3 fishingSpotCell = fishingSpot.Position + new IntVec3(0, 0, -1).RotatedBy(fishingSpot.Rotation);
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
