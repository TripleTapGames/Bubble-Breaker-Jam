using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>An ordered tray sequence. Only the front stage accepts balls.</summary>
public sealed class TrayColumn : MonoBehaviour
{
    [Serializable]
    public sealed class TrayStage
    {
        public BallColor color;
        [Min(1)] public int capacity = 8;
        public Transform trayRoot;
        public Transform[] landingSlots = Array.Empty<Transform>();
        public SpriteRenderer trayRenderer;
    }

    [SerializeField] private TrayStage[] stages = Array.Empty<TrayStage>();

    [Header("Single-tray fallback")]
    [SerializeField] private BallColor color;
    [SerializeField, Min(1)] private int capacity = 8;
    [SerializeField] private Transform[] landingSlots = Array.Empty<Transform>();
    [SerializeField] private SpriteRenderer trayRenderer;

    [Header("Pickup and motion")]
    [SerializeField] private Transform pickupBand;
    [SerializeField] private Vector2 pickupHalfExtents = new Vector2(0.5f, 0.05f);
    [SerializeField, Min(0.01f)] private float landingDuration = 0.12f;
    [SerializeField, Min(0f)] private float horizontalDrift = 0.12f;
    [SerializeField, Range(0f, 0.5f)] private float impactSquash = 0.12f;
    [SerializeField, Min(0f)] private float completionHold = 0.45f;
    [SerializeField, Min(0.01f)] private float forwardSlideDuration = 0.35f;

    private sealed class Landing
    {
        public Ball Ball;
        public int SlotIndex;
        public Vector3 StartPosition;
        public Vector3 BaseScale;
        public float Progress;
    }

    private readonly List<Landing> landings = new List<Landing>();
    private readonly List<Ball> landedBalls = new List<Ball>();
    private int activeStageIndex;
    private bool transitioning;
    private bool finished;
    private BallPool ballPool;

    public event Action<TrayColumn> Completed;
    public BallColor Color => CurrentColor;
    public int Capacity => CurrentCapacity;
    public int ReservedCount => landings.Count + landedBalls.Count;
    public int RemainingCapacity => Mathf.Max(0, CurrentCapacity - ReservedCount);
    public bool IsComplete => finished;
    public bool IsTransitioning => transitioning;

    private bool HasStages => stages != null && stages.Length > 0;
    private TrayStage CurrentStage => HasStages && activeStageIndex < stages.Length ? stages[activeStageIndex] : null;
    private BallColor CurrentColor => CurrentStage != null ? CurrentStage.color : color;
    private int CurrentCapacity => CurrentStage != null ? Mathf.Max(1, CurrentStage.capacity) : Mathf.Max(1, capacity);

    private void Awake()
    {
        ResetStageVisuals();
    }

    public void Initialize(BallPool sharedBallPool)
    {
        ballPool = sharedBallPool;
    }

    public void ConfigureGeneratedTray(BallColor ballColor, Transform root, Transform[] slots, Transform pickup)
    {
        stages = new[] { new TrayStage { color = ballColor, capacity = slots.Length,
            trayRoot = root, landingSlots = slots } };
        pickupBand = pickup;
        pickupHalfExtents = new Vector2(0.45f, 0.2f);
        activeStageIndex = 0;
        finished = false;
        transitioning = false;
        ResetStageVisuals();
    }

    public bool CanAccept(Ball ball)
    {
        return !finished && !transitioning && ball != null && ball.State == BallState.Queued &&
            ball.Color == CurrentColor && RemainingCapacity > 0;
    }

    public bool TryAcceptBall(Ball ball)
    {
        if (!CanAccept(ball)) return false;
        int slotIndex = ReservedCount; // Reserve before animation begins.
        ball.TargetTray = this;
        ball.SetState(BallState.TrayLanding);
        landings.Add(new Landing
        {
            Ball = ball,
            SlotIndex = slotIndex,
            StartPosition = ball.transform.position,
            BaseScale = ball.transform.localScale,
            Progress = 0f
        });
        return true;
    }

    public bool IsBallInPickupBand(Ball ball)
    {
        if (ball == null || pickupBand == null) return false;
        Vector3 delta = ball.transform.position - pickupBand.position;
        return Mathf.Abs(delta.x) <= pickupHalfExtents.x && Mathf.Abs(delta.y) <= pickupHalfExtents.y;
    }

