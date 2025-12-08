using UnityEngine;
using TMPro;

public class BuildLabel : MonoBehaviour
{
    [TextArea]
    public string versionText = "Airbender v0.4 – 2025-11-24";
    public TMP_Text targetText;  // or UnityEngine.UI.Text

    void Start()
    {
        if (targetText)
            targetText.text = versionText + "\n" + Application.version;
    }
}
