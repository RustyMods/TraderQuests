using HarmonyLib;
using UnityEngine;
using UnityEngine.Serialization;

namespace TraderQuests.Behaviors;

public class TreasureFinder : StatusEffect
{
    private static SE_Finder Finder = null!;
    
    public EffectList m_pingEffectNear = new EffectList();
    public EffectList m_pingEffectMed = new EffectList();
    public EffectList m_pingEffectFar = new EffectList();
    public float m_closerTriggerDistance = 2f;
    public float m_furtherTriggerDistance = 4f;
    public float m_closeFrequency = 1f;
    public float m_distantFrequency = 5f;
    
    public float m_updateBeaconTimer;
    public float m_pingTimer;
    [FormerlySerializedAs("m_beacon")] public Treasure? m_treasure;
    public float m_lastDistance;

    public override void Setup(Character character)
    {
        m_pingEffectNear = Finder.m_pingEffectNear;
        m_pingEffectMed = Finder.m_pingEffectMed;
        m_pingEffectFar = Finder.m_pingEffectFar;
        m_closerTriggerDistance = Finder.m_closerTriggerDistance;
        m_furtherTriggerDistance = Finder.m_furtherTriggerDistance;
        m_closeFrequency = Finder.m_closeFrequency;
        m_distantFrequency = Finder.m_distantFrequency;
        
        base.Setup(character);
    }

    public override void UpdateStatusEffect(float dt)
    {
        m_updateBeaconTimer += dt;
        if (m_updateBeaconTimer > 1.0)
        {
            m_updateBeaconTimer = 0.0f;
            var closest = Treasure.FindClosestTreasureInRange(m_character.transform.position);
            if (closest != m_treasure)
            {
                m_treasure = closest;
                if (m_treasure is not null)
                {
                    m_lastDistance = Utils.DistanceXZ(m_character.transform.position, m_treasure.transform.position);
                    m_pingTimer = 0.0f;
                }
            }
        }

        if (m_treasure is null) return;
        var num1 = Utils.DistanceXZ(m_character.transform.position, m_treasure.transform.position);
        var t = Mathf.Clamp01(num1 / m_treasure.m_range);
        var num2 = Mathf.Lerp(m_closeFrequency, m_distantFrequency, t);
        m_pingTimer += dt;
        if (m_pingTimer <= (double)num2) return;
        m_pingTimer = 0.0f;
        var transform = m_character.transform;
        if (t < 0.20000000298023224)
        {
            m_pingEffectNear.Create(transform.position, transform.rotation, transform);
        }
        else if (t < 0.6000000238418579)
        {
            m_pingEffectMed.Create(m_character.transform.position, transform.transform.rotation, transform);
        }
        else
        {
            m_pingEffectFar.Create(m_character.transform.position, transform.transform.rotation, transform);
        }
        m_lastDistance = num1;
    }
    
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static class ObjectDB_Awake_Patch
    {
        private static void Postfix(ObjectDB __instance)
        {
            if (!__instance || !ZNetScene.instance) return;
            if (__instance.GetStatusEffect("Wishbone".GetStableHashCode()) is SE_Finder { } finder)
            {
                Finder = finder;
            }
        }
    }
}