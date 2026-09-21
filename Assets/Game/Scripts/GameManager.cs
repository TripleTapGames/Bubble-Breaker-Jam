using System;
using System.Collections.Generic;
using PaperSort.Game;
using UnityEngine;

public enum GameState { Ready, Playing, Won, Lost }

/// <summary>
/// Composition root for the gameplay scene. Put this on the common Game parent;
/// it discovers and connects all gameplay systems below it.
/// </summary>
public sealed partial class GameManager : MonoBehaviour
{
    [Header("Scene references — assign only here")]
    [SerializeField] private BallPool ballPool;
    [SerializeField] private BranchSpawnController branchController;
    [SerializeField] private FunnelController funnelController;
    [SerializeField] private ConveyorBelt conveyorBelt;
    [SerializeField] private SplineConveyorBelt2D splineBelt;
    [SerializeField] private TrayColumn[] trayColumns = Array.Empty<TrayColumn>();

    [Header("Rules")]
    [SerializeField, Min(0f)] private float noMatchFailDelay = 1f;
    [SerializeField, Min(1f)] private float fastFinishTimeScale = 2f;
    private bool eventsSubscribed;
    private float noMatchElapsed;

    public event Action Won;
    public event Action Lost;
    public GameState State { get; private set; } = GameState.Ready;
    public BallPool Balls => ballPool;
    public BranchSpawnController Branches => branchController;
    public ConveyorBelt Belt => conveyorBelt;
    public IReadOnlyList<TrayColumn> Trays => trayColumns;

    private void Awake()
    {
        ApplyWiring();
    }

    private void OnEnable()
    {
        SubscribeToTrayEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromTrayEvents();
        RestoreTimeScale();
    }

    /// <summary>
    /// Pushes the references assigned here into their dependent systems.
    /// </summary>
    [ContextMenu("Apply Gameplay Wiring")]
    public void ApplyWiring()
    {
        UnsubscribeFromTrayEvents();
        if (trayColumns == null) trayColumns = Array.Empty<TrayColumn>();
        branchController?.Initialize(ballPool);
        funnelController?.Initialize(ballPool, conveyorBelt);
        conveyorBelt?.Initialize(splineBelt);
        foreach (TrayColumn tray in trayColumns) if (tray != null) tray.Initialize(ballPool);

        ValidateWiring();
        if (isActiveAndEnabled) SubscribeToTrayEvents();
    }

    /// <summary>Use this only when a level creates a new tray array at runtime.</summary>
    public void SetTrayColumns(TrayColumn[] newTrayColumns)
    {
        UnsubscribeFromTrayEvents();
        trayColumns = newTrayColumns ?? Array.Empty<TrayColumn>();
        ApplyWiring();
    }

    public void BeginLevel()
    {
        RestoreTimeScale();
        noMatchElapsed = 0f;
        State = GameState.Playing;
    }

    public void RetryLevel()
    {
        RestoreTimeScale();
        noMatchElapsed = 0f;
        conveyorBelt?.ClearOccupancy();
        funnelController?.ResetFunnel();
        branchController?.ResetReleaseTracking();
        foreach (TrayColumn tray in trayColumns) if (tray != null) tray.Clear(ballPool);
        ballPool?.ReturnAllActiveBalls();
        State = GameState.Ready;
        if (loadSampleLevelOnStart) BuildSampleLevel();
    }

    private void Update()
    {
        if (State != GameState.Playing) return;

        if (loadSampleLevelOnStart) UpdateSampleInput();

        funnelController?.UpdateFunnel();
        conveyorBelt?.UpdateBelt();
        foreach (TrayColumn tray in trayColumns) if (tray != null) tray.UpdateTray();
        CollectSeatedBalls();

        if (conveyorBelt == null) return;
        UpdateDeadlockCheck();
        if (State == GameState.Playing) UpdateFastFinish();
    }

    private void CollectSeatedBalls()
    {
        if (conveyorBelt == null) return;
        IReadOnlyList<Ball> queuedBalls = conveyorBelt.GetQueuedBalls();
        foreach (Ball ball in queuedBalls)
        {
            if (ball == null || !conveyorBelt.IsBallSeated(ball)) continue;
            foreach (TrayColumn tray in trayColumns)
            {
                if (tray == null || !tray.IsBallInPickupBand(ball) || !tray.CanAccept(ball)) continue;
                conveyorBelt.RemoveBall(ball);
                if (!tray.TryAcceptBall(ball))
                    Debug.LogError("Tray rejected a ball after accepting its reservation check.", tray);
                break;
            }
        }
    }

