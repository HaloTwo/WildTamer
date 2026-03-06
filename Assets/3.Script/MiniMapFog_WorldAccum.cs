using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class MiniMapFog_WorldAccum : Singleton<MiniMapFog_WorldAccum>
{
    [Header("Refs")]
    [SerializeField] Transform player;
    [SerializeField] Camera minimapCam;          // 미니맵 카메라(플레이어 따라오는 카메라)
    [SerializeField] RawImage fogImage;          // FogOverlay RawImage
    [SerializeField] RectTransform minimapRect;  // 미니맵 Rect
    [SerializeField] RectTransform playerIcon;   // 플레이어 아이콘 Rect
    [SerializeField] Tilemap boundsTilemap;      // 맵 전체 bounds용 타일맵

    [Header("Fog Texture (World Accum)")]
    [SerializeField] int texSize = 1024;
    [SerializeField] float revealRadiusWorld = 5.0f;
    [SerializeField] float updateInterval = 0.1f;

    [Header("Fog Alpha")]
    [SerializeField, Range(0, 255)] byte unseenAlpha = 255;
    [SerializeField, Range(0, 255)] byte visitedAlpha = 140;
    [SerializeField, Range(0, 255)] byte visibleAlpha = 0; // "현재 시야" 느낌 주고 싶으면 유지

    [Header("MapFog")]
    [SerializeField] SpriteRenderer worldFogRenderer; // WorldFogOverlay의 SpriteRenderer
    [SerializeField] Material worldFogMat;            // 인스턴스 머티리얼(중요)

    [SerializeField] float revealMoveThreshold = 0.25f; // 플레이어가 이 이상 움직여야 다시 Reveal 시도 (제곱값으로 비교해서 연산 최적화)
    [SerializeField] float saveInterval = 3f; // Fog 상태 저장 간격 (초)

    bool saveDirty;

    bool hasPrevReveal;
    int prevCx, prevCy, prevRx, prevRy;

    Texture2D fogTex;
    Color32[] pixels;
    byte[] visited;

    float nextTime;
    float nextSaveTime;

    Vector2 worldMin;
    Vector2 worldMax;

    Vector2 lastRevealPos;

    protected override void Awake()
    {
        base.Awake();
    }

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

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, unseenAlpha);

        fogTex.SetPixels32(pixels);
        fogTex.Apply(false);

        fogImage.texture = fogTex;

        //불러오기
        LoadFog();

        // 시작 위치 뚫기
        RevealWorld(player.position);

        // 카메라 뷰에 맞게 FogOverlay UV 잘라서 표시
        UpdateFogUVByCameraView();

        lastRevealPos = player.position;
        nextTime = Time.time;
        nextSaveTime = Time.time + saveInterval;

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

        UpdateFogUVByCameraView();

        Vector2 pos = player.position;

        // 일정 거리 이상 이동했을 때만, 일정 주기로만 Reveal
        if (Time.time >= nextTime && (pos - lastRevealPos).sqrMagnitude >= revealMoveThreshold * revealMoveThreshold)
        {
            nextTime = Time.time + updateInterval;
            lastRevealPos = pos;
            RevealWorld(pos);
        }

        // 저장은 dirty일 때만
        if (saveDirty && Time.time >= nextSaveTime)
        {
            nextSaveTime = Time.time + saveInterval;
            SaveFog();
            saveDirty = false;
        }
    }

    // 갱신
    void RevealWorld(Vector2 worldPos)
    {
        if (!WorldToUV_WholeMap(worldPos, out float u, out float v)) return;

        int cx = Mathf.RoundToInt(u * (texSize - 1));
        int cy = Mathf.RoundToInt(v * (texSize - 1));

        float mapW = Mathf.Max(0.0001f, worldMax.x - worldMin.x);
        float mapH = Mathf.Max(0.0001f, worldMax.y - worldMin.y);

        int rx = Mathf.CeilToInt((revealRadiusWorld / mapW) * texSize);
        int ry = Mathf.CeilToInt((revealRadiusWorld / mapH) * texSize);
        if (rx < 1) rx = 1;
        if (ry < 1) ry = 1;

        // 현재 reveal 사각형
        int curXMin = Mathf.Max(0, cx - rx);
        int curXMax = Mathf.Min(texSize - 1, cx + rx);
        int curYMin = Mathf.Max(0, cy - ry);
        int curYMax = Mathf.Min(texSize - 1, cy + ry);

        // 이전 reveal과 현재 reveal의 합집합만 갱신
        int xMin = curXMin;
        int xMax = curXMax;
        int yMin = curYMin;
        int yMax = curYMax;

        // 이전 reveal이 있으면 합집합 영역 계산
        if (hasPrevReveal)
        {
            xMin = Mathf.Min(xMin, Mathf.Max(0, prevCx - prevRx));
            xMax = Mathf.Max(xMax, Mathf.Min(texSize - 1, prevCx + prevRx));
            yMin = Mathf.Min(yMin, Mathf.Max(0, prevCy - prevRy));
            yMax = Mathf.Max(yMax, Mathf.Min(texSize - 1, prevCy + prevRy));
        }

        bool changed = false;
        bool visitedChanged = false;

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                int idx = y * texSize + x;

                // 현재 reveal 원 안에 있는지
                float dx = (x - cx) / (float)rx;
                float dy = (y - cy) / (float)ry;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                bool inCurrentReveal = d <= 1f;

                if (inCurrentReveal && visited[idx] == 0)
                {
                    visited[idx] = 1;
                    visitedChanged = true;
                }

                byte a = unseenAlpha;
                if (visited[idx] == 1) a = visitedAlpha;
                if (inCurrentReveal) a = visibleAlpha;

                if (pixels[idx].a != a)
                {
                    pixels[idx].a = a;
                    changed = true;
                }
            }
        }

        // 변경된 픽셀이 있으면 텍스처에 적용
        if (changed)
        {
            fogTex.SetPixels32(pixels);
            fogTex.Apply(false);
        }

        // visited 배열이 변경된 경우에는 저장이 필요하다고 표시
        if (visitedChanged) saveDirty = true;

        prevCx = cx;
        prevCy = cy;
        prevRx = rx;
        prevRy = ry;
        hasPrevReveal = true;
    }

    // 카메라 뷰는 맵 전체 좌표 기준으로 UV(0..1) 변환, 맵 전체에 대한 상대 위치 계산
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

    // 카메라 뷰를 맵 전체 UV(0..1)로 변환해서 RawImage.uvRect로 잘라 보여줌
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


    void CalculateWorldBoundsFromTilemap(Tilemap tm, out Vector2 min, out Vector2 max)
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


    /// <summary>
    /// Fog 상태 저장
    /// </summary>
    public void SaveFog()
    {
        if (visited == null || visited.Length == 0) return;

        SaveManager.Instance.SaveBytes(SaveName.MapFogData, visited);
    }


    /// <summary>
    /// Fog 상태 로드
    /// </summary>
    /// <returns></returns>
    public bool LoadFog()
    {
        if (visited == null || pixels == null || fogTex == null) return false;

        byte[] data = SaveManager.Instance.LoadBytes(SaveName.MapFogData);
        if (data == null || data.Length != visited.Length) return false; // 데이터가 없거나 크기가 맞지 않으면 로드 실패

        Buffer.BlockCopy(data, 0, visited, 0, data.Length);
        RebuildFogFromVisited();

        return true;
    }


    /// <summary>
    /// 다시 visited 배열을 기반으로 pixels의 알파값을 재구성해서 텍스처에 적용
    /// </summary>
    void RebuildFogFromVisited()
    {
        if (visited == null || pixels == null || fogTex == null) return;

        for (int i = 0; i < pixels.Length; i++)
        {
            byte a = unseenAlpha;
            if (visited[i] == 1) a = visitedAlpha;
            pixels[i].a = a;
        }

        fogTex.SetPixels32(pixels);
        fogTex.Apply(false);
    }
}