using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EIOP.Tab_Handlers;
using EIOP.Tools;
using TMPro;
using UnityEngine;

namespace EIOP.Core;

public class MenuHandler : MonoBehaviour
{
    public static readonly Vector3 TargetMenuScale = Vector3.one * 15f;
    public static readonly Vector3 BaseMenuPosition = new(0.25f, 0f, 0.05f);
    public static readonly Quaternion BaseMenuRotation = Quaternion.Euler(300f, 0f, 180f);

    public bool IsMenuOpen;
    public GameObject Menu;
    private bool wasPressed;

    // --- COLORS ---
    private Color BarColor = Color.green; // Bright Green (Side/Bottom bars)
    private Color BaseColor = new Color(0.0f, 0.2f, 0.0f, 1.0f); // Dark Green (Background/Buttons)

    private void Start()
    {
        GameObject menuPrefab = Plugin.EIOPBundle.LoadAsset<GameObject>("Menu");
        Menu = Instantiate(menuPrefab, EIOPUtils.RealLeftController);
        Destroy(menuPrefab);
        Menu.name = "Menu";

        Menu.transform.localPosition = BaseMenuPosition;
        Menu.transform.localRotation = BaseMenuRotation;
        Menu.transform.localScale    = Vector3.zero;

        // 1. Fix Shaders
        PerformShaderManagement(Menu);

        // 2. Apply Main Background Colors
        Renderer mainRenderer = Menu.GetComponent<Renderer>();
        if (mainRenderer != null) mainRenderer.material.color = BaseColor; 

        Transform panelTrans = Menu.transform.Find("ModePanel");
        if (panelTrans != null)
        {
            Renderer panelRenderer = panelTrans.GetComponent<Renderer>();
            if (panelRenderer != null) panelRenderer.material.color = BarColor;
        }

        Plugin.MainColour      = BaseColor;
        Plugin.SecondaryColour = BarColor;

        // 3. FORCE COLOR ALL BUTTONS (Fixes ButtonType1, ButtonType2, SoundboardButton)
        ColorAllButtonsInMenu();

        // 4. Update Title Text
        UpdateTitleText(panelTrans);

        Menu.SetActive(false);
        SetUpTabs();

        gameObject.AddComponent<PCHandler>().MenuHandlerInstance = this;
    }

    private void ColorAllButtonsInMenu()
    {
        // Get EVERY Renderer inside the menu
        Renderer[] allRenderers = Menu.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer rend in allRenderers)
        {
            string objName = rend.gameObject.name;
            string parentName = rend.transform.parent != null ? rend.transform.parent.name : "";

            // Check for specific names you mentioned or general "Button" names
            bool isButton = objName.Contains("Button") || parentName.Contains("Button");
            bool isSpecificType = objName.Equals("ButtonType1") || objName.Equals("ButtonType2") || objName.Equals("SoundboardButton");

            // If it matches, color it Dark Green
            if (isButton || isSpecificType)
            {
                rend.material.color = BaseColor;
            }
        }
    }

    private void UpdateTitleText(Transform panelTrans)
    {
        // Find Title Object
        Transform textObj = Menu.transform.Find("BlatantPromo");
        if (textObj == null && panelTrans != null) textObj = panelTrans.Find("BlatantPromo");
        if (textObj == null) textObj = Menu.transform.Find("Title");

        if (textObj != null)
        {
            TextMeshPro textComp = textObj.GetComponent<TextMeshPro>();
            if (textComp != null) 
            {
                textComp.text = "EIOP: (Everything In One Place)\nEdited by: WesGoof & Pico\nOriginal Mod: HanSolo1000Falcon";
                textComp.alignment = TextAlignmentOptions.Center;
                textComp.enableAutoSizing = true;
                textComp.fontSizeMin = 0.1f;
                textComp.fontSizeMax = 10f;
                textComp.color = Color.white;
            }
        }
    }

    private void Update()
    {
        bool isPressed = ControllerInputPoller.instance.leftControllerSecondaryButton;

        if (isPressed && !wasPressed)
        {
            IsMenuOpen = !IsMenuOpen;
            CoroutineManager.Instance.StartCoroutine(IsMenuOpen ? OpenMenu() : CloseMenu());
        }

        wasPressed = isPressed;
    }

    private void SetUpTabs()
    {
        List<Type> tabHandlerTypes = Assembly.GetExecutingAssembly().GetTypes()
                                             .Where(t => !t.IsAbstract && t.IsClass &&
                                                         typeof(TabHandlerBase).IsAssignableFrom(t)).ToList();

        List<Transform> tabViews = Menu.GetComponentsInChildren<Transform>(true)
                                       .Where(t => t.gameObject.name.EndsWith("View",
                                                      StringComparison.OrdinalIgnoreCase)).ToList();

        Transform modePanel = Menu.transform.Find("ModePanel");
        List<Transform> tabButtons = modePanel.GetComponentsInChildren<Transform>(true)
                                              .Where(t => t.gameObject.name.EndsWith("Button",
                                                             StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (Type tabHandlerType in tabHandlerTypes)
        {
            string tabName = tabHandlerType.Name.Replace("Handler", "");
            
            Transform tabView = tabViews.FirstOrDefault(t => t.gameObject.name.Equals(tabName + "View", StringComparison.OrdinalIgnoreCase));
            Transform tabButton = tabButtons.FirstOrDefault(t => t.gameObject.name.Equals(tabName + "Button", StringComparison.OrdinalIgnoreCase));

            if (tabView == null || tabButton == null)
            {
                Debug.LogWarning($"[MenuHandler] Missing View/Button for: {tabName}");
                continue;
            }

            // --- RENAME BUTTONS ---
            TextMeshPro btnText = tabButton.GetComponentInChildren<TextMeshPro>();
            if (btnText != null)
            {
                if (tabName == "Room")           btnText.text = "LOBBY";
                else if (tabName == "AntiCheat") btnText.text = "SECURITY";
                else if (tabName == "Visuals")   btnText.text = "ESP";
                else 
                {
                    btnText.text = tabName.ToUpper(); 
                }
                btnText.color = Color.white; 
            }

            EIOPButton button = tabButton.gameObject.AddComponent<EIOPButton>();
            button.OnPress = () =>
                             {
                                 foreach (Transform tab in tabViews) tab.gameObject.SetActive(false);
                                 tabView.gameObject.SetActive(true);
                             };

            tabView.gameObject.AddComponent(tabHandlerType);
            tabView.gameObject.SetActive(false);
        }
    }

    public static void PerformShaderManagement(GameObject obj)
    {
        foreach (Transform child in obj.transform)
            PerformShaderManagement(child.gameObject);

        if (obj.TryGetComponent(out Renderer renderer))
            if (renderer.material.shader.name.Contains("Universal"))
            {
                renderer.material.shader = Plugin.UberShader;
                if (renderer.material.mainTexture != null) renderer.material.EnableKeyword("_USE_TEXTURE");
            }

        if (obj.TryGetComponent(out TextMeshPro tmp))
            tmp.fontMaterial = new Material(tmp.fontMaterial)
            {
                    shader = Shader.Find("TextMeshPro/Mobile/Distance Field"),
            };

        if (obj.TryGetComponent(out TextMeshProUGUI tmpUGUI))
            tmpUGUI.fontMaterial = new Material(tmpUGUI.fontMaterial)
            {
                    shader = Shader.Find("TextMeshPro/Mobile/Distance Field"),
            };
    }

    public IEnumerator OpenMenu()
    {
        Menu.SetActive(true);
        Menu.transform.localScale = Vector3.zero;
        float startTime = Time.time;

        while (Time.time - startTime < 0.1f)
        {
            float t = (Time.time - startTime) / 0.1f;
            Menu.transform.localScale = Vector3.Lerp(Vector3.zero, TargetMenuScale, t);

            yield return null;
        }

        Menu.transform.localScale = TargetMenuScale;
    }

    public IEnumerator CloseMenu()
    {
        Menu.transform.localScale = TargetMenuScale;
        float startTime = Time.time;

        while (Time.time - startTime < 0.1f)
        {
            float t = (Time.time - startTime) / 0.1f;
            Menu.transform.localScale = Vector3.Lerp(TargetMenuScale, Vector3.zero, t);

            yield return null;
        }

        Menu.transform.localScale = Vector3.zero;
        Menu.SetActive(false);
    }
}
