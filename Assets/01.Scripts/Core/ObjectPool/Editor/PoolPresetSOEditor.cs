using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PoolPresetSO))]
public class PoolPresetSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PoolPresetSO preset = (PoolPresetSO)target;
        IReadOnlyList<PoolType> types = preset.Types;

        StringBuilder problems = new();
        HashSet<PoolType> used = new();

        for (int i = 0; i < types.Count; i++)
        {
            if (types[i] == PoolType.None)
                problems.AppendLine($"- {i}번: None 입니다.");
            else if (!used.Add(types[i]))
                problems.AppendLine($"- {i}번: {types[i]}이 중복입니다.");
            else if (!Enum.IsDefined(typeof(PoolType), types[i]))
                problems.AppendLine($"- {i}번: PoolType에 없는 값입니다. Generate Enum을 다시 실행하세요.");
        }

        EditorGUILayout.Space();

        if (problems.Length > 0)
            EditorGUILayout.HelpBox(problems.ToString().TrimEnd(), MessageType.Error);
        else
            EditorGUILayout.HelpBox($"{used.Count}종류를 미리 생성합니다.", MessageType.Info);
    }
}
