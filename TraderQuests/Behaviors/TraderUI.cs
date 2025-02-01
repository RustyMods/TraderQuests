using System.ComponentModel;
using System.Linq;
using HarmonyLib;
using TraderQuests.Quest;
using TraderQuests.translations;
using TraderQuests.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TraderQuests.Behaviors;

public class TraderUI : MonoBehaviour
{
    private static GameObject? m_questUI;
    public static GameObject? m_item;
    public enum FontOptions
    {
        Norse, AveriaSerifLibre  
    }
    
    public static TraderUI m_instance = null!;
    
    public static void LoadAssets()
    {
        if (TraderQuestsPlugin.Assets.LoadAsset<GameObject>("QuestGUI") is { } panel)
        {
            m_questUI = panel;
        }
        else
        {
            TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Quest GUI is null");
        }

        if (TraderQuestsPlugin.Assets.LoadAsset<GameObject>("QuestItem") is { } item)
        {
            m_item = item;
        }
        else
        {
            TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Quest Item is null");
        }
    }

    [Description("Headers")]
    public Text m_topic = null!;
    public Text m_activeText = null!;
    
    [Description("Button text components")]
    public Text m_bountyButtonText = null!;
    public Text m_treasureButtonText = null!;
    public Text m_shopButtonText = null!;
    public Text m_selectButtonText = null!;
    public Text m_cancelButtonText = null!;

    public Text m_tooltip = null!;

    [Description("Root scrollbar containers")]
    public RectTransform m_listRoot = null!;
    public RectTransform m_activeRoot = null!;
    public RectTransform m_tooltipRoot = null!;

    public string CurrentTopic = "$button_bounty";

