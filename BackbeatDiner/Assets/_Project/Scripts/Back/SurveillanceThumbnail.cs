using BackbeatDiner.Core;
using BackbeatDiner.Front;
using BackbeatDiner.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Back
{
    /// <summary>
    /// KG-4 / KG-3 §6: Back-player surveillance thumbnail of FrontCamera (~2Hz).
    /// Shows BLIND overlay while <see cref="NoteHighway.HasActiveNotes"/> (last frame held).
    /// Japanese HUD label.
    /// </summary>
    public sealed class SurveillanceThumbnail : MonoBehaviour
    {
        private static readonly Color Gold = CookingSimHud.Gold;
        private static readonly Color Teal = CookingSimHud.Teal;
        private static readonly Color PanelBg = CookingSimHud.PanelBg;
        private static readonly Color BlindBg = new Color(0.02f, 0.02f, 0.04f, 0.88f);

        [SerializeField] private NoteHighway highway;
        [SerializeField] private Camera frontCamera;
        [SerializeField] private Camera surveillanceCamera;
        [SerializeField] private float updateHz = 2f;
        [SerializeField] private int textureWidth = 256;
        [SerializeField] private int textureHeight = 144;

        private RenderTexture _rt;
        private Camera _surveillanceCam;
        private bool _ownsSurveillanceCam;
        private RawImage _raw;
        private GameObject _blindRoot;
        private Text _title;
        private Text _blindLabel;
        private float _nextCaptureTime;
        private bool _captureArmed;
        private bool _wasBlind;

        public bool IsBlind => highway != null && highway.HasActiveNotes;

        public void Configure(NoteHighway noteHighway, Camera front)
        {
            Configure(noteHighway, front, null);
        }

        public void Configure(NoteHighway noteHighway, Camera front, Camera surveillance)
        {
            highway = noteHighway;
            frontCamera = front;
            if (surveillance != null)
            {
                surveillanceCamera = surveillance;
            }

            EnsurePipeline();
            EnsureUi();
            ApplyBlindVisual(IsBlind);
        }

        private void Start()
        {
            EnsurePipeline();
            EnsureUi();
            ApplyBlindVisual(IsBlind);
        }

        private void OnDestroy()
        {
            if (_surveillanceCam != null)
            {
                _surveillanceCam.targetTexture = null;
                _surveillanceCam.enabled = false;
                if (_ownsSurveillanceCam)
                {
                    Destroy(_surveillanceCam.gameObject);
                }

                _surveillanceCam = null;
                _ownsSurveillanceCam = false;
            }

            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }
        }

        private void Update()
        {
            EnsurePipeline();
            EnsureUi();

            var blind = IsBlind;
            if (blind != _wasBlind)
            {
                ApplyBlindVisual(blind);
                _wasBlind = blind;
            }

            if (blind)
            {
                if (_surveillanceCam != null)
                {
                    _surveillanceCam.enabled = false;
                }

                _captureArmed = false;
                return;
            }

            // URP-safe ~2Hz: enable cam for one frame, then disable.
            if (_captureArmed)
            {
                if (_surveillanceCam != null)
                {
                    _surveillanceCam.enabled = false;
                }

                _captureArmed = false;
                var hz = Mathf.Max(0.25f, updateHz);
                _nextCaptureTime = Time.unscaledTime + 1f / hz;
                return;
            }

            if (Time.unscaledTime >= _nextCaptureTime)
            {
                SyncSurveillanceCam();
                if (_surveillanceCam != null)
                {
                    _surveillanceCam.enabled = true;
                    _captureArmed = true;
                }
            }
        }

        private void ApplyBlindVisual(bool blind)
        {
            if (_blindRoot != null)
            {
                _blindRoot.SetActive(blind);
            }

            if (_title != null)
            {
                _title.text = blind ? "監視 — ブラインド" : "監視";
                _title.color = blind ? Gold : Teal;
            }

            if (_blindLabel != null)
            {
                _blindLabel.text = "BLIND\n譜面中";
            }
        }

        private void EnsurePipeline()
        {
            if (frontCamera == null)
            {
                var go = GameObject.Find("FrontCamera");
                if (go != null)
                {
                    frontCamera = go.GetComponent<Camera>();
                }
            }

            if (_rt == null)
            {
                _rt = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.Default)
                {
                    name = "SurveillanceRT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    antiAliasing = 1
                };
                _rt.Create();
            }

            if (_surveillanceCam == null)
            {
                // Prefer scene-authored / Configure-assigned Camera. Runtime recreate is fallback only.
                // Do not use `??` with GetComponent — Unity "fake null" destroyed Cameras are
                // non-null in C# and skip AddComponent, then throw MissingComponentException on use.
                Camera existing = null;
                if (surveillanceCamera != null)
                {
                    existing = surveillanceCamera;
                }

                GameObject camGo = null;
                if (existing == null)
                {
                    camGo = GameObject.Find("SurveillanceCamera");
                    if (camGo != null)
                    {
                        existing = camGo.GetComponent<Camera>();
                    }
                }
                else
                {
                    camGo = existing.gameObject;
                }

                if (existing == null)
                {
                    if (camGo != null)
                    {
                        // Rename so deferred Destroy cannot be re-found as SurveillanceCamera this frame.
                        camGo.name = "SurveillanceCamera_Stale";
                        Destroy(camGo);
                    }

                    camGo = new GameObject("SurveillanceCamera");
                    _surveillanceCam = camGo.AddComponent<Camera>();
                    _ownsSurveillanceCam = true;
                }
                else
                {
                    _surveillanceCam = existing;
                    surveillanceCamera = existing;
                    _ownsSurveillanceCam = false;
                }

                if (_surveillanceCam == null)
                {
                    Debug.LogError("[Surveillance] Failed to bind SurveillanceCamera");
                    return;
                }

                _surveillanceCam.enabled = false;
                _surveillanceCam.targetTexture = _rt;
                _surveillanceCam.clearFlags = CameraClearFlags.Skybox;
                _surveillanceCam.backgroundColor = new Color(0.055f, 0.122f, 0.2f);
                _surveillanceCam.depth = -10;
                _surveillanceCam.nearClipPlane = 0.1f;
                _surveillanceCam.farClipPlane = 60f;
                _surveillanceCam.rect = new Rect(0f, 0f, 1f, 1f);
                _surveillanceCam.allowHDR = false;
                _surveillanceCam.allowMSAA = false;
                var listener = _surveillanceCam.GetComponent<AudioListener>();
                if (listener != null)
                {
                    Destroy(listener);
                }
            }

            SyncSurveillanceCam();

            if (_raw != null && _raw.texture != _rt)
            {
                _raw.texture = _rt;
            }
        }

        private void SyncSurveillanceCam()
        {
            if (_surveillanceCam == null || frontCamera == null)
            {
                return;
            }

            var t = frontCamera.transform;
            _surveillanceCam.transform.SetPositionAndRotation(t.position, t.rotation);
            _surveillanceCam.fieldOfView = frontCamera.fieldOfView;
            _surveillanceCam.orthographic = frontCamera.orthographic;
            _surveillanceCam.orthographicSize = frontCamera.orthographicSize;
            _surveillanceCam.nearClipPlane = frontCamera.nearClipPlane;
            _surveillanceCam.farClipPlane = frontCamera.farClipPlane;
            _surveillanceCam.cullingMask = frontCamera.cullingMask;
            _surveillanceCam.clearFlags = frontCamera.clearFlags;
            _surveillanceCam.backgroundColor = frontCamera.backgroundColor;
            _surveillanceCam.targetTexture = _rt;
        }

        private void EnsureUi()
        {
            if (_raw != null)
            {
                return;
            }

            var canvasGo = GameObject.Find("VerticalSliceHUD");
            if (canvasGo == null)
            {
                canvasGo = GameObject.Find("SurveillanceHUD");
                if (canvasGo == null)
                {
                    canvasGo = new GameObject("SurveillanceHUD");
                    var canvas = canvasGo.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 24;
                    canvasGo.AddComponent<CanvasScaler>().uiScaleMode =
                        CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    canvasGo.AddComponent<GraphicRaycaster>();
                }
            }

            var root = canvasGo.transform.Find("SurveillanceHud");
            GameObject rootGo;
            Image bg;
            if (root == null)
            {
                rootGo = new GameObject("SurveillanceHud");
                rootGo.transform.SetParent(canvasGo.transform, false);
                bg = rootGo.AddComponent<Image>();
            }
            else
            {
                rootGo = root.gameObject;
                bg = rootGo.GetComponent<Image>() ?? rootGo.AddComponent<Image>();
            }

            bg.color = PanelBg;
            bg.raycastTarget = false;
            var rt = bg.rectTransform;
            // Back fullscreen — lower-right PiP of Front floor
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-24f, 280f);
            rt.sizeDelta = new Vector2(320f, 210f);

            var outline = rootGo.GetComponent<Outline>() ?? rootGo.AddComponent<Outline>();
            outline.effectColor = CookingSimHud.BorderGold;
            outline.effectDistance = new Vector2(3f, -3f);

            // Inner teal frame for PiP clarity
            var frameT = rootGo.transform.Find("PipFrame");
            Image frame;
            if (frameT == null)
            {
                var frameGo = new GameObject("PipFrame");
                frameGo.transform.SetParent(rootGo.transform, false);
                frame = frameGo.AddComponent<Image>();
            }
            else
            {
                frame = frameT.GetComponent<Image>() ?? frameT.gameObject.AddComponent<Image>();
            }

            frame.color = new Color(Teal.r, Teal.g, Teal.b, 0.55f);
            frame.raycastTarget = false;
            var frt = frame.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(1f, 1f);
            frt.offsetMin = new Vector2(6f, 6f);
            frt.offsetMax = new Vector2(-6f, -34f);

            _title = EnsureTitle(rootGo.transform);

            var rawT = rootGo.transform.Find("SurveillanceRaw");
            if (rawT == null)
            {
                var rawGo = new GameObject("SurveillanceRaw");
                rawGo.transform.SetParent(rootGo.transform, false);
                _raw = rawGo.AddComponent<RawImage>();
            }
            else
            {
                _raw = rawT.GetComponent<RawImage>() ?? rawT.gameObject.AddComponent<RawImage>();
            }

            _raw.raycastTarget = false;
            _raw.texture = _rt;
            _raw.color = Color.white;
            var rawRt = _raw.rectTransform;
            rawRt.anchorMin = new Vector2(0f, 0f);
            rawRt.anchorMax = new Vector2(1f, 1f);
            rawRt.offsetMin = new Vector2(10f, 10f);
            rawRt.offsetMax = new Vector2(-10f, -38f);

            var blindT = rootGo.transform.Find("BlindOverlay");
            if (blindT == null)
            {
                _blindRoot = new GameObject("BlindOverlay");
                _blindRoot.transform.SetParent(rootGo.transform, false);
                var img = _blindRoot.AddComponent<Image>();
                img.color = BlindBg;
                img.raycastTarget = false;
            }
            else
            {
                _blindRoot = blindT.gameObject;
                var img = _blindRoot.GetComponent<Image>() ?? _blindRoot.AddComponent<Image>();
                img.color = BlindBg;
            }

            var blindRt = _blindRoot.GetComponent<RectTransform>();
            blindRt.anchorMin = new Vector2(0f, 0f);
            blindRt.anchorMax = new Vector2(1f, 1f);
            blindRt.offsetMin = new Vector2(10f, 10f);
            blindRt.offsetMax = new Vector2(-10f, -38f);

            _blindLabel = EnsureBlindLabel(_blindRoot.transform);
            _blindRoot.SetActive(false);
        }

        private static Text EnsureTitle(Transform parent)
        {
            var t = parent.Find("SurveillanceTitle");
            Text label;
            if (t == null)
            {
                var go = new GameObject("SurveillanceTitle");
                go.transform.SetParent(parent, false);
                label = go.AddComponent<Text>();
            }
            else
            {
                label = t.GetComponent<Text>() ?? t.gameObject.AddComponent<Text>();
            }

            label.font = CookingSimHud.UiFont();
            label.raycastTarget = false;
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -4f);
            rt.sizeDelta = new Vector2(-12f, 28f);
            label.fontSize = CookingSimHud.FontRow;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Teal;
            label.text = "監視カメラ（表フロア）";
            return label;
        }

        private static Text EnsureBlindLabel(Transform parent)
        {
            var t = parent.Find("BlindLabel");
            Text label;
            if (t == null)
            {
                var go = new GameObject("BlindLabel");
                go.transform.SetParent(parent, false);
                label = go.AddComponent<Text>();
            }
            else
            {
                label = t.GetComponent<Text>() ?? t.gameObject.AddComponent<Text>();
            }

            label.font = CookingSimHud.UiFont();
            label.raycastTarget = false;
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            label.fontSize = 28;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Gold;
            label.text = "BLIND\n譜面演奏中";
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }
    }
}
