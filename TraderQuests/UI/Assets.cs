using UnityEngine;
using UnityEngine.UI;

namespace TraderQuests.UI;

public static class Assets
{
    public static Sprite WoodPanel_512x512 = null!;
    public static Material LitPanel = null!;
    public static Material ItemMat = null!;
    public static Sprite ButtonImage = null!;
    public static Button BuyButton = null!;
    public static Sprite ScrollBackground = null!;
    public static Sprite ScrollHandle = null!;
    public static Sprite ListBackground = null!;
    public static Sprite RefreshImage = null!;

    public static void CacheAssets(StoreGui store)
    {
        var background = Utils.FindChild(store.transform, "border (1)").GetComponent<Image>();
        WoodPanel_512x512 = background.sprite;
        LitPanel = background.material;
        var button = Utils.FindChild(store.transform, "BuyButton");
        ButtonImage = button.GetComponent<Image>().sprite;
        BuyButton = button.GetComponent<Button>();
        ScrollBackground = Utils.FindChild(store.transform, "ItemScroll").GetComponent<Image>().sprite;
        ScrollHandle = Utils.FindChild(store.transform, "Handle").GetComponent<Image>().sprite;
        ItemMat = store.transform.Find("Store/SellPanel/SellButton/Image").GetComponent<Image>().material;
        ListBackground = Utils.FindChild(store.transform, "Items").GetComponent<Image>().sprite;
        RefreshImage = store.transform.Find("Store/SellPanel/SellButton/Image (1)").GetComponent<Image>().sprite;
    }
}