using System.Collections.Generic;
using UnityEngine;

public sealed class BallPool : MonoBehaviour
{
    [SerializeField] private Ball ballPrefab;
    [SerializeField, Min(0)] private int initialSize = 120;
    [SerializeField] private Transform poolRoot;

    private readonly Queue<Ball> available = new Queue<Ball>();
    private readonly List<Ball> activeBalls = new List<Ball>();
    public IReadOnlyList<Ball> ActiveBalls => activeBalls;

    private void Awake()
    {
        if (poolRoot == null)
        {
            poolRoot = new GameObject("BallPool Root").transform;
            poolRoot.SetParent(transform, false);
        }
        if (ballPrefab == null) ballPrefab = CreateRuntimeBallTemplate();
        for (int i = 0; i < initialSize; i++)
        {
            Ball ball = CreateBallInstance();
            ball.ResetForPool();
            ball.transform.SetParent(poolRoot, false);
            available.Enqueue(ball);
        }
    }

    public Ball GetBall(BallColor color, Vector3 worldPosition, Transform parent = null)
    {
        Ball ball = available.Count > 0 ? available.Dequeue() : CreateBallInstance();
        ball.Configure(color, GetColorFor(color), worldPosition, parent != null ? parent : poolRoot);
        if (!activeBalls.Contains(ball)) activeBalls.Add(ball);
        return ball;
    }

    public void ReturnBall(Ball ball)
    {
        if (ball == null || !activeBalls.Remove(ball)) return;
        ball.ResetForPool();
        ball.transform.SetParent(poolRoot, false);
        available.Enqueue(ball);
    }

    public void ReturnAllActiveBalls()
    {
        Ball[] snapshot = activeBalls.ToArray();
        foreach (Ball ball in snapshot) ReturnBall(ball);
    }

    private Ball CreateBallInstance()
    {
        Ball clone = Instantiate(ballPrefab, poolRoot, false);
        clone.name = "Ball";
        clone.gameObject.SetActive(false);
        return clone;
    }

    private Ball CreateRuntimeBallTemplate()
    {
        GameObject ballObject = new GameObject("Runtime Ball Template");
        ballObject.transform.SetParent(transform, false);
        ballObject.transform.localScale = Vector3.one * 0.8f;
        SpriteRenderer sr = ballObject.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.sortingOrder = 10;
        Rigidbody2D rb = ballObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1.5f;
        rb.mass = 10f;
        rb.angularDrag = 0.05f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        CircleCollider2D collider = ballObject.AddComponent<CircleCollider2D>();
        collider.radius = 0.18181819f;
        Ball ball = ballObject.AddComponent<Ball>();
        ballObject.SetActive(false);
        return ball;
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime Ball Circle";
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[size * size];
        float radiusSquared = size * size * 0.2025f;
        Vector2 center = Vector2.one * (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 offset = new Vector2(x, y) - center;
            pixels[y * size + x] = offset.sqrMagnitude <= radiusSquared
                ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 50f);
    }

    public static Color GetColorFor(BallColor color)
    {
        switch (color)
        {
            case BallColor.Red: return new Color(1f, 0.2f, 0.2f);
            case BallColor.Blue: return new Color(0.2f, 0.5f, 1f);
            case BallColor.Yellow: return new Color(1f, 0.86f, 0.2f);
            case BallColor.Green: return new Color(0.2f, 0.8f, 0.3f);
            case BallColor.Purple: return new Color(0.65f, 0.3f, 1f);
            case BallColor.Orange: return new Color(1f, 0.6f, 0.1f);
            case BallColor.Pink: return new Color(1f, 0.35f, 0.75f);
            default: return Color.white;
        }
    }
}
