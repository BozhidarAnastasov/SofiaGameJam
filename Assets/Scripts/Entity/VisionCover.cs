using UnityEngine;

/// Put on an object with a Collider2D to block the psycho's view.
/// Low cover (a desk) only hides the girl while she is crawling; tall cover always hides her.
public class VisionCover : MonoBehaviour
{
    [Tooltip("On: only hides her while she crawls (she is seen over it when standing).")]
    public bool lowCover = true;
}
