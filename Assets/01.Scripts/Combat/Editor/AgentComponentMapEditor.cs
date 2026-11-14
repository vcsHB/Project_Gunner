using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Agent 인스펙터 맨 위에 <b>컴포넌트 지도</b>를 그린다.
///
/// 컴포넌트가 열다섯 개를 넘어가면 "무엇이 붙어 있나"를 스크롤로 파악하게 된다.
/// 역할별로 자식에 나눠두면 스크롤은 짧아지지만 이번엔 "어디 갔나"가 문제가 된다.
/// 지도는 그 둘을 같이 푼다 — <b>무엇이, 어디에</b> 있는지 한 화면에 보여준다.
///
/// <see cref="RequireAgentComponentAttribute"/>로 선언된 의존도 여기서 검사한다.
/// 계층 전체를 보므로 컴포넌트를 자식으로 옮겨도 그대로 맞는다.
/// </summary>
[CustomEditor(typeof(Agent), true)]
public class AgentComponentMapEditor : Editor
{
    private static bool s_expanded = true;

    private readonly List<IAgentComponent> _components = new();
    private readonly List<string> _problems = new();

    public override void OnInspectorGUI()
    {
        DrawMap();

        EditorGUILayout.Space();
        base.OnInspectorGUI();
    }

    private void DrawMap()
    {
        Agent agent = (Agent)target;

        Collect(agent);

        s_expanded = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_expanded, $"Agent 컴포넌트 ({_components.Count})");

        if (s_expanded)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawProblems();
                DrawGroups(agent);
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void Collect(Agent agent)
    {
        _components.Clear();
        _problems.Clear();

        // 비활성도 포함한다. Agent 자신이 그렇게 모으므로 지도도 같은 것을 봐야 한다.
        agent.GetComponentsInChildren(true, _components);

        CheckDuplicates();
        CheckRequirements();
    }

    /// <summary>
    /// Agent는 타입당 하나만 등록한다(TryAdd). 둘이 붙어 있으면 뒤엣것이 조용히 버려진다.
    /// 실행해 보기 전에는 드러나지 않는 종류의 사고라 여기서 잡는다.
    /// </summary>
    private void CheckDuplicates()
    {
        HashSet<Type> seen = new();

        for (int i = 0; i < _components.Count; i++)
        {
            Type type = _components[i].GetType();

            if (!seen.Add(type))
                _problems.Add($"{type.Name}가 두 개 이상입니다. Agent는 첫 번째만 등록합니다.");
        }
    }

    private void CheckRequirements()
    {
        HashSet<Type> present = new(_components.Select(c => c.GetType()));

        for (int i = 0; i < _components.Count; i++)
        {
            Type type = _components[i].GetType();

            object[] attributes = type.GetCustomAttributes(typeof(RequireAgentComponentAttribute), true);

            for (int j = 0; j < attributes.Length; j++)
            {
                RequireAgentComponentAttribute require = (RequireAgentComponentAttribute)attributes[j];

                // 파생 타입으로 채워졌을 수도 있다. 이름이 정확히 같은 것만 보면 놓친다.
                bool satisfied = present.Any(p => require.ComponentType.IsAssignableFrom(p));
                if (satisfied) continue;

                string mark = require.Optional ? "선택" : "필수";
                _problems.Add($"{type.Name} → {require.ComponentType.Name}가 없습니다. ({mark})");
            }
        }
    }

    private void DrawProblems()
    {
        if (_problems.Count == 0) return;

        EditorGUILayout.HelpBox(string.Join("\n", _problems), MessageType.Warning);
        EditorGUILayout.Space(2f);
    }

    /// <summary>오브젝트별로 묶어서 그린다. "어디에 있나"가 지도의 본체다.</summary>
    private void DrawGroups(Agent agent)
    {
        IEnumerable<IGrouping<GameObject, IAgentComponent>> groups = _components
            .GroupBy(c => ((MonoBehaviour)c).gameObject);

        foreach (IGrouping<GameObject, IAgentComponent> group in groups)
        {
            GameObject host = group.Key;
            string label = host == agent.gameObject ? "루트" : GetPath(host.transform, agent.transform);

            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (IAgentComponent component in group)
                    DrawRow(component);
            }
        }
    }

    private void DrawRow(IAgentComponent component)
    {
        MonoBehaviour behaviour = (MonoBehaviour)component;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(behaviour.GetType().Name);

            // 자식에 있는 것은 클릭으로 바로 가야 한다. 안 그러면 숨긴 만큼 찾기 어려워진다.
            if (GUILayout.Button("선택", EditorStyles.miniButton, GUILayout.Width(44f)))
            {
                Selection.activeObject = behaviour.gameObject;
                EditorGUIUtility.PingObject(behaviour);
            }
        }
    }

    private static string GetPath(Transform target, Transform root)
    {
        string path = target.name;

        for (Transform parent = target.parent; parent != null && parent != root; parent = parent.parent)
            path = $"{parent.name}/{path}";

        return path;
    }
}
