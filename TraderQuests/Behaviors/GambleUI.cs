using System.Collections.Generic;
using System.Linq;
using TraderQuests.Quest;
using TraderQuests.translations;
using TraderQuests.UI;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace TraderQuests.Behaviors;

public class GambleUI : MonoBehaviour
{
    public static GambleUI m_instance = null!;
    public EffectList m_successEffects = new();
    public EffectList m_failedEffects = new();
    public static Sprite m_defaultIcon = null!;
    
    private RectTransform m_slot_1 = null!;
    private RectTransform m_slot_2 = null!;
    private RectTransform m_slot_3 = null!;
    private RectTransform m_slot_4 = null!;
    private RectTransform m_slot_5 = null!;
    private RectTransform m_slot_6 = null!;
    private RectTransform m_slot_7 = null!;
    private RectTransform m_slot_8 = null!;
    private RectTransform m_slot_9 = null!;
    private RectTransform m_slot_10 = null!;
    private RectTransform m_slot_11 = null!;
    private RectTransform m_slot_12 = null!;

    private Scrollbar m_scrollbar_1 = null!;
    private Scrollbar m_scrollbar_2 = null!;
    private Scrollbar m_scrollbar_3 = null!;
    private Scrollbar m_scrollbar_4 = null!;
    private Scrollbar m_scrollbar_5 = null!;
    private Scrollbar m_scrollbar_6 = null!;
    private Scrollbar m_scrollbar_7 = null!;
    private Scrollbar m_scrollbar_8 = null!;
    private Scrollbar m_scrollbar_9 = null!;
    private Scrollbar m_scrollbar_10 = null!;
    private Scrollbar m_scrollbar_11 = null!;
    private Scrollbar m_scrollbar_12 = null!;

    private Text m_header = null!;
    private Image m_rewardIcon = null!;
    private RectTransform m_info = null!;
    private RectTransform m_itemData = null!;
    private RectTransform m_infoRoot = null!;
    private RectTransform m_itemDataRoot = null!;
    private Text m_infoText = null!;
    private Text m_itemText = null!;

    private readonly Dictionary<Scrollbar, float> m_allScrollbars = new();
    private readonly List<Image> m_allIcons = new();
    private readonly List<Image> m_lastIcons = new();
    private readonly List<Image> m_matches = new();

    private float m_staggerDelay = 0.3f;
    private float m_duration = 2f;

