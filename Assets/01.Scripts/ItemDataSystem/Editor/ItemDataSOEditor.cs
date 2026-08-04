using UnityEditor;
using UnityEngine;

/// <summary>파생 클래스(PlayerWeaponDataSO 등)에도 함께 적용된다.</summary>
[CustomEditor(typeof(ItemDataSO), true)]
public class ItemDataSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ItemDataSO item = (ItemDataSO)target;

        if (item.IsRegistered)
            EditorGUILayout.LabelField("Id", item.Id.ToString(), EditorStyles.boldLabel);
        else
            EditorGUILayout.HelpBox("아직 Id가 없습니다. ItemDatabase에서 스캔을 실행하세요.", MessageType.Warning);

        EditorGUILayout.Space();

        // _id는 의도적으로 숨긴다. 손으로 고치면 세이브 호환이 깨진다.
        DrawPropertiesExcluding(serializedObject, "m_Script", "_id");
        serializedObject.ApplyModifiedProperties();
    }
}
