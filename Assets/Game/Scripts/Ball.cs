using UnityEngine;

public enum BallColor { Red, Blue, Yellow, Green, Purple, Orange, Pink }

/// <summary>Exactly one system owns a ball's movement in each state.</summary>
public enum BallState { InBranch, Falling, FunnelWaiting, Queued, TrayLanding, InTray }

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
public sealed class Ball : MonoBehaviour
{
    [Header("Falling physics")]
    [SerializeField] private float fallingGravityScale = 1.5f;
    [SerializeField, Range(0f, 1f)] private float bounceRetention = 0.699f;
    [SerializeField, Min(0f)] private float minimumBounceSpeed = 0.35f;

    public BallColor Color { get; private set; }
    public BallState State { get; private set; }
    public Rigidbody2D Rigidbody { get; private set; }
    public CircleCollider2D Collider { get; private set; }
    public SpriteRenderer SpriteRenderer { get; private set; }
    public bool IsActive { get; private set; }
    public int SlotIndex { get; internal set; } = -1;
    public TrayColumn TargetTray { get; internal set; }

    private Vector3 prefabLocalScale;
    private Quaternion prefabLocalRotation;
    private int prefabSortingOrder;

    private void Awake()
    {
        Rigidbody = GetComponent<Rigidbody2D>();
        Collider = GetComponent<CircleCollider2D>();
        SpriteRenderer = GetComponent<SpriteRenderer>();
        if (SpriteRenderer == null) SpriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        prefabLocalScale = transform.localScale;
        prefabLocalRotation = transform.localRotation;
        prefabSortingOrder = SpriteRenderer.sortingOrder;
    }

    public void Configure(BallColor ballColor, Color tint, Vector3 worldPosition, Transform parent = null)
    {
        StopAllCoroutines();
        transform.SetParent(parent, true);
        transform.position = worldPosition;
        transform.localScale = prefabLocalScale;
        transform.localRotation = prefabLocalRotation;
        Color = ballColor;
        SpriteRenderer.color = tint;
        SpriteRenderer.sortingOrder = prefabSortingOrder;
        IsActive = true;
        SlotIndex = -1;
        TargetTray = null;

        Rigidbody.velocity = Vector2.zero;
        Rigidbody.angularVelocity = 0f;
        Rigidbody.mass = 10f;
        Rigidbody.drag = 0f;
        Rigidbody.angularDrag = 0.05f;
        Rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        Rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        Rigidbody.freezeRotation = false;
        Collider.radius = 0.18181819f;
        Collider.enabled = true;
        Collider.isTrigger = true;
        gameObject.SetActive(true);
        SetState(BallState.InBranch);
    }

    public void Release(Vector2 velocity, float angularVelocity = 0f)
    {
        if (State != BallState.InBranch) return;
        transform.SetParent(null, true);
        SetState(BallState.Falling);
        Rigidbody.velocity = velocity;
        Rigidbody.angularVelocity = angularVelocity;
    }

    public void SetFunnelDynamic()
    {
        State = BallState.FunnelWaiting;
        Rigidbody.simulated = true;
        Rigidbody.bodyType = RigidbodyType2D.Dynamic;
        Rigidbody.gravityScale = fallingGravityScale;
        Collider.enabled = true;
        Collider.isTrigger = false;
    }

    public void SetFunnelExtracting()
    {
        State = BallState.FunnelWaiting;
        Rigidbody.velocity = Vector2.zero;
        Rigidbody.angularVelocity = 0f;
        Rigidbody.simulated = true;
        Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        Rigidbody.gravityScale = 0f;
        Collider.enabled = true;
        Collider.isTrigger = true;
    }

    public void SetState(BallState newState)
    {
        State = newState;
        switch (newState)
        {
            case BallState.InBranch:
                Rigidbody.velocity = Vector2.zero;
                Rigidbody.angularVelocity = 0f;
                Rigidbody.simulated = true;
                Rigidbody.bodyType = RigidbodyType2D.Kinematic;
                Rigidbody.gravityScale = 0f;
                Collider.enabled = true;
                Collider.isTrigger = true;
                break;
            case BallState.Falling:
                Rigidbody.simulated = true;
                Rigidbody.bodyType = RigidbodyType2D.Dynamic;
                Rigidbody.gravityScale = fallingGravityScale;
                Collider.enabled = true;
                Collider.isTrigger = false;
                break;
            case BallState.FunnelWaiting:
                SetFunnelDynamic();
                break;
            case BallState.Queued:
                Rigidbody.velocity = Vector2.zero;
                Rigidbody.angularVelocity = 0f;
                Rigidbody.simulated = false;
                Rigidbody.bodyType = RigidbodyType2D.Kinematic;
                Rigidbody.gravityScale = 0f;
                Collider.enabled = true;
                Collider.isTrigger = true;
                break;
            case BallState.TrayLanding:
            case BallState.InTray:
                Rigidbody.velocity = Vector2.zero;
                Rigidbody.angularVelocity = 0f;
                Rigidbody.simulated = false;
                Rigidbody.bodyType = RigidbodyType2D.Kinematic;
                Rigidbody.gravityScale = 0f;
                Collider.enabled = false;
                Collider.isTrigger = false;
                break;
        }
    }

    public void ResetForPool()
    {
        StopAllCoroutines();
        IsActive = false;
        State = BallState.InTray;
        Rigidbody.velocity = Vector2.zero;
        Rigidbody.angularVelocity = 0f;
        Rigidbody.gravityScale = 0f;
        Rigidbody.simulated = false;
        Rigidbody.bodyType = RigidbodyType2D.Kinematic;
        Collider.enabled = false;
        Collider.isTrigger = false;
        transform.SetParent(null, false);
        transform.localScale = prefabLocalScale;
        transform.localRotation = prefabLocalRotation;
        SpriteRenderer.sortingOrder = prefabSortingOrder;
        TargetTray = null;
        SlotIndex = -1;
        gameObject.SetActive(false);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (State != BallState.Falling || collision.contactCount == 0) return;
        Vector2 incoming = collision.relativeVelocity;
        if (incoming.magnitude < minimumBounceSpeed) return;
        Vector2 reflected = Vector2.Reflect(incoming, collision.GetContact(0).normal) * bounceRetention;
        if (reflected.sqrMagnitude > Rigidbody.velocity.sqrMagnitude) Rigidbody.velocity = reflected;
    }
}
