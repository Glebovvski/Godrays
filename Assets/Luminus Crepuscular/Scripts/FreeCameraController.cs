using UnityEngine;

namespace Luminus.Runtime.Common
{

    public class FreeCameraController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float fastMultiplier = 3f;

        private float yaw;
        private float pitch;

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            Look();
            Move();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Look()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
            pitch = Mathf.Clamp(pitch, -89f, 89f);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void Move()
        {
            float speed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * fastMultiplier : moveSpeed;

            Vector3 input = new Vector3(
                Input.GetAxisRaw("Horizontal"),
                0f,
                Input.GetAxisRaw("Vertical"));

            Vector3 direction = transform.right * input.x + transform.forward * input.z;

            if (Input.GetKey(KeyCode.E))
                direction += Vector3.up;

            if (Input.GetKey(KeyCode.Q))
                direction += Vector3.down;

            transform.position += direction.normalized * speed * Time.deltaTime;
        }
    }
}