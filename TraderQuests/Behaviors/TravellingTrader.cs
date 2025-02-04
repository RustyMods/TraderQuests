using HarmonyLib;

namespace TraderQuests.Behaviors;

public class TravellingTrader : Trader
{
    public Character m_character = null!;

    public void Awake()
    {
        m_character = GetComponent<Character>();
    }
    
    public void CustomStart()
    {
        InvokeRepeating(nameof(RandomTalk), m_randomTalkInterval, m_randomTalkInterval);
    }
    
    public void CustomUpdate()
    {
        m_character.m_baseAI.StopMoving();
    }

    public string CustomHoverText()
    {
        return "";
    }
    
    


    [HarmonyPatch(typeof(Character), nameof(Character.GetHoverText))]
    private static class Character_GetHoverText_Patch
    {
        private static bool Prefix(Character __instance, ref string __result)
        {
            if (__instance.TryGetComponent(out TravellingTrader component))
            {
                __result = component.CustomHoverText();
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Trader), nameof(Start))]
    private static class Trader_Start_Patch
    {
        private static bool Prefix(Trader __instance)
        {
            if (__instance is TravellingTrader component)
            {
                component.CustomStart();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Trader), nameof(Update))]
    private static class Trader_Update_Patch
    {
        private static bool Prefix(Trader __instance)
        {
            if (__instance is TravellingTrader component)
            {
                component.CustomUpdate();
                return false;
            }

            return true;
        } 
    }
}