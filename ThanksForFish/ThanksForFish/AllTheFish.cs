using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using UnityEngine;

namespace AllTheFish {
    [DefOf]
    public static class WorkTypeDefOf {
        public static WorkTypeDef Fishing;
    }

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

    //TODO fishing poles

    //TODO fish instead of meat from catch

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
}
