using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Silkroad.EditorTools
{
    /// <summary>
    /// 상단 메뉴 [Silkroad] → [씬 자동 생성] 을 누르면
    /// 그림판 앱 전체 UI 를 만들어 Assets/_Project/Scenes/Main.unity 로 저장합니다.
    /// 모양(이미지/색)은 나중에 자유롭게 바꾸면 됩니다.
    /// </summary>
    public static class SilkroadSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        // 임시 테마 색 (나중에 직접 만든 이미지로 교체)
        static readonly Color BgColor = new Color(0.16f, 0.12f, 0.09f);
        static readonly Color FrameColor = new Color(0.86f, 0.77f, 0.60f);
        static readonly Color BorderColor = new Color(0.35f, 0.25f, 0.16f);
        static readonly Color ButtonColor = new Color(0.93f, 0.86f, 0.72f);
        static readonly Color AccentColor = new Color(1f, 0.78f, 0.25f);
        static readonly Color TextColor = new Color(0.22f, 0.14f, 0.08f);

        static DefaultControls.Resources _res;

        [MenuItem("Silkroad/씬 자동 생성 (Build Scene)", priority = 0)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            _res = new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            };

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; }

            // ── EventSystem ──
            var es = new GameObject("EventSystem", typeof(EventSystem));
            // Input System 패키지가 있으면 그 모듈을, 없으면 기존 모듈을 사용 (패키지 없이도 컴파일되도록 이름으로 찾음)
            var isModule = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (isModule != null) es.AddComponent(isModule);
            else es.AddComponent<StandaloneInputModule>();

            // ── ComfyUI 클라이언트 (설정은 여기서) ──
            var comfyGo = new GameObject("ComfyUI (설정)");
            var comfy = comfyGo.AddComponent<ComfyUIClient>();

            // ── Canvas ──
            var canvasGo = new GameObject("SilkroadCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // 가로 기준: 16:10 태블릿(2000x1200)에서도 위아래 여유
            canvasGo.AddComponent<KoreanFontFixer>();
            var app = canvasGo.AddComponent<AppController>();
            var root = canvasGo.transform;

            var bg = Panel(root, "Background", BgColor);
            bg.GetComponent<Image>().raycastTarget = false;

            // ════════ 메인 화면 ════════
            var main = Panel(root, "MainPanel", Color.clear);
            Box(main.transform, "Frame", new Vector2(0, 0), new Vector2(1500, 850), FrameColor);
            Label(main.transform, "Title", "실크로드 그림 공방", new Vector2(0, 180), new Vector2(1300, 160), 96, TextColor);
            Label(main.transform, "Subtitle", "내가 그린 그림이 실크로드의 그림이 됩니다", new Vector2(0, 60), new Vector2(1300, 70), 40, TextColor);
            var startBtn = Btn(main.transform, "StartButton", "시작하기", new Vector2(0, -120), new Vector2(420, 120), 44, AccentColor);
            var quitBtn = Btn(main.transform, "QuitButton", "종료", new Vector2(0, -280), new Vector2(300, 90), 32, ButtonColor);
            var settingsBtn = Btn(main.transform, "SettingsButton", "설정", new Vector2(620, -340), new Vector2(160, 80), 28, ButtonColor);

            // ════════ 그림판 화면 ════════
            var paint = Panel(root, "PaintPanel", Color.clear);
            var pt = paint.transform;
            Box(pt, "Frame", Vector2.zero, new Vector2(1800, 1020), FrameColor);
            Box(pt, "CanvasBorder", new Vector2(-110, -55), new Vector2(1512, 856), BorderColor);

            var drawGo = new GameObject("DrawingCanvas", typeof(RectTransform), typeof(RawImage));
            drawGo.transform.SetParent(pt, false);
            Place(drawGo, new Vector2(-110, -55), new Vector2(1500, 844));
            var paintCanvas = drawGo.AddComponent<PaintCanvas>();

            var toolbar = paint.AddComponent<PaintToolbar>();
            toolbar.canvas = paintCanvas;
            toolbar.app = app;
            const float topY = 440f;
            toolbar.brushButton = Btn(pt, "BrushButton", "브러시", new Vector2(-830, topY), new Vector2(120, 96), 26, ButtonColor);
            toolbar.eraserButton = Btn(pt, "EraserButton", "지우개", new Vector2(-700, topY), new Vector2(120, 96), 26, ButtonColor);
            toolbar.fillButton = Btn(pt, "FillButton", "채우기", new Vector2(-570, topY), new Vector2(120, 96), 26, ButtonColor);
            toolbar.undoButton = Btn(pt, "UndoButton", "취소", new Vector2(-440, topY), new Vector2(120, 96), 26, ButtonColor);
            toolbar.redoButton = Btn(pt, "RedoButton", "복원", new Vector2(-310, topY), new Vector2(120, 96), 26, ButtonColor);

            Label(pt, "SizeTitle", "굵기", new Vector2(-195, topY), new Vector2(80, 50), 26, TextColor);
            var sliderGo = DefaultControls.CreateSlider(_res);
            sliderGo.name = "SizeSlider";
            sliderGo.transform.SetParent(pt, false);
            Place(sliderGo, new Vector2(-20, topY), new Vector2(240, 30));
            toolbar.sizeSlider = sliderGo.GetComponent<Slider>();
            toolbar.sizeLabel = Label(pt, "SizeValue", "4", new Vector2(140, topY), new Vector2(60, 50), 28, TextColor);

            var preview = Box(pt, "CurrentColor", new Vector2(205, topY), new Vector2(56, 56), Color.black);
            preview.sprite = _res.knob;
            toolbar.currentColorPreview = preview;

            var palette = new GameObject("Palette", typeof(RectTransform), typeof(GridLayoutGroup));
            palette.transform.SetParent(pt, false);
            Place(palette, new Vector2(470, topY), new Vector2(454, 86));
            var grid = palette.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(40, 40);
            grid.spacing = new Vector2(6, 6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 10;
            Color32[] colors =
            {
                new Color32(0,0,0,255), new Color32(90,90,90,255), new Color32(160,160,160,255), new Color32(255,255,255,255),
                new Color32(120,70,40,255), new Color32(140,20,30,255), new Color32(230,40,40,255), new Color32(245,140,30,255),
                new Color32(250,215,40,255), new Color32(255,240,160,255),
                new Color32(60,190,70,255), new Color32(20,110,50,255), new Color32(120,210,240,255), new Color32(30,150,230,255),
                new Color32(30,50,140,255), new Color32(140,70,190,255), new Color32(245,150,190,255), new Color32(240,200,170,255),
                new Color32(190,140,90,255), new Color32(210,210,210,255),
            };
            for (int i = 0; i < colors.Length; i++)
            {
                var c = new GameObject("Color" + i, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
                c.transform.SetParent(palette.transform, false);
                var img = c.GetComponent<Image>();
                img.sprite = _res.standard; img.type = Image.Type.Sliced;
                img.color = colors[i];
                c.GetComponent<Button>().targetGraphic = img;
                c.GetComponent<Outline>().effectColor = new Color(0, 0, 0, 0.5f);
            }
            toolbar.paletteRoot = palette.transform;

            toolbar.sendButton = Btn(pt, "SendButton", "보내기", new Vector2(770, 200), new Vector2(200, 110), 32, AccentColor);
            toolbar.clearButton = Btn(pt, "ClearButton", "지우기", new Vector2(770, 40), new Vector2(200, 110), 32, ButtonColor);
            toolbar.homeButton = Btn(pt, "HomeButton", "메인", new Vector2(770, -120), new Vector2(200, 110), 32, ButtonColor);
            toolbar.toastText = Label(pt, "Toast", "", new Vector2(-110, -55), new Vector2(900, 90), 40, Color.white);
            toolbar.toastText.gameObject.AddComponent<Outline>().effectColor = Color.black;
            toolbar.normalColor = ButtonColor;
            toolbar.selectedColor = AccentColor;

            // ════════ 생성 중 화면 ════════
            var loading = Panel(root, "LoadingPanel", Color.clear);
            var lt = loading.transform;
            Box(lt, "Frame", Vector2.zero, new Vector2(1500, 850), FrameColor);
            var lv = loading.AddComponent<LoadingView>();
            var icon = Box(lt, "Icon", new Vector2(0, 60), new Vector2(140, 140), BorderColor);
            icon.sprite = _res.knob;
            lv.icon = icon.rectTransform;
            lv.messageText = Label(lt, "Message", "생성 중...", new Vector2(0, -80), new Vector2(600, 80), 48, TextColor);
            lv.errorText = Label(lt, "Error", "", new Vector2(0, 60), new Vector2(1300, 500), 26, new Color(0.55f, 0.1f, 0.1f));
            lv.backButton = Btn(lt, "BackButton", "취소", new Vector2(0, -330), new Vector2(260, 90), 30, ButtonColor);

            // ════════ 결과 비교 화면 ════════
            var result = Panel(root, "ResultPanel", Color.clear);
            var rt = result.transform;
            Box(rt, "Frame", Vector2.zero, new Vector2(1800, 1020), FrameColor);
            Box(rt, "CompareBorder", new Vector2(0, 50), new Vector2(1512, 856), BorderColor);
            var rv = result.AddComponent<ResultView>();

            var area = new GameObject("CompareArea", typeof(RectTransform));
            area.transform.SetParent(rt, false);
            Place(area, new Vector2(0, 50), new Vector2(1500, 844));
            rv.compareArea = (RectTransform)area.transform;

            var mine = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage));
            mine.transform.SetParent(area.transform, false);
            Stretch(mine);
            rv.mineImage = mine.GetComponent<RawImage>();
            rv.mineImage.raycastTarget = false;

            var mask = new GameObject("AIMask", typeof(RectTransform), typeof(RectMask2D));
            mask.transform.SetParent(area.transform, false);
            Stretch(mask);
            rv.aiMask = (RectTransform)mask.transform;

            var ai = new GameObject("AIResult", typeof(RectTransform), typeof(RawImage));
            ai.transform.SetParent(mask.transform, false);
            var aiRt = (RectTransform)ai.transform;
            aiRt.anchorMin = new Vector2(1, 0); aiRt.anchorMax = new Vector2(1, 1);
            aiRt.pivot = new Vector2(1, 0.5f);
            aiRt.anchoredPosition = Vector2.zero;
            aiRt.sizeDelta = new Vector2(1500, 0);
            rv.aiImage = ai.GetComponent<RawImage>();
            rv.aiImage.raycastTarget = false;

            // 가운데 막대(슬라이더)
            var div = DefaultControls.CreateSlider(_res);
            div.name = "Divider";
            div.transform.SetParent(area.transform, false);
            Stretch(div);
            var divSlider = div.GetComponent<Slider>();
            var divBg = div.transform.Find("Background");
            if (divBg) divBg.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f); // 투명하지만 클릭은 받음
            var fillArea = div.transform.Find("Fill Area");
            if (fillArea) fillArea.gameObject.SetActive(false);
            divSlider.fillRect = null;
            var handleArea = (RectTransform)div.transform.Find("Handle Slide Area");
            handleArea.offsetMin = Vector2.zero; handleArea.offsetMax = Vector2.zero;
            var handle = divSlider.handleRect;
            handle.sizeDelta = new Vector2(14, 0);
            var hImg = handle.GetComponent<Image>();
            hImg.sprite = _res.standard; hImg.type = Image.Type.Sliced; hImg.color = BorderColor;
            divSlider.value = 0.5f;
            rv.divider = divSlider;

            // ('내 그림' / 'AI 결과' 글씨는 사용하지 않음)

            rv.homeButton = Btn(rt, "HomeButton", "홈", new Vector2(-560, -440), new Vector2(260, 90), 30, ButtonColor);
            rv.retryButton = Btn(rt, "RetryButton", "재요청", new Vector2(-190, -440), new Vector2(260, 90), 30, AccentColor);
            rv.editButton = Btn(rt, "EditButton", "그림 수정", new Vector2(190, -440), new Vector2(260, 90), 30, ButtonColor);
            rv.saveButton = Btn(rt, "SaveButton", "저장", new Vector2(560, -440), new Vector2(260, 90), 30, ButtonColor);
            rv.statusText = Label(rt, "Status", "", new Vector2(0, 492), new Vector2(1600, 36), 22, TextColor);

            // ════════ 설정 창 (메인 화면의 '설정' 버튼) ════════
            var settings = Panel(root, "SettingsPanel", new Color(0, 0, 0, 0.6f));
            var st = settings.transform;
            Box(st, "Window", Vector2.zero, new Vector2(1100, 700), FrameColor);
            var sv = settings.AddComponent<SettingsView>();
            sv.comfy = comfy;
            sv.openButton = settingsBtn;
            Label(st, "Title", "ComfyUI 설정", new Vector2(0, 280), new Vector2(900, 70), 44, TextColor);
            Label(st, "UrlLabel", "서버 주소 (예: http://192.168.0.10:8188)", new Vector2(0, 180), new Vector2(900, 50), 28, TextColor);
            var input = DefaultControls.CreateInputField(_res);
            input.name = "UrlInput";
            input.transform.SetParent(st, false);
            Place(input, new Vector2(0, 110), new Vector2(900, 80));
            sv.urlInput = input.GetComponent<InputField>();
            sv.urlInput.contentType = InputField.ContentType.Standard;
            foreach (var t in input.GetComponentsInChildren<Text>(true)) t.fontSize = 32;
            var ph = input.transform.Find("Placeholder");
            if (ph) ph.GetComponent<Text>().text = "http://PC의IP:8188";

            var toggle = DefaultControls.CreateToggle(_res);
            toggle.name = "DummyToggle";
            toggle.transform.SetParent(st, false);
            Place(toggle, new Vector2(0, 10), new Vector2(900, 60));
            var tbg = (RectTransform)toggle.transform.Find("Background");
            if (tbg) { tbg.sizeDelta = new Vector2(50, 50); tbg.anchoredPosition = new Vector2(25, -25); }
            var tl = toggle.transform.Find("Label");
            if (tl) { var tt = tl.GetComponent<Text>(); tt.text = "더미 모드 (서버 없이 가짜 결과로 테스트)"; tt.fontSize = 30; tt.color = TextColor;
                      var tlr = (RectTransform)tl; tlr.offsetMin = new Vector2(70, 0); }
            sv.dummyToggle = toggle.GetComponent<Toggle>();

            sv.testButton = Btn(st, "TestButton", "연결 테스트", new Vector2(-300, -130), new Vector2(260, 90), 30, ButtonColor);
            sv.saveButton = Btn(st, "SaveButton", "저장", new Vector2(0, -130), new Vector2(260, 90), 30, AccentColor);
            sv.closeButton = Btn(st, "CloseButton", "닫기", new Vector2(300, -130), new Vector2(260, 90), 30, ButtonColor);
            sv.statusText = Label(st, "Status", "", new Vector2(0, -250), new Vector2(1000, 90), 26, TextColor);

            // ── 한글 폰트 적용 (태블릿에서도 깨지지 않게 프로젝트에 포함된 폰트 사용) ──
            var krFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Art/Fonts/NotoSansKR-Subset.ttf");
            if (krFont != null)
            {
                canvasGo.GetComponent<KoreanFontFixer>().customFont = krFont;
                foreach (var t in canvasGo.GetComponentsInChildren<Text>(true)) t.font = krFont;
            }

            // ── AppController 연결 ──
            app.mainPanel = main;
            app.paintPanel = paint;
            app.loadingPanel = loading;
            app.resultPanel = result;
            app.paintCanvas = paintCanvas;
            app.paintToolbar = toolbar;
            app.comfy = comfy;
            app.loadingView = lv;
            app.resultView = rv;
            app.startButton = startBtn;
            app.quitButton = quitBtn;
            settings.transform.SetAsLastSibling();

            SetLayerRecursive(canvasGo, LayerMask.NameToLayer("UI"));

            // ── 저장 ──
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Selection.activeGameObject = comfyGo;

            EditorUtility.DisplayDialog("Silkroad",
                "씬이 만들어졌어요!\n\n" + ScenePath + "\n\n위쪽 ▶(Play) 버튼을 눌러 실행해 보세요.\n" +
                "ComfyUI 설정은 Hierarchy 의 'ComfyUI (설정)' 오브젝트에서 바꿉니다.", "확인");
        }

        // ───────────── 안드로이드 태블릿 설정 ─────────────
        [MenuItem("Silkroad/안드로이드 태블릿 설정 적용", priority = 20)]
        public static void ApplyAndroidSettings()
        {
            // 가로 화면 고정 (좌/우 회전만 허용)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // ComfyUI(PC) 는 보통 http:// 라서 허용 필요
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

            // 64비트 ARM (요즘 안드로이드 태블릿 필수)
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true; // QR 공유 서버(태블릿 웹서버)용

            if (string.IsNullOrEmpty(PlayerSettings.productName) || PlayerSettings.productName == "New Project")
                PlayerSettings.productName = "실크로드 그림 공방";

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Silkroad",
                "안드로이드 설정을 적용했어요.\n\n- 가로 화면 고정\n- http 통신 허용\n- IL2CPP / ARM64\n\n" +
                "File → Build Profiles 에서 Android 를 선택하고 'Switch Platform' 을 눌렀는지 확인하세요.", "확인");
        }

        // ───────────── 헬퍼 ─────────────
        static GameObject Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = color.a > 0;
            return go;
        }

        static Image Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Place(go, pos, size);
            var img = go.GetComponent<Image>();
            img.sprite = _res.standard;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text Label(Transform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            var go = DefaultControls.CreateText(_res);
            go.name = name;
            go.transform.SetParent(parent, false);
            Place(go, pos, size);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static Button Btn(Transform parent, string name, string label, Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            var go = DefaultControls.CreateButton(_res);
            go.name = name;
            go.transform.SetParent(parent, false);
            Place(go, pos, size);
            go.GetComponent<Image>().color = color;
            var t = go.GetComponentInChildren<Text>();
            t.text = label;
            t.fontSize = fontSize;
            t.color = TextColor;
            return go.GetComponent<Button>();
        }

        static void Place(GameObject go, Vector2 pos, Vector2 size)
        {
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        static void Stretch(GameObject go)
        {
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        static void Corner(RectTransform r, Vector2 anchor, Vector2 pos)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.anchoredPosition = pos;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            if (layer < 0) return;
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        static void AddToBuildSettings(string path)
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == path);
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
