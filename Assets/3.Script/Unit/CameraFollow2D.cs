using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Rigidbody2D targetRb;
    [SerializeField] Vector3 offset = new Vector3(0f, 1f, -10f);

    [Header("Tiny lag")]
    [SerializeField] float smoothMoving = 0.5f;   // 이동 중: 아주 살짝만
    [SerializeField] float smoothStopping = 0.5f; // 멈춤: 조금 더 느리게 마무리

    [Header("Clamp")]
    [SerializeField] float maxDistance = 0.25f;     // 카메라가 플레이어에서 최대 이 거리까지만 떨어지게

    [Header("Moving 판단")]
    [SerializeField] float stopSpeedThreshold = 0.05f;

    Vector3 velocity;

    void LateUpdate()
    {
        if (target == null) return;

        bool isMoving = targetRb != null &&
                        targetRb.linearVelocity.sqrMagnitude > stopSpeedThreshold * stopSpeedThreshold;

        float smooth = isMoving ? smoothMoving : smoothStopping;

        Vector3 desired = target.position + offset;

        // 먼저 스무스 이동
        Vector3 next = Vector3.SmoothDamp(transform.position, desired, ref velocity, smooth);

        // 너무 멀어지면 클램프해서 "절대 크게 뒤처지지 않게"
        Vector3 delta = next - desired;
        delta.z = 0f; // z는 고정이니까 제외
        if (delta.sqrMagnitude > maxDistance * maxDistance)
        {
            Vector3 clamped = desired + Vector3.ClampMagnitude(delta, maxDistance);
            clamped.z = desired.z;
            next = clamped;
            velocity = Vector3.zero; // 여기서 속도 리셋(튀는 거 방지)
        }

        // z 고정
        next.z = desired.z;
        transform.position = next;
    }
}