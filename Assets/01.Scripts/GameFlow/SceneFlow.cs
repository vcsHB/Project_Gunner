using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 주둔지 ↔ 필드. <b>씬을 바꾸는 곳은 여기 하나</b>다.
///
/// | 언제 | 부르는 것 | 탄 기체 |
/// |---|---|---|
/// | 출격구에 들어간다 | <see cref="Sortie"/> | 들고 간다 |
/// | 탈출에 성공한다 | <see cref="ReturnToGarrison"/>(true) | 들고 온다 — 손상·적재함 그대로 |
/// | 기체가 부서진다 | <see cref="ReturnToGarrison"/>(false) | 잃는다 |
///
/// 들고 가는 방법 — 떠나는 씬에서 기체를 기록(<see cref="MechaRecord"/>)으로 적고(<see cref="PlayerPilot.LeaveScene"/>),
/// 도착한 씬의 기체에 다시 입힌다(<see cref="PlayerPilot"/>가 씬이 열릴 때). 기체 오브젝트는 씬과 함께 사라진다.
/// 판 밖 상태(창고·격납고)는 조종권이 씬 밖(DontDestroyOnLoad)에서 들고 있다.
///
/// 두 씬은 Build Settings에 있어야 한다.
/// </summary>
public static class SceneFlow
{
    public const string GarrisonScene = "GarrisonScene";
    public const string FieldScene = "IngameScene";

    /// <summary>씬을 여는 중. 두 번 눌러 두 번 넘어가지 않게.</summary>
    public static bool IsLoading { get; private set; }

    public static bool IsInGarrison => SceneManager.GetActiveScene().name == GarrisonScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        IsLoading = false;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    /// <summary>출격. 탄 기체를 그대로 들고 필드로.</summary>
    public static void Sortie() => Go(FieldScene, carryMecha: true);

    /// <summary>주둔지로. <paramref name="withMecha"/>가 false면 기체를 잃은 채로 돌아온다(조립기에서 새로 만든다).</summary>
    public static void ReturnToGarrison(bool withMecha) => Go(GarrisonScene, withMecha);

    private static void Go(string scene, bool carryMecha)
    {
        if (IsLoading) return;

        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"[SceneFlow] {scene}이 Build Settings에 없습니다.");
            return;
        }

        LeaveAll(carryMecha);

        IsLoading = true;
        SceneManager.LoadScene(scene);
    }

    private static void LeaveAll(bool carryMecha)
    {
        for (int i = 0; i < PlayerPilot.All.Count; i++)
            PlayerPilot.All[i].LeaveScene(carryMecha);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => IsLoading = false;
}
