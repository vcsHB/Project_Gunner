using UnityEngine;

/// <summary>
/// Resources 안에서 전역 SO 하나를 찾아온다.
///
/// Resources.Load는 Resources 루트 기준 경로를 요구해서, 에셋을 하위 폴더로 옮기면 조용히 null이 된다.
/// 이름으로 먼저 찾고, 없으면 타입으로 전체를 훑어서 폴더 구조에 묶이지 않게 한다.
/// </summary>
public static class ResourceLocator
{
    public static T LoadSingle<T>(string resourceName) where T : Object
    {
        T direct = Resources.Load<T>(resourceName);
        if (direct != null) return direct;

        // 하위 폴더에 있을 수 있다. 타입이 맞는 것만 걸러지므로 비용은 크지 않다.
        T[] all = Resources.LoadAll<T>(string.Empty);
        if (all.Length == 0) return null;

        if (all.Length > 1)
            Debug.LogWarning($"[ResourceLocator] Resources에 {typeof(T).Name}이 {all.Length}개 있습니다. 첫 번째를 씁니다.");

        return all[0];
    }
}
