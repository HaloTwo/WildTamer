using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMover2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] float speed = 1f;
    [SerializeField] float deadZone = 0.15f;

    [Header("Flip")]
    [SerializeField] Transform flipRoot; // Body 추천

    [Header("Animator Param")]
    [SerializeField] string isMovingParam = "IsMoving"; // Animator bool 이름

    Rigidbody2D rb;
    Animator anim;

    SpriteRenderer[] sprites;      // 하위 파츠 스프라이트 전부
    bool facingLeft = false;       // 현재 바라보는 방향

    Vector2 input;
    float facing = 1f;

    void Awake()
    {
        TryGetComponent(out rb);
        TryGetComponent(out anim);

        sprites = GetComponentsInChildren<SpriteRenderer>(true);

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Update()
    {
        // 입력
        if (UIJoyStick.Instance == null || !UIJoyStick.Instance.isDragging)
        {
            input = Vector2.zero;
        }
        else
        {
            Vector2 dir = UIJoyStick.Instance.Dir2D;
            input = (dir.magnitude < deadZone) ? Vector2.zero : dir;
        }

        // 이동 애니메이션 (bool)
        bool isMoving = input.sqrMagnitude > 0.0001f;
        if (anim != null) anim.SetBool(isMovingParam, isMoving);

        // 좌/우 플립 (방향이 바뀔 때만 전체 적용)
        if (input.x > 0.01f)
            SetFacingLeft(true);
        else if (input.x < -0.01f)
            SetFacingLeft(false);
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + input * speed * Time.fixedDeltaTime);
    }

    void SetFacingLeft(bool left)
    {
        if (facingLeft == left) return; // 이미 그 방향이면 아무것도 안 함
        facingLeft = left;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                sprites[i].flipX = left;
        }
    }


}