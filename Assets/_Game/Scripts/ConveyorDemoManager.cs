using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PaperSort.Game
{
    public class ConveyorDemoManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SplineConveyorBelt2D conveyorBelt;
        [SerializeField] private List<RectTransform> mainSquareUIElements = new List<RectTransform>();
        [SerializeField] private RectTransform centerPositionRef;
        [SerializeField] private Camera gameCamera;

        [Header("Animation Settings")]
        [SerializeField] private float slideDuration = 0.35f;

        [Header("Small Falling Square Setup")]
        [SerializeField] private Sprite smallSquareSprite;
        [SerializeField] private Vector2 smallSquareSize = new Vector2(0.4f, 0.4f);
        [SerializeField] private float dropDuration = 0.45f;
        [SerializeField] private float staggerDelay = 0.08f;

        private List<RectTransform> activeQueue = new List<RectTransform>();
        private bool isProcessingClick = false;
        private Dictionary<int, Transform> slotOccupants = new Dictionary<int, Transform>();

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            EnsureReferences();
            InitializeQueue();
        }

        public void EnsureReferences()
        {
            if (conveyorBelt == null)
            {
                conveyorBelt = Object.FindObjectOfType<SplineConveyorBelt2D>();
            }

            if (gameCamera == null)
            {
                gameCamera = Camera.main;
                if (gameCamera == null) gameCamera = Object.FindObjectOfType<Camera>();
            }

            if (centerPositionRef == null)
            {
                GameObject centerRefObj = GameObject.Find("CenterPositionRef");
                if (centerRefObj != null)
                {
                    centerPositionRef = centerRefObj.GetComponent<RectTransform>();
                }
            }

            if (mainSquareUIElements != null)
            {
                mainSquareUIElements.RemoveAll(item => item == null);
            }
            else
            {
                mainSquareUIElements = new List<RectTransform>();
            }

            // Auto-find Canvas2 UI squares if list is empty
            if (mainSquareUIElements.Count == 0)
            {
                Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
                foreach (Canvas c in canvases)
                {
                    if (c.name.Contains("Canvas2") || c.name.Contains("Canvas"))
                    {
                        foreach (Transform child in c.transform)
                        {
                            if (child.gameObject.activeSelf && !child.name.Contains("Panel") && !child.name.Contains("Ref"))
                            {
                                RectTransform rt = child as RectTransform;
                                if (rt != null && !mainSquareUIElements.Contains(rt))
                                {
                                    mainSquareUIElements.Add(rt);
                                }
                            }
                        }
                    }
                }
            }

            // Sort mainSquareUIElements so the square closest to centerPositionRef is ALWAYS index 0 (Active initially)!
            if (centerPositionRef != null && mainSquareUIElements.Count > 1)
            {
                Vector2 centerAnchored = centerPositionRef.anchoredPosition;
                mainSquareUIElements.Sort((a, b) =>
                {
                    if (a == null) return 1;
                    if (b == null) return -1;
                    float distA = Vector2.Distance(a.anchoredPosition, centerAnchored);
                    float distB = Vector2.Distance(b.anchoredPosition, centerAnchored);
                    return distA.CompareTo(distB);
                });
            }
        }

        public void InitializeQueue()
        {
            activeQueue.Clear();
            if (mainSquareUIElements != null)
            {
                foreach (var item in mainSquareUIElements)
                {
                    if (item != null)
                    {
                        activeQueue.Add(item);
                    }
                }
            }

            // Keep initial positions untouched on Start. Make only center square clickable.
            UpdateInteractivityOnly();
        }

        private void UpdateInteractivityOnly()
        {
            for (int i = 0; i < activeQueue.Count; i++)
            {
                RectTransform rect = activeQueue[i];
                if (rect == null) continue;

                Button btn = rect.GetComponent<Button>();
                if (btn == null) btn = rect.gameObject.AddComponent<Button>();

                btn.onClick.RemoveAllListeners();

                if (i == 0)
                {
                    // Center Square (Green / Closest to CenterPositionRef) -> Clickable
                    btn.interactable = !isProcessingClick;
                    btn.onClick.AddListener(OnCenterSquareClicked);
                }
                else
                {
                    // Queued Side Squares -> Non-clickable
                    btn.interactable = false;
                }
            }
        }

        private void OnCenterSquareClicked()
        {
            if (isProcessingClick || activeQueue.Count == 0) return;
            StartCoroutine(Routine_ProcessCenterClick());
        }

        private IEnumerator Routine_ProcessCenterClick()
        {
            isProcessingClick = true;

            if (activeQueue.Count == 0 || activeQueue[0] == null)
            {
                isProcessingClick = false;
                yield break;
            }

            // 1. Get current active center square
            RectTransform centerRect = activeQueue[0];
            Image img = centerRect.GetComponent<Image>();
            Color color = img != null ? img.color : Color.red;

            // Disable button during processing
            Button centerBtn = centerRect.GetComponent<Button>();
            if (centerBtn != null) centerBtn.interactable = false;

            // Convert UI position to 2D World Space
            Vector3 spawnWorldPos = GetWorldPositionFromUI(centerPositionRef != null ? centerPositionRef : centerRect);

            // 2. Spawn 7 small squares falling into rotating belt circles
            yield return StartCoroutine(Routine_SpawnSevenSquares(color, spawnWorldPos));

            // 3. Disappear animation for center main square (shrink to 0)
            float shrinkElapsed = 0f;
            Vector3 origScale = centerRect.localScale;
            while (shrinkElapsed < 0.2f)
            {
                shrinkElapsed += Time.deltaTime;
                float t = shrinkElapsed / 0.2f;
                if (centerRect != null)
                {
                    centerRect.localScale = Vector3.Lerp(origScale, Vector3.zero, t);
                }
                yield return null;
            }

            if (centerRect != null)
            {
                centerRect.gameObject.SetActive(false);
            }

            activeQueue.RemoveAt(0); // Remove finished center square from queue

            // 4. AFTER CENTER IS DONE: Move next queued square into CenterPositionRef!
            if (activeQueue.Count > 0 && activeQueue[0] != null)
            {
                RectTransform nextSquare = activeQueue[0];
                Vector2 targetCenterPos = centerPositionRef != null ? centerPositionRef.anchoredPosition : new Vector2(0f, -217f);

                // Smoothly slide next square to center
                yield return StartCoroutine(Routine_SlideUI(nextSquare, targetCenterPos, slideDuration));
            }

            isProcessingClick = false;

            // Enable new center square for clicking
            UpdateInteractivityOnly();
        }

        private Vector3 GetWorldPositionFromUI(RectTransform rectTransform)
        {
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameCamera == null) gameCamera = Object.FindObjectOfType<Camera>();

            Canvas canvas = rectTransform != null ? rectTransform.GetComponentInParent<Canvas>() : null;
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                if (gameCamera != null)
                {
                    Vector3 screenPoint = rectTransform.position;
                    screenPoint.z = Mathf.Abs(gameCamera.transform.position.z);
                    Vector3 worldPoint = gameCamera.ScreenToWorldPoint(screenPoint);
                    worldPoint.z = 0f;
                    return worldPoint;
                }
            }
            else if (canvas != null && canvas.worldCamera != null)
            {
                Vector3 screenPoint = canvas.worldCamera.WorldToScreenPoint(rectTransform.position);
                if (gameCamera != null)
                {
                    screenPoint.z = Mathf.Abs(gameCamera.transform.position.z);
                    Vector3 worldPoint = gameCamera.ScreenToWorldPoint(screenPoint);
                    worldPoint.z = 0f;
                    return worldPoint;
                }
            }

            if (rectTransform != null)
            {
                Vector3 pos = rectTransform.position;
                pos.z = 0f;
                return pos;
            }

            return Vector3.zero;
        }

        private IEnumerator Routine_SlideUI(RectTransform rect, Vector2 targetAnchoredPos, float duration)
        {
            float elapsed = 0f;
            Vector2 startPos = rect.anchoredPosition;
            while (elapsed < duration)
            {
                if (rect == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeT = 1f - Mathf.Pow(1f - t, 3f); // Smooth EaseOutCubic
                rect.anchoredPosition = Vector2.Lerp(startPos, targetAnchoredPos, easeT);
                yield return null;
            }
            if (rect != null) rect.anchoredPosition = targetAnchoredPos;
        }

        private IEnumerator Routine_SpawnSevenSquares(Color color, Vector3 spawnOriginWorldPos)
        {
            if (conveyorBelt == null)
            {
                conveyorBelt = Object.FindObjectOfType<SplineConveyorBelt2D>();
                if (conveyorBelt == null) yield break;
            }

            int totalSlots = conveyorBelt.SlotCount;
            if (totalSlots == 0) yield break;

            int spawnedCount = 0;

            for (int i = 0; i < totalSlots && spawnedCount < 7; i++)
            {
                if (!slotOccupants.ContainsKey(i) || slotOccupants[i] == null)
                {
                    int targetSlotIndex = i;
                    GameObject squareObj = CreateSmallSquareGameObject(color, spawnOriginWorldPos);
                    slotOccupants[targetSlotIndex] = squareObj.transform;

                    StartCoroutine(Routine_FallAndAttachToSlot(squareObj.transform, targetSlotIndex, dropDuration));

                    spawnedCount++;
                    yield return new WaitForSeconds(staggerDelay);
                }
            }
        }

        private GameObject CreateSmallSquareGameObject(Color color, Vector3 initialPosition)
        {
            GameObject squareObj = new GameObject("Small_Square");
            squareObj.transform.position = initialPosition;
            squareObj.transform.localScale = (Vector3)smallSquareSize;

            SpriteRenderer sr = squareObj.AddComponent<SpriteRenderer>();
            sr.color = color;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 10;

            if (smallSquareSprite == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            else
            {
                sr.sprite = smallSquareSprite;
            }

            return squareObj;
        }

        private IEnumerator Routine_FallAndAttachToSlot(Transform squareTransform, int targetSlotIndex, float duration)
        {
            float elapsed = 0f;
            Vector3 startPos = squareTransform.position;

            while (elapsed < duration)
            {
                if (squareTransform == null) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeT = t * t;

                Vector3 currentSlotWorldPos = conveyorBelt != null ? conveyorBelt.GetSlotWorldPosition(targetSlotIndex) : startPos;
                squareTransform.position = Vector3.Lerp(startPos, currentSlotWorldPos, easeT);
                yield return null;
            }

            if (squareTransform != null && conveyorBelt != null)
            {
                conveyorBelt.AttachObjectToSlot(squareTransform, targetSlotIndex, resetLocalPosition: true);

                SpriteRenderer sr = squareTransform.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingOrder = 3;
                }
            }
        }
    }
}
