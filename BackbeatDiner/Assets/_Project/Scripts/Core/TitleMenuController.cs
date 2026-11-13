using System.Collections;
using BackbeatDiner.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// Title menu: full-bleed art, brand logo, catchcopy, role CTAs → VerticalSlicePrototype.
    /// Two-device: pick 裏 / 表, then Host create or Join localhost (NGO), or offline start.
    /// </summary>
    public sealed class TitleMenuController : MonoBehaviour
    {
        public static readonly Color Gold = new Color(0.7882353f, 0.6352941f, 0.15294118f); // #C9A227
        public static readonly Color GoldBright = new Color(0.93f, 0.78f, 0.32f, 1f);
        public static readonly Color Teal = new Color(0.101960784f, 0.419607843f, 0.419607843f); // #1A6B6B
        public static readonly Color Navy = new Color(0.05490196f, 0.12156863f, 0.2f); // #0E1F33
        public static readonly Color Cream = new Color(0.94f, 0.90f, 0.82f, 0.92f);

        private const string VerticalSliceScene = "VerticalSlicePrototype";
        private const string CatchcopyPrimary = "見えない相棒と、最高の一皿を。";
        private const string CatchcopySecondary = "音で支える、非対称協力レストランゲーム。";
        private const string CreditsLine = "BACKBEAT DINER  ·  prototype build";

        [SerializeField] private string playSceneName = VerticalSliceScene;
        [SerializeField] private Sprite titleBackground;

        private Text _netStatusLabel;
        private Text _roleSelectLabel;
        private bool _netBusy;

        private void Start()
        {
            EnsureCamera();
            EnsureEventSystem();
            ResolveBackgroundSprite();
            if (!VerticalSliceRoleSelection.HasUserSelection)
            {
                VerticalSliceRoleSelection.LoadSavedOrDefault();
            }

            BuildUi();
            RefreshRoleSelectLabel();
            Debug.Log(
                "[BACKBEAT DINER] TitleMenu ready — role 1/2 · Host(H) / Join(J) localhost · offline 裏/表 → " +
                playSceneName);
        }

        private void Update()
        {
            if (_netBusy)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                SelectRole(VerticalSlicePlayRole.Back);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                SelectRole(VerticalSlicePlayRole.Front);
            }
            else if (Input.GetKeyDown(KeyCode.H))
            {
                StartAsHost();
            }
            else if (Input.GetKeyDown(KeyCode.J))
            {
                StartAsClient();
            }
        }

        public void StartDayAsBack() => StartDayAs(VerticalSlicePlayRole.Back);

        public void StartDayAsFront() => StartDayAs(VerticalSlicePlayRole.Front);

        public void SelectRoleBack() => SelectRole(VerticalSlicePlayRole.Back);

        public void SelectRoleFront() => SelectRole(VerticalSlicePlayRole.Front);

        public void SelectRole(VerticalSlicePlayRole role)
        {
            VerticalSliceRoleSelection.Select(role);
            RefreshRoleSelectLabel();
            Debug.Log($"[BACKBEAT DINER] Role selected → {VerticalSliceRoleSelection.DisplayName}");
        }

        public void StartDayAs(VerticalSlicePlayRole role)
        {
            NetSessionConfig.ConfigureOffline();
            VerticalSliceRoleSelection.Select(role);
            LoadPlayScene($"オフライン {VerticalSliceRoleSelection.DisplayName}");
        }

        public void StartAsHost()
        {
            if (_netBusy)
            {
                return;
            }

            EnsureRoleSelected();
            SetNetStatus("ホスト作成中…");
            if (!NetcodeSessionBootstrap.StartHost())
            {
                SetNetStatus("ホスト作成に失敗");
                return;
            }

            LoadPlayScene($"Host {VerticalSliceRoleSelection.DisplayName}");
        }

        public void StartAsClient()
        {
            if (_netBusy)
            {
                return;
            }

            EnsureRoleSelected();
            StartCoroutine(JoinHostThenLoad());
        }

        private IEnumerator JoinHostThenLoad()
        {
            _netBusy = true;
            SetNetStatus($"参加中 {NetSessionConfig.DefaultAddress}:{NetSessionConfig.DefaultPort}…");
            if (!NetcodeSessionBootstrap.StartClient())
            {
                SetNetStatus("参加に失敗");
                _netBusy = false;
                yield break;
            }

            var nm = NetworkManager.Singleton;
            var timeout = 8f;
            while (timeout > 0f && nm != null && !nm.IsConnectedClient && nm.IsClient)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (nm == null || !nm.IsConnectedClient)
            {
                SetNetStatus("参加タイムアウト — ホストを先に起動");
                if (nm != null && nm.IsListening)
                {
                    nm.Shutdown();
                }

                _netBusy = false;
                yield break;
            }

            LoadPlayScene($"Client {VerticalSliceRoleSelection.DisplayName}");
            _netBusy = false;
        }

        private static void EnsureRoleSelected()
        {
            if (!VerticalSliceRoleSelection.HasUserSelection)
            {
                VerticalSliceRoleSelection.LoadSavedOrDefault();
                VerticalSliceRoleSelection.Select(VerticalSliceRoleSelection.Selected);
            }
        }

        private void LoadPlayScene(string reason)
        {
            var target = string.IsNullOrWhiteSpace(playSceneName) ? VerticalSliceScene : playSceneName.Trim();
            Debug.Log($"[BACKBEAT DINER] {reason} — loading " + target);
            SceneManager.LoadScene(target, LoadSceneMode.Single);
        }

        private void SetNetStatus(string message)
        {
            if (_netStatusLabel != null)
            {
                _netStatusLabel.text = message;
            }

            Debug.Log("[BACKBEAT DINER] " + message);
        }

        private void RefreshRoleSelectLabel()
        {
            if (_roleSelectLabel == null)
            {
                return;
            }

            _roleSelectLabel.text =
                $"選択中の役: {VerticalSliceRoleSelection.DisplayName}（ホスト/参加前に 1=裏 / 2=表）";
        }

        private void ResolveBackgroundSprite()
        {
            if (titleBackground != null)
            {
                return;
            }

            titleBackground = Resources.Load<Sprite>("TitleBackground");
        }

        private static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
                go.tag = "MainCamera";
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Navy;
            cam.transform.position = new Vector3(0f, 1.2f, -6f);
            cam.transform.rotation = Quaternion.identity;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildUi()
        {
            if (GameObject.Find("TitleMenuHUD") != null)
            {
                return;
            }

            var canvasGo = new GameObject("TitleMenuHUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            BuildFullBleedBackground(canvasGo.transform);
            BuildBottomGradient(canvasGo.transform);
            BuildVignetteEdges(canvasGo.transform);

            var stack = new GameObject("ContentStack");
            stack.transform.SetParent(canvasGo.transform, false);
            var stackRt = stack.AddComponent<RectTransform>();
            stackRt.anchorMin = new Vector2(0.5f, 0f);
            stackRt.anchorMax = new Vector2(0.5f, 0f);
            stackRt.pivot = new Vector2(0.5f, 0f);
            stackRt.anchoredPosition = new Vector2(0f, 72f);
            stackRt.sizeDelta = new Vector2(1100f, 640f);

            var brand = CreateLabel(stack.transform, "BrandLogo", "BACKBEAT DINER",
                new Vector2(0f, 430f), 78, Gold, FontStyle.Bold);
            brand.alignment = TextAnchor.MiddleCenter;
            brand.horizontalOverflow = HorizontalWrapMode.Overflow;
            brand.rectTransform.sizeDelta = new Vector2(1100f, 96f);

            CreatePanel(
                stack.transform,
                "GoldRule",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 372f),
                new Vector2(220f, 3f),
                new Color(Gold.r, Gold.g, Gold.b, 0.85f));

            var sub = CreateLabel(stack.transform, "BrandSub", "バックビート・ダイナー",
                new Vector2(0f, 332f), 22, new Color(Teal.r, Teal.g, Teal.b, 0.95f), FontStyle.Normal);
            sub.alignment = TextAnchor.MiddleCenter;
            sub.rectTransform.sizeDelta = new Vector2(720f, 34f);

            var catch1 = CreateLabel(stack.transform, "CatchcopyPrimary", CatchcopyPrimary,
                new Vector2(0f, 270f), 30, Cream, FontStyle.Normal);
            catch1.alignment = TextAnchor.MiddleCenter;
            catch1.rectTransform.sizeDelta = new Vector2(980f, 44f);

            var catch2 = CreateLabel(stack.transform, "CatchcopySecondary", CatchcopySecondary,
                new Vector2(0f, 226f), 22, new Color(Cream.r, Cream.g, Cream.b, 0.72f), FontStyle.Normal);
            catch2.alignment = TextAnchor.MiddleCenter;
            catch2.rectTransform.sizeDelta = new Vector2(980f, 36f);

            var roleLine = CreateLabel(stack.transform, "RoleLine", "2デバイス · 各1画面 — 役選択 → ホスト作成 / 参加",
                new Vector2(0f, 170f), 18, new Color(Gold.r, Gold.g, Gold.b, 0.9f), FontStyle.Normal);
            roleLine.alignment = TextAnchor.MiddleCenter;
            roleLine.rectTransform.sizeDelta = new Vector2(980f, 28f);

            _roleSelectLabel = CreateLabel(stack.transform, "RoleSelect", "選択中の役: —",
                new Vector2(0f, 138f), 16, new Color(Cream.r, Cream.g, Cream.b, 0.85f), FontStyle.Normal);
            _roleSelectLabel.alignment = TextAnchor.MiddleCenter;
            _roleSelectLabel.rectTransform.sizeDelta = new Vector2(900f, 24f);

            BuildRoleCta(stack.transform, "SelectBackButton", "裏を選ぶ", new Vector2(-210f, 92f), SelectRoleBack, compact: true);
            BuildRoleCta(stack.transform, "SelectFrontButton", "表を選ぶ", new Vector2(210f, 92f), SelectRoleFront, compact: true);

            BuildRoleCta(stack.transform, "HostButton", "ホスト作成", new Vector2(-210f, 18f), StartAsHost);
            BuildRoleCta(stack.transform, "JoinButton", "参加 localhost", new Vector2(210f, 18f), StartAsClient);

            BuildRoleCta(stack.transform, "StartBackButton", "オフライン裏", new Vector2(-210f, -56f), StartDayAsBack, compact: true);
            BuildRoleCta(stack.transform, "StartFrontButton", "オフライン表", new Vector2(210f, -56f), StartDayAsFront, compact: true);

            _netStatusLabel = CreateLabel(stack.transform, "NetStatus", "Netcode: 待機（H=ホスト / J=参加）",
                new Vector2(0f, -108f), 14, new Color(Teal.r, Teal.g, Teal.b, 0.9f), FontStyle.Normal);
            _netStatusLabel.alignment = TextAnchor.MiddleCenter;
            _netStatusLabel.rectTransform.sizeDelta = new Vector2(920f, 22f);

            var hint = CreateLabel(stack.transform, "Hint", "1/2=役　H=ホスト　J=参加　オフライン=各ボタン",
                new Vector2(0f, -140f), 14, new Color(Teal.r, Teal.g, Teal.b, 0.75f), FontStyle.Normal);
            hint.alignment = TextAnchor.MiddleCenter;
            hint.rectTransform.sizeDelta = new Vector2(720f, 22f);

            var credits = CreateLabel(canvasGo.transform, "Credits", CreditsLine,
                new Vector2(0f, 28f), 14, new Color(1f, 1f, 1f, 0.38f), FontStyle.Normal);
            credits.alignment = TextAnchor.MiddleCenter;
            var creditsRt = credits.rectTransform;
            creditsRt.anchorMin = new Vector2(0.5f, 0f);
            creditsRt.anchorMax = new Vector2(0.5f, 0f);
            creditsRt.pivot = new Vector2(0.5f, 0f);
            creditsRt.anchoredPosition = new Vector2(0f, 18f);
            creditsRt.sizeDelta = new Vector2(720f, 22f);
        }

        private void BuildFullBleedBackground(Transform parent)
        {
            var go = new GameObject("TitleBackground");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            if (titleBackground != null)
            {
                img.sprite = titleBackground;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
                img.color = Color.white;
            }
            else
            {
                img.color = Navy;
                Debug.LogWarning("[TitleMenu] TitleBackground sprite missing — navy fallback.");
            }
        }

        private static void BuildBottomGradient(Transform parent)
        {
            var go = new GameObject("BottomGradient");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.62f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<RawImage>();
            img.texture = CreateVerticalGradientTexture(
                new Color(0.02f, 0.05f, 0.09f, 0f),
                new Color(0.02f, 0.04f, 0.07f, 0.92f));
            img.raycastTarget = false;
            img.color = Color.white;
        }

        private static void BuildVignetteEdges(Transform parent)
        {
            CreatePanel(
                parent,
                "LeftWash",
                new Vector2(0f, 0f),
                new Vector2(0.22f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.05f, 0.09f, 0.28f));

            CreatePanel(
                parent,
                "RightWash",
                new Vector2(0.78f, 0f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.05f, 0.09f, 0.22f));

            CreatePanel(
                parent,
                "TopShade",
                new Vector2(0f, 0.82f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.04f, 0.07f, 0.35f));
        }

        private void BuildRoleCta(
            Transform parent,
            string name,
            string label,
            Vector2 pos,
            UnityEngine.Events.UnityAction onClick,
            bool compact = false)
        {
            var buttonGo = new GameObject(name);
            buttonGo.transform.SetParent(parent, false);
            var rt = buttonGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = compact ? new Vector2(300f, 52f) : new Vector2(360f, 64f);

            var glow = CreatePanel(
                buttonGo.transform,
                "CtaGlow",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(400f, 92f),
                new Color(Gold.r, Gold.g, Gold.b, 0.18f));
            glow.transform.SetAsFirstSibling();

            var image = buttonGo.AddComponent<Image>();
            image.color = Gold;

            var outline = buttonGo.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.92f, 0.55f, 0.45f);
            outline.effectDistance = new Vector2(2f, -2f);

            var button = buttonGo.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Gold;
            colors.highlightedColor = GoldBright;
            colors.pressedColor = Teal;
            colors.selectedColor = GoldBright;
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var text = CreateLabel(buttonGo.transform, "Label", label, Vector2.zero, 26, Navy, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            var labelRt = text.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
        }

        private static Texture2D CreateVerticalGradientTexture(Color top, Color bottom)
        {
            const int height = 64;
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "TitleBottomGradient"
            };

            for (var y = 0; y < height; y++)
            {
                var t = y / (float)(height - 1);
                tex.SetPixel(0, y, Color.Lerp(bottom, top, t));
            }

            tex.Apply(false, true);
            return tex;
        }

        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            if (Mathf.Approximately(size.x, 0f) && Mathf.Approximately(size.y, 0f) &&
                (anchorMin != anchorMax))
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return go;
        }

        private static Text CreateLabel(
            Transform parent,
            string name,
            string content,
            Vector2 anchoredPos,
            int fontSize,
            Color color,
            FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(400f, 40f);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            return text;
        }
    }
}
