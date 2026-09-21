using System.Collections;
using UnityEngine;

/// <summary>
/// Boundary between the future branch mechanic and the shared ball pipeline.
/// Branch shape, links, exposure and tap rules are intentionally not defined here.
/// </summary>
public sealed class BranchSpawnController : MonoBehaviour
{
    private BallPool ballPool;
    private int unreleasedBallCount;
    private int scheduledReleaseCount;

    public int UnreleasedBallCount => unreleasedBallCount;
    public int ScheduledReleaseCount => scheduledReleaseCount;
    public bool HasPendingReleases => unreleasedBallCount > 0 || scheduledReleaseCount > 0;

    public void Initialize(BallPool sharedBallPool)
    {
        ballPool = sharedBallPool;
    }

    public Ball CreateLinkedBall(BallColor color, Vector3 worldPosition, Transform linkParent)
    {
        if (ballPool == null)
        {
            Debug.LogError("BranchSpawnController requires a BallPool.", this);
            return null;
        }
        Ball ball = ballPool.GetBall(color, worldPosition, linkParent);
        ball.SetState(BallState.InBranch);
        unreleasedBallCount++;
        return ball;
    }

    public bool ReleaseBall(Ball ball, Vector2 velocity, float angularVelocity = 0f)
    {
        if (ball == null || ball.State != BallState.InBranch) return false;
        unreleasedBallCount = Mathf.Max(0, unreleasedBallCount - 1);
        ball.Release(velocity, angularVelocity);
        return true;
    }

    public void ScheduleRelease(Ball ball, Vector2 velocity, float delay, float angularVelocity = 0f)
    {
        if (ball == null || ball.State != BallState.InBranch) return;
        StartCoroutine(ReleaseAfterDelay(ball, velocity, Mathf.Max(0f, delay), angularVelocity));
    }

    public void ResetReleaseTracking()
    {
        StopAllCoroutines();
        unreleasedBallCount = 0;
        scheduledReleaseCount = 0;
    }

    private IEnumerator ReleaseAfterDelay(Ball ball, Vector2 velocity, float delay, float angularVelocity)
    {
        scheduledReleaseCount++;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        scheduledReleaseCount = Mathf.Max(0, scheduledReleaseCount - 1);
        ReleaseBall(ball, velocity, angularVelocity);
    }
}
