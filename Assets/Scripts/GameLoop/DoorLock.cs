using UnityEngine;

/// A story lock for a DoorExit on the same GameObject. While it is locked, pressing A at the door
/// only shows the message. Examples:
///   Lobby -> SurgicalWing : needs power (the lever in Electrical).
///   Lobby -> office       : needs the key card selected in the hotbar.
public class DoorLock : MonoBehaviour
{
    public enum Requirement { StoryFlag, SelectedItem }

    [SerializeField] private Requirement requirement = Requirement.StoryFlag;
    [Tooltip("Used with Requirement = StoryFlag.")]
    [SerializeField] private StoryFlag flag = StoryFlag.PowerOn;
    [Tooltip("Used with Requirement = SelectedItem: the hotbar item that must be selected (its name).")]
    [SerializeField] private string itemName = "Card";
    [SerializeField] private string lockedMessage = "It won't open... no power.";
    [Tooltip("Shown with Requirement = SelectedItem when she has the item but hasn't selected it.")]
    [SerializeField] private string selectItemMessage = "Maybe the card works here... (select it in the hotbar)";

    public Requirement Needs => requirement;
    public StoryFlag Flag => flag;

    public bool IsOpen
    {
        get
        {
            if (requirement == Requirement.StoryFlag) return GameProgress.Get(flag);
            string sel = GameProgress.SelectedItemName();
            return !string.IsNullOrEmpty(sel) && sel.StartsWith(itemName);
        }
    }

    public string Message =>
        requirement == Requirement.SelectedItem && itemName == "Card" && GameProgress.HasCard ? selectItemMessage : lockedMessage;
}