    public GambleSystem.GambleItem? m_item;
    private int m_count;
    private bool m_success;
    private bool m_roll;
    public void Awake()
    {
        m_instance = this;

        m_header = gameObject.transform.Find("Tooltip/RewardInfo/$text_gamble_reward").GetComponent<Text>();
        m_rewardIcon = gameObject.transform.Find("Tooltip/RewardInfo/icon").GetComponent<Image>();
        m_info = gameObject.transform.Find("Tooltip/RewardInfo/info").GetComponent<RectTransform>();
        m_itemData = gameObject.transform.Find("Tooltip/itemData").GetComponent<RectTransform>();
        m_infoRoot = m_info.Find("Content/$list_tooltip").GetComponent<RectTransform>();
        m_itemDataRoot = m_itemData.Find("Content/$list_tooltip").GetComponent<RectTransform>();
        m_infoText = m_infoRoot.Find("$text_tooltip").GetComponent<Text>();
        m_itemText = m_itemDataRoot.Find("$text_tooltip").GetComponent<Text>();

        m_info.Find("Content").GetComponent<Image>().sprite = Assets.ListBackground;
        m_itemData.Find("Content").GetComponent<Image>().sprite = Assets.ListBackground;
        m_rewardIcon.material = Assets.ItemMat;
        m_header.color = new Color32(255, 164, 0, 255);
        
        gameObject.transform.Find("Slots (0)/$Slot_1/$scrollRect").GetComponent<Image>().sprite = Assets.ListBackground;
        gameObject.transform.Find("Slots (0)/$Slot_2/$scrollRect").GetComponent<Image>().sprite = Assets.ListBackground;
        gameObject.transform.Find("Slots (0)/$Slot_3/$scrollRect").GetComponent<Image>().sprite = Assets.ListBackground;
        gameObject.transform.Find("Slots (0)/$Slot_4/$scrollRect").GetComponent<Image>().sprite = Assets.ListBackground;
        gameObject.transform.Find("Slots (1)/$Slot_5/$scrollRect").GetComponent<Image>().sprite = Assets.ListBackground;
        gameObject.transform.Find("Slots (1)/$Slot_6/$scrollRect").GetComponent<Image>().sprite = Assets.ListBackground;
        gameObject.transform.Find("Slots (1)/$Slot_7/$scrollRect").GetComponent<Image>().sprite = Assets.ListBackground;
        gameObject.transform.Find("Slots (1)/$Slot_8/$scrollRect").GetComponent<Image>().sprite = Assets.ListBackground;
        gameObject.transform.Find("Slots (2)/$Slot_9/$scrollRect").GetComponent<Image>().sprite = Assets.ListBackground;
        gameObject.transform.Find("Slots (2)/$Slot_10/$scrollRect").GetComponent<Image>().sprite = Assets.ListBackground;
        gameObject.transform.Find("Slots (2)/$Slot_11/$scrollRect").GetComponent<Image>().sprite = Assets.ListBackground;
        gameObject.transform.Find("Slots (2)/$Slot_12/$scrollRect").GetComponent<Image>().sprite = Assets.ListBackground;

        gameObject.transform.Find("Tooltip/RewardInfo/info/ItemScroll").GetComponent<Image>().sprite = Assets.ListBackground;
        gameObject.transform.Find("Tooltip/itemData/ItemScroll").GetComponent<Image>().sprite = Assets.ListBackground;

        gameObject.transform.Find("Tooltip/RewardInfo/info/ItemScroll/Sliding Area/Handle").GetComponent<Image>().sprite = Assets.ScrollHandle;
        gameObject.transform.Find("Tooltip/itemData/ItemScroll/Sliding Area/Handle").GetComponent<Image>().sprite = Assets.ScrollHandle;

        m_slot_1 = gameObject.transform.Find("Slots (0)/$Slot_1/$scrollRect/$list_content").GetComponent<RectTransform>();
        m_slot_2 = gameObject.transform.Find("Slots (0)/$Slot_2/$scrollRect/$list_content").GetComponent<RectTransform>();
        m_slot_3 = gameObject.transform.Find("Slots (0)/$Slot_3/$scrollRect/$list_content").GetComponent<RectTransform>();
        m_slot_4 = gameObject.transform.Find("Slots (0)/$Slot_4/$scrollRect/$list_content").GetComponent<RectTransform>();
        m_slot_5 = gameObject.transform.Find("Slots (1)/$Slot_5/$scrollRect/$list_content").GetComponent<RectTransform>();
        m_slot_6 = gameObject.transform.Find("Slots (1)/$Slot_6/$scrollRect/$list_content").GetComponent<RectTransform>();
        m_slot_7 = gameObject.transform.Find("Slots (1)/$Slot_7/$scrollRect/$list_content").GetComponent<RectTransform>();
        m_slot_8 = gameObject.transform.Find("Slots (1)/$Slot_8/$scrollRect/$list_content").GetComponent<RectTransform>();
        m_slot_9 = gameObject.transform.Find("Slots (2)/$Slot_9/$scrollRect/$list_content").GetComponent<RectTransform>();
        m_slot_10 = gameObject.transform.Find("Slots (2)/$Slot_10/$scrollRect/$list_content").GetComponent<RectTransform>();
        m_slot_11 = gameObject.transform.Find("Slots (2)/$Slot_11/$scrollRect/$list_content").GetComponent<RectTransform>();
        m_slot_12 = gameObject.transform.Find("Slots (2)/$Slot_12/$scrollRect/$list_content").GetComponent<RectTransform>();

        m_scrollbar_1 = gameObject.transform.Find("Slots (0)/$Slot_1/ItemScroll").GetComponent<Scrollbar>();
        m_scrollbar_2 = gameObject.transform.Find("Slots (0)/$Slot_2/ItemScroll").GetComponent<Scrollbar>();
        m_scrollbar_3 = gameObject.transform.Find("Slots (0)/$Slot_3/ItemScroll").GetComponent<Scrollbar>();
        m_scrollbar_4 = gameObject.transform.Find("Slots (0)/$Slot_4/ItemScroll").GetComponent<Scrollbar>();
        m_scrollbar_5 = gameObject.transform.Find("Slots (1)/$Slot_5/ItemScroll").GetComponent<Scrollbar>();
        m_scrollbar_6 = gameObject.transform.Find("Slots (1)/$Slot_6/ItemScroll").GetComponent<Scrollbar>();
        m_scrollbar_7 = gameObject.transform.Find("Slots (1)/$Slot_7/ItemScroll").GetComponent<Scrollbar>();
        m_scrollbar_8 = gameObject.transform.Find("Slots (1)/$Slot_8/ItemScroll").GetComponent<Scrollbar>();
        m_scrollbar_9 = gameObject.transform.Find("Slots (2)/$Slot_9/ItemScroll").GetComponent<Scrollbar>();
        m_scrollbar_10 = gameObject.transform.Find("Slots (2)/$Slot_10/ItemScroll").GetComponent<Scrollbar>();
        m_scrollbar_11 = gameObject.transform.Find("Slots (2)/$Slot_11/ItemScroll").GetComponent<Scrollbar>();
        m_scrollbar_12 = gameObject.transform.Find("Slots (2)/$Slot_12/ItemScroll").GetComponent<Scrollbar>();
        
        m_allScrollbars.Add(m_scrollbar_1, 0f);
        m_allScrollbars.Add(m_scrollbar_2, 0f);
        m_allScrollbars.Add(m_scrollbar_3, 0f);
        m_allScrollbars.Add(m_scrollbar_4, 0f);
        m_allScrollbars.Add(m_scrollbar_5, 0f);
        m_allScrollbars.Add(m_scrollbar_6, 0f);
        m_allScrollbars.Add(m_scrollbar_7, 0f);
        m_allScrollbars.Add(m_scrollbar_8, 0f);
        m_allScrollbars.Add(m_scrollbar_9, 0f);
        m_allScrollbars.Add(m_scrollbar_10, 0f);
        m_allScrollbars.Add(m_scrollbar_11, 0f);
        m_allScrollbars.Add(m_scrollbar_12, 0f);
        
        m_allIcons.AddRange(m_slot_1.GetComponentsInChildren<Image>());
        m_allIcons.AddRange(m_slot_2.GetComponentsInChildren<Image>());
        m_allIcons.AddRange(m_slot_3.GetComponentsInChildren<Image>());
        m_allIcons.AddRange(m_slot_4.GetComponentsInChildren<Image>());
        m_allIcons.AddRange(m_slot_5.GetComponentsInChildren<Image>());
        m_allIcons.AddRange(m_slot_6.GetComponentsInChildren<Image>());
        m_allIcons.AddRange(m_slot_7.GetComponentsInChildren<Image>());
        m_allIcons.AddRange(m_slot_8.GetComponentsInChildren<Image>());
        m_allIcons.AddRange(m_slot_9.GetComponentsInChildren<Image>());
        m_allIcons.AddRange(m_slot_10.GetComponentsInChildren<Image>());
        m_allIcons.AddRange(m_slot_11.GetComponentsInChildren<Image>());
        m_allIcons.AddRange(m_slot_12.GetComponentsInChildren<Image>());

        foreach (var image in m_allIcons)
        {
            image.material = Assets.ItemMat;
        }
        
        m_lastIcons.Add(m_slot_1.Find("Item (0)/Icon").GetComponent<Image>());
        m_lastIcons.Add(m_slot_2.Find("Item (0)/Icon").GetComponent<Image>());
        m_lastIcons.Add(m_slot_3.Find("Item (0)/Icon").GetComponent<Image>());
        m_lastIcons.Add(m_slot_4.Find("Item (0)/Icon").GetComponent<Image>());
        m_lastIcons.Add(m_slot_5.Find("Item (0)/Icon").GetComponent<Image>());
        m_lastIcons.Add(m_slot_6.Find("Item (0)/Icon").GetComponent<Image>());
        m_lastIcons.Add(m_slot_7.Find("Item (0)/Icon").GetComponent<Image>());
        m_lastIcons.Add(m_slot_8.Find("Item (0)/Icon").GetComponent<Image>());
        m_lastIcons.Add(m_slot_9.Find("Item (0)/Icon").GetComponent<Image>());
        m_lastIcons.Add(m_slot_10.Find("Item (0)/Icon").GetComponent<Image>());
        m_lastIcons.Add(m_slot_11.Find("Item (0)/Icon").GetComponent<Image>());
        m_lastIcons.Add(m_slot_12.Find("Item (0)/Icon").GetComponent<Image>());
        
        GetEffects();
    }
    public void Update()
    {
        if (!m_roll) return;
        float dt = Time.deltaTime;

        List<Scrollbar> scrollbars = new List<Scrollbar>(m_allScrollbars.Keys);
        for (int index = 0; index < scrollbars.Count; ++index)
        {
            Scrollbar scrollbar = scrollbars[index];
            float startDelay = index * m_staggerDelay;

            if (Time.time >= startDelay)
            {
                float elapsedTime = m_allScrollbars[scrollbar] + dt;
                float time = Mathf.Clamp01(elapsedTime / m_duration);

                float ease = Mathf.SmoothStep(0f, 1f, time);

                m_allScrollbars[scrollbar] = elapsedTime;
                scrollbar.value = ease;
            }
        }
        
        if (AllScrollbarsFinished()) Stop();
    }

