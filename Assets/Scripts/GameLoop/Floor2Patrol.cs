using System.Collections;
using ArcadesTemplate.Core;
using UnityEngine;

/// Second floor, right after the bed crashed through the hole: the noise brings him up the stairs.
/// He comes up, then patrols - first around the hole, then around the desk, and round again - and
/// doesn't stop until she has left the floor. His vision cone works as usual (stay out of it or
/// hide under the coat); if he loses her during a chase he goes back to his round.
public class Floor2Patrol : MonoBehaviour
{
    [SerializeField] private EntityAI psycho;
    [Tooltip("Where he appears at the top of the stairs.")]
    [SerializeField] private Transform stairsTop;
    [Tooltip("His first step off the stairs into the hall.")]
    [SerializeField] private Transform offStairs;
    [Tooltip("Patrol: the points around the hole first, then the ones around the desk (it loops).")]
    [SerializeField] private Transform[] patrol;
    [SerializeField] private float startDelay = 2f;
    [SerializeField] private string message = "Heavy steps on the stairs... he's coming up!";

    private bool wasDropped, started;

    void Start()
    {
        wasDropped = GameProgress.Get(StoryFlag.BedDropped);
        if (GameProgress.Get(StoryFlag.Floor2PatrolDone)) enabled = false;
    }

    void Update()
    {
        if (started || psycho == null) return;
        bool dropped = GameProgress.Get(StoryFlag.BedDropped);
        if (dropped && !wasDropped) { started = true; StartCoroutine(Come()); }    // it fell during this visit
        wasDropped = dropped;
    }

    IEnumerator Come()
    {
        Debug.Log($"[Floor2Patrol] The bed fell - he comes up the stairs in {startDelay} s.", this);
        yield return new WaitForSeconds(startDelay);
        if (RoomTransition.IsBusy) yield break;
        if (ArcadeMessageService.Instance != null && !string.IsNullOrEmpty(message)) ArcadeMessageService.Instance.ShowMessage(message);

        Vector2 from = stairsTop.position;
        Vector2 step = offStairs != null ? (Vector2)offStairs.position : from;
        psycho.ScriptAppear(from, step - from);
        if (offStairs != null)
        {
            psycho.ScriptWalkTo(step);
            while (!psycho.ScriptArrived || psycho.Hunting)
            {
                if (psycho.Current == EntityAI.State.Grabbing) yield break;
                yield return null;
            }
        }
        Debug.Log("[Floor2Patrol] He's on the floor - patrolling the hole, then the desk, until she leaves.", this);
        psycho.BeginPatrol(patrol);
    }

    // she left the floor: his patrol here is over for good
    void OnDestroy()
    {
        if (started) GameProgress.Set(StoryFlag.Floor2PatrolDone);
    }

    void OnDrawGizmosSelected()
    {
        if (patrol == null) return;
        Gizmos.color = Color.magenta;
        for (int i = 0; i < patrol.Length; i++)
            if (patrol[i] != null && patrol[(i + 1) % patrol.Length] != null)
                Gizmos.DrawLine(patrol[i].position, patrol[(i + 1) % patrol.Length].position);
        if (stairsTop != null) Gizmos.DrawWireSphere(stairsTop.position, 0.3f);
    }
}
