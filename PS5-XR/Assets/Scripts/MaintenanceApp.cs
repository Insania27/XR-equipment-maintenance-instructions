using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Ps5Maintenance
{
    public class MaintenanceApp : MonoBehaviour
    {
        public ARSession arSession;
        public ARTrackedImageManager imageManager;
        public Camera sceneCamera;
        public EquipmentLayout layout;
        public Transform contentAnchor;
        private InstructionData data;
        private MaintenanceSession session;
        private IProgressRepository repository;
        private ConsoleModel model;
        private bool demo, demoTracking = true, tracking, confirmed, started, storageError, equipmentRecognized;
        private string failure = "";
        private Transform arrow;
        private GameObject mock, panel;
        private GameObject markerOutline;
        private TMP_Text stateLabel, stepLabel, titleLabel, descriptionLabel, cautionLabel, storageLabel;
        private TMP_Text modelLabel, modeLabel, nextLabel, confirmLabel, modeButtonLabel, trackingButtonLabel;
        private Button nextButton, backButton, modelButton, confirmButton;
        private Image progress;
        private readonly Color ink = new Color(.055f, .075f, .12f);
        private readonly Color cyan = new Color(.25f, .87f, .93f);
        public bool IsDemo => demo;
        public bool MarkerVisible => tracking;
        public MaintenanceSession Session => session;

        private void Start()
        {
            data = InstructionData.Load();
            session = new MaintenanceSession(data.steps.Length);
            demo = Application.platform != RuntimePlatform.Android;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                repository = new JavaProgressRepository();
#else
                repository = new JsonProgressRepository();
#endif
            }
            catch (Exception e) { SetStorageFailure(e); }
            CreateVisuals();
            CreateUi();
            SetMode(demo);
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-smokeTest"))
                StartCoroutine(RunSmokeTest());
            else if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-uiClickTest"))
                StartCoroutine(RunUiClickTest());
        }

        private void Update()
        {
            if (data == null) return;
            tracking = demo && demoTracking;
            if (!demo)
            {
                foreach (var image in imageManager.trackables)
                {
                    if (image.referenceImage.name != "ps5-maintenance" || image.trackingState != TrackingState.Tracking) continue;
                    contentAnchor.SetPositionAndRotation(image.transform.position, image.transform.rotation);
                    tracking = true;
                    equipmentRecognized = true;
                    break;
                }
            }
            contentAnchor.gameObject.SetActive(tracking && !session.Finished);
            Refresh();
        }

        private Vector3 TargetForCurrentStep() => !demo && layout != null && layout.targets != null &&
            layout.targets.Length == data.steps.Length ? layout.targets[session.Index] : data.steps[session.Index].target;

        public void SetMode(bool useDemo)
        {
            if (!useDemo && Application.platform != RuntimePlatform.Android)
            {
                failure = "AR-режим доступен в сборке для Android. Сейчас используется схема для проверки.";
                Refresh();
                return;
            }
            if (started && !session.Finished) { failure = "Завершите или сбросьте текущий сеанс перед сменой режима."; return; }
            if (session.Finished) ResetSession();
            demo = useDemo;
            arSession.gameObject.SetActive(!demo);
            imageManager.enabled = !demo;
            sceneCamera.GetComponent<ARCameraManager>().enabled = !demo;
            sceneCamera.GetComponent<ARCameraBackground>().enabled = !demo;
            sceneCamera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>().enabled = !demo;
            mock.SetActive(demo);
            markerOutline.SetActive(!demo);
            equipmentRecognized = demo;
            sceneCamera.rect = demo ? new Rect(0, .50f, 1, .32f) : new Rect(0, 0, 1, 1);
            if (demo)
            {
                contentAnchor.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                sceneCamera.transform.SetPositionAndRotation(new Vector3(.16f, .72f, -.50f),
                    Quaternion.LookRotation(new Vector3(0f, -.72f, .50f)));
                sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                sceneCamera.backgroundColor = new Color(.10f, .15f, .21f);
            }
            tracking = demo && demoTracking;
            confirmed = false;
            failure = "";
            Refresh();
        }

        public void ChooseModel(ConsoleModel selected)
        {
            if (started) return;
            model = selected;
            confirmed = false;
            Refresh();
        }

        public void SetDemoTracking(bool visible)
        {
            demoTracking = visible;
            tracking = demo && visible;
            confirmed = false;
            Refresh();
        }

        public void ConfirmStep(bool value) { confirmed = value; Refresh(); }

        public void Advance()
        {
            var step = data.steps[session.Index];
            if (session.Finished || storageError || !tracking || !confirmed || model == ConsoleModel.Unknown) return;
            try
            {
                if (!started)
                {
                    repository.Begin(data.equipment, model.ToString(), data.steps.Length);
                    started = true;
                }
                repository.RecordStep(session.Index);
                session.Advance(confirmed, tracking, !step.requiresModel || model != ConsoleModel.Unknown);
                if (session.Finished) repository.Finish();
                confirmed = false;
                Refresh();
            }
            catch (Exception e) { SetStorageFailure(e); Refresh(); }
        }

        public void Back()
        {
            if (session.Back()) confirmed = false;
            Refresh();
        }

        public void ResetSession()
        {
            // An abandoned record remains unfinished; never count it as successful.
            session = new MaintenanceSession(data.steps.Length);
            started = false;
            confirmed = false;
            failure = storageError ? failure : "";
            Refresh();
        }

        private void SetStorageFailure(Exception e)
        {
            storageError = true;
            failure = "Не удалось сохранить результат. Сеанс не засчитан. " + e.Message;
            Debug.LogException(e);
        }

        private void Refresh()
        {
            if (stateLabel == null) return;
            var step = data.steps[session.Index];
            bool searching = !demo && !equipmentRecognized;
            modelLabel.text = "Модель: " + ModelName(model) + (started ? "" : "  ›");
            modelButton.interactable = !started;
            modeLabel.text = demo ? "РЕЖИМ ПК · УСЛОВНАЯ СХЕМА" : "AR · КАМЕРА ANDROID";
            modeButtonLabel.text = demo ? "Камера AR (Android)" : "Проверка на ПК";
            trackingButtonLabel.text = demo ? "Маркер: показать / скрыть" : "Как сканировать";
            string arState = ARSession.state == ARSessionState.Unsupported ? "Устройство не поддерживает ARCore" :
                ARSession.state == ARSessionState.NeedsInstall ? "Требуется Google Play Services for AR" : "Наведите камеру на маркер";
            stateLabel.text = session.Finished ? "ОСМОТР ЗАВЕРШЁН" : tracking ?
                (demo ? "Маркер имитируется · это не проверка камеры" : "Положение маркера отслеживается") :
                (demo ? "Маркер потерян · шаги приостановлены" : arState);
            stateLabel.color = tracking || session.Finished ? cyan : new Color(1f, .71f, .34f);
            stepLabel.text = session.Finished ? (storageError ? "Ошибка сохранения" : "Результат сохранён") : $"ШАГ {session.Index + 1:00} / {data.steps.Length:00}";
            titleLabel.text = session.Finished ? (storageError ? "Проверьте сохранение результата" : "Осмотр выполнен") : step.title;
            descriptionLabel.text = session.Finished ? (storageError ? "Все шаги подтверждены, но сохранение завершения не удалось." : "Все шаги подтверждены. Результат сохранён локально.\nМожно начать новый сеанс.") : step.TextFor(model);
            if (searching)
            {
                stepLabel.text = "ПОИСК ОБОРУДОВАНИЯ";
                titleLabel.text = "Наведите камеру на маркер";
                descriptionLabel.text = "Поместите распечатанный маркер PS5 XR в кадр. После обнаружения появится инструкция и бирюзовый контур маркера.";
            }
            cautionLabel.text = !string.IsNullOrEmpty(failure) ? failure : session.Finished ?
                "Для защиты сохраните запись демонстрации на настоящем Android." : model == ConsoleModel.Unknown ?
                "Выберите семейство PS5 сверху и сверьте его с CFI-номером на корпусе." : step.caution;
            if (searching && string.IsNullOrEmpty(failure)) cautionLabel.text = "Сканирование автоматическое. Держите телефон спокойно при хорошем освещении.";
            confirmLabel.text = confirmed ? "Шаг выполнен" : "Подтвердить выполнение шага";
            confirmButton.interactable = !session.Finished && tracking && !storageError;
            nextButton.interactable = !session.Finished && tracking && confirmed && !storageError && model != ConsoleModel.Unknown;
            backButton.interactable = session.Index > 0 && !session.Finished && !searching;
            nextLabel.text = session.Index == data.steps.Length - 1 ? "Завершить" : "Далее";
            progress.fillAmount = searching ? 0f : session.Finished ? 1f : (float)session.Index / data.steps.Length;
            bool calibrated = layout != null && layout.CanShowRealAr(model, data.steps.Length);
            // Turning the console invalidates the first-side placement. Until a second
            // pose calibration exists, hide spatial hints for steps 7–10 in real AR.
            arrow.gameObject.SetActive(!session.Finished && step.showArrow && (demo || calibrated && session.Index < 6));
            if (arrow.gameObject.activeSelf)
                arrow.localPosition = TargetForCurrentStep() + Vector3.up * (.045f + Mathf.Sin(Time.time * 3f) * .005f);
            panel.SetActive(session.Index != 4 && session.Index != 8);
            storageLabel.text = (storageError ? "Хранилище недоступно" : "Результаты сохраняются на устройстве") +
                (demo ? "  •  Стрелки демонстрационные" : calibrated && session.Index < 6 ? "  •  Положение настроено" : "  •  Стрелки ждут калибровки");
        }

        private static string ModelName(ConsoleModel value) => value == ConsoleModel.Original ? "Обычная · CFI-10/11/12xx" :
            value == ConsoleModel.Slim ? "Slim · CFI-20/21xx" : value == ConsoleModel.Pro ? "Pro · CFI-70/71xx" : "не выбрана";

        private GameObject Solid(string name, PrimitiveType shape, Transform parent, Vector3 position, Vector3 size, Color color)
        {
            var obj = GameObject.CreatePrimitive(shape);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = size;
            Destroy(obj.GetComponent<Collider>());
            var material = new Material(Resources.Load<Material>("OverlayMaterial"));
            material.color = color;
            obj.GetComponent<Renderer>().material = material;
            return obj;
        }

        private void CreateVisuals()
        {
            mock = new GameObject("Schematic console - not an accurate PS5 model");
            mock.transform.SetParent(contentAnchor, false);
            Solid("Body", PrimitiveType.Cube, mock.transform, new Vector3(.18f, .02f, 0), new Vector3(.25f, .035f, .39f), ink);
            Solid("Lower cover", PrimitiveType.Cube, mock.transform, new Vector3(.18f, 0f, 0), new Vector3(.27f, .008f, .41f), Color.white);
            panel = Solid("Removable outer cover", PrimitiveType.Cube, mock.transform, new Vector3(.18f, .045f, 0), new Vector3(.27f, .008f, .41f), new Color(.86f, .90f, .96f));
            Solid("Fan grille placeholder", PrimitiveType.Cylinder, mock.transform, new Vector3(.18f, .04f, .04f), new Vector3(.09f, .003f, .09f), new Color(.32f, .39f, .49f));
            for (int i = 0; i < 6; i++)
                Solid("Grille", PrimitiveType.Cube, mock.transform, new Vector3(.15f + i * .012f, .043f, .04f), new Vector3(.004f, .003f, .065f), ink);
            var marker = Solid("Marker 12 cm", PrimitiveType.Cube, mock.transform, Vector3.zero, new Vector3(.12f, .002f, .12f), Color.white);
            var renderer = marker.GetComponent<Renderer>();
            renderer.material.mainTexture = Resources.Load<Texture2D>("ps5-marker");
            arrow = new GameObject("Step arrow").transform;
            arrow.SetParent(contentAnchor, false);
            Solid("Arrow shaft", PrimitiveType.Cylinder, arrow, new Vector3(0, .07f, 0), new Vector3(.009f, .025f, .009f), cyan);
            var meshObject = new GameObject("Arrow tip", typeof(MeshFilter), typeof(MeshRenderer));
            meshObject.transform.SetParent(arrow, false);
            var vertices = new Vector3[18];
            vertices[0] = Vector3.zero;
            vertices[17] = Vector3.up * .045f;
            for (int i = 0; i < 16; i++)
                vertices[i + 1] = new Vector3(Mathf.Cos(i * Mathf.PI / 8) * .018f, .045f, Mathf.Sin(i * Mathf.PI / 8) * .018f);
            var triangles = new int[96];
            for (int i = 0; i < 16; i++)
            {
                int next = (i + 1) % 16 + 1;
                triangles[i * 6] = 0; triangles[i * 6 + 1] = i + 1; triangles[i * 6 + 2] = next;
                triangles[i * 6 + 3] = 17; triangles[i * 6 + 4] = next; triangles[i * 6 + 5] = i + 1;
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            meshObject.GetComponent<MeshFilter>().mesh = mesh;
            meshObject.GetComponent<MeshRenderer>().material = new Material(Resources.Load<Material>("OverlayMaterial")) { color = cyan };
            var light = new GameObject("Demo light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            light.GetComponent<Light>().intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = new Color(.6f, .6f, .6f);
            markerOutline = new GameObject("AR marker outline");
            markerOutline.transform.SetParent(contentAnchor, false);
            for (int i = 0; i < 4; i++)
            {
                bool horizontal = i < 2;
                var position = horizontal ? new Vector3(0, .002f, i == 0 ? -.06f : .06f) :
                    new Vector3(i == 2 ? -.06f : .06f, .002f, 0);
                Solid("Marker edge", PrimitiveType.Cube, markerOutline.transform, position,
                    horizontal ? new Vector3(.12f, .002f, .002f) : new Vector3(.002f, .002f, .12f), cyan);
            }
        }

        private RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            return rect;
        }
        private TMP_Text Label(string name, Transform parent, int size, Color color, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
        {
            var obj = Rect(name, parent, min, max, low, high);
            var text = obj.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Resources.Load<TMP_FontAsset>("InstructionFont");
            text.fontSize = size; text.color = color; text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableAutoSizing = true; text.fontSizeMax = size; text.fontSizeMin = size * .78f;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }
        private Button Button(string name, Transform parent, string caption, Action action,
            Vector2 min, Vector2 max, Vector2 low, Vector2 high, Color color, out TMP_Text text)
        {
            var rect = Rect(name, parent, min, max, low, high);
            rect.gameObject.AddComponent<Image>().color = color;
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors; colors.disabledColor = new Color(.35f, .39f, .46f, .7f); button.colors = colors;
            button.onClick.AddListener(() => action());
            text = Label("Caption", rect, 25, Color.white, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-16, 0));
            text.text = caption; text.alignment = TextAlignmentOptions.Center;
            return button;
        }
        private void CreateUi()
        {
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null) eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            foreach (var oldModule in eventSystem.GetComponents<BaseInputModule>())
                if (!(oldModule is InputSystemUIInputModule)) oldModule.enabled = false;
            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null) inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
            inputModule.actionsAsset.Enable();
            inputModule.enabled = true;
            var canvas = new GameObject("Instruction UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1100, 1500); scaler.matchWidthOrHeight = 1;
            var safe = Rect("Safe area", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();
            var header = Rect("Header", safe, new Vector2(0, 1), Vector2.one, new Vector2(0, -200), Vector2.zero);
            header.gameObject.AddComponent<Image>().color = ink;
            var brand = Label("Brand", header, 34, Color.white, Vector2.zero, Vector2.one, new Vector2(28, 112), new Vector2(-28, -18));
            brand.text = "PS5  /  XR ИНСТРУКЦИЯ"; brand.fontStyle = FontStyles.Bold;
            modeLabel = Label("Mode", header, 20, cyan, Vector2.zero, Vector2.one, new Vector2(28, 84), new Vector2(-28, -76));
            modelButton = Button("Model", header, "", () => ChooseModel((ConsoleModel)(((int)model + 1) % 4)),
                Vector2.zero, new Vector2(.65f, 0), new Vector2(28, 20), new Vector2(-12, 74), new Color(.16f, .22f, .31f), out modelLabel);
            Button("Source", header, "Руководство Sony", () => Application.OpenURL(data.sourceUrl),
                new Vector2(.65f, 0), new Vector2(1, 0), new Vector2(8, 20), new Vector2(-28, 74), new Color(.16f, .22f, .31f), out _);
            stateLabel = Label("Tracking", safe, 25, cyan, new Vector2(0, 1), Vector2.one, new Vector2(28, -270), new Vector2(-28, -215));
            var footer = Rect("Instruction card", safe, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 665));
            footer.gameObject.AddComponent<Image>().color = new Color(.075f, .10f, .16f, .97f);
            stepLabel = Label("Step", footer, 23, cyan, new Vector2(0, 1), Vector2.one, new Vector2(28, -62), new Vector2(-28, -20));
            titleLabel = Label("Title", footer, 38, Color.white, new Vector2(0, 1), Vector2.one, new Vector2(28, -148), new Vector2(-28, -62));
            titleLabel.fontStyle = FontStyles.Bold;
            descriptionLabel = Label("Description", footer, 28, new Color(.88f, .92f, .98f), new Vector2(0, 1), Vector2.one, new Vector2(28, -334), new Vector2(-28, -150));
            cautionLabel = Label("Caution", footer, 24, new Color(1f, .77f, .43f), new Vector2(0, 1), Vector2.one, new Vector2(28, -429), new Vector2(-28, -337));
            confirmButton = Button("Confirm", footer, "", () => ConfirmStep(!confirmed), Vector2.zero, new Vector2(1, 0),
                new Vector2(28, 161), new Vector2(-28, 220), new Color(.18f, .25f, .34f), out confirmLabel);
            backButton = Button("Back", footer, "Назад", Back, Vector2.zero, new Vector2(.30f, 0),
                new Vector2(28, 89), new Vector2(-8, 147), new Color(.18f, .25f, .34f), out _);
            Button("Reset", footer, "Сначала", ResetSession, new Vector2(.30f, 0), new Vector2(.58f, 0),
                new Vector2(8, 89), new Vector2(-8, 147), new Color(.18f, .25f, .34f), out _);
            nextButton = Button("Next", footer, "", Advance, new Vector2(.58f, 0), new Vector2(1, 0),
                new Vector2(8, 89), new Vector2(-28, 147), new Color(.05f, .46f, .51f), out nextLabel);
            var bar = Rect("Progress", footer, Vector2.zero, new Vector2(1, 0), new Vector2(28, 68), new Vector2(-28, 74));
            bar.gameObject.AddComponent<Image>().color = new Color(.20f, .26f, .34f);
            var fill = Rect("Fill", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            progress = fill.gameObject.AddComponent<Image>(); progress.color = cyan;
            // uGUI Filled requires a sprite; use an ordinary generated white sprite.
            progress.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f));
            progress.type = Image.Type.Filled; progress.fillMethod = Image.FillMethod.Horizontal;
            storageLabel = Label("Storage", footer, 20, new Color(.60f, .69f, .80f), Vector2.zero, new Vector2(1, 0), new Vector2(28, 20), new Vector2(-28, 55));
            Button("Tracking simulation", safe, "", () => {
                if (demo) SetDemoTracking(!demoTracking);
                else { failure = "Держите маркер в кадре при хорошем освещении. Поиск выполняется автоматически."; Refresh(); }
            }, new Vector2(0, 0), new Vector2(.5f, 0), new Vector2(28, 690), new Vector2(-12, 745), new Color(.14f, .22f, .31f), out trackingButtonLabel);
            Button("Mode switch", safe, "", () => SetMode(!demo),
                new Vector2(.5f, 0), new Vector2(1, 0), new Vector2(12, 690), new Vector2(-28, 745), new Color(.14f, .22f, .31f), out modeButtonLabel);
        }

        private IEnumerator RunSmokeTest()
        {
            yield return null;
            try
            {
                ChooseModel(ConsoleModel.Original);
                Advance();
                if (session.Index != 0) throw new Exception("Unconfirmed step advanced");
                ConfirmStep(true); SetDemoTracking(false); Advance();
                if (session.Index != 0) throw new Exception("Lost marker advanced");
                SetDemoTracking(true);
                ConfirmStep(true); Advance(); Back();
                if (session.Index != 0) throw new Exception("Back failed");
                for (int i = 0; i < data.steps.Length; i++) { ConfirmStep(true); Advance(); }
                if (!session.Finished || repository.CompletedSessions() < 1) throw new Exception("Completion not persisted");
                ResetSession(); ChooseModel(ConsoleModel.Slim);
                for (int i = 0; i < 4; i++) { ConfirmStep(true); Advance(); }
                Debug.Log("PS5_XR_RUNTIME_SMOKE_PASSED");
            }
            catch (Exception e) { Debug.LogException(e); Application.Quit(1); yield break; }
            yield return new WaitForEndOfFrame();
            string capture = Path.Combine(Application.dataPath, "..", "preview.png");
            // Render our own scene directly: a hidden Windows test window can return
            // a black swap-chain screenshot even while gameplay is running correctly.
            CapturePreview(capture);
            CapturePreview(Path.Combine(Application.dataPath, "..", "preview-small.png"), 840, 783);
            // Verify only the scanning UI here; a Windows test does not exercise ARCore.
            ResetSession(); ChooseModel(ConsoleModel.Unknown);
            demo = false; equipmentRecognized = false; tracking = false;
            contentAnchor.gameObject.SetActive(false);
            sceneCamera.rect = new Rect(0, 0, 1, 1);
            Refresh();
            if (nextButton.interactable || confirmButton.interactable) throw new Exception("Scanning UI allowed advance before detection");
            CapturePreview(Path.Combine(Application.dataPath, "..", "preview-scanner.png"));
            Debug.Log("PS5_XR_SCANNER_UI_CHECK_PASSED");
            Application.Quit(0);
        }

        private IEnumerator RunUiClickTest()
        {
            yield return null;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            UnityEngine.InputSystem.InputSystem.EnableDevice(mouse);
            var module = FindFirstObjectByType<InputSystemUIInputModule>();
            Canvas.ForceUpdateCanvases();
            try
            {
                ClickUsingInputSystem(mouse, module, modelButton);
                if (model != ConsoleModel.Original) throw new Exception("Model button did not receive pointer click");
                ClickUsingInputSystem(mouse, module, confirmButton);
                if (!confirmed || !nextButton.interactable) throw new Exception("Confirmation click failed");
                ClickUsingInputSystem(mouse, module, nextButton);
                if (session.Index != 1) throw new Exception("Next click failed");
                ClickUsingInputSystem(mouse, module, backButton);
                if (session.Index != 0) throw new Exception("Back click failed");
                Debug.Log("PS5_XR_POINTER_CLICKS_PASSED");
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
                Application.Quit(0);
            }
            catch (Exception e) { Debug.LogException(e); Application.Quit(1); }
        }

        private void ClickUsingInputSystem(UnityEngine.InputSystem.Mouse mouse, InputSystemUIInputModule module, Button button)
        {
            var position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Debug.Log($"UI_POINTER_CHECK button={button.name} position={position} hits={string.Join(",", hits.ConvertAll(hit => hit.gameObject.name))} pointEnabled={module.point.action.enabled}");
            // Send pointer state through Input System and its raycaster, not onClick.Invoke.
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = position });
            UnityEngine.InputSystem.InputSystem.Update();
            Debug.Log($"UI_POINTER_VALUES mouse={mouse.position.ReadValue()} action={module.point.action.ReadValue<Vector2>()} controls={module.point.action.controls.Count}");
            module.Process();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = position, buttons = 1 });
            UnityEngine.InputSystem.InputSystem.Update();
            module.Process();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = position });
            UnityEngine.InputSystem.InputSystem.Update();
            module.Process();
        }

        private void CapturePreview(string path, int width = 1100, int height = 1500)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            foreach (var child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            var uiCamera = new GameObject("Validation UI camera", typeof(Camera)).GetComponent<Camera>();
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.cullingMask = 1 << 5;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1;
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = height / 1500f;
            var target = new RenderTexture(width, height, 24);
            target.Create();
            sceneCamera.cullingMask &= ~(1 << 5);
            sceneCamera.targetTexture = target;
            uiCamera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            sceneCamera.Render();
            uiCamera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            RenderTexture.active = previous;
            sceneCamera.targetTexture = null;
            uiCamera.targetTexture = null;
            target.Release();
        }

        private void OnDestroy() => repository?.Dispose();
    }
}