    public void GetEffects()
    {
        if (!ZNetScene.instance) return;
        m_successEffects.m_effectPrefabs = new List<EffectList.EffectData>()
        {
            new (){m_prefab = ZNetScene.instance.GetPrefab("sfx_coins_pile_destroyed")}
        }.ToArray();
        m_failedEffects.m_effectPrefabs = new List<EffectList.EffectData>()
        {
            new () {m_prefab = ZNetScene.instance.GetPrefab("sfx_haldor_laugh")}
        }.ToArray();
    }

    public void SetupReward(GambleSystem.GambleItem? item)
    {
        if (item is null)
        {
            ResetTooltips();
            SetTooltip($"\n<color=red>{Keys.NoGambleItems}</color>");
            SetIconsColor(false);
            return;
        }
        m_count = Random.Range(3, 5);
        SetRewardItem(item, m_count);
        TraderUI.m_instance.SetSelectionButtons(Keys.Roll, Keys.Roll);

    }

    public void SetTooltip(string tooltip) => m_infoText.text = Localization.m_instance.Localize(tooltip);
    public void SetItemText(string text) => m_itemText.text = Localization.m_instance.Localize(text);

    public void SetRewardIcon(Sprite? icon)
    {
        m_rewardIcon.sprite = icon;
        m_rewardIcon.color = icon is null ? Color.clear : Color.white;
    }

