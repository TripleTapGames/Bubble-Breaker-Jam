using UnityEngine;

public sealed partial class GameManager
{
    [Header("Sample level (temporary branch rules)")]
    [SerializeField] private bool loadSampleLevelOnStart;
    [SerializeField] private GameObject sampleTrayPrefab;
    [SerializeField] private Sprite[] sampleBallSprites;
    [SerializeField] private Sprite[] sampleTraySprites;
    private Transform sampleRoot;
    private Ball[] sampleBalls;
    private LineRenderer[] sampleLinks;
    private readonly int[] sampleParents = { -1, 0, 0, 1, 1, 2, 2, 3, 6 };
    private Material sampleLineMaterial;

    private void Start()
    {
        if (loadSampleLevelOnStart) BuildSampleLevel();
    }

    private void BuildSampleLevel()
    {
        if (ballPool == null || branchController == null || splineBelt == null ||
            funnelController == null || funnelController.OutletPoint == null ||
            sampleTrayPrefab == null || trayColumns.Length != 3)
        {
            Debug.LogError("Sample level requires the shared controllers, outlet, tray prefab and three tray anchors.", this);
            return;
        }
        foreach (TrayColumn column in trayColumns) if (column == null) return;
        if (sampleRoot != null)
        {
            sampleRoot.gameObject.SetActive(false);
            Destroy(sampleRoot.gameObject);
        }
        sampleRoot = new GameObject("Generated Sample Level").transform;
        sampleRoot.SetParent(transform, false);
        if (sampleLineMaterial == null) sampleLineMaterial = new Material(Shader.Find("Sprites/Default"));

        BallColor[] colors = { BallColor.Red, BallColor.Blue, BallColor.Yellow };
        for (int c = 0; c < 3; c++)
        {
            Transform tray = Instantiate(sampleTrayPrefab, sampleRoot).transform;
            tray.name = colors[c] + " Tray (3 balls)";
            tray.position = trayColumns[c].transform.position;
            tray.rotation = Quaternion.identity;
            foreach (SpriteRenderer renderer in tray.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.name.StartsWith("Slot_") || renderer.name == "Cover") renderer.enabled = false;
                else if (renderer.name == "Tray_renderer" && sampleTraySprites != null && sampleTraySprites.Length == 3)
                {
                    renderer.sprite = sampleTraySprites[c];
                    renderer.color = Color.white;
                }
            }
            Transform[] slots = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                slots[i] = new GameObject("Landing " + i).transform;
                slots[i].SetParent(tray, false);
                slots[i].localPosition = new Vector3((i - 1) * 0.4f, 0f, 0f);
            }
            // Sample the actual spline so pickups sit on the belt even when it is repositioned.
            Vector2 pickupPosition = Vector2.zero;
            float best = float.PositiveInfinity;
            for (int i = 0; i < 256; i++)
            {
                splineBelt.EvaluateAtProgress(i / 256f, out Vector2 point, out _);
                float distance = (point - (Vector2)tray.position).sqrMagnitude;
                if (distance < best) { best = distance; pickupPosition = point; }
            }
            Transform pickup = new GameObject(colors[c] + " Pickup").transform;
            pickup.SetParent(sampleRoot, false);
            pickup.position = pickupPosition;
            trayColumns[c].ConfigureGeneratedTray(colors[c], tray, slots, pickup);
        }

        Vector3 outlet = funnelController.OutletPoint.position;
        Vector2[] positions = {
            new Vector2(0, 4.1f), new Vector2(-0.65f, 3.45f), new Vector2(0.65f, 3.45f),
            new Vector2(-1.15f, 2.8f), new Vector2(-0.35f, 2.8f),
            new Vector2(0.35f, 2.8f), new Vector2(1.15f, 2.8f),
            new Vector2(-1.35f, 2.15f), new Vector2(1.35f, 2.15f)
        };
        sampleBalls = new Ball[positions.Length];
        sampleLinks = new LineRenderer[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            sampleBalls[i] = branchController.CreateLinkedBall(colors[i % 3], outlet + (Vector3)positions[i], sampleRoot);
            foreach (SpriteRenderer renderer in sampleBalls[i].GetComponentsInChildren<SpriteRenderer>(true))
                renderer.enabled = renderer == sampleBalls[i].SpriteRenderer;
            SpriteRenderer visual = sampleBalls[i].SpriteRenderer;
            if (sampleBallSprites != null && sampleBallSprites.Length == 3)
            {
                visual.sprite = sampleBallSprites[i % 3];
                visual.color = Color.white;
            }
            visual.sortingOrder = 10;
            if (sampleParents[i] >= 0)
                sampleLinks[i] = DrawSampleLine("Link " + i, sampleBalls[sampleParents[i]].transform.position,
                    sampleBalls[i].transform.position, 0.045f, new Color(0.75f, 0.85f, 1f));
        }
        // Temporary physical funnel for this sample; scene wall objects are currently empty.
        CreateSampleWall(outlet, -1f);
        CreateSampleWall(outlet, 1f);
        BeginLevel();
    }

    private LineRenderer DrawSampleLine(string label, Vector3 start, Vector3 end, float width, Color color)
    {
        var line = new GameObject(label).AddComponent<LineRenderer>();
        line.transform.SetParent(sampleRoot, false);
        line.sharedMaterial = sampleLineMaterial;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.sortingOrder = 5;
        return line;
    }

    private void CreateSampleWall(Vector3 outlet, float side)
    {
        Vector3 top = outlet + new Vector3(side * 1.9f, 1.5f, 0);
        Vector3 bottom = outlet + new Vector3(side * 0.22f, 0.15f, 0);
        LineRenderer wall = DrawSampleLine("Sample funnel wall", top, bottom, 0.08f, Color.white);
        EdgeCollider2D collider = wall.gameObject.AddComponent<EdgeCollider2D>();
        collider.points = new[] { (Vector2)wall.transform.InverseTransformPoint(top),
            (Vector2)wall.transform.InverseTransformPoint(bottom) };
    }

    private void UpdateSampleInput()
    {
        if (sampleBalls == null || !Input.GetMouseButtonDown(0)) return;
        Camera camera = Camera.main;
        if (camera == null) return;
        Vector2 pointer = camera.ScreenToWorldPoint(Input.mousePosition);
        int selected = -1;
        float nearest = 0.3f;
        for (int i = 0; i < sampleBalls.Length; i++)
        {
            if (sampleBalls[i] == null || sampleBalls[i].State != BallState.InBranch) continue;
            bool hasChild = false;
            for (int j = 0; j < sampleParents.Length; j++)
                if (sampleParents[j] == i && sampleBalls[j] != null && sampleBalls[j].State == BallState.InBranch)
                    hasChild = true;
            if (hasChild) continue;
            float distance = Vector2.Distance(pointer, sampleBalls[i].transform.position);
            if (distance < nearest) { nearest = distance; selected = i; }
        }
        if (selected < 0) return;
        if (sampleLinks[selected] != null) sampleLinks[selected].gameObject.SetActive(false);
        branchController.ReleaseBall(sampleBalls[selected], Vector2.down * 1.5f);
    }

    private void OnGUI()
    {
        if (!loadSampleLevelOnStart) return;
        GUI.Box(new Rect(10, 10, 310, 65), "Sample: 9 balls / 3 trays\nTap a ball with no linked children.\n" + State);
        if (GUI.Button(new Rect(10, 80, 100, 35), "Retry")) RetryLevel();
    }

    private void OnDestroy()
    {
        if (sampleLineMaterial != null) Destroy(sampleLineMaterial);
    }
}
