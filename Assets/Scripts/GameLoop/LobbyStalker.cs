using System.Collections;
using ArcadesTemplate.Core;
using UnityEngine;

/// The psycho's walk through the Lobby after she found the coat (first time she comes back with it).
/// He appears in the bottom-right corner, walks to the stairs and goes up to the second floor,
/// stays up there for a while, comes back down and disappears into Electrical through the
/// kicked-in door. His vision cone is on the whole time: if he sees her he chases her
/// (hide under the coat or get out of his sight), and when he loses her he carries on with the walk.
/// Afterwards he waits in Electrical (StoryFlag.EnemyInElectrical).
public class LobbyStalker : MonoBehaviour
{
    [SerializeField] private EntityAI psycho;
    [Tooltip("Bottom-right corner -> ... -> the foot of the stairs. The first point is where he appears.")]
    [SerializeField] private Transform[] toStairs;
    [Tooltip("Up the stairs: where he disappears / reappears.")]
    [SerializeField] private Transform stairsTop;
    [Tooltip("From the stairs to the Electrical door. The last point is where he disappears.")]
    [SerializeField] private Transform[] toElectrical;
    [SerializeField] private float startDelay = 1.5f;
    [Tooltip("How long he stays upstairs.")]
    [SerializeField] private float upstairsTime = 9f;
    [SerializeField] private string arrivalMessage = "Footsteps... someone is here. (select the coat, hold C to hide)";

    private bool running, finished;

    void Start()
    {
        bool due = GameProgress.HasCoat && !GameProgress.Get(StoryFlag.CoatEventDone);
        if (psycho == null) { Debug.LogWarning("[LobbyStalker] No psycho assigned.", this); return; }
        if (!due) { Debug.Log($"[LobbyStalker] Not now (coat {GameProgress.HasCoat}, walk done {GameProgress.Get(StoryFlag.CoatEventDone)}).", this); return; }
        StartCoroutine(Walk());
    }

    IEnumerator Walk()
    {
        running = true;
        yield return new WaitForSeconds(startDelay);
        Debug.Log("[LobbyStalker] He appears in the bottom-right corner.", this);
        if (ArcadeMessageService.Instance != null && !string.IsNullOrEmpty(arrivalMessage)) ArcadeMessageService.Instance.ShowMessage(arrivalMessage);

        Vector2 first = toStairs[0].position;
        Vector2 look = toStairs.Length > 1 ? (Vector2)toStairs[1].position - first : Vector2.left;
        psycho.ScriptAppear(first, look);
        GameAudio.Play(GameAudio.Sounds != null ? GameAudio.Sounds.knifeGrinding : null, 0.8f);   // he announces himself
        for (int i = 1; i < toStairs.Length; i++) yield return WalkTo(toStairs[i].position);
        if (stairsTop != null) yield return WalkTo(stairsTop.position);

        Debug.Log("[LobbyStalker] He went up to the second floor.", this);
        psycho.ScriptVanish();
        yield return new WaitForSeconds(upstairsTime);

        Debug.Log("[LobbyStalker] He comes back down and heads for Electrical.", this);
        Vector2 from = stairsTop != null ? (Vector2)stairsTop.position : (Vector2)toStairs[toStairs.Length - 1].position;
        psycho.ScriptAppear(from, Vector2.down);
        for (int i = 0; i < toElectrical.Length; i++) yield return WalkTo(toElectrical[i].position);

        psycho.ScriptVanish();
        Finish();
        Debug.Log("[LobbyStalker] He went into Electrical and waits there.", this);
    }

    IEnumerator WalkTo(Vector2 p)
    {
        psycho.ScriptWalkTo(p);
        // keeps waiting while he chases / searches for her; he resumes the walk when he loses her
        while (!psycho.ScriptArrived || psycho.Hunting)
        {
            if (psycho.Current == EntityAI.State.Grabbing) { while (true) yield return null; }   // caught: the death screen takes over
            yield return null;
        }
    }

    void Finish()
    {
        if (finished) return;
        finished = true;
        GameProgress.Set(StoryFlag.CoatEventDone);
        GameProgress.Set(StoryFlag.EnemyInElectrical);
    }

    // she left the Lobby in the middle of it: he finishes his walk off-screen and ends up in Electrical
    void OnDestroy()
    {
        if (running && !finished && psycho != null && psycho.Current != EntityAI.State.Grabbing) Finish();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        void Path(Transform[] pts) { if (pts == null) return; for (int i = 1; i < pts.Length; i++) if (pts[i - 1] && pts[i]) Gizmos.DrawLine(pts[i - 1].position, pts[i].position); }
        Path(toStairs); Path(toElectrical);
        if (stairsTop != null) Gizmos.DrawWireSphere(stairsTop.position, 0.3f);
    }
}
