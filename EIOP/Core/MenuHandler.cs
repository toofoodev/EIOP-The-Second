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
    // Background Color (Bright Green)
    private Color BrightGreen = Color.green; 
    // Button Color (Dark Green)
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

        // 1. Fix Shaders & Preserve Icons
        PerformShaderManagement(Menu);

        // 2. Apply Background Colors (Everything Bright Green)
        ApplyBackgroundColors();

        // 3. Color ALL Buttons (Everything Dark Green)
        ColorAllButtonsInMenu();

        // 4. Fix Title Text
        UpdateTitleText();

        Menu.SetActive(false);
        SetUpTabs();

        gameObject.AddComponent<PCHandler>().MenuHandlerInstance = this;
    }

    private void ApplyBackgroundColors()
    {
        // Main Background -> Bright Green
        Renderer mainRenderer = Menu.GetComponent<Renderer>();
        if (mainRenderer != null) mainRenderer.material.color = BrightGreen;

        // ModePanel (Bottom Bar) -> Bright Green
        Transform modePanel = Menu.transform.Find("ModePanel");
        if (modePanel != null)
        {
            Renderer panelRenderer = modePanel.GetComponent<Renderer>();
            if (panelRenderer != null) panelRenderer.material.color = BrightGreen;
        }

        // SidePanel (Side Bar) -> Bright Green
        Transform sidePanel = Menu.transform.Find("SidePanel");
        if (sidePanel != null)
        {
            Renderer sideRenderer = sidePanel.GetComponent<Renderer>();
            if (sideRenderer != null) sideRenderer.material.color = BrightGreen;
        }

        // Update Global Settings
        Plugin.MainColour      = BrightGreen;
        Plugin.SecondaryColour = DarkGreen;
    }

    private void ColorAllButtonsInMenu()
    {
        // Get EVERY Renderer inside the menu
        Renderer[] allRenderers = Menu.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer rend in allRenderers)
        {
            string objName = rend.gameObject.name;
            Transform parent = rend.transform.parent;
            string parentName = parent != null ? parent.name : "";

            // --- CHECK IF THIS IS AN ICON ---
            // If it has a texture, it's likely an Icon. Keep it white so it's visible.
            if (rend.material.mainTexture != null) 
            {
                rend.material.color = Color.white; 
                continue;
            }

            // --- COLORING LOGIC ---
            
            // 1. Buttons inside ModePanel OR SidePanel
            bool isBarButton = parentName.Equals("ModePanel", StringComparison.OrdinalIgnoreCase) || 
                               parentName.Equals("SidePanel", StringComparison.OrdinalIgnoreCase);

            if (isBarButton)
            {
                rend.material.color = DarkGreen; // Dark button on Bright background
                continue;
            }

            // 2. Main Menu Buttons & Specific Fixes
            bool isButton = objName.Contains("Button") || parentName.Contains("Button");
            bool isSpecificType = objName.Equals("ButtonType1") || objName.Equals("ButtonType2") || objName.Equals("SoundboardButton");

            if (isButton || isSpecificType)
            {
                rend.material.color = DarkGreen; // Dark button on Bright background
            }
        }
    }

    private void UpdateTitleText()
    {
        Transform modePanel = Menu.transform.Find("ModePanel");
        Transform sidePanel = Menu.transform.Find("SidePanel");
        
        // Find Title Object
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
                // Text needs to be Black or Dark Green to be visible on Bright Green background?
                // Or White if you prefer. I'll stick to White, but if it's hard to read, change to Color.black
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

        // Gather buttons from BOTH ModePanel AND SidePanel
        List<Transform> tabButtons = new List<Transform>();

        Transform modePanel = Menu.transform.Find("ModePanel");
        if (modePanel != null)
        {
            tabButtons.AddRange(modePanel.GetComponentsInChildren<Transform>(true)
                                  .Where(t => t.gameObject.name.EndsWith("Button", StringComparison.OrdinalIgnoreCase)));
        }

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

            if (tabView == null || tabButton == null)
            {
                continue; 
            }

            // Rename Buttons logic
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

        // 1. HANDLE RENDERERS (Buttons, Backgrounds, Icons)
        if (obj.TryGetComponent(out Renderer renderer))
        {
            if (!(renderer is ParticleSystemRenderer))
            {
                // Save the existing texture (Icon) before switching shaders
                Texture originalTexture = renderer.material.mainTexture;
                
                // If mainTexture is null, check for URP properties (_BaseMap)
                if (originalTexture == null && renderer.material.HasProperty("_BaseMap"))
                    originalTexture = renderer.material.GetTexture("_BaseMap");

                // Apply UberShader
                renderer.material.shader = Plugin.UberShader;
                
                // Re-apply the texture so the Icon appears
                if (originalTexture != null)
                {
                    renderer.material.mainTexture = originalTexture;
                    renderer.material.EnableKeyword("_USE_TEXTURE");
                }
                
                // Default to white so texture isn't tinted invisible.
                renderer.material.color = Color.white; 
            }
        }

        // 2. HANDLE 3D TEXT
        if (obj.TryGetComponent(out TextMeshPro tmp))
        {
            tmp.fontMaterial = new Material(tmp.fontMaterial)
            {
                shader = Shader.Find("TextMeshPro/Mobile/Distance Field")
            };
            tmp.color = Color.white; 
        }

        // 3. HANDLE UI TEXT
        if (obj.TryGetComponent(out TextMeshProUGUI tmpUGUI))
        {
            tmpUGUI.fontMaterial = new Material(tmpUGUI.fontMaterial)
            {
                shader = Shader.Find("TextMeshPro/Mobile/Distance Field")
            };
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
