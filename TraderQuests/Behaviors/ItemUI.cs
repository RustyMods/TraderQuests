using UnityEngine;
using UnityEngine.UI;

namespace TraderQuests.Behaviors;

public class ItemUI : MonoBehaviour
{
    public Text m_name = null!;
    public Image m_icon = null!;
    public Image m_currency = null!;
    public Text m_price = null!;
    public GameObject m_selected = null!;
    public Image m_selectedImage = null!;
    public Button m_button = null!;

    public void Awake()
    {
        m_name = Utils.FindChild(transform, "$text_name").GetComponent<Text>();
        m_icon = Utils.FindChild(transform, "$image_icon").GetComponent<Image>();
        m_currency = Utils.FindChild(transform, "$image_currency").GetComponent<Image>();
        m_price = Utils.FindChild(transform, "$text_currency").GetComponent<Text>();
        m_selected = Utils.FindChild(transform, "$image_selected").gameObject;
        m_selectedImage = m_selected.GetComponent<Image>();
        m_button = GetComponent<Button>();
    }

    public void SetName(string text, bool active)
    {
        m_name.text = Localization.instance.Localize(text);
        m_name.color = active ? new Color32(255, 255, 255, 255) : new Color32(150, 150, 150, 255);
    }

    public void SetIcon(Sprite icon, bool active)
    {
        m_icon.sprite = icon;
        m_icon.color = active ? Color.white : Color.gray;
    }

    public void SetCurrency(Sprite icon, bool active)
    {
        m_currency.sprite = icon;
        m_currency.color = active ? Color.white : Color.gray;
    }

    public void SetPrice(string text, bool active)
    {
        m_price.text = text;
        m_price.color = active ? new Color32(255, 164, 0, 255) : new Color32(150, 150, 150, 255);
    }

    public void SetSelected(bool active)
    {
        m_selectedImage.color = active ? new Color32(255, 164, 0, 255) : new Color32(255, 164, 0, 200);
    }

    public void OnSelected(bool active)
    {
        m_selected.SetActive(enabled);
    }
}