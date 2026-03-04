using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMover2D : MonoBehaviour
{
    [SerializeField] float speed = 1f;
    [SerializeField] float deadZone = 0.15f;

    Rigidbody2D rb;
    Vector2 input;

    void Awake()
    {
        TryGetComponent(out rb);

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Update()
    {
        if (UIJoyStick.Instance == null || !UIJoyStick.Instance.isDragging)
        {
            input = Vector2.zero;
            return;
        }

        Vector2 dir = UIJoyStick.Instance.Dir2D; // 조이스틱이 Vector2 제공하게
        input = (dir.magnitude < deadZone) ? Vector2.zero : dir;
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + input * speed * Time.fixedDeltaTime);
    }
}