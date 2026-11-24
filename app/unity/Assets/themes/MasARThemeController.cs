using UnityEngine;


/// <summary>
/// Controls the monster themes shown on the MasAR+ HUD.
/// Only the selected monster's theme is active during a hunt.
/// The HUD returns to the neutral theme when the hunt ends or is ignored.
/// </summary>
public class MasARThemeController : MonoBehaviour
{
    [Header("Theme objects under ThemeCanvas")]
    [SerializeField] private GameObject buDaryahTheme;
    [SerializeField] private GameObject omElsaafTheme;
    [SerializeField] private GameObject tantalTheme;
    [SerializeField] private GameObject hamaretTheme;
    [SerializeField] private GameObject noTheme;

    /// <summary>
    /// Starts with no monster theme selected.
    /// </summary>
    private void Awake()
    {

        ShowOnly(null);
    }


    /// <summary>
    /// Shows Bu Daryah's theme when his hunt begins.
    /// </summary>
    public void BeginBuDaryahHunt() => ShowOnly(buDaryahTheme);
    /// <summary>
    /// Shows Om Elsaaf's theme when her hunt begins.
    /// </summary>
    public void BeginOmElsaafHunt() => ShowOnly(omElsaafTheme);
    /// <summary>
    /// Shows Tantal's theme when his hunt begins.
    /// </summary>
    public void BeginTantalHunt() => ShowOnly(tantalTheme);
    public void BeginHamaretHunt() => ShowOnly(hamaretTheme);

    /// <summary>
    /// Returns to the neutral HUD when the player ignores a monster.
    /// </summary>
    public void IgnoreMonster() => ShowOnly(null);
    /// <summary>
    /// Removes the active theme when a hunt is cancelled.
    /// </summary>
    public void CancelHunt() => ShowOnly(null);
    /// <summary>
    /// Removes the active theme after the hunt ends.
    /// </summary>
    public void EndHunt() => ShowOnly(null);
    /// <summary>
    /// Displays the neutral HUD before another hunt starts.
    /// </summary>
    public void ShowNoTheme() => ShowOnly(null);

    /// <summary>
    /// Enables the chosen theme and disables the others.
    /// </summary>
    private void ShowOnly(GameObject selected)
    {
        if (buDaryahTheme != null) buDaryahTheme.SetActive(buDaryahTheme == selected);
        if (omElsaafTheme != null) omElsaafTheme.SetActive(omElsaafTheme == selected);
        if (tantalTheme != null) tantalTheme.SetActive(tantalTheme == selected);
        if (hamaretTheme != null) hamaretTheme.SetActive(hamaretTheme == selected);
        // The neutral group is only visible when no monster is selected.
        if (noTheme != null) noTheme.SetActive(selected == null);
    }
}
