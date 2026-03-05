using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;

public class MiniMapFog_WorldAccum : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Transform player;
    [SerializeField] Camera minimapCam;          // 미니맵 카메라(플레이어 따라오는 카메라)
    [SerializeField] RawImage fogImage;          // FogOverlay RawImage
    [SerializeField] RectTransform minimapRect;  // 미니맵 Rect
    [SerializeField] RectTransform playerIcon;   // 플레이어 아이콘 Rect
    [SerializeField] Tilemap boundsTilemap;      // 맵 전체 bounds용 타일맵

    [Header("Fog Texture (World Accum)")]
    [SerializeField] int texSize = 512;
    [SerializeField] float revealRadiusWorld = 2.0f;
    [SerializeField] float updateInterval = 0.05f;

    [Header("Fog Alpha")]
    [SerializeField, Range(0, 255)] byte unseenAlpha = 255;
    [SerializeField, Range(0, 255)] byte visitedAlpha = 140;
    [SerializeField, Range(0, 255)] byte visibleAlpha = 0; // "현재 시야" 느낌 주고 싶으면 유지

    [Header("MapFog")]
    [SerializeField] SpriteRenderer worldFogRenderer; // WorldFogOverlay의 SpriteRenderer
    [SerializeField] Material worldFogMat;            // 인스턴스 머티리얼(중요)

    Texture2D fogTex;
    Color32[] pixels;
    byte[] visited;
    byte[] visible;

    float nextTime;

    Vector2 worldMin;
    Vector2 worldMax;

    void Start()
    {
        if (player == null || minimapCam == null || fogImage == null || boundsTilemap == null || minimapRect == null)
        {
            Debug.LogError("[MiniMapFog_WorldAccum] Missing references.");
            enabled = false;
            return;
        }

        CalculateWorldBoundsFromTilemap(boundsTilemap, out worldMin, out worldMax);

        fogTex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        fogTex.wrapMode = TextureWrapMode.Clamp;
        fogTex.filterMode = FilterMode.Bilinear;

        pixels = new Color32[texSize * texSize];
        visited = new byte[texSize * texSize];
        visible = new byte[texSize * texSize];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, unseenAlpha);

        fogTex.SetPixels32(pixels);
        fogTex.Apply(false);

        fogImage.texture = fogTex;

        // 시작 위치 뚫기
        RevealWorld(player.position);

        // 카메라 뷰에 맞게 FogOverlay UV 잘라서 표시
        UpdateFogUVByCameraView();
        UpdatePlayerIcon(player.position);

        if (worldFogRenderer != null)
        {
            // 머티리얼은 반드시 인스턴스(복제)여야 함 (공용 머티리얼 오염 방지)
            var mat = worldFogRenderer.material;
            mat.SetTexture("_FogTex", fogTex);
            mat.SetVector("_WorldMin", new Vector4(worldMin.x, worldMin.y, 0, 0));
            mat.SetVector("_WorldMax", new Vector4(worldMax.x, worldMax.y, 0, 0));
            mat.SetFloat("_Darkness", 1f);
        }
    }

    void Update()
    {
        if (player == null) return;

        // 미니맵 카메라가 따라오니까 매 프레임 "현재 뷰"에 맞게 Fog UV를 잘라서 보여줌
        UpdateFogUVByCameraView();

        // 플레이어 아이콘은 미니맵 화면 중앙 고정(원하면 회전만)
        UpdatePlayerIcon(player.position);

        if (Time.time < nextTime) return;
        nextTime = Time.time + updateInterval;

        RevealWorld(player.position);
    }

    // ------------------ 핵심 1: 누적 Reveal은 맵 전체 좌표 기준 ------------------
    void RevealWorld(Vector2 worldPos)
    {
        if (!WorldToUV_WholeMap(worldPos, out float u, out float v))
            return;

        int cx = Mathf.RoundToInt(u * (texSize - 1));
        int cy = Mathf.RoundToInt(v * (texSize - 1));

        float mapW = Mathf.Max(0.0001f, worldMax.x - worldMin.x);
        float mapH = Mathf.Max(0.0001f, worldMax.y - worldMin.y);

        int rx = Mathf.CeilToInt((revealRadiusWorld / mapW) * texSize);
        int ry = Mathf.CeilToInt((revealRadiusWorld / mapH) * texSize);
        if (rx < 1) rx = 1;
        if (ry < 1) ry = 1;

        int xMin = Mathf.Max(0, cx - rx);
        int xMax = Mathf.Min(texSize - 1, cx + rx);
        int yMin = Mathf.Max(0, cy - ry);
        int yMax = Mathf.Min(texSize - 1, cy + ry);

        // visible 리셋(맵 전체 기준 visible 효과 필요 없으면 이 블록 통째로 제거 가능)
        for (int i = 0; i < visible.Length; i++)
            visible[i] = 0;

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                float dx = (x - cx) / (float)rx;
                float dy = (y - cy) / (float)ry;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) continue;

                int idx = y * texSize + x;
                visible[idx] = 1;
                visited[idx] = 1;
            }
        }

        bool changed = false;
        for (int i = 0; i < pixels.Length; i++)
        {
            byte a = unseenAlpha;
            if (visited[i] == 1) a = visitedAlpha;
            if (visible[i] == 1) a = visibleAlpha;

            if (pixels[i].a != a)
            {
                pixels[i].a = a;
                changed = true;
            }
        }

        if (changed)
        {
            fogTex.SetPixels32(pixels);
            fogTex.Apply(false);
        }
    }

    bool WorldToUV_WholeMap(Vector2 worldPos, out float u, out float v)
    {
        float w = worldMax.x - worldMin.x;
        float h = worldMax.y - worldMin.y;
        if (w <= 0.0001f || h <= 0.0001f)
        {
            u = v = 0f;
            return false;
        }

        u = (worldPos.x - worldMin.x) / w;
        v = (worldPos.y - worldMin.y) / h;

        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return false;

        return true;
    }

    // ------------------ 핵심 2: FogOverlay는 카메라 뷰 영역만 "잘라서" 표시 ------------------
    void UpdateFogUVByCameraView()
    {
        // 카메라 뷰를 "맵 전체 UV(0..1)"로 변환해서 RawImage.uvRect로 잘라 보여줌
        float h = minimapCam.orthographicSize;
        float w = h * minimapCam.aspect;

        Vector2 c = minimapCam.transform.position;
        Vector2 viewMin = c - new Vector2(w, h);
        Vector2 viewMax = c + new Vector2(w, h);

        // viewMin/viewMax를 map UV로
        float uMin = Mathf.InverseLerp(worldMin.x, worldMax.x, viewMin.x);
        float vMin = Mathf.InverseLerp(worldMin.y, worldMax.y, viewMin.y);
        float uMax = Mathf.InverseLerp(worldMin.x, worldMax.x, viewMax.x);
        float vMax = Mathf.InverseLerp(worldMin.y, worldMax.y, viewMax.y);

        // clamp
        uMin = Mathf.Clamp01(uMin);
        vMin = Mathf.Clamp01(vMin);
        uMax = Mathf.Clamp01(uMax);
        vMax = Mathf.Clamp01(vMax);

        float uW = Mathf.Max(0.0001f, uMax - uMin);
        float vH = Mathf.Max(0.0001f, vMax - vMin);

        // RawImage.uvRect는 (x,y,w,h) in UV
        fogImage.uvRect = new Rect(uMin, vMin, uW, vH);
    }

    // ------------------ 플레이어 아이콘 ------------------
    void UpdatePlayerIcon(Vector2 worldPos)
    {
        if (playerIcon == null) return;

        // 미니맵 카메라가 플레이어를 따라오면, 아이콘을 중앙에 고정하는 게 일반적
        playerIcon.anchoredPosition = Vector2.zero;

        // "방향"을 표시하고 싶으면 player의 facing으로 회전만 줘라.
        // playerIcon.localRotation = ...
    }

    static void CalculateWorldBoundsFromTilemap(Tilemap tm, out Vector2 min, out Vector2 max)
    {
        var cb = tm.cellBounds;
        Vector3Int cmin = cb.min;
        Vector3Int cmax = cb.max;

        Vector3 wmin = tm.CellToWorld(cmin);
        Vector3 wmax = tm.CellToWorld(cmax);

        wmax += tm.cellSize;

        float minX = Mathf.Min(wmin.x, wmax.x);
        float minY = Mathf.Min(wmin.y, wmax.y);
        float maxX = Mathf.Max(wmin.x, wmax.x);
        float maxY = Mathf.Max(wmin.y, wmax.y);

        min = new Vector2(minX, minY);
        max = new Vector2(maxX, maxY);
    }
}