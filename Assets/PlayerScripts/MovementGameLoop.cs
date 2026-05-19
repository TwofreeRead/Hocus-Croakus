using UnityEngine;

public class MovementGameLoop : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerMovement movementEngine;
    [SerializeField] private bool showDebug = true;

    void Update()
    {
        if (inputReader != null && movementEngine != null)
        {
            movementEngine.ProcessMovement(inputReader.CurrentInput);
        }
    }

    void OnGUI()
    {
        if (!showDebug || movementEngine == null) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 18;
        style.normal.textColor = Color.green;

        string debugText = $"State: {movementEngine.CurrentState}\n" +
                           $"Speed: {movementEngine.CurrentSpeed:F2}\n" +
                           $"Slope Angle: {movementEngine.CurrentSlopeAngle:F1}\n" +
                           $"Slope Effect: {movementEngine.SlopeMultiplier:F2}\n" +
                           $"Slide Time: {movementEngine.SlideTimeElapsed:F2}";

        GUI.Label(new Rect(20, 20, 400, 200), debugText, style);
    }
}