using System;
using System.Reflection;

namespace TraderQuests.API;

public static class EpicMMOSystemAPI
{
    public static API_State state = API_State.NotReady;
    private static MethodInfo? eGetLevel;
    private static MethodInfo? eAddExp;
    private static MethodInfo? eGetAttribute;
    private static MethodInfo? eSetSingleRate;


    public enum API_State
    {
        NotReady, NotInstalled, Ready,
    }

    public enum Attribut
    {
        Strength = 0, Agility = 1, Intellect = 2, Body = 3, Vigour = 4, Special = 5,
    }

    public static int GetLevel()
    {
        Init();
        return (int)(eGetLevel?.Invoke(null, null) ?? int.MaxValue);
    }
    
    public static int GetAttribute(Attribut attribute)
    {
        Init();
        return (int)(eGetAttribute?.Invoke(null, new object[] { attribute.ToString() }) ?? int.MaxValue);
    }

    public static void AddExp(int value)
    {
        Init();
        eAddExp?.Invoke(null, new object[] { value });
    }

    public static void SetSingleRate(float rate)
    {
        Init();
        eSetSingleRate?.Invoke(null, new object[] { rate });
    }  
 
    private static void Init()
    { 
        if (state is API_State.Ready or API_State.NotInstalled) return;
        if (Type.GetType("EpicMMOSystem.EpicMMOSystem, EpicMMOSystem") == null)
        {
            state = API_State.NotInstalled;
            return;
        }

        state = API_State.Ready;

        if (Type.GetType("API.EMMOS_API, EpicMMOSystem") is not { } actionsMO) return;
        eGetLevel = actionsMO.GetMethod("GetLevel", BindingFlags.Public | BindingFlags.Static);
        eAddExp = actionsMO.GetMethod("AddExp", BindingFlags.Public | BindingFlags.Static);
        eGetAttribute = actionsMO.GetMethod("GetAttributeRusty", BindingFlags.Public | BindingFlags.Static);
        eSetSingleRate = actionsMO.GetMethod("SetSingleRate", BindingFlags.Public | BindingFlags.Static);
    }
}
