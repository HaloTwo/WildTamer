using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMover2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] float speed = 3.5f;
    [SerializeField] float deadZone = 0.15f;

    Transform flipRoot;
    Rigidbody2D rb;
    Animator anim;

    Vector2 input;
    public Vector2 LastMoveDir { get; private set; } = Vector2.right;

    public Vector2 MoveInput => input;

    string isMovingParam = "IsMoving";
    float facing = 1f;

    void Awake()
    {
        TryGetComponent(out rb);
        TryGetComponent(out anim);
        TryGetComponent(out flipRoot);

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Update()
    {
        if (UIJoyStick.Instance == null || !UIJoyStick.Instance.isDragging)
        {
            input = Vector2.zero;
        }
        else
        {
            Vector2 dir = UIJoyStick.Instance.Dir2D;
            input = (dir.magnitude < deadZone) ? Vector2.zero : dir;
        }

        bool isMoving = input.sqrMagnitude > 0.0001f;
        if (anim != null) anim.SetBool(isMovingParam, isMoving);

        if (input.x > 0.01f)
            facing = -1f;
        else if (input.x < -0.01f)
            facing = 1f;

        Vector3 scale = flipRoot.localScale;
        scale.x = facing;
        flipRoot.localScale = scale;

        if (input.sqrMagnitude > 0.0001f)
            LastMoveDir = input.normalized;
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + input * speed * Time.fixedDeltaTime);

        bool isMoving = input.sqrMagnitude > 0.0001f;
        if (anim != null) anim.SetBool(isMovingParam, isMoving);
    }

    public void PlayFootstep()
    {
        if (input.sqrMagnitude <= 0.0001f) return;

        SoundManager.Instance.PlayFootstep();
    }
}