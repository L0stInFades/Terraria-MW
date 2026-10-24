using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace ModernArsenal;

internal static class PhalanxTargeting
{
    internal const float Range = 3200;
    internal const float Speed = 100;
    internal static bool Hostile(NPC npc) => npc.active && !npc.friendly && !npc.townNPC && npc.lifeMax > 5 &&
        !npc.dontTakeDamage && !npc.immortal && npc.type is not (NPCID.CultistBossClone or NPCID.SolarCrawltipedeHead or
        NPCID.SolarCrawltipedeBody or NPCID.MoonLordFreeEye or NPCID.GolemHeadFree or NPCID.PlanterasHook) &&
        (PillarOrder(npc.type) < 0 || Shield(npc.type) == 0) && npc.CanBeChasedBy();

    internal static int PillarOrder(int type) => type switch
    {
        NPCID.LunarTowerVortex => 0, NPCID.LunarTowerStardust => 1,
        NPCID.LunarTowerSolar => 2, NPCID.LunarTowerNebula => 3, _ => -1
    };
    internal static int Shield(int type) => type switch
    {
        NPCID.LunarTowerVortex => NPC.ShieldStrengthTowerVortex,
        NPCID.LunarTowerStardust => NPC.ShieldStrengthTowerStardust,
        NPCID.LunarTowerSolar => NPC.ShieldStrengthTowerSolar,
        NPCID.LunarTowerNebula => NPC.ShieldStrengthTowerNebula, _ => 0
    };
    // These are the exact NPC types that send a shield-reducing soul in vanilla NPCLoot.
    internal static int ShieldCredit(int type) => type switch
    {
        425 or 426 or 427 or 429 => NPCID.LunarTowerVortex,
        402 or 405 or 407 or 409 or 411 => NPCID.LunarTowerStardust,
        412 or 413 or 414 or 415 or 416 or 417 or 418 or 419 or 518 => NPCID.LunarTowerSolar,
        420 or 421 or 423 or 424 => NPCID.LunarTowerNebula, _ => -1
    };
    internal static NPC PriorityPillar(Vector2 origin)
    {
        NPC result = null;
        foreach (NPC npc in Main.npc)
        {
            int order = PillarOrder(npc.type);
            if (!npc.active || order < 0 || Vector2.DistanceSquared(origin, npc.Center) > Range * Range) continue;
            if (result == null || order < PillarOrder(result.type)) result = npc;
        }
        return result;
    }
    internal static int Rank(NPC npc, NPC pillar)
    {
        if (pillar != null)
        {
            if (Shield(pillar.type) > 0 && ShieldCredit(npc.type) == pillar.type) return 0;
            if (Shield(pillar.type) == 0 && npc.whoAmI == pillar.whoAmI) return 0;
        }
        return npc.type switch
        {
            NPCID.MoonLordCore => 0,
            NPCID.MoonLordHead or NPCID.MoonLordHand or NPCID.Creeper or NPCID.PlanterasTentacle or
                NPCID.SkeletronHand or NPCID.PrimeLaser or NPCID.PrimeCannon or NPCID.WallofFleshEye or
                NPCID.Spazmatism or NPCID.Probe or NPCID.SolarCrawltipedeTail => 1,
            NPCID.GolemHead or NPCID.Golem or NPCID.CultistBoss or NPCID.Retinazer or
                NPCID.EaterofWorldsHead or NPCID.TheDestroyer => 2,
            NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail or NPCID.TheDestroyerBody or
                NPCID.TheDestroyerTail => 3,
            NPCID.StardustCellSmall or NPCID.StardustJellyfishSmall or NPCID.StardustSpiderSmall or NPCID.VortexLarva => 8,
            _ => npc.boss ? 2 : 5
        };
    }
    internal static NPC Acquire(Vector2 origin, bool lineOfSight = true)
    {
        NPC pillar = PriorityPillar(origin), selected = null;
        long best = long.MaxValue;
        foreach (NPC npc in Main.npc)
        {
            if (!Hostile(npc) || PillarOrder(npc.type) >= 0 && Shield(npc.type) > 0) continue;
            float distance = Vector2.DistanceSquared(origin, npc.Center);
            if (distance > Range * Range) continue;
            long score = Rank(npc, pillar) * 100000000L + (long)distance;
            if (score >= best || lineOfSight && !Collision.CanHitLine(origin, 1, 1, npc.position, npc.width, npc.height)) continue;
            best = score; selected = npc;
        }
        return selected;
    }
    internal static Vector2 Aim(Vector2 origin, NPC target)
    {
        Vector2 offset = target.Center - origin;
        float a = target.velocity.LengthSquared() - Speed * Speed;
        float b = 2 * Vector2.Dot(offset, target.velocity), c = offset.LengthSquared();
        float discriminant = b * b - 4 * a * c;
        float t = Math.Abs(a) < 0.01f || discriminant < 0 ? offset.Length() / Speed : (-b - MathF.Sqrt(discriminant)) / (2 * a);
        t = float.IsFinite(t) ? Math.Clamp(t, 0, 30) : 0;
        Vector2 predicted = target.Center + target.velocity * t;
        if (!Collision.CanHitLine(origin, 1, 1, predicted, 1, 1)) predicted = target.Center;
        return (predicted - origin).SafeNormalize(Vector2.UnitX);
    }
}