    public void ResetTooltips()
    {
        SetTooltip("");
        SetItemText("");
        TraderUI.m_instance.SetCurrencyIcon(null);
        TraderUI.m_instance.SetCurrentCurrency("");
        SetRewardIcon(m_defaultIcon);
    }

    private void ResizeTextRoots()
    {
        if (m_info.sizeDelta.y > m_infoText.preferredHeight)
        {
            m_infoRoot.offsetMin = Vector2.zero;
        }
        else
        {
            var difference = m_infoText.preferredHeight - m_info.sizeDelta.y + 10f;
            m_infoRoot.offsetMin = new Vector2(0f, -difference);
        }

        if (m_itemData.sizeDelta.y > m_itemText.preferredHeight)
        {
            m_itemDataRoot.offsetMin = Vector2.zero;
        }
        else
        {
            var difference = m_itemText.preferredHeight - m_itemData.sizeDelta.y + 10f;
            m_itemDataRoot.offsetMin = new Vector2(0f, -difference);
        }
    }

    private void SetRewardItem(GambleSystem.GambleItem item, int count)
    {
        m_item = item;
        SetRewardIcon(item.Icon);
        SetTooltip($"\n{Keys.RequiredMatches}: " + count);
        var itemData = item.ItemData.Clone();
        itemData.m_stack = item.Config.Amount;
        itemData.m_quality = item.Config.Quality;
        itemData.m_durability = itemData.GetMaxDurability();
        var title = item.Config.Amount > 1 ? $"{item.SharedName} x{item.Config.Amount}" : item.SharedName;
        SetItemText($"\n<color=orange>{title}</color>\n\n{itemData.GetTooltip()}");
        ResizeTextRoots();
        UpdateCurrentCurrency();
    }

    public void UpdateCurrentCurrency()
    {
        if (m_item is null) return;
        TraderUI.m_instance.SetCurrencyIcon(m_item.CurrencyIcon);
        TraderUI.m_instance.SetCurrentCurrency(Player.m_localPlayer.GetInventory().CountItems(m_item.CurrencySharedName).ToString());
    }

