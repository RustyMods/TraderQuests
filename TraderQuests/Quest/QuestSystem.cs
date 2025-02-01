using System;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TraderQuests.Quest;

public static class QuestSystem
{
    private const float maxRadius = 9500f;
    private static float m_checkTimer;
    private const float m_checkInterval = 5f;

    [HarmonyPatch(typeof(Player), nameof(Player.Update))]
    private static class Player_Update_Patch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            if (__instance.IsDead() || __instance.IsTeleporting()) return;
            m_checkTimer += Time.fixedTime;
            if (m_checkTimer < m_checkInterval) return;
            m_checkTimer = 0.0f;
            BountySystem.CheckActiveBounties(__instance);
            TreasureSystem.CheckActiveTreasures(__instance);
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    public static class ZNet_OnNewConnection_Patch
    {
        private static void Postfix(ZNetPeer peer)
        {
            peer.m_rpc.Register(nameof(RPC_RecordKill), new Action<ZRpc, ZPackage>(RPC_RecordKill));
            peer.m_rpc.Register(nameof(RPC_RemoveRecord), new Action<ZRpc,ZPackage>(RPC_RemoveRecord));
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class ZNet_Awake_Patch
    {
        private static void Postfix()
        {
            TreasureSystem.UpdateServerTreasures();
            Shop.UpdateServerStore();
            BountySystem.UpdateServerBounties();
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Save))]
    private static class Player_Save_Patch
    {
        private static void Prefix()
        {
            BountySystem.SaveToPlayer();
            TreasureSystem.SaveToPlayer();
        }
    }
    
    public static bool IsWithinQuestLocation(Vector3 a, Vector3 b, float radius)
    {
        float num1 = a.x - b.x;
        float num2 = a.z - b.z;

        return Math.Sqrt(num1 * num1 + num2 * num2) <= radius;
    }

    public static bool FindSpawnLocation(Heightmap.Biome biome, out Vector3 pos)
    {
        pos = Vector3.zero;
        
        // Try get location within margin
        for (int index = 0; index < 1000; ++index)
        {
            Vector3 vector3 = GetRandomVectorWithin(Player.m_localPlayer.transform.position, 3000f);
            
            if (WorldGenerator.instance.GetBiome(vector3) != biome) continue;
            if (WorldGenerator.instance.GetBiomeArea(vector3) is not Heightmap.BiomeArea.Median) continue;
            if (biome != Heightmap.Biome.Ocean && ZoneSystem.m_instance.GetSolidHeight(vector3) <= ZoneSystem.m_instance.m_waterLevel + 0.5f) continue;
            pos = vector3;
            return true;
        }
        // Else try get location entire world
        for (int index = 0; index < 1000; ++index)
        {
            Vector3 vector3 = GetRandomVector();

            if (WorldGenerator.instance.GetBiome(vector3) != biome) continue;
            if (WorldGenerator.instance.GetBiomeArea(vector3) is not Heightmap.BiomeArea.Median) continue;
            if (biome != Heightmap.Biome.Ocean && ZoneSystem.m_instance.GetSolidHeight(vector3) <= ZoneSystem.m_instance.m_waterLevel + 0.5f) continue;
            pos = vector3;
            return true;
        }
        return false;
    }
    
    public static Vector3 FindSpawnPoint(Vector3 point, float maxDistance)
    {
        for (int index = 0; index < 100; ++index)
        {
            var vector3 = GetRandomVectorWithin(point, maxDistance);
            if (WorldGenerator.instance.GetBiome(vector3) == Heightmap.Biome.Ocean)
            {
                vector3.y = ZoneSystem.instance.m_waterLevel - 0.3f;
            }
            else
            {
                ZoneSystem.instance.GetSolidHeight(vector3, out float height, 1000);
                if (height >= 0.0 && Mathf.Abs(height - point.y) <= 10f && Vector3.Distance(vector3, point) >= 2f)
                {
                    vector3.y = height + 5f;
                }
                else
                {
                    continue;
                }
            }

            return vector3;
        }

        return point;
    }

    private static Vector3 GetRandomVectorWithin(Vector3 point, float margin)
    {
        Vector2 vector2 = Random.insideUnitCircle * margin;
        return point + new Vector3(vector2.x, 0.0f, vector2.y);
    }

    private static Vector3 GetRandomVector()
    {
        float x = Random.Range(-maxRadius, maxRadius);
        float y = Random.Range(0f, 5000f);
        float z = Random.Range(-maxRadius, maxRadius);
        return new Vector3(x, y, z);
    }

    public static void RPC_RecordKill(ZRpc rpc, ZPackage pkg) => BountySystem.RecordKill(pkg.ReadString(), pkg.ReadLong());

    public static void RPC_RemoveRecord(ZRpc rpc, ZPackage pkg) => BountySystem.RemoveRecord(pkg.ReadLong(), pkg.ReadString());
}