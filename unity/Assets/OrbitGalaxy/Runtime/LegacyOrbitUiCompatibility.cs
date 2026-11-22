using UnityEngine.UI;

namespace Orbit.Unity
{
    // The frozen legacy scripts in Assets/Orbit compile in the predefined
    // Assembly-CSharp. OrbitApp.cs references CanvasScaler and GraphicRaycaster
    // but omits `using UnityEngine.UI;`. These thin subclasses live in the same
    // namespace as OrbitApp, so those unqualified names resolve without editing
    // the frozen Orbit sources. They inherit the real uGUI components, so
    // AddComponent<...>() and the inherited ScaleMode enum keep working.
    public sealed class CanvasScaler : UnityEngine.UI.CanvasScaler
    {
    }

    public sealed class GraphicRaycaster : UnityEngine.UI.GraphicRaycaster
    {
    }
}
