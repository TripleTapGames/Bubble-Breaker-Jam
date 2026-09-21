using UnityEngine;

public sealed class FunnelController : MonoBehaviour
{
    [SerializeField] private Transform outletPoint;
    [SerializeField, Min(0f)] private float captureRadius = 1.2f;
    [SerializeField, Min(0f)] private float extractionSpeed = 6f;
    [SerializeField, Min(0f)] private float outletTolerance = 0.2f;
    [SerializeField, Min(0f)] private float catchDistance = 0.5f;

    private Ball guidedBall;
    public Transform OutletPoint => outletPoint;
    private BallPool ballPool;
    private ConveyorBelt conveyorBelt;
    public bool HasWaitingBalls => guidedBall != null || FindClosestWaitingBall() != null;

    public void Initialize(BallPool sharedBallPool, ConveyorBelt sharedConveyorBelt)
    {
        ballPool = sharedBallPool;
        conveyorBelt = sharedConveyorBelt;
    }

    public void UpdateFunnel()
    {
        if (ballPool == null || outletPoint == null) return;

        // Scan the centrally tracked pool instead of relying on a bounded overlap query.
        for (int i = 0; i < ballPool.ActiveBalls.Count; i++)
        {
            Ball ball = ballPool.ActiveBalls[i];
            if (ball == null || ball.State != BallState.Falling) continue;
            if (Vector2.Distance(ball.transform.position, outletPoint.position) <= captureRadius)
            {
                // Waiting balls stay dynamic and solid so the pile remains physical.
                ball.SetFunnelDynamic();
            }
        }

        if (guidedBall == null || !guidedBall.IsActive || guidedBall.State != BallState.FunnelWaiting)
        {
            guidedBall = FindClosestWaitingBall();
            if (guidedBall != null) guidedBall.SetFunnelExtracting();
        }
        if (guidedBall == null) return;

        float distanceToOutlet = Vector2.Distance(guidedBall.transform.position, outletPoint.position);
        if (distanceToOutlet <= outletTolerance && conveyorBelt != null &&
            conveyorBelt.TryAttachBall(guidedBall, catchDistance))
        {
            guidedBall = null;
            return;
        }

        guidedBall.transform.position = Vector3.MoveTowards(
            guidedBall.transform.position, outletPoint.position, extractionSpeed * Time.deltaTime);

        // A ball can wait at the outlet until a free belt slot passes within catch distance.
        if (conveyorBelt != null && conveyorBelt.TryAttachBall(guidedBall, catchDistance)) guidedBall = null;
    }

    public void ResetFunnel()
    {
        guidedBall = null;
    }

    private Ball FindClosestWaitingBall()
    {
        if (ballPool == null || outletPoint == null) return null;
        Ball best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < ballPool.ActiveBalls.Count; i++)
        {
            Ball ball = ballPool.ActiveBalls[i];
            if (ball == null || ball == guidedBall || ball.State != BallState.FunnelWaiting) continue;
            float distance = Vector2.Distance(ball.transform.position, outletPoint.position);
            if (distance < bestDistance)
            {
                best = ball;
                bestDistance = distance;
            }
        }
        return best;
    }
}