    public void Roll()
    {
        if (m_roll || m_item is null) return;
        if (m_item.Completed) return;
        if (Player.m_localPlayer.GetInventory().CountItems(m_item.CurrencySharedName) < m_item.Config.Price) return;
        Player.m_localPlayer.GetInventory().RemoveItem(m_item.CurrencySharedName, m_item.Config.Price);
        SetIconsColor(true);
        LoadRandomIcons(new(){m_item.Config.PrefabName});
        if (Random.Range(0f, 100f) <= m_item.Config.SuccessChance)
        {
            SetupSuccess();
        }
        else
        {
            SetupFail();
        }
        List<Scrollbar> scrollbars = new List<Scrollbar>(m_allScrollbars.Keys);
        float accumulatedDelay = 0f;

        foreach (var scrollbar in scrollbars)
        {
            float randomDelay = Random.Range(0.1f, m_staggerDelay);
            m_allScrollbars[scrollbar] = -accumulatedDelay;
            accumulatedDelay += randomDelay;
        }

        UpdateCurrentCurrency();
        m_roll = true;
    }

    public void Stop()
    {
        m_roll = false;
        if (!m_success) OnFailed();
        else OnSuccess();
    }

    public void Reset()
    {
        Stop();
        m_count = 0;
        m_item = null;
        foreach (var scrollbar in m_allScrollbars.Keys)
        {
            m_allScrollbars[scrollbar] = 0f;
            scrollbar.value = 0f;
        }
    }

    public void OnFailed()
    {
        if (StoreGui.m_instance.m_trader is null || m_item is null) return;
        m_failedEffects.Create(StoreGui.m_instance.m_trader.transform.position, Quaternion.identity);
        SetIconsColor(false);
        m_item.Completed = false;
        Player.m_localPlayer.Message(MessageHud.MessageType.Center, Keys.OnFail);
    }

    public void OnSuccess()
    {
        if (StoreGui.m_instance.m_trader is null || m_item is null) return;
        m_successEffects.Create(StoreGui.m_instance.m_trader.transform.position, Quaternion.identity);
        SetIconsColor(false);
        m_item.Completed = true;
        TraderUI.m_instance.SetSelectionButtons(Keys.Collect, Keys.Collect);
        Player.m_localPlayer.Message(MessageHud.MessageType.Center, Keys.OnSuccess);
    }

    private void SetIconsColor(bool reset)
    {
        if (reset)
        {
            foreach (var icon in m_allIcons)
            {
                icon.color = Color.white;
            }
        }
        else
        {
            foreach (var icon in m_allIcons)
            {
                icon.color = Color.black;
            }

            foreach (var icon in m_matches)
            {
                icon.color = Color.white;
            }
        }
    }

    private bool AllScrollbarsFinished()
    {
        foreach (var progress in m_allScrollbars.Values)
        {
            if (progress < m_duration) return false;
        }
        return true;
    }

    public void LoadRandomIcons() => LoadRandomIcons(new());

    public void LoadRandomIcons(List<string> invalidPrefabs)
    {
        var items = GetAvailableIcons(invalidPrefabs);
        foreach (var icon in m_allIcons)
        {
            icon.sprite = items[Random.Range(0, items.Count)];
        }
    }

    public void SetupSuccess()
    {
        if (m_item is null) return;
        m_matches.Clear();
        HashSet<int> setIndexes = new(); // Ensure unique indexes
        for (int index = 0; index < m_count; ++index)
        {
            int i;
            do
            {
                i = Random.Range(0, m_lastIcons.Count);
            } while (setIndexes.Contains(i)); // Ensure unique selection

            setIndexes.Add(i);
            m_lastIcons[i].sprite = m_item.Icon;
            m_matches.Add(m_lastIcons[i]);
        }

        m_success = true;
    }

    public void SetupFail()
    {
        if (m_item is null) return;
        m_matches.Clear();
        HashSet<int> setIndexes = new(); // Ensure unique indexes
        for (int index = 0; index < Random.Range(0, m_count - 1); ++index)
        {
            int i;
            do
            {
                i = Random.Range(0, m_lastIcons.Count);
            } while (setIndexes.Contains(i)); // Ensure unique selection

            setIndexes.Add(i);
            m_lastIcons[i].sprite = m_item.Icon;
            m_matches.Add(m_lastIcons[i]);
        }

        m_success = false;
    }

    private static List<Sprite> GetAvailableIcons(List<string> invalidPrefabs)
    {
        if (!ObjectDB.m_instance) return new();
        List<Sprite> sprites = new();
        foreach (GameObject prefab in ObjectDB.m_instance.m_items.Where(item =>
                     !invalidPrefabs.Contains(item.name) && item.TryGetComponent(out ItemDrop component) &&
                     component.m_itemData.m_shared.m_icons.Length > 0))
        {
            sprites.Add(prefab.GetComponent<ItemDrop>().m_itemData.GetIcon());
        }

        return sprites;
    }
}