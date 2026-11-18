using UnityEngine;

public class EditorHuntTester : MonoBehaviour
{
    public MonsterHuntUIController huntUI;

    [ContextMenu("TEST: Accept Hunt")]
    public void TestAcceptHunt()
    {
        if (huntUI == null)
        {
            Debug.LogError("EditorHuntTester: Hunt UI is not assigned.");
            return;
        }

        Debug.Log("EditorHuntTester: Accepting hunt.");
        huntUI.AcceptHunt();
    }
}