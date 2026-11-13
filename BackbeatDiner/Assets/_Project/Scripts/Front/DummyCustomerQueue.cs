using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Front
{
    /// <summary>
    /// Spawns 1–3 low-poly primitive customers with timeout bars (display-only).
    /// </summary>
    public sealed class DummyCustomerQueue : MonoBehaviour
    {
        [SerializeField] private Transform spawnRoot;
        [SerializeField] private Material tealMaterial;
        [SerializeField] private Material goldMaterial;
        [SerializeField] private int spawnCount = 2;
        [SerializeField] private float timeoutSeconds = 28f;

        private readonly List<CustomerSlot> _slots = new List<CustomerSlot>(3);
        private Canvas _worldHud;

        public int ActiveCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].Root != null && _slots[i].Root.activeSelf)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public void Configure(Transform root, Material teal, Material gold, int count)
        {
            spawnRoot = root;
            tealMaterial = teal;
            goldMaterial = gold;
            spawnCount = Mathf.Clamp(count, 1, 3);
            Rebuild();
        }

        /// <summary>
        /// Serve the waiting customer with the least remaining patience. Returns false if none active.
        /// </summary>
        public bool TryServeOldest(out float patienceRatio)
        {
            patienceRatio = 0f;
            var bestIndex = -1;
            var bestRemaining = float.MaxValue;
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Root == null || !slot.Root.activeSelf)
                {
                    continue;
                }

                if (slot.Remaining < bestRemaining)
                {
                    bestRemaining = slot.Remaining;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return false;
            }

            var served = _slots[bestIndex];
            patienceRatio = timeoutSeconds > 0f ? Mathf.Clamp01(served.Remaining / timeoutSeconds) : 0f;
            served.Root.SetActive(false);
            served.Remaining = timeoutSeconds;
            served.RespawnAt = Time.time + UnityEngine.Random.Range(1.8f, 3.5f);
            if (served.Fill != null)
            {
                served.Fill.fillAmount = 0f;
            }

            _slots[bestIndex] = served;
            return true;
        }

        private void Start()
        {
            if (_slots.Count == 0)
            {
                Rebuild();
            }
        }

        private void Update()
        {
            for (var i = _slots.Count - 1; i >= 0; i--)
            {
                var slot = _slots[i];
                if (slot.Root == null || !slot.Root.activeSelf)
                {
                    continue;
                }

                slot.Remaining -= Time.deltaTime;
                if (slot.Fill != null)
                {
                    slot.Fill.fillAmount = Mathf.Clamp01(slot.Remaining / timeoutSeconds);
                    slot.Fill.color = Color.Lerp(FrontHudController.Gold, FrontHudController.Teal, slot.Fill.fillAmount);
                }

                if (slot.Remaining <= 0f)
                {
                    slot.Root.SetActive(false);
                    // Respawn after a short pause so the shell stays lively.
                    slot.Remaining = timeoutSeconds;
                    slot.RespawnAt = Time.time + Random.Range(2.5f, 5f);
                }

                _slots[i] = slot;
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Root != null && !slot.Root.activeSelf && slot.RespawnAt > 0f && Time.time >= slot.RespawnAt)
                {
                    slot.Root.SetActive(true);
                    slot.Remaining = timeoutSeconds * Random.Range(0.7f, 1f);
                    slot.RespawnAt = 0f;
                    if (slot.Fill != null)
                    {
                        slot.Fill.fillAmount = 1f;
                    }

                    _slots[i] = slot;
                }
            }
        }

        private void Rebuild()
        {
            ClearSlots();
            if (spawnRoot == null)
            {
                var go = new GameObject("CustomerQueueRoot");
                spawnRoot = go.transform;
            }

            EnsureWorldHud();
            spawnCount = Mathf.Clamp(spawnCount <= 0 ? Random.Range(1, 4) : spawnCount, 1, 3);

            for (var i = 0; i < spawnCount; i++)
            {
                var x = (i - (spawnCount - 1) * 0.5f) * 1.35f;
                var customer = BuildCustomer($"Customer_{i + 1}", new Vector3(x, 0f, 1.2f));
                var bar = BuildTimeoutBar(customer.transform, i);
                _slots.Add(new CustomerSlot
                {
                    Root = customer,
                    Fill = bar,
                    Remaining = timeoutSeconds * Random.Range(0.55f, 1f),
                    RespawnAt = 0f
                });
            }
        }

        private GameObject BuildCustomer(string name, Vector3 localPos)
        {
            var root = new GameObject(name);
            root.transform.SetParent(spawnRoot, false);
            root.transform.localPosition = localPos;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.55f, 0.7f, 0.55f);
            ApplyMat(body, tealMaterial, FrontHudController.Teal);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            head.transform.localScale = Vector3.one * 0.45f;
            ApplyMat(head, goldMaterial, FrontHudController.Gold);

            return root;
        }

        private Image BuildTimeoutBar(Transform customer, int index)
        {
            if (_worldHud == null)
            {
                return null;
            }

            var go = new GameObject($"Timeout_{index}");
            go.transform.SetParent(_worldHud.transform, false);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.08f, 0.75f);
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.55f);
            rt.anchorMax = new Vector2(0.5f, 0.55f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 14f);
            rt.anchoredPosition = new Vector2((index - 1) * 140f, 0f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            var fill = fillGo.AddComponent<Image>();
            fill.color = FrontHudController.Teal;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);

            // Follow customer in screen space via simple updater component.
            var follower = go.AddComponent<TimeoutBarFollower>();
            follower.Configure(customer, new Vector3(0f, 2.35f, 0f));
            return fill;
        }

        private void EnsureWorldHud()
        {
            if (_worldHud != null)
            {
                return;
            }

            var canvasGo = GameObject.Find("CustomerTimeoutHUD");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("CustomerTimeoutHUD");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            _worldHud = canvasGo.GetComponent<Canvas>();
        }

        private void ClearSlots()
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Root != null)
                {
                    Destroy(_slots[i].Root);
                }
            }

            _slots.Clear();
        }

        private static void ApplyMat(GameObject go, Material preferred, Color fallback)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }

            if (preferred != null)
            {
                renderer.sharedMaterial = preferred;
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", fallback);
            }
            else if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", fallback);
            }

            renderer.sharedMaterial = mat;
        }

        private struct CustomerSlot
        {
            public GameObject Root;
            public Image Fill;
            public float Remaining;
            public float RespawnAt;
        }

        private sealed class TimeoutBarFollower : MonoBehaviour
        {
            private Transform _target;
            private Vector3 _worldOffset;
            private RectTransform _rt;
            private Camera _cam;

            public void Configure(Transform target, Vector3 worldOffset)
            {
                _target = target;
                _worldOffset = worldOffset;
                _rt = GetComponent<RectTransform>();
                _cam = Camera.main;
            }

            private void LateUpdate()
            {
                if (_target == null || _rt == null)
                {
                    return;
                }

                if (_cam == null)
                {
                    _cam = Camera.main;
                }

                if (_cam == null)
                {
                    return;
                }

                var screen = _cam.WorldToScreenPoint(_target.position + _worldOffset);
                if (screen.z < 0f)
                {
                    _rt.gameObject.SetActive(false);
                    return;
                }

                _rt.gameObject.SetActive(_target.gameObject.activeInHierarchy);
                _rt.position = screen;
            }
        }
    }
}
