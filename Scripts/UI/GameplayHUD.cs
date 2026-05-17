using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MobControl.UI;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Crea y gestiona toda la UI de gameplay programáticamente.
    /// No requiere jerarquía manual en el Canvas — todo se genera en Awake.
    ///
    /// SETUP: solo añadir este script al Canvas (Screen Space - Overlay).
    /// LauncherController obtiene las barras a través de GameplayHUD.Instance.
    ///
    /// POSICIONAMIENTO:
    /// Barras en la parte inferior de la pantalla, a los lados del centro.
    /// SuperSoldier (izquierda) y Especiales (derecha).
    /// No bloquean la vista del campo de juego.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class GameplayHUD : MonoBehaviour
    {
        public static GameplayHUD Instance { get; private set; }

        [Header("Colores de barras")]
        [SerializeField] private Color _barNormalColor    = new Color(0.3f, 0.7f, 1f, 1f);
        [SerializeField] private Color _barFullColor      = new Color(0.2f, 1f, 0.4f, 1f);
        [SerializeField] private Color _specialNormalColor = new Color(0.1f, 0.8f, 0.9f, 1f);
        [SerializeField] private Color _specialFullColor   = new Color(0f, 1f, 0.8f, 1f);
        [SerializeField] private Color _barBgColor        = new Color(0f, 0f, 0f, 0.5f);

        // ── Referencias generadas ────────────────────────────────────────

        public ChargeBarController SuperSoldierBar { get; private set; }
        public ChargeBarController SpecialUnitBar  { get; private set; }

        // ── Dimensiones de las barras ────────────────────────────────────

        private const float BarWidth    = 120f;
        private const float BarHeight   = 18f;
        private const float BarMarginX  = 20f;
        private const float BarMarginY  = 30f;
        private const float BarSpacingY = 24f;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode      = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder    = 10;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            BuildBars();
        }

        // ── Construcción de barras ───────────────────────────────────────

        private void BuildBars()
        {
            // Barra izquierda — Súper Soldado
            SuperSoldierBar = CreateBar(
                "SuperSoldierBar",
                label:        "SS",
                anchorMin:    new Vector2(0f, 0f),
                anchorMax:    new Vector2(0f, 0f),
                pivot:        new Vector2(0f, 0f),
                anchoredPos:  new Vector2(BarMarginX, BarMarginY),
                normalColor:  _barNormalColor,
                fullColor:    _barFullColor
            );

            // Barra derecha — Unidades Especiales
            SpecialUnitBar = CreateBar(
                "SpecialUnitBar",
                label:        "ESP",
                anchorMin:    new Vector2(1f, 0f),
                anchorMax:    new Vector2(1f, 0f),
                pivot:        new Vector2(1f, 0f),
                anchoredPos:  new Vector2(-BarMarginX, BarMarginY),
                normalColor:  _specialNormalColor,
                fullColor:    _specialFullColor
            );
        }

        /// <summary>
        /// Crea una barra de carga con su fondo y relleno, correctamente anclada.
        /// </summary>
        private ChargeBarController CreateBar(string barName,
                                              string label,
                                              Vector2 anchorMin,
                                              Vector2 anchorMax,
                                              Vector2 pivot,
                                              Vector2 anchoredPos,
                                              Color normalColor,
                                              Color fullColor)
        {
            // Contenedor
            GameObject container = new GameObject(barName);
            container.transform.SetParent(transform, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin      = anchorMin;
            containerRect.anchorMax      = anchorMax;
            containerRect.pivot          = pivot;
            containerRect.anchoredPosition = anchoredPos;
            containerRect.sizeDelta      = new Vector2(BarWidth, BarHeight + 20f);

            // Label
            GameObject labelGO = new GameObject("Label");
            labelGO.transform.SetParent(container.transform, false);
            TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 14;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            RectTransform labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin      = new Vector2(0f, 1f);
            labelRect.anchorMax      = new Vector2(1f, 1f);
            labelRect.pivot          = new Vector2(0f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, 0f);
            labelRect.sizeDelta      = new Vector2(0f, 18f);

            // Fondo
            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(container.transform, false);
            Image bgImg   = bg.AddComponent<Image>();
            bgImg.color   = _barBgColor;
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin      = new Vector2(0f, 0f);
            bgRect.anchorMax      = new Vector2(1f, 0f);
            bgRect.pivot          = new Vector2(0f, 0f);
            bgRect.anchoredPosition = new Vector2(0f, 0f);
            bgRect.sizeDelta      = new Vector2(0f, BarHeight);

            // Fill
            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(bg.transform, false);
            Image fillImg      = fillGO.AddComponent<Image>();
            fillImg.color      = normalColor;
            fillImg.type       = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0f;
            RectTransform fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin      = Vector2.zero;
            fillRect.anchorMax      = Vector2.one;
            fillRect.offsetMin      = Vector2.zero;
            fillRect.offsetMax      = Vector2.zero;

            // ChargeBarController
            ChargeBarController bar = container.AddComponent<ChargeBarController>();
            bar.SetupReferences(fillImg, normalColor, fullColor);

            return bar;
        }
    }
}