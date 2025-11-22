using System;
using UnityEngine;

public class EditorCamera : MonoBehaviour
{
    [SerializeField] private EditorCameraSettings Settings;

    /// <summary>
    /// Gets the settings of the editor camera.
    /// </summary>
    /// <returns></returns>
    public EditorCameraSettings GetEditorCameraSettings() =>
        Settings;

    private void Start()
    {
        // Setting camera
        _camera = GetComponent<Camera>();

        // Setting current yaw and pitch
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }
    private void Update()
    {
        if (_allowMovement) UpdateMovement();
        if (_allowRotation) UpdateLooking();
    }

    /// <summary>
    /// Gets the linked camera to this controller.
    /// </summary>
    /// <returns></returns>
    public Camera GetCamera()
    {
        return _camera;
    }

    /// <summary>
    /// Updates the position of the camera based on the pressed keybinds.
    /// </summary>
    private void UpdateMovement()
    {
        // Calculating speed
        float speed = Settings.MovementSpeed * (Input.GetKey(KeyCode.LeftShift)
            ? Settings.SprintSpeedMultiplier : 1f);

        // Getting input direction
        Vector3 inputDir = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
            inputDir += new Vector3(0, 0, 1);
        if (Input.GetKey(KeyCode.S))
            inputDir += new Vector3(0, 0, -1);
        if (Input.GetKey(KeyCode.A))
            inputDir += new Vector3(-1, 0, 0);
        if (Input.GetKey(KeyCode.D))
            inputDir += new Vector3(1, 0, 0);

        // Moving camera
        Vector3 moveDir = transform.TransformDirection(inputDir.normalized);
        transform.position += moveDir * speed * Time.deltaTime;
    }
    /// <summary>
    /// Updates the rotation of the camera based on the mouse movement.
    /// </summary>
    private void UpdateLooking()
    {
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * Settings.Sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * Settings.Sensitivity;
            pitch = Mathf.Clamp(pitch, Settings.CameraConstraints.x, Settings.CameraConstraints.y);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    /// <summary>
    /// Sets if the user is able to move the camera.
    /// </summary>
    /// <param name="allow"></param>
    public void SetAllowMovement(bool allow) =>
        _allowMovement = allow;
    /// <summary>
    /// Sets if the user is allowed to rotate the camera.
    /// </summary>
    /// <param name="allow"></param>
    public void SetAllowRotation(bool allow) =>
        _allowRotation = allow;

    private bool _allowMovement = true;
    private bool _allowRotation = true;

    private Camera _camera;
    private float yaw = 0f;
    private float pitch = 0f;
}

[Serializable]
public struct EditorCameraSettings
{
    public float MovementSpeed;
    public float SprintSpeedMultiplier;
    public Vector2 CameraConstraints;
    public float Sensitivity;
}