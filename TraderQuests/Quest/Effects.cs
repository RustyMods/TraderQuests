using HarmonyLib;
using UnityEngine;

namespace TraderQuests.Quest;

public static class Effects
{
    public static EffectList PreSpawnEffects = null!;
    public static EffectList DoneSpawnEffects = null!;

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        private static void Postfix(ZNetScene __instance)
        {
            GetPreSpawnEffects(__instance);
            GetSpawnEffects(__instance);
        }
    }
    
    private static void GetPreSpawnEffects(ZNetScene instance)
    {
        GameObject? VFX_PreSpawn = instance.GetPrefab("vfx_prespawn");
        GameObject? SFX_PreSpawn = instance.GetPrefab("sfx_prespawn");
        if (!VFX_PreSpawn || !SFX_PreSpawn) return;
        
        PreSpawnEffects = new EffectList()
        {
            m_effectPrefabs = new[]
            {
                new EffectList.EffectData()
                {
                    m_prefab = VFX_PreSpawn,
                    m_enabled = true,
                    m_variant = -1
                },
                new EffectList.EffectData()
                {
                    m_prefab = SFX_PreSpawn,
                    m_enabled = true,
                    m_variant = -1,
                }
            }
        };
    }

    private static void GetSpawnEffects(ZNetScene instance)
    {
        GameObject? VFX_Spawn = instance.GetPrefab("vfx_spawn");
        GameObject? SFX_Spawn = instance.GetPrefab("sfx_spawn");
        if (!VFX_Spawn || !SFX_Spawn) return;

        DoneSpawnEffects = new EffectList()
        {
            m_effectPrefabs = new[]
            {
                new EffectList.EffectData()
                {
                    m_prefab = VFX_Spawn,
                    m_enabled = true,
                    m_variant = -1
                },
                new EffectList.EffectData()
                {
                    m_prefab = SFX_Spawn,
                    m_enabled = true,
                    m_variant = -1
                }
            }
        };
    }
}