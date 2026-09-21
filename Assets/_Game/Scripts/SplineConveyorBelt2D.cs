using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PaperSort.Game
{
    [ExecuteAlways]
    [RequireComponent(typeof(SplineContainer))]
    public class SplineConveyorBelt2D : MonoBehaviour
    {
        [Header("Sprite Settings")]
        [Tooltip("The 2D sprite used for the conveyor belt rollers/treads.")]
        [SerializeField] private Sprite rollerSprite;

        [Tooltip("Color tint applied to each 2D roller sprite.")]
        [SerializeField] private Color spriteColor = Color.white;

        [Tooltip("If TRUE, sprites rotate to align with the curve direction. If FALSE, sprites maintain a fixed upright rotation (e.g. shadow stays on top).")]
        [SerializeField] private bool rotateWithTangent = false;

        [Tooltip("Fixed or additional 2D Z-rotation (in degrees). Use 0 for upright shading.")]
        [SerializeField] private float rotationOffset = 0f;

        [Tooltip("Sorting layer name for the 2D roller sprites.")]
        [SerializeField] private string sortingLayerName = "Default";

        [Tooltip("Order in layer for the 2D roller sprites.")]
        [SerializeField] private int sortingOrder = 1;

        [Tooltip("Scale for each 2D roller sprite (unaffected by parent Spline scale).")]
        [SerializeField] private Vector2 spriteScale = Vector2.one;

        [Header("Conveyor Belt Settings")]
        [Tooltip("Number of 2D roller elements distributed along the spline loop.")]
        [SerializeField] private int totalElements = 16;

        [Tooltip("Speed of movement along the spline (loops per second).")]
        [SerializeField] private float speed = 0.2f;

        [Tooltip("Direction of movement: Positive = Forward, Negative = Reverse.")]
        [SerializeField] private bool moveReverse = false;

        [Header("Editor Preview Toggle")]
        [Tooltip("Toggle to show/hide roller sprites preview in Editor mode. Uncheck to view only the raw Spline path.")]
        [SerializeField] private bool showEditorPreview = true;

        [Tooltip("Enable smooth animation directly inside the Unity Editor without entering Play Mode.")]
        [SerializeField] private bool animateInEditor = true;

        private SplineContainer splineContainer;
        private List<Transform> elementTransforms = new List<Transform>();
        private List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
        private float[] progressOffsets;

#if UNITY_EDITOR
        private double lastEditorTime;
#endif

        #region Properties & Public API for Dynamic Attachment

        /// <summary>
        /// Gets the total number of circle roller slots on the conveyor belt.
        /// </summary>
        public int SlotCount => elementTransforms != null ? elementTransforms.Count : 0;

        /// <summary>
        /// Gets the Transform of a specific slot by index (0 to SlotCount - 1).
        /// Childing items (e.g. Cubes/Paper) to this Transform automatically attaches them to the belt.
        /// </summary>
        public Transform GetSlotTransform(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < elementTransforms.Count)
                return elementTransforms[slotIndex];
            return null;
        }

        /// <summary>
        /// Gets the current 3D/2D world position of a specific slot by index.
        /// </summary>
        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < elementTransforms.Count && elementTransforms[slotIndex] != null)
                return elementTransforms[slotIndex].position;
            return Vector3.zero;
        }

        /// <summary>
        /// Gets the current normalized progress (0.0 to 1.0) along the spline loop for a specific slot.
        /// </summary>
        public float GetSlotProgress(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < progressOffsets.Length)
                return progressOffsets[slotIndex];
            return 0f;
        }

        /// <summary>
        /// Dynamically attaches a game item (e.g. Cube / Block / Paper) to a target slot index on the conveyor belt.
        /// </summary>
        /// <param name="item">The Transform of the item to attach.</param>
        /// <param name="slotIndex">Target slot index (0 to SlotCount - 1).</param>
        /// <param name="resetLocalPosition">If true, snaps item position to the center of the circle slot.</param>
        public void AttachObjectToSlot(Transform item, int slotIndex, bool resetLocalPosition = true)
        {
            Transform slot = GetSlotTransform(slotIndex);
            if (slot != null && item != null)
            {
                item.SetParent(slot);
                if (resetLocalPosition)
                {
                    item.localPosition = Vector3.zero;
                    item.localRotation = Quaternion.identity;
                }
            }
        }

        /// <summary>
        /// Detaches a game item from the conveyor belt slot.
        /// </summary>
        public void DetachObject(Transform item, Transform newParent = null)
        {
            if (item != null)
            {
                item.SetParent(newParent);
            }
        }

        #endregion

        private void Awake()
        {
            splineContainer = GetComponent<SplineContainer>();
        }

        private void OnEnable()
        {
            splineContainer = GetComponent<SplineContainer>();
            SetupElements();

#if UNITY_EDITOR
            lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= OnEditorUpdate;
#endif
        }