    public void Awake()
    {
        m_instance = this;

        m_topic = Utils.FindChild(gameObject.transform, "$text_topic").GetComponent<Text>();
        m_bountyButtonText = Utils.FindChild(gameObject.transform, "$text_bounty").GetComponent<Text>();
        m_treasureButtonText = Utils.FindChild(gameObject.transform, "$text_treasure").GetComponent<Text>();
        m_activeText = Utils.FindChild(gameObject.transform, "$text_current").GetComponent<Text>();
        m_tooltip = Utils.FindChild(gameObject.transform, "$text_tooltip").GetComponent<Text>();
        m_selectButtonText = Utils.FindChild(gameObject.transform, "$text_select").GetComponent<Text>();
        m_cancelButtonText = Utils.FindChild(gameObject.transform, "$text_cancel").GetComponent<Text>();
        m_shopButtonText = Utils.FindChild(gameObject.transform, "$text_shop").GetComponent<Text>();
            
        m_topic.color = new Color32(255, 164, 0, 255);
        m_activeText.color = new Color32(255, 164, 0, 255);
        
        if (Utils.FindChild(gameObject.transform, "$list_content") is RectTransform { } list)
        {
            m_listRoot = list;
        }
        if (Utils.FindChild(gameObject.transform, "$list_current") is RectTransform { } current)
        {
            m_activeRoot = current;
        }
        if (Utils.FindChild(gameObject.transform, "$list_tooltip") is RectTransform { } tooltip)
        {
            m_tooltipRoot = tooltip;
        }

        m_bountyButtonText.text = Localization.instance.Localize(Keys.Bounty);
        m_treasureButtonText.text = Localization.instance.Localize(Keys.Treasure);
        m_shopButtonText.text = Localization.instance.Localize(Keys.Shop);
        
        UpdatePanelPosition();
        SetFont();
        SetAssets();
        SetButtons();
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
    
    public void UpdatePanelPosition()
    {
        if (gameObject.transform is RectTransform rect)
        {
            rect.anchoredPosition = TraderQuestsPlugin.PanelPosition.Value;
        }
    }
    
    public void SetFont()
    {
        var fontOption = TraderQuestsPlugin.Font.Value switch
        {   
            FontOptions.Norse => "Norse",
            FontOptions.AveriaSerifLibre => "AveriaSerifLibre-Regular",
            _ => "AveriaSerifLibre-Regular"
        };
        if (GetFont(fontOption) is not { } font) return;
        foreach (Text text in gameObject.GetComponentsInChildren<Text>(true))
        {
            text.font = font;
        }

        if (m_item is not null)
        {
            foreach (Text text in m_item.GetComponentsInChildren<Text>(true))
            {
                text.font = font;
            }
        }
    }
    
    private static Font? GetFont(string name)
    {
        Font[]? fonts = Resources.FindObjectsOfTypeAll<Font>();
        return fonts.FirstOrDefault(x => x.name == name);
    }
    
    private void SetAssets()
    {
        var panel = gameObject.transform.Find("Panel").GetComponent<Image>();
        panel.sprite = Assets.WoodPanel_512x512;
        panel.material = Assets.LitPanel;
        foreach (var button in gameObject.GetComponentsInChildren<Button>())
        {
            if (button.TryGetComponent(out Image component))
            {
                component.sprite = Assets.ButtonImage;
            }

            var state = button.spriteState;
            state.highlightedSprite = Assets.BuyButton.spriteState.highlightedSprite;
            state.pressedSprite = Assets.BuyButton.spriteState.pressedSprite;
            state.selectedSprite = Assets.BuyButton.spriteState.selectedSprite;
            state.disabledSprite = Assets.BuyButton.spriteState.disabledSprite;
            button.spriteState = state;
        }

        Utils.FindChild(gameObject.transform, "$image_content").GetComponent<Image>().sprite = Assets.ListBackground;
        foreach (var scrollbar in gameObject.GetComponentsInChildren<Scrollbar>())
        {
            if (scrollbar.TryGetComponent(out Image component))
            {
                component.sprite = Assets.ScrollBackground;
            }

            scrollbar.transform.Find("Sliding Area/Handle").GetComponent<Image>().sprite = Assets.ScrollHandle;
        }

        gameObject.transform.Find("Panel/TooltipPanel/ItemList/Content").GetComponent<Image>().sprite = Assets.ListBackground;
        gameObject.transform.Find("Panel/TooltipPanel/Tooltip/Content").GetComponent<Image>().sprite = Assets.ListBackground;

        Utils.FindChild(gameObject.transform, "$button_refresh").Find("Icon").GetComponent<Image>().sprite = Assets.RefreshImage;
        
        if (m_item is null) return;
        m_item.transform.Find("Border").GetComponent<Image>().sprite = Assets.ListBackground;
        m_item.transform.Find("$image_selected").GetComponent<Image>().sprite = Assets.ListBackground;
        m_item.transform.Find("$image_icon").GetComponent<Image>().material = Assets.ItemMat;
        m_item.transform.Find("Price/$image_currency").GetComponent<Image>().material = Assets.ItemMat;
    }
    
    private void SetButtons()
    {
        foreach (Button button in gameObject.GetComponentsInChildren<Button>())
        {
            switch (button.name)
            {
                case "$button_bounty" or "$button_treasure" or "$button_shop":
                    button.onClick.AddListener(() =>
                    {
                        CurrentTopic = button.name;
                        UpdateTopic();
                        UpdatePanel();
                    });
                    break;
                case "$button_select":
                    button.onClick.AddListener(() =>
                    {
                        switch (CurrentTopic)
                        {
                            case "$button_bounty":
                                if (BountySystem.SelectedBounty is null) return;
                                if (!BountySystem.SelectedBounty.Activate()) return;
                                break;
                            case "$button_treasure":
                                if (TreasureSystem.SelectedTreasure is null) return;
                                if (!TreasureSystem.SelectedTreasure.Activate()) return;
                                break;
                            case "$button_shop":
                                if (Shop.SelectedItem is null) return;
                                if (!Shop.SelectedItem.Purchase(false)) return;
                                break;
                        }

                        UpdatePanel();
                    });
                    break;
                case "$button_cancel":
                    button.onClick.AddListener(() =>
                    {
                        switch (CurrentTopic)
                        {
                            case "$button_bounty":
                                if (BountySystem.SelectedActiveBounty is null) return;
                                if (BountySystem.SelectedActiveBounty.IsComplete())
                                {
                                    BountySystem.SelectedActiveBounty.CollectReward(Player.m_localPlayer);
                                    BountySystem.CompletedBounties[BountySystem.SelectedActiveBounty.Config.UniqueID] = BountySystem.SelectedActiveBounty.CompletedOn;
                                }
                                else
                                {
                                    BountySystem.AvailableBounties[BountySystem.SelectedActiveBounty.Config.UniqueID] = BountySystem.SelectedActiveBounty;
                                    // return cost
                                }

                                BountySystem.ActiveBounties.Remove(BountySystem.SelectedActiveBounty.Config.UniqueID);
                                BountySystem.SelectedActiveBounty = null;
                                BountySystem.UpdateMinimap();
                                break;
                            case "$button_treasure":
                                if (TreasureSystem.SelectedActiveTreasure is null) return;
                                TreasureSystem.ActiveTreasures.Remove(TreasureSystem.SelectedActiveTreasure.Config.UniqueID);
                                TreasureSystem.AvailableTreasures[TreasureSystem.SelectedActiveTreasure.Config.UniqueID] = TreasureSystem.SelectedActiveTreasure;
                                TreasureSystem.SelectedActiveTreasure = null;
                                TreasureSystem.UpdateMinimap();
                                break;
                            case "$button_shop":
                                if (Shop.SelectedSaleItem is null) return;
                                Shop.SelectedSaleItem.Purchase(true);
                                break;
                        }
                        UpdatePanel();
                    });
                    break;
                case "$button_refresh":
                    button.onClick.AddListener(() =>
                    {
                        UpdateTopic();
                        UpdatePanel();
                    });
                    break;
            }
        }

        m_bountyButtonText.color = new Color32(255, 164, 0, 255);
        m_treasureButtonText.color = new Color32(255, 164, 0, 255);
        m_shopButtonText.color = new Color32(255, 164, 0, 255);
        m_selectButtonText.color = new Color32(255, 164, 0, 255);
        m_cancelButtonText.color = new Color32(255, 164, 0, 255);
    }
    
    private void SetTopic(string topic) => m_topic.text = Localization.instance.Localize(topic);
    public void SetTooltip(string tooltip)
    {
        m_tooltip.text = Localization.instance.Localize(tooltip);
        
        if (m_tooltip.transform.parent.parent.parent.transform is RectTransform rect)
        {
            var contentHeight = rect.sizeDelta.y;
            if (m_tooltip.preferredHeight < contentHeight)
            {
                m_tooltipRoot.offsetMin = Vector2.zero;
            }
            else
            {
                var difference = m_tooltip.preferredHeight - contentHeight + 10f;
                m_tooltipRoot.offsetMin = new Vector2(0f, -difference);
            }
        }
    }
    private void SetSecondTopic(string text) => m_activeText.text = Localization.instance.Localize(text);
    
    public void UpdatePanel()
    {
        UpdateTopic();
        DestroyItems();
        SetTooltip("");
        switch (CurrentTopic)
        {
            case "$button_bounty":
                BountySystem.LoadAvailable();
                BountySystem.LoadActive();
                break;
            case "$button_treasure":
                TreasureSystem.LoadAvailable();
                TreasureSystem.LoadActive();
                break;
            case "$button_shop":
                Shop.LoadAvailable();
                break;
        }
    }
    
    private void UpdateTopic()
    {
        switch (CurrentTopic)
        {
            case "$button_bounty":
                SetTopic(Keys.AvailableBounty);
                SetSecondTopic(Keys.ActiveBounty);
                SetSelectionButtons(Keys.Select, Keys.Cancel);
                break;
            case "$button_treasure":
                SetTopic(Keys.AvailableTreasure);
                SetSecondTopic(Keys.ActiveTreasure);
                SetSelectionButtons(Keys.Select, Keys.Cancel);

                break;
            case "$button_shop":
                SetTopic(Keys.AvailableItems);
                SetSecondTopic(Keys.OnSaleItems);
                SetSelectionButtons(Keys.Buy, Keys.Select);
                break;
        }
    }

    public void SetSelectionButtons(string select, string cancel)
    {
        m_selectButtonText.text = Localization.instance.Localize(select);
        m_cancelButtonText.text = Localization.instance.Localize(cancel);
    }

    public void DestroyItems()
    {
        foreach (Transform child in m_listRoot) Destroy(child.gameObject);
        foreach (Transform child in m_activeRoot) Destroy(child.gameObject);
    }
    
    public void DeselectAll()
    {
        foreach (Transform child in m_listRoot)
        {
            child.Find("$image_selected").gameObject.SetActive(false);
        }

        foreach (Transform child in m_activeRoot)
        {
            child.Find("$image_selected").gameObject.SetActive(false);
        }
    }
    
    public void ResizeListRoot(int childCount)
    {
        if (m_item is null) return;
        if (m_item.transform is not RectTransform itemRect) return;
        if (m_listRoot.parent.parent.transform is not RectTransform listRect) return;

        var height = (itemRect.sizeDelta.y + 1) * childCount;
        var contentHeight = listRect.sizeDelta.y;
        
        if (contentHeight > height) m_listRoot.offsetMin = Vector2.zero;
        else
        {
            var difference = height - contentHeight;
            m_listRoot.offsetMin = new Vector2(0f, -difference);
        }
    }

    public void ResizeActiveListRoot(int childCount)
    {
        if (m_item is null) return;
        if (m_item.transform is not RectTransform itemRect) return;
        if (m_activeRoot.parent.parent.transform is not RectTransform listRect) return;

        var height = (itemRect.sizeDelta.y + 1) * childCount;
        var contentHeight = listRect.sizeDelta.y;
        
        if (contentHeight > height) m_activeRoot.offsetMin = Vector2.zero;
        else
        {
            var difference = height - contentHeight;
            m_activeRoot.offsetMin = new Vector2(0f, -difference);
        }
    }
    
    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.Awake))]
    private static class StoreGUI_Awake_Patch
    {
        private static void Postfix(StoreGui __instance)
        {
            Assets.CacheAssets(__instance);
            GameObject? panel = Instantiate(m_questUI, __instance.transform);
            if (panel is null) return;
            TraderUI? component = panel.AddComponent<TraderUI>();
            component.Hide();
        }
    }
    
    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.Show))]
    private static class StoreGUI_Show_Patch
    {
        private static void Postfix(Trader trader)
        {
            if (!m_instance) return;
            switch (TraderQuestsPlugin.AffectedTraders.Value)
            {
                case TraderQuestsPlugin.Traders.None:
                    return;
                case TraderQuestsPlugin.Traders.Haldor:
                    if (trader.m_name != "$npc_haldor") return;
                    break;
                case TraderQuestsPlugin.Traders.Hildir:
                    if (trader.m_name != "$npc_hildir") return;
                    break;
                case TraderQuestsPlugin.Traders.Custom:
                    if (trader.name.Replace("(Clone)", string.Empty) != TraderQuestsPlugin.CustomTrader.Value) return;
                    break;
            }
            m_instance.Show();
            m_instance.UpdatePanel();
        }
    }

    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.Hide))]
    private static class StoreGUI_Hide_Patch
    {
        private static void Postfix()
        {
            if (!m_instance) return;
            m_instance.Hide();
            m_instance.DestroyItems();
        }
    }
}