    private void UpdateDeadlockCheck()
    {
        bool hasMatch = false;
        foreach (Ball ball in conveyorBelt.GetQueuedBalls())
        {
            if (ball == null) continue;
            foreach (TrayColumn tray in trayColumns)
            {
                if (tray != null && tray.CanAccept(ball)) { hasMatch = true; break; }
            }
            if (hasMatch) break;
        }

        if (conveyorBelt.IsFull && !hasMatch) noMatchElapsed += Time.unscaledDeltaTime;
        else noMatchElapsed = 0f;
        if (noMatchElapsed >= noMatchFailDelay) LoseLevel();
    }

    private void UpdateFastFinish()
    {
        if (branchController == null || branchController.HasPendingReleases || ballPool == null)
        {
            RestoreTimeScale();
            return;
        }

        foreach (Ball ball in ballPool.ActiveBalls)
        {
            if (ball != null && (ball.State == BallState.InBranch || ball.State == BallState.Falling ||
                ball.State == BallState.FunnelWaiting ||
                (ball.State == BallState.Queued && !conveyorBelt.IsBallSeated(ball))))
            {
                RestoreTimeScale();
                return;
            }
        }

        IReadOnlyList<Ball> queuedBalls = conveyorBelt.GetQueuedBalls();
        if (queuedBalls.Count == 0) { RestoreTimeScale(); return; }

        bool hasImmediatelyCollectableBall = false;
        Dictionary<BallColor, int> queuedByColor = new Dictionary<BallColor, int>();
        foreach (Ball ball in queuedBalls)
        {
            if (ball == null) { RestoreTimeScale(); return; }
            queuedByColor.TryGetValue(ball.Color, out int count);
            queuedByColor[ball.Color] = count + 1;
            foreach (TrayColumn tray in trayColumns)
                if (tray != null && tray.CanAccept(ball)) { hasImmediatelyCollectableBall = true; break; }
        }
        if (!hasImmediatelyCollectableBall) { RestoreTimeScale(); return; }

        Dictionary<BallColor, int> requiredByColor = new Dictionary<BallColor, int>();
        foreach (TrayColumn tray in trayColumns)
        {
            if (tray == null) continue;
            if (tray.IsTransitioning) { RestoreTimeScale(); return; }
            tray.AddRemainingRequirements(requiredByColor);
        }

        int queuedTotal = 0;
        foreach (int count in queuedByColor.Values) queuedTotal += count;
        int requiredTotal = 0;
        foreach (KeyValuePair<BallColor, int> requirement in requiredByColor)
        {
            requiredTotal += requirement.Value;
            queuedByColor.TryGetValue(requirement.Key, out int available);
            if (available != requirement.Value) { RestoreTimeScale(); return; }
        }
        if (queuedTotal != requiredTotal) { RestoreTimeScale(); return; }

        Time.timeScale = fastFinishTimeScale;
    }

    private void HandleTrayCompleted(TrayColumn ignored)
    {
        foreach (TrayColumn tray in trayColumns)
            if (tray != null && !tray.IsComplete) return;
        State = GameState.Won;
        RestoreTimeScale();
        Won?.Invoke();
    }

    private void LoseLevel()
    {
        if (State != GameState.Playing) return;
        State = GameState.Lost;
        RestoreTimeScale();
        Lost?.Invoke();
    }

    private void SubscribeToTrayEvents()
    {
        if (eventsSubscribed) return;
        foreach (TrayColumn tray in trayColumns)
            if (tray != null) tray.Completed += HandleTrayCompleted;
        eventsSubscribed = true;
    }

    private void UnsubscribeFromTrayEvents()
    {
        if (!eventsSubscribed) return;
        foreach (TrayColumn tray in trayColumns)
            if (tray != null) tray.Completed -= HandleTrayCompleted;
        eventsSubscribed = false;
    }

    private void ValidateWiring()
    {
        if (ballPool == null) Debug.LogError("Assign BallPool in GameManager.", this);
        if (branchController == null) Debug.LogError("Assign BranchSpawnController in GameManager.", this);
        if (funnelController == null) Debug.LogError("Assign FunnelController in GameManager.", this);
        if (conveyorBelt == null) Debug.LogError("Assign ConveyorBelt in GameManager.", this);
        if (splineBelt == null) Debug.LogError("Assign SplineConveyorBelt2D in GameManager.", this);
        if (trayColumns == null || trayColumns.Length == 0)
            Debug.LogError("Assign at least one TrayColumn in GameManager.", this);
    }

    private static void RestoreTimeScale()
    {
        if (!Mathf.Approximately(Time.timeScale, 1f)) Time.timeScale = 1f;
    }
}