#if UNITY_EDITOR
        private void OnEditorUpdate()
        {
            if (Application.isPlaying || this == null || gameObject == null) return;

            if (!showEditorPreview)
            {
                if (elementTransforms.Count > 0) ClearElements();
                return;
            }

            if (elementTransforms.Count != totalElements || elementTransforms.Contains(null))
            {
                SetupElements();
            }

            if (!animateInEditor)
            {
                UpdatePositions(0f);
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - lastEditorTime);
            lastEditorTime = currentTime;

            deltaTime = Mathf.Min(deltaTime, 0.05f);

            if (splineContainer != null && splineContainer.Spline != null && elementTransforms.Count == totalElements)
            {
                float speedDirection = moveReverse ? -speed : speed;
                UpdatePositions(speedDirection * deltaTime);
                SceneView.RepaintAll();
            }
        }
#endif

        private void OnValidate()
        {
            if (splineContainer == null) splineContainer = GetComponent<SplineContainer>();
#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (this != null) SetupElements();
            };
#endif
        }

        public void ClearElements()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name.StartsWith("Roller_2D_"))
                {
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }
            elementTransforms.Clear();
            spriteRenderers.Clear();
        }

        public void SetupElements()
        {
            ClearElements();

            if (!Application.isPlaying && !showEditorPreview) return;
            if (totalElements <= 0) return;

            progressOffsets = new float[totalElements];
            float spacing = 1f / totalElements;

            for (int i = 0; i < totalElements; i++)
            {
                GameObject obj = new GameObject($"Roller_2D_{i}");
                obj.transform.SetParent(transform);

                SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = rollerSprite;
                sr.color = spriteColor;
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = sortingOrder;

                elementTransforms.Add(obj.transform);
                spriteRenderers.Add(sr);
                progressOffsets[i] = i * spacing;
            }

            UpdatePositions(0f);
        }

        private void Update()
        {
            if (splineContainer == null || splineContainer.Spline == null) return;

            if (Application.isPlaying)
            {
                if (elementTransforms.Count != totalElements || elementTransforms.Contains(null))
                {
                    SetupElements();
                    return;
                }

                float speedDirection = moveReverse ? -speed : speed;
                UpdatePositions(speedDirection * Time.deltaTime);
            }
        }

        private void UpdatePositions(float deltaProgress)
        {
            Vector3 parentLossyScale = transform.lossyScale;
            Vector3 unscaledLocalScale = new Vector3(
                spriteScale.x / (Mathf.Abs(parentLossyScale.x) > 0.0001f ? Mathf.Abs(parentLossyScale.x) : 1f),
                spriteScale.y / (Mathf.Abs(parentLossyScale.y) > 0.0001f ? Mathf.Abs(parentLossyScale.y) : 1f),
                1f
            );

            for (int i = 0; i < elementTransforms.Count; i++)
            {
                if (elementTransforms[i] == null) continue;

                if (spriteRenderers.Count > i && spriteRenderers[i] != null)
                {
                    spriteRenderers[i].sprite = rollerSprite;
                    spriteRenderers[i].color = spriteColor;
                    spriteRenderers[i].sortingLayerName = sortingLayerName;
                    spriteRenderers[i].sortingOrder = sortingOrder;
                }

                elementTransforms[i].localScale = unscaledLocalScale;

                progressOffsets[i] = (progressOffsets[i] + deltaProgress) % 1f;
                if (progressOffsets[i] < 0f) progressOffsets[i] += 1f;

                float3 position = splineContainer.EvaluatePosition(progressOffsets[i]);
                float3 tangent = splineContainer.EvaluateTangent(progressOffsets[i]);

                elementTransforms[i].position = (Vector3)position;

                if (rotateWithTangent && math.lengthsq(tangent.xy) > 0.0001f)
                {
                    float zAngle = (Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg) + rotationOffset;
                    elementTransforms[i].rotation = Quaternion.Euler(0f, 0f, zAngle);
                }
                else
                {
                    elementTransforms[i].rotation = Quaternion.Euler(0f, 0f, rotationOffset);
                }
            }
        }

        /// <summary>
        /// Gets the current 2D world position and tangent direction for any target progress (0.0 to 1.0) along the spline.
        /// Useful for attaching blocks or paper items to the belt.
        /// </summary>
        public void EvaluateAtProgress(float progress, out Vector2 worldPos, out Vector2 direction)
        {
            progress = progress % 1f;
            if (progress < 0f) progress += 1f;

            float3 pos = splineContainer.EvaluatePosition(progress);
            float3 tan = splineContainer.EvaluateTangent(progress);

            worldPos = pos.xy;
            direction = math.lengthsq(tan.xy) > 0.0001f ? math.normalize(tan.xy) : Vector2.right;
        }
    }
}