    public void UpdateTray()
    {
        for (int i = landings.Count - 1; i >= 0; i--)
        {
            Landing landing = landings[i];
            Ball ball = landing.Ball;
            if (ball == null)
            {
                landings.RemoveAt(i);
                continue;
            }

            landing.Progress = Mathf.Clamp01(landing.Progress + Time.deltaTime / landingDuration);
            float eased = 1f - Mathf.Pow(1f - landing.Progress, 3f);
            Vector3 target = ResolveLandingPosition(landing.SlotIndex);
            float drift = Mathf.Sin(landing.Progress * Mathf.PI) * horizontalDrift;
            ball.transform.position = Vector3.LerpUnclamped(landing.StartPosition, target, eased) + Vector3.right * drift;
            float squash = Mathf.Sin(landing.Progress * Mathf.PI) * impactSquash;
            ball.transform.localScale = new Vector3(
                landing.BaseScale.x * (1f + squash), landing.BaseScale.y * (1f - squash), landing.BaseScale.z);

            if (landing.Progress < 1f) continue;
            ball.transform.position = target;
            ball.transform.localScale = landing.BaseScale;
            ball.SetState(BallState.InTray);
            ball.TargetTray = null;
            landedBalls.Add(ball);
            landings.RemoveAt(i);
        }

        if (!transitioning && !finished && landedBalls.Count >= CurrentCapacity && landings.Count == 0)
            StartCoroutine(CompleteCurrentStage());
    }

    public void AddRemainingRequirements(IDictionary<BallColor, int> requirements)
    {
        if (finished || requirements == null) return;
        AddRequirement(requirements, CurrentColor, RemainingCapacity);
        if (!HasStages) return;
        for (int i = activeStageIndex + 1; i < stages.Length; i++)
            if (stages[i] != null) AddRequirement(requirements, stages[i].color, Mathf.Max(1, stages[i].capacity));
    }

    public void Clear(BallPool fallbackPool)
    {
        StopAllCoroutines();
        BallPool usedPool = ballPool != null ? ballPool : fallbackPool;
        foreach (Landing landing in landings)
            if (landing.Ball != null && usedPool != null) usedPool.ReturnBall(landing.Ball);
        foreach (Ball ball in landedBalls)
            if (ball != null && usedPool != null) usedPool.ReturnBall(ball);
        landings.Clear();
        landedBalls.Clear();
        activeStageIndex = 0;
        transitioning = false;
        finished = false;
        ResetStageVisuals();
    }

    public void SetColor(BallColor newColor)
    {
        color = newColor;
        if (trayRenderer != null) trayRenderer.color = BallPool.GetColorFor(newColor);
    }

    private IEnumerator CompleteCurrentStage()
    {
        transitioning = true;
        if (completionHold > 0f) yield return new WaitForSeconds(completionHold);

        bool hasNextStage = HasStages && activeStageIndex + 1 < stages.Length;
        if (!hasNextStage)
        {
            finished = true;
            transitioning = false;
            Completed?.Invoke(this);
            yield break;
        }

        if (ballPool == null)
        {
            Debug.LogError("A multi-stage TrayColumn requires a BallPool to clear completed trays.", this);
            transitioning = false;
            yield break;
        }

        foreach (Ball ball in landedBalls) if (ball != null) ballPool.ReturnBall(ball);
        landedBalls.Clear();

        TrayStage previous = CurrentStage;
        activeStageIndex++;
        TrayStage next = CurrentStage;
        if (previous?.trayRoot != null) previous.trayRoot.gameObject.SetActive(false);
        if (next?.trayRoot != null)
        {
            next.trayRoot.gameObject.SetActive(true);
            Vector3 end = previous?.trayRoot != null ? previous.trayRoot.position : next.trayRoot.position;
            Vector3 start = next.trayRoot.position;
            float elapsed = 0f;
            while (elapsed < forwardSlideDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / forwardSlideDuration));
                next.trayRoot.position = Vector3.LerpUnclamped(start, end, t);
                yield return null;
            }
            next.trayRoot.position = end;
        }
        transitioning = false;
    }

    private Vector3 ResolveLandingPosition(int index)
    {
        Transform[] slots = CurrentStage != null ? CurrentStage.landingSlots : landingSlots;
        if (slots != null && index >= 0 && index < slots.Length && slots[index] != null) return slots[index].position;
        return CurrentStage?.trayRoot != null ? CurrentStage.trayRoot.position : transform.position;
    }

    private void ResetStageVisuals()
    {
        if (!HasStages) return;
        for (int i = 0; i < stages.Length; i++)
            if (stages[i]?.trayRoot != null) stages[i].trayRoot.gameObject.SetActive(i == activeStageIndex);
    }

    private static void AddRequirement(IDictionary<BallColor, int> requirements, BallColor ballColor, int amount)
    {
        requirements.TryGetValue(ballColor, out int current);
        requirements[ballColor] = current + Mathf.Max(0, amount);
    }

    private void OnDrawGizmosSelected()
    {
        if (pickupBand == null) return;
        Gizmos.color = UnityEngine.Color.cyan;
        Gizmos.DrawWireCube(pickupBand.position, pickupHalfExtents * 2f);
    }
}
