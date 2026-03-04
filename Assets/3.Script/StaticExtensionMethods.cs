using UnityEngine;

public static class ExtensionMethods
{

    /// <summary>
    /// XZ 좌표를 Vector3로 리턴받기
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    public static Vector3 ToXZ(this Vector2 v)
    {
        return new Vector3(v.x, 0, v.y);
    }
}
