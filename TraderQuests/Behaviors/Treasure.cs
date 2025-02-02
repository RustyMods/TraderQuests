using System;
using System.Collections.Generic;
using TraderQuests.Quest;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TraderQuests.Behaviors;

[RequireComponent(typeof(IDestructible))]
[RequireComponent(typeof(HoverText))]
public class Treasure : MonoBehaviour
{
    public ZNetView m_nview = null!;
    public HoverText m_hoverText = null!;
    private TreasureSystem.TreasureData? m_data;

    public string m_recordID = "";
    private Minimap.PinData? m_pin;

    public float m_range = 20f;
    private static readonly List<Treasure> m_instances = new List<Treasure>();
    
    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_hoverText = GetComponent<HoverText>();
        var component = GetComponent<IDestructible>();
        if (component is Destructible destructible)
        {
            destructible.m_onDestroyed += OnDestroyed;
        }
        else if (component is WearNTear wearNTear)
        {
            wearNTear.m_onDestroyed += OnDestroyed;
        }
        m_instances.Add(this);
    }
    public void Update()
    {
        if (m_data is null) return;
        if (m_pin is not null)
        {
            m_pin.m_pos = transform.position;
        }
        else AddPin();
    }
    public void OnDestroy()
    {
        m_instances.Remove(this);
        if (m_pin is null) return;
        Minimap.m_instance.RemovePin(m_pin);
    }
    private void AddPin()
    {
        if (m_pin is not null || m_data is null) return;
        m_pin = Minimap.m_instance.AddPin(transform.position, Minimap.PinType.RandomEvent, m_data.Config.Name, false, false);
        m_pin.m_doubleSize = true;
        m_pin.m_animate = true;
    }

    public void SetData(TreasureSystem.TreasureData data)
    {
        SetRecordID(data.Config.UniqueID);
        LoadData();
    }

    public void LoadData()
    {
        m_recordID = GetRecordID();
        if (TreasureSystem.AllTreasures.TryGetValue(m_recordID, out TreasureSystem.TreasureData data))
        {
            m_data = data;
        }

        if (m_data is null) return;

        m_hoverText.m_text = m_data.Config.Name;
    }

    public void SetRecordID(string recordID) => m_nview.GetZDO().Set("RecordID".GetStableHashCode(), recordID);
    private string GetRecordID() => m_nview.GetZDO().GetString("RecordID".GetStableHashCode());

    public void OnDestroyed()
    {
        if (m_data is null) return;
        Vector3 position = transform.position;
        float groundHeight = ZoneSystem.instance.GetGroundHeight(position);
        if (position.y < groundHeight)
        {
            position.y = groundHeight + 0.1f;
        }

        for (var index = 0; index < m_data.Rewards.Count; index++)
        {
            TreasureSystem.TreasureData.RewardData reward = m_data.Rewards[index];
            Vector2 pos = Random.insideUnitCircle * 0.5f;
            Vector3 spawnPoint = position + Vector3.up * 0.5f + new Vector3(pos.x, 0.3f, index * pos.y);
            Quaternion rotation = Quaternion.Euler(0.0f, Random.Range(0, 360), 0.0f);
            var prefab = Instantiate(reward.Prefab, spawnPoint, rotation);
            if (prefab.TryGetComponent(out ItemDrop component))
            {
                component.m_itemData.m_stack = reward.Config.Amount;
                component.m_itemData.m_quality = Math.Max(reward.Config.Quality, 1);
            }
            ItemDrop.OnCreateNew(prefab);
        }
        m_data.SetCompleted(true, DateTime.Now.Ticks);
    }

    public static Treasure? FindClosestTreasureInRange(Vector3 point)
    {
        Treasure? closestTreasure = null;
        float closestDistance = float.MaxValue;
        foreach (Treasure treasure in m_instances)
        {
            float distance = Vector3.Distance(point, treasure.transform.position);
            if (distance < treasure.m_range && closestTreasure is null || distance < closestDistance)
            {
                closestTreasure = treasure;
                closestDistance = distance;
            }
        }

        return closestTreasure;
    }

    public static void FindTreasureInRange(Vector3 point, ref List<Treasure> treasures)
    {
        foreach (var treasure in m_instances)
        {
            if (Vector3.Distance(point, treasure.transform.position) < treasure.m_range)
            {
                treasures.Add(treasure);
            }
        }
    }
}