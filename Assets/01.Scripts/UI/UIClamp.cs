using UnityEngine;

/// <summary>
/// 커서를 따라 뜨는 UI(툴팁·메뉴)를 부모 영역 밖으로 나가지 않게 민다.
/// 오른쪽 아래 칸에서 띄웠을 때 반쯤 잘려 보이는 게 가장 흔한 사고다.
/// </summary>
public static class UIClamp
{
    /// <param name="position">부모 좌표계에서 원하는 위치.</param>
    /// <param name="parent">부모의 rect. 대상의 앵커가 부모 중앙이라고 본다.</param>
    public static Vector2 InsideParent(Vector2 position, Rect parent, RectTransform target)
    {
        Rect self = target.rect;

        float minX = parent.xMin - self.xMin;
        float maxX = parent.xMax - self.xMax;
        float minY = parent.yMin - self.yMin;
        float maxY = parent.yMax - self.yMax;

        return new Vector2(
            Mathf.Clamp(position.x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX)),
            Mathf.Clamp(position.y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY)));
    }
}
