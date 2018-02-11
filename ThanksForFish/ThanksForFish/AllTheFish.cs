using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AllTheFish {
    public class PlaceWorker_DeepWaterFishing : PlaceWorker {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null) {
            TerrainDef terrainDef = map.terrainGrid.TerrainAt(loc);
            if (terrainDef == TerrainDef.Named("WaterDeep") || terrainDef == TerrainDef.Named("WaterOceanDeep") || terrainDef == TerrainDef.Named("WaterMovingDeep")) {
                return true;
            }
            return new AcceptanceReport("AllTheFish.DeepWaterFishing".Translate());
        }
    }
}
