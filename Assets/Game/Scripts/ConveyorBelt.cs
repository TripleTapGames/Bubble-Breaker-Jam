using System;
using System.Collections.Generic;
using System.Linq;
using PaperSort.Game;
using UnityEngine;

public sealed class ConveyorBelt : MonoBehaviour
{
    private SplineConveyorBelt2D splineBelt;
    [Tooltip("Optional fallback when no SplineConveyorBelt2D is assigned.")]
    [SerializeField] private List<Transform> slotTransforms = new List<Transform>();
    [SerializeField, Min(0.01f)] private float seatDuration = 0.12f;
    [SerializeField] private AnimationCurve seatCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private sealed class Occupant
    {
        public Ball Ball;
        public Vector3 StartLocalPosition;
        public float Progress;
        public bool IsSeated;
    }

    private readonly Dictionary<int, Occupant> occupants = new Dictionary<int, Occupant>();

    public event Action<Ball> BallSeated;
    public int OccupiedCount => occupants.Count;
    public int Capacity => splineBelt != null ? splineBelt.SlotCount : slotTransforms.Count;
    public bool IsFull => Capacity > 0 && OccupiedCount >= Capacity;

    public void Initialize(SplineConveyorBelt2D discoveredSplineBelt)
    {
        if (splineBelt == null) splineBelt = discoveredSplineBelt;
    }

    public void UpdateBelt()
    {
        foreach (KeyValuePair<int, Occupant> pair in occupants.ToArray())
        {
            Occupant occupant = pair.Value;
            if (occupant.Ball == null)
            {
                occupants.Remove(pair.Key);
                continue;
            }
            if (occupant.IsSeated) continue;

            occupant.Progress = Mathf.Clamp01(occupant.Progress + Time.deltaTime / seatDuration);
            float eased = seatCurve.Evaluate(occupant.Progress);
            occupant.Ball.transform.localPosition = Vector3.LerpUnclamped(occupant.StartLocalPosition, Vector3.zero, eased);
            occupant.Ball.transform.localRotation = Quaternion.Slerp(
                occupant.Ball.transform.localRotation, Quaternion.identity, eased);

            if (occupant.Progress < 1f) continue;
            occupant.Ball.transform.localPosition = Vector3.zero;
            occupant.Ball.transform.localRotation = Quaternion.identity;
            occupant.IsSeated = true;
            BallSeated?.Invoke(occupant.Ball);
        }
    }

    public bool TryAttachBall(Ball ball, float catchDistance)
    {
        if (ball == null || Capacity <= 0 || IsFull) return false;

        int selectedSlot = -1;
        float bestDistance = Mathf.Infinity;
        for (int i = 0; i < Capacity; i++)
        {
            if (occupants.ContainsKey(i)) continue;
            Transform slot = GetSlotTransform(i);
            if (slot == null) continue;
            float distance = Vector2.Distance(ball.transform.position, slot.position);
            if (distance <= catchDistance && distance < bestDistance)
            {
                selectedSlot = i;
                bestDistance = distance;
            }
        }
        if (selectedSlot < 0) return false;

        Transform selectedTransform = GetSlotTransform(selectedSlot);
        ball.SetState(BallState.Queued);
        ball.SlotIndex = selectedSlot;
        ball.transform.SetParent(selectedTransform, true);
        occupants.Add(selectedSlot, new Occupant
        {
            Ball = ball,
            StartLocalPosition = ball.transform.localPosition,
            Progress = 0f,
            IsSeated = false
        });
        return true;
    }

    public bool IsBallSeated(Ball ball)
    {
        return ball != null && ball.SlotIndex >= 0 && occupants.TryGetValue(ball.SlotIndex, out Occupant item)
            && item.Ball == ball && item.IsSeated;
    }

    public void RemoveBall(Ball ball)
    {
        if (ball == null) return;
        int slotIndex = ball.SlotIndex;
        if (slotIndex >= 0 && occupants.TryGetValue(slotIndex, out Occupant item) && item.Ball == ball)
        {
            occupants.Remove(slotIndex);
        }
        ball.SlotIndex = -1;
        ball.transform.SetParent(null, true);
    }

    public IReadOnlyList<Ball> GetQueuedBalls()
    {
        return occupants.Values.Where(item => item.Ball != null).Select(item => item.Ball).ToArray();
    }

    public void ClearOccupancy()
    {
        foreach (Occupant item in occupants.Values)
        {
            if (item.Ball != null)
            {
                item.Ball.SlotIndex = -1;
                item.Ball.transform.SetParent(null, true);
            }
        }
        occupants.Clear();
    }

    private Transform GetSlotTransform(int index)
    {
        return splineBelt != null ? splineBelt.GetSlotTransform(index)
            : index >= 0 && index < slotTransforms.Count ? slotTransforms[index] : null;
    }
}
