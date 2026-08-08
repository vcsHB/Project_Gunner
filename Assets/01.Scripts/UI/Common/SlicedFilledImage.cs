using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 9슬라이스와 fillAmount를 동시에 지원하는 Image.
///
/// 유니티 기본 Image는 Sliced와 Filled가 배타적이라, 둥근 끝을 가진 게이지를 만들면
/// 모서리가 늘어나거나(Filled) 채우기가 안 되거나(Sliced) 둘 중 하나가 된다.
///
/// fillAmount / fillCenter는 Image의 것을 그대로 쓰므로 기존 Image 참조에 그대로 꽂힌다.
/// 채우기 방향만 별도 필드로 받는다. (방사형은 슬라이스와 의미가 맞지 않아 지원하지 않는다)
/// </summary>
[AddComponentMenu("UI/Sliced Filled Image")]
public class SlicedFilledImage : Image
{
    public enum FillDirection
    {
        Right,
        Left,
        Up,
        Down,
    }

    [SerializeField] private FillDirection _fillDirection = FillDirection.Right;

    public FillDirection Direction
    {
        get => _fillDirection;
        set
        {
            if (_fillDirection == value) return;

            _fillDirection = value;
            SetVerticesDirty();
        }
    }

    public bool IsHorizontal => _fillDirection is FillDirection.Right or FillDirection.Left;

    // 3x3 격자의 경계값. 매 프레임 할당하지 않도록 재사용한다.
    private readonly float[] _xs = new float[4];
    private readonly float[] _ys = new float[4];
    private readonly float[] _us = new float[4];
    private readonly float[] _vs = new float[4];

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        Sprite sprite = overrideSprite;
        if (sprite == null)
        {
            base.OnPopulateMesh(vh);
            return;
        }

        vh.Clear();

        if (fillAmount <= 0f) return;

        BuildGrid(sprite);
        FillGrid(vh, sprite);
    }

    private void BuildGrid(Sprite sprite)
    {
        Rect rect = GetPixelAdjustedRect();

        float multiplier = pixelsPerUnit * pixelsPerUnitMultiplier;
        Vector4 border = GetAdjustedBorders(sprite.border / multiplier, rect);
        Vector4 padding = UnityEngine.Sprites.DataUtility.GetPadding(sprite) / multiplier;

        _xs[0] = rect.x + padding.x;
        _xs[1] = rect.x + border.x;
        _xs[2] = rect.x + rect.width - border.z;
        _xs[3] = rect.x + rect.width - padding.z;

        _ys[0] = rect.y + padding.y;
        _ys[1] = rect.y + border.y;
        _ys[2] = rect.y + rect.height - border.w;
        _ys[3] = rect.y + rect.height - padding.w;

        Vector4 outer = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
        Vector4 inner = UnityEngine.Sprites.DataUtility.GetInnerUV(sprite);

        _us[0] = outer.x;
        _us[1] = inner.x;
        _us[2] = inner.z;
        _us[3] = outer.z;

        _vs[0] = outer.y;
        _vs[1] = inner.y;
        _vs[2] = inner.w;
        _vs[3] = outer.w;
    }

    private void FillGrid(VertexHelper vh, Sprite sprite)
    {
        GetFillRange(out float fillMin, out float fillMax);

        // 테두리가 없으면 가운데 한 칸이 전부라 fillCenter를 무시해야 한다.
        bool hasBorder = sprite.border.sqrMagnitude > 0f;
        bool drawCenter = fillCenter || !hasBorder;

        Color32 vertexColor = color;
        bool horizontal = IsHorizontal;

        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                if (!drawCenter && x == 1 && y == 1) continue;

                float x0 = _xs[x];
                float x1 = _xs[x + 1];
                float y0 = _ys[y];
                float y1 = _ys[y + 1];

                float u0 = _us[x];
                float u1 = _us[x + 1];
                float v0 = _vs[y];
                float v1 = _vs[y + 1];

                bool visible = horizontal
                    ? Clip(ref x0, ref x1, ref u0, ref u1, fillMin, fillMax)
                    : Clip(ref y0, ref y1, ref v0, ref v1, fillMin, fillMax);

                if (!visible) continue;

                AddQuad(vh, x0, y0, x1, y1, u0, v0, u1, v1, vertexColor);
            }
        }
    }

    /// <summary>채우기가 살아남는 구간을 로컬 좌표로 구한다.</summary>
    private void GetFillRange(out float min, out float max)
    {
        float amount = Mathf.Clamp01(fillAmount);

        if (IsHorizontal)
        {
            float filled = (_xs[3] - _xs[0]) * amount;

            if (_fillDirection == FillDirection.Right)
            {
                min = _xs[0];
                max = _xs[0] + filled;
            }
            else
            {
                min = _xs[3] - filled;
                max = _xs[3];
            }

            return;
        }

        float filledY = (_ys[3] - _ys[0]) * amount;

        if (_fillDirection == FillDirection.Up)
        {
            min = _ys[0];
            max = _ys[0] + filledY;
        }
        else
        {
            min = _ys[3] - filledY;
            max = _ys[3];
        }
    }

    /// <summary>
    /// 한 칸을 채우기 구간으로 잘라내고 UV를 그만큼 보간한다.
    /// 완전히 잘려나가면 false.
    /// </summary>
    private static bool Clip(ref float p0, ref float p1, ref float t0, ref float t1, float min, float max)
    {
        float size = p1 - p0;
        if (size <= 0f) return false; // 테두리가 0인 칸

        float clipped0 = Mathf.Max(p0, min);
        float clipped1 = Mathf.Min(p1, max);
        if (clipped1 <= clipped0) return false;

        // 보간 비율은 원래 UV로 계산해야 한다. t0을 먼저 덮으면 t1이 어긋난다.
        float originT0 = t0;
        float originT1 = t1;

        t0 = Mathf.Lerp(originT0, originT1, (clipped0 - p0) / size);
        t1 = Mathf.Lerp(originT0, originT1, (clipped1 - p0) / size);

        p0 = clipped0;
        p1 = clipped1;
        return true;
    }

    private static void AddQuad(VertexHelper vh,
        float x0, float y0, float x1, float y1,
        float u0, float v0, float u1, float v1,
        Color32 color)
    {
        int start = vh.currentVertCount;

        vh.AddVert(new Vector3(x0, y0), color, new Vector2(u0, v0));
        vh.AddVert(new Vector3(x0, y1), color, new Vector2(u0, v1));
        vh.AddVert(new Vector3(x1, y1), color, new Vector2(u1, v1));
        vh.AddVert(new Vector3(x1, y0), color, new Vector2(u1, v0));

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }

    /// <summary>
    /// Image 내부 구현과 같은 규칙. 사각형이 테두리 합보다 작아지면 테두리를 비례해서 줄인다.
    /// 안 하면 좌우 테두리가 서로를 뚫고 지나간다.
    /// </summary>
    private Vector4 GetAdjustedBorders(Vector4 border, Rect adjustedRect)
    {
        Rect originalRect = rectTransform.rect;

        for (int axis = 0; axis <= 1; axis++)
        {
            if (originalRect.size[axis] != 0f)
            {
                float ratio = adjustedRect.size[axis] / originalRect.size[axis];
                border[axis] *= ratio;
                border[axis + 2] *= ratio;
            }

            float combined = border[axis] + border[axis + 2];
            if (adjustedRect.size[axis] < combined && combined != 0f)
            {
                float ratio = adjustedRect.size[axis] / combined;
                border[axis] *= ratio;
                border[axis + 2] *= ratio;
            }
        }

        return border;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        // 메시는 직접 만들지만, 레이아웃 계산 등 Image 내부가 type을 보는 곳이 있다.
        type = Type.Sliced;
    }
#endif
}
