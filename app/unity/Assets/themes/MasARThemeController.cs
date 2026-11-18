using UnityEngine;

/// <summary>
/// Shows exactly one MasAR+ monster HUD theme at a time.
/// Attach to ThemeCanvas (NOT to an individual theme).
/// Call the Begin...Hunt methods from the existing Hunt selection flow,
/// and EndHunt when the hunt is finished or cancelled.
/// </summary>
public class MasARThemeController : MonoBehaviour
{
    [Header("Theme objects under ThemeCanvas")]
    [SerializeField] private GameObject buDaryahTheme;
    [SerializeField] private GameObject omElsaafTheme;
    [SerializeField] private GameObject tantalTheme;  // Optional until this theme is ready.
    [SerializeField] private GameObject noTheme;      // Optional neutral group.

    private void Awake()
    {
        // Start with no monster selected, regardless of editor active checkboxes.
        ShowOnly(null);
    }

    // These methods can be called from a Button OnClick, UnityEvent, or another script.
    public void BeginBuDaryahHunt() => ShowOnly(buDaryahTheme);
    public void BeginOmElsaafHunt() => ShowOnly(omElsaafTheme);
    public void BeginTantalHunt() => ShowOnly(tantalTheme);

    public void IgnoreMonster() => ShowOnly(null);
    public void CancelHunt() => ShowOnly(null);
    public void EndHunt() => ShowOnly(null);
    public void ShowNoTheme() => ShowOnly(null);

    private void ShowOnly(GameObject selected)
    {
        if (buDaryahTheme != null) buDaryahTheme.SetActive(buDaryahTheme == selected);
        if (omElsaafTheme != null) omElsaafTheme.SetActive(omElsaafTheme == selected);
        if (tantalTheme != null) tantalTheme.SetActive(tantalTheme == selected);
        if (noTheme != null) noTheme.SetActive(selected == null);
    }
}
