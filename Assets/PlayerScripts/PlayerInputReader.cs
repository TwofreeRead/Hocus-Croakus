using UnityEngine;

public struct PlayerInputData
{
    public Vector2 moveInput;
    public bool jumpPressed;
    public bool sprintHeld;
    public bool crouchPressed;
    public bool crouchHeld;
}

public class PlayerInputReader : MonoBehaviour
{
    public PlayerInputData CurrentInput { get; private set; }

    void Update()
    {
        PlayerInputData data = new PlayerInputData();
        data.moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        data.jumpPressed = Input.GetButtonDown("Jump");
        data.sprintHeld = Input.GetKey(KeyCode.LeftShift);
        data.crouchPressed = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C);
        data.crouchHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);

        CurrentInput = data;
    }
}