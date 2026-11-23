using UnityEngine;

public enum HintItemType
{
    Crowbar,
    Keycard,
    Cloak,
    Light,
    HeightTip,
    Power
}

public class PlayerItemHintPopup : MonoBehaviour
{
    public static PlayerItemHintPopup Instance { get; private set; }

    [Header("Auto-Wired Hierarchy Children")]
    [SerializeField] private GameObject bubbleBackground;
    [SerializeField] private GameObject crowbarObj;
    [SerializeField] private GameObject keycardObj;
    [SerializeField] private GameObject cloakObj;
    [SerializeField] private GameObject lightObj;
    [SerializeField] private GameObject heightTipObj;
    [SerializeField] public static bool HintsEnabled;

    [Header("Floating Animation")]
    [SerializeField] private float bobSpeed = 3.5f;
    [SerializeField] private float bobAmount = 0.05f;

    private Vector3 initialLocalPos;
    private Vector3 initialLocalScale;
    private Object activeSource;

    private void Awake()
    {
        Instance = this;
        initialLocalPos = transform.localPosition;
        initialLocalScale = transform.localScale;

        if (bubbleBackground == null) bubbleBackground = FindChildSafe("BubbleBackground");
        if (crowbarObj == null) crowbarObj = FindChildSafe("Crowbar");
        if (keycardObj == null) keycardObj = FindChildSafe("Keycard");
        if (cloakObj == null) cloakObj = FindChildSafe("Cloak");
        if (lightObj == null) lightObj = FindChildSafe("light");
        if (heightTipObj == null) heightTipObj = FindChildSafe("HeightTip");

        HideAllImmediate();
    }

    private void LateUpdate()
    {
        if (activeSource == null) return;

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        transform.localPosition = initialLocalPos + new Vector3(0f, bob, 0f);

        if (transform.parent != null)
        {
            float parentSign = Mathf.Sign(transform.parent.lossyScale.x);
            transform.localScale = new Vector3(
                Mathf.Abs(initialLocalScale.x) * parentSign,
                initialLocalScale.y,
                initialLocalScale.z
            );
        }
    }

    public void ShowHint(HintItemType itemType, Object source)
    {
        if (activeSource == source && bubbleBackground != null && bubbleBackground.activeSelf)
            return;

        activeSource = source;
        HideAllIconsOnly();

        if (bubbleBackground != null) bubbleBackground.SetActive(true);

        GameObject targetIcon = GetItemObject(itemType);
        if (targetIcon != null) targetIcon.SetActive(true);
    }

    public void HideHint(Object source)
    {
        if (activeSource == source || source == null)
        {
            activeSource = null;
            HideAllImmediate();
        }
    }

    private GameObject GetItemObject(HintItemType type)
    {
        switch (type)
        {
            case HintItemType.Crowbar: return crowbarObj;
            case HintItemType.Keycard: return keycardObj;
            case HintItemType.Cloak: return cloakObj;
            case HintItemType.Light: return lightObj;
            case HintItemType.HeightTip: return heightTipObj;
            default: return null;
        }
    }

    private void HideAllIconsOnly()
    {
        if (crowbarObj != null) crowbarObj.SetActive(false);
        if (keycardObj != null) keycardObj.SetActive(false);
        if (cloakObj != null) cloakObj.SetActive(false);
        if (lightObj != null) lightObj.SetActive(false);
        if (heightTipObj != null) heightTipObj.SetActive(false);
    }

    private void HideAllImmediate()
    {
        if (bubbleBackground != null) bubbleBackground.SetActive(false);
        HideAllIconsOnly();
    }

    private GameObject FindChildSafe(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.gameObject : null;
    }
}