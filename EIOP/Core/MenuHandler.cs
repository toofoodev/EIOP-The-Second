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

        PerformShaderManagement(Menu);
        ApplyBackgroundColors();
        ColorAllButtonsInMenu();
        UpdateTitleText();

        Menu.SetActive(false);
        SetUpTabs();

        gameObject.AddComponent<PCHandler>().MenuHandlerInstance = this;
    }

    // ----------------------------------
    // VERSION CHECK (STRING BASED)
    // ----------------------------------
    private bool IsPluginOutdated()
    {
        // If version hasn't been fetched yet, allow menu
        if (Plugin.Instance == null || string.IsNullOrEmpty(Plugin.Instance.NewestVer))
            return false;

        string current = Constants.PluginVersion.Trim();
        string latest  = Plugin.Instance.NewestVer.Trim();

        return !current.Equals(latest, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyBackgroundColors()
    {
        Renderer mainRenderer = Menu.GetComponent<Renderer>();
        if (mainRenderer != null) mainRenderer.material.color = DarkGreen;

        Transform modePanel = Menu.transform.Find("ModePanel");
        if (modePanel != null)
        {
            Renderer panelRenderer = modePanel.GetComponent<Renderer>();
            if (panelRenderer != null) panelRenderer.material.color = BrightGreen;
        }

        Transform sidePanel = Menu.transform.Find("SidePanel");
        if (sidePanel != null)
        {
            Renderer sideRenderer = sidePanel.GetComponent<Renderer>();
            if (sideRenderer != null) sideRenderer.material.color = BrightGreen;
        }

        Plugin.MainColour      = BrightGreen;
        Plugin.SecondaryColour = DarkGreen;
    }

    private void ColorAllButtonsInMenu()
    {
        Renderer[] allRenderers = Menu.GetComponentsInChildren<Renderer>(true);

        Transform modePanel = Menu.transform.Find("ModePanel");
        Transform sidePanel = Menu.transform.Find("SidePanel");

        foreach (Renderer rend in allRenderers)
        {
            // --- 1. IMAGE RULE ---
            if (rend.gameObject.name.Equals("Image", StringComparison.OrdinalIgnoreCase))
            {
                rend.material.color = Color.white;
                // Set rotation for images
                rend.transform.localRotation = Quaternion.Euler(-90f, 0f, -180f);
                continue;
            }

            // --- 2. PANEL BACKGROUNDS ONLY (NOT CHILDREN) ---
            bool isModePanel = modePanel != null && rend.transform == modePanel;
            bool isSidePanel = sidePanel != null && rend.transform == sidePanel;

            if (isModePanel || isSidePanel)
            {
                continue; // leave panel color as is
            }

            // --- 3. EVERYTHING ELSE = BUTTON ---
            rend.material.color = DarkGreen;
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
                textComp.text = "EIOP The Second:\nEdited by: WesGoof & Pico\nOriginal Mod: https://github.com/hansolo1000falcon/eiop/releases/latest";
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
            // ----------------------------------
            // BLOCK MENU IF OUTDATED
            // ----------------------------------
            if (IsPluginOutdated())
            {
                Notifications.SendNotification(
                    $"<color=red>OUTDATED VERSION</color>\n" +
                    $"Current: {Constants.PluginVersion}\n" +
                    $"Latest: {Plugin.Instance.NewestVer}\n\n" +
                    $"Please update to use the menu."
                );

                // Force menu closed
                if (IsMenuOpen)
                {
                    IsMenuOpen = false;
                    CoroutineManager.Instance.StartCoroutine(CloseMenu());
                }

                wasPressed = isPressed;
                return;
            }

            // Normal toggle
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

            if (tabView == null || tabButton == null) continue; 

            TextMeshPro btnText = tabButton.GetComponentInChildren<TextMeshPro>();
            if (btnText != null)
            {
                if (tabName == "Room") btnText.text = "LOBBY";
                else if (tabName == "AntiCheat") btnText.text = "SECURITY";
                else if (tabName == "Visuals") btnText.text = "ESP";
                else btnText.text = tabName.ToUpper(); 

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
        {
            if (!(renderer is ParticleSystemRenderer))
            {
                Texture originalTexture = renderer.material.mainTexture;

                if (originalTexture == null && renderer.material.HasProperty("_BaseMap"))
                    originalTexture = renderer.material.GetTexture("_BaseMap");

                renderer.material.shader = Plugin.UberShader;

                if (originalTexture != null)
                {
                    renderer.material.mainTexture = originalTexture;
                    renderer.material.EnableKeyword("_USE_TEXTURE");
                }

                renderer.material.color = Color.white; 
            }
        }

        if (obj.TryGetComponent(out TextMeshPro tmp))
        {
            tmp.fontMaterial = new Material(tmp.fontMaterial)
            {
                shader = Shader.Find("TextMeshPro/Mobile/Distance Field")
            };
            tmp.color = Color.white; 
        }

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
        // Extra safety: never open if outdated
        if (IsPluginOutdated())
            yield break;

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
