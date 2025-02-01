using System;
using HarmonyLib;
using TraderQuests.Quest;
using UnityEngine;

namespace TraderQuests.Behaviors;

[RequireComponent(typeof(ZNetView))] 
[RequireComponent(typeof(Character))]
public class Bounty : MonoBehaviour
{
    public ZNetView m_nview = null!;
    public Character m_character = null!;
    private Minimap.PinData? m_pin;

    public long m_hunter;
    public string m_name = "";
    public bool m_isBoss;
    public string m_iconPrefab = "";
    public string m_recordID = "";
    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_character = GetComponent<Character>();

        m_character.m_onDeath += OnDeath;
    }

    public void SetData(BountySystem.BountyData bountyData, BountySystem.BountyData.CreatureData creatureData)
    {
        SetHunter(Player.m_localPlayer.GetPlayerID());
        SetNameOverride(creatureData.Config.OverrideName);
        SetBoss(creatureData.Config.IsBoss);
        SetIconPrefab(bountyData.Config.IconPrefab);
        SetRecordID($"{creatureData.BountyID}:{creatureData.Config.UniqueID}");
        SetLevel(creatureData.Config.Level);

        LoadData();
    }
    
    private void SetHunter(long hunterID) => m_nview.GetZDO().Set("Hunter".GetStableHashCode(), hunterID);
    private void SetNameOverride(string nameOverride) => m_nview.GetZDO().Set("NameOverride".GetStableHashCode(), nameOverride);
    private void SetBoss(bool boss) => m_nview.GetZDO().Set("IsBoss".GetStableHashCode(), boss);
    private void SetIconPrefab(string prefabName) => m_nview.GetZDO().Set("IconPrefab".GetStableHashCode(), prefabName);
    private void SetRecordID(string recordID) => m_nview.GetZDO().Set("UniqueID".GetStableHashCode(), recordID);
    private void SetLevel(int level) => m_nview.GetZDO().Set(ZDOVars.s_level, level);

    private long GetHunterID() => m_nview.GetZDO().GetLong("Hunter".GetStableHashCode());
    private string GetNameOverride() => m_nview.GetZDO().GetString("NameOverride".GetStableHashCode());
    private bool IsBoss() => m_nview.GetZDO().GetBool("IsBoss".GetStableHashCode());
    private string GetIconPrefabName() => m_nview.GetZDO().GetString("IconPrefab".GetStableHashCode());
    private string GetRecordID() => m_nview.GetZDO().GetString("UniqueID".GetStableHashCode());

    public void LoadData()
    {
        m_hunter = GetHunterID();
        m_name = GetNameOverride();
        m_isBoss = IsBoss();
        m_iconPrefab = GetIconPrefabName();
        m_recordID = GetRecordID();

        m_character.m_name = m_name;
        m_character.m_boss = m_isBoss;
        m_character.m_level = m_nview.GetZDO().GetInt(ZDOVars.s_level, 1);
        m_character.m_baseAI.SetHuntPlayer(true);
        m_character.m_baseAI.SetAlerted(true);
        m_character.SetupMaxHealth();
    }

    public void AddPin()
    {
        var pin = Minimap.instance.AddPin(transform.position, Minimap.PinType.Boss, m_name, false, false);
        if (ObjectDB.instance.GetItemPrefab(m_iconPrefab) is { } iconPrefab && iconPrefab.TryGetComponent(out ItemDrop component))
        {
            pin.m_icon = component.m_itemData.GetIcon();
        }

        m_pin = pin;
    }

    public void Update()
    {
        if (m_pin is null)
        {
            AddPin();
            return;
        }
        m_pin.m_pos = transform.position;
    }

    public void OnDestroy()
    {
        Minimap.instance.RemovePin(m_pin);
    }

    public void OnDeath()
    {
        if (Player.m_localPlayer.GetPlayerID() != m_hunter)
        {
            if (!m_nview.IsOwner()) return;

            if (!ZNet.m_instance.IsServer())
            {
                var server = ZNet.m_instance.GetServerPeer();
                var pkg = new ZPackage();
                pkg.Write(m_recordID);
                pkg.Write(m_hunter);
                server.m_rpc.Invoke(nameof(QuestSystem.RPC_RecordKill), pkg);
            }
            else
            {
                BountySystem.RecordKill(m_recordID, m_hunter);
            }
        }
        else
        {
            var parts = m_recordID.Split(':');
            var bountyID = parts[0];
            var creatureID = parts[1];
            if (BountySystem.ActiveBounties.TryGetValue(bountyID, out BountySystem.BountyData bounty))
            {
                if (bounty.Creatures.TryGetValue(creatureID, out BountySystem.BountyData.CreatureData creature) && !creature.Killed)
                {
                    creature.Killed = true;
                    ++bounty.KilledCreatureCount;
                }

                if (bounty.KilledCreatureCount >= bounty.Creatures.Count)
                {
                    bounty.SetCompleted(true, DateTime.Now.Ticks);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.Awake))]
    private static class Character_Awake_Patch
    {
        private static void Postfix(Character __instance)
        {
            if (__instance is Player) return;
            if (!__instance.m_nview.IsValid()) return;
            if (__instance.m_nview.GetZDO().GetLong("Hunter".GetStableHashCode()) != 0L)
            {
                var component = __instance.gameObject.AddComponent<Bounty>();
                component.LoadData();
            }
        }
    }
}