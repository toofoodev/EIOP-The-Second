using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EIOP.Tab_Handlers;
using EIOP.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    private Color BrightGreen = Color.green; 
    private Color DarkGreen = new Color(0.0f, 0.2f, 0.0f, 1.0f); 

    private void Start()
    {
        GameObject menuPrefab = Plugin.EIOPBundle.LoadAsset<GameObject>("Menu");
        Menu = Instantiate(menuPrefab, EIOPUtils.RealLeftController);
        Destroy(menuPrefab);
        Menu.name = "Menu";

        Menu.transform.localPosition = BaseMenuPosition;
        Menu.transform.localRotation = BaseMenuRotation;
        Menu.transform.localScale    = Vector3.zero;

        // 1. Fix Shaders & Restore Icons
        PerformShaderManagement(Menu);

        // 2. Apply Background Colors (Menu & Panels)
        ApplyBackgroundColors();

        // 3. Color Buttons (Anything in panels = Button)
        ColorAllButtonsInMenu();

        // 4. Fix Title Text
        UpdateTitleText();

        Menu.SetActive(false);
        SetUpTabs();

        gameObject.AddComponent<PCHandler>().MenuHandlerInstance = this;
    }

    private void ApplyBackgroundColors()
    {
        // 1. Main Menu Background -> DARK GREEN
        Renderer mainRenderer = Menu.GetComponent<Renderer>();
        if (mainRenderer != null) mainRenderer.material.color = DarkGreen;

        // 2. ModePanel (Bottom Bar) -> BRIGHT GREEN
        Transform modePanel = Menu.transform.Find("ModePanel");
        if (modePanel != null)
        {
            Renderer panelRenderer = modePanel.GetComponent<Renderer>();
            if (panelRenderer != null) panelRenderer.material.color = BrightGreen;
        }

        // 3. SidePanel (Side Bar) -> BRIGHT GREEN
        Transform sidePanel = Menu.transform.Find("SidePanel");
        if (sidePanel != null)
        {
            Renderer sideRenderer = sidePanel.GetComponent<Renderer>();
            if (sideRenderer != null) sideRenderer.material.color = BrightGreen;
        }

        Plugin.MainColour      = DarkGreen;
        Plugin.SecondaryColour = BrightGreen;
    }

    private void ColorAllButtonsInMenu()
    {
        Renderer[] allRenderers = Menu.GetComponentsInChildren<Renderer>(true);
        Transform modePanel = Menu.transform.Find("ModePanel");
        Transform sidePanel = Menu.transform.Find("SidePanel");

        foreach (Renderer rend in allRenderers)
        {
            // --- 1. ICON CHECK (PRIORITY) ---
            // If it has a texture, it is an Icon. Keep it WHITE.
            if (rend.material.mainTexture != null)
            {
                rend.material.color = Color.white; 
                continue; 
            }

            // --- 2. HIERARCHY CHECK ---
            // Check if this object is inside ModePanel or SidePanel
            bool insideModePanel = modePanel != null && rend.transform.IsChildOf(modePanel);
            bool insideSidePanel = sidePanel != null && rend.transform.IsChildOf(sidePanel);

            // If it's inside one of the panels (and not an icon), it is a BUTTON.
            if (insideModePanel || insideSidePanel)
            {
                rend.material.color = DarkGreen;
                continue;
            }

            // --- 3. GENERIC BUTTON CHECK ---
            string objName = rend.gameObject.name;
            Transform parent = rend.transform.parent;
            string parentName = parent != null ? parent.name : "";

            bool isButton = objName.Contains("Button") || parentName.Contains("Button");
            bool isSpecificType = objName.Equals("ButtonType1") || objName.Equals("ButtonType2") || objName.Equals("SoundboardButton");

            if (isButton || isSpecificType)
            {
                rend.material.color = DarkGreen;
            }
        }
    }

    private void UpdateTitleText()
    {
        Transform modePanel = Menu.transform.Find("ModePanel");
        Transform sidePanel = Menu.transform.Find("SidePanel");
        
        Transform textObj = Menu.transform.Find("BlatantPromo");
        if (textObj == null && modePanel != null) textObj = modePanel.Find("BlatantPromo");
        if (textObj == null && sidePanel != null) textObj = sidePanel.Find("BlatantPromo");
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
                textComp.fontSizeMax = 8f;
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

        List<Transform> tabButtons = new List<Transform>();

        // Find buttons in ModePanel
        Transform modePanel = Menu.transform.Find("ModePanel");
        if (modePanel != null)
        {
            tabButtons.AddRange(modePanel.GetComponentsInChildren<Transform>(true)
                                  .Where(t => t.gameObject.name.EndsWith("Button", StringComparison.OrdinalIgnoreCase)));
        }

        // Find buttons in SidePanel
        Transform sidePanel = Menu.transform.Find("SidePanel");
        if (sidePanel != null)
        {
            tabButtons.AddRange(sidePanel.GetComponentsInChildren<Transform>(true)
                                  .Where(t => t.gameObject.name.EndsWith("Button", StringComparison.OrdinalIgnoreCase)));
        }

        foreach (Type tabHandlerType in tabHandlerTypes)
        {
            string tabName = tabHandlerType.Name.Replace("Handler", "");
            
            Transform tabView = tabViews.FirstOrDefault(t => t.gameObject.name.Equals(tabName + "View", StringComparison.OrdinalIgnoreCase));
            Transform tabButton = tabButtons.FirstOrDefault(t => t.gameObject.name.Equals(tabName + "Button", StringComparison.OrdinalIgnoreCase));

            if (tabView == null || tabButton == null) continue; 

            // --- TEXT EDITING LOGIC ---
            TextMeshPro btnText = tabButton.GetComponentInChildren<TextMeshPro>();
            
            // Check if this is a Special Button Type (Type 1 or 2) - likely has custom icons
            bool isSpecialMesh = tabButton.name.Contains("ButtonType1") || tabButton.name.Contains("ButtonType2");

            if (btnText != null)
            {
                // Only rename if it is NOT a special mesh
                if (!isSpecialMesh)
                {
                    if (tabName == "Room")           btnText.text = "LOBBY";
                    else if (tabName == "AntiCheat") btnText.text = "SECURITY";
                    else if (tabName == "Visuals")   btnText.text = "ESP";
                    else btnText.text = tabName.ToUpper(); 
                }
                
                // Ensure text is visible
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

        // 1. HANDLE RENDERERS
        if (obj.TryGetComponent(out Renderer renderer))
        {
            if (!(renderer is ParticleSystemRenderer))
            {
                // Capture texture BEFORE changing shader
                Texture originalTexture = renderer.material.mainTexture;
                
                // Fallback for URP textures
                if (originalTexture == null && renderer.material.HasProperty("_BaseMap"))
                    originalTexture = renderer.material.GetTexture("_BaseMap");

                // Switch Shader
                renderer.material.shader = Plugin.UberShader;
                
                // RESTORE TEXTURE (Critical for Icons)
                if (originalTexture != null)
                {
                    renderer.material.mainTexture = originalTexture;
                    renderer.material.EnableKeyword("_USE_TEXTURE");
                }
            }
        }

        // 2. HANDLE TEXT
        if (obj.TryGetComponent(out TextMeshPro tmp))
        {
            tmp.fontMaterial = new Material(tmp.fontMaterial) { shader = Shader.Find("TextMeshPro/Mobile/Distance Field") };
            tmp.color = Color.white; 
        }

        if (obj.TryGetComponent(out TextMeshProUGUI tmpUGUI))
        {
            tmpUGUI.fontMaterial = new Material(tmpUGUI.fontMaterial) { shader = Shader.Find("TextMeshPro/Mobile/Distance Field") };
            tmpUGUI.color = Color.white;
        }
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
