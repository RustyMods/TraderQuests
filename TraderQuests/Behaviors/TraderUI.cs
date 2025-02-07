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
            m_item.AddComponent<ItemUI>();
        }
        else
        {
            TraderQuestsPlugin.TraderQuestsLogger.LogWarning("Quest Item is null");
        }
    }

    [Description("Headers")]
    public Text m_topic = null!;
    public Text m_activeText = null!;
    public Text m_currencyText = null!;
    public Image m_currencyImage = null!;
    
    public Text m_bountyButtonText = null!;
    public Text m_treasureButtonText = null!;
    public Text m_shopButtonText = null!;
    public Text m_gambleButtonText = null!;
    public Text m_selectButtonText = null!;
    public Text m_cancelButtonText = null!;
    public Text m_tooltip = null!;

    [Description("Root scrollbar containers")]
    public RectTransform m_listRoot = null!;
    public RectTransform m_activeRoot = null!;
    public RectTransform m_tooltipRoot = null!;

    [Description("Tabs")] 
    public Button m_bountyButton = null!;
    public Button m_treasureButton = null!;
    public Button m_shopButton = null!;
    public Button m_gambleButton = null!;

    public GameObject m_itemList = null!;
    public GameObject m_tooltipPanel = null!;
    public GameObject m_gamblePanel = null!;

    public Button m_selectButton = null!;
    public Button m_cancelButton = null!;
    
    public string CurrentTopic = "$button_bounty";

    public void Awake()
    {
        m_instance = this;

        m_itemList = gameObject.transform.Find("Panel/ItemList").gameObject;
        m_tooltipPanel = gameObject.transform.Find("Panel/TooltipPanel").gameObject;
        m_gamblePanel = gameObject.transform.Find("GamblePanel").gameObject;

        m_gamblePanel.AddComponent<GambleUI>();

        m_topic = Utils.FindChild(gameObject.transform, "$text_topic").GetComponent<Text>();
        m_bountyButtonText = Utils.FindChild(gameObject.transform, "$text_bounty").GetComponent<Text>();
        m_treasureButtonText = Utils.FindChild(gameObject.transform, "$text_treasure").GetComponent<Text>();
        m_gambleButtonText = Utils.FindChild(gameObject.transform, "$text_gamble").GetComponent<Text>();
        m_activeText = Utils.FindChild(gameObject.transform, "$text_current").GetComponent<Text>();
        m_tooltip = Utils.FindChild(gameObject.transform, "$text_tooltip").GetComponent<Text>();
        m_selectButtonText = Utils.FindChild(gameObject.transform, "$text_select").GetComponent<Text>();
        m_cancelButtonText = Utils.FindChild(gameObject.transform, "$text_cancel").GetComponent<Text>();
        m_shopButtonText = Utils.FindChild(gameObject.transform, "$text_shop").GetComponent<Text>();
        m_currencyText = Utils.FindChild(gameObject.transform, "$text_currency").GetComponent<Text>();
        m_currencyImage = Utils.FindChild(gameObject.transform, "$image_currency").GetComponent<Image>();
            
        m_topic.color = new Color32(255, 164, 0, 255);
        m_activeText.color = new Color32(255, 164, 0, 255);
        m_currencyText.color = new Color32(255, 205, 0, 255);
        
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
        m_gambleButtonText.text = Localization.instance.Localize(Keys.Gamble);
        
        UpdatePanelPosition();
        SetFont();
        SetupAssets();
        SetupButtons();
    }
    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);

    public void ShowGamble(bool enable)
    {
        if (enable)
        {
            m_itemList.SetActive(false);
            m_tooltipPanel.SetActive(false);
            m_gamblePanel.SetActive(true);
            GambleUI.m_instance.LoadRandomIcons();
            GambleUI.m_instance.ResetTooltips();
            GambleUI.m_instance.SetupReward(GambleSystem.GetItem());
            m_selectButton.gameObject.SetActive(false);
            
            
        }
        else
        {
            m_itemList.SetActive(true);
            m_tooltipPanel.SetActive(true);
            m_gamblePanel.SetActive(false);
            m_selectButton.gameObject.SetActive(true);
        }
    }
    public void SetCurrentCurrency(string text) => m_currencyText.text = text;
    public void SetCurrencyIcon(Sprite? icon)
    {
        m_currencyImage.color = icon is null ? Color.clear : Color.white;
        m_currencyImage.sprite = icon;
    }
    public void UpdatePanelPosition()
    {
        if (gameObject.transform is RectTransform rect)
        {
            rect.anchoredPosition = TraderQuestsPlugin.PanelPosition.Value;
        }
    }
    
    public void SetFont()
    {
        string fontOption = TraderQuestsPlugin.Font.Value switch
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
    
    private void SetupAssets()
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
        m_currencyImage.material = Assets.ItemMat;
    }
    
    private void SetupButtons()
    {
        foreach (Button button in gameObject.GetComponentsInChildren<Button>())
        {
            switch (button.name)
            {
                case "$button_bounty" or "$button_treasure" or "$button_shop" or "$button_gamble":
                    switch (button.name)
                    {
                        case "$button_bounty":
                            m_bountyButton = button;
                            break;
                        case "$button_treasure":
                            m_treasureButton = button;
                            break;
                        case "$button_shop":
                            m_shopButton = button;
                            break;
                        case "$button_gamble":
                            m_gambleButton = button;
                            break;
                    }
                    button.onClick.AddListener(() =>
                    {
                        CurrentTopic = button.name;
                        UpdatePanel();
                    });
                    break;
                case "$button_select":
                    m_selectButton = button;
                    button.onClick.AddListener(OnSelect);
                    break;
                case "$button_cancel":
                    m_cancelButton = button;
                    button.onClick.AddListener(OnCancel);
                    break;
                case "$button_refresh":
                    button.onClick.AddListener(OnRefresh);
                    break;
            }

            button.gameObject.AddComponent<ButtonSfx>().m_sfxPrefab = Assets.ButtonSFX.m_sfxPrefab;
        }

        SetDefaultButtonTextColor();
    }
    private void OnSelect()
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
            case "$button_gamble":
                if (GambleUI.m_instance.m_item is null) return;
                if (!GambleUI.m_instance.m_item.CollectReward()) return;
                break;
        }

        UpdatePanel();
    }

    private void OnCancelBounty()
    {
        if (BountySystem.SelectedActiveBounty is null) return;
        if (BountySystem.SelectedActiveBounty.IsComplete())
        {
            BountySystem.SelectedActiveBounty.CollectReward(Player.m_localPlayer);
        }
        else
        {
            if (!BountySystem.SelectedActiveBounty.Deactivate(TraderQuestsPlugin.BountyReturnCost.Value is TraderQuestsPlugin.Toggle.On)) return;
        }
        BountySystem.SelectedActiveBounty = null;
        UpdatePanel();
    }

    private void OnCancelTreasure()
    {
        if (TreasureSystem.SelectedActiveTreasure is null) return;
        if (!TreasureSystem.SelectedActiveTreasure.Deactivate(TraderQuestsPlugin.TreasureReturnCost.Value is TraderQuestsPlugin.Toggle.On)) return;
        TreasureSystem.SelectedActiveTreasure = null;
        UpdatePanel();
    }

    private void OnPurchaseSale()
    {
        if (Shop.SelectedSaleItem is null) return;
        if (!Shop.SelectedSaleItem.Purchase(true)) return;
        UpdatePanel();
    }

    private void OnGamble()
    {
        if (GambleUI.m_instance.m_item is null || !GambleUI.m_instance.m_item.Completed)
        {
            GambleUI.m_instance.Roll();
        }
        else
        {
            if (!GambleUI.m_instance.m_item.CollectReward()) return;
        }
    }

    private void OnCancel()
    {
        switch (CurrentTopic)
        {
            case "$button_bounty":
                OnCancelBounty();
                break;
            case "$button_treasure":
                OnCancelTreasure();
                break;
            case "$button_shop":
                OnPurchaseSale();
                break;
            case "$button_gamble":
                OnGamble();
                break;
        }
    }

    private void OnRefresh()
    {
        UpdateTopic();
        UpdatePanel();
    }
    
    public void SetCancelButtonColor(bool enable) => m_cancelButtonText.color = enable ? new Color32(255, 164, 0, 255) : Color.gray;
    public void setSelectButtonColor(bool enable) => m_selectButtonText.color = enable ? new Color32(255, 164, 0, 255) : Color.gray;
    public void SetDefaultButtonTextColor()
    {
        m_bountyButtonText.color = new Color32(255, 164, 0, 255);
        m_treasureButtonText.color = new Color32(255, 164, 0, 255);
        m_shopButtonText.color = new Color32(255, 164, 0, 255);
        m_gambleButtonText.color = new Color32(255, 164, 0, 255);
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
        SetCurrencyIcon(null);
        SetCurrentCurrency("");
        UpdateTabs();
        ShowGamble(CurrentTopic == "$button_gamble");
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

    public void UpdateTabs()
    {
        m_bountyButton.gameObject.SetActive(TraderQuestsPlugin.BountyEnabled.Value is TraderQuestsPlugin.Toggle.On);
        m_treasureButton.gameObject.SetActive(TraderQuestsPlugin.TreasureEnabled.Value is TraderQuestsPlugin.Toggle.On);
        m_shopButton.gameObject.SetActive(TraderQuestsPlugin.StoreEnabled.Value is TraderQuestsPlugin.Toggle.On);
        m_gambleButton.gameObject.SetActive(TraderQuestsPlugin.GambleEnabled.Value is TraderQuestsPlugin.Toggle.On);
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
            case "$button_gamble":
                SetTopic(Keys.SlotMachine);
                SetSecondTopic("");
                SetSelectionButtons(Keys.Collect, Keys.Roll);
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