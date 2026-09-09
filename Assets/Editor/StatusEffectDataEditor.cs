using Data;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StatusEffectData))]
public sealed class StatusEffectDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        Field("id"); Field("effectName"); Field("icon"); Field("description"); Field("displayColor"); Field("displayPriority");
        Field("durationType"); Field("cureType");
        var cure = (EffectCureType)serializedObject.FindProperty("cureType").enumValueIndex;
        if (cure == EffectCureType.TurnBased) Field("maxTurns");
        if (cure == EffectCureType.ChancePerTurn) Field("cureChancePerTurn");
        EditorGUILayout.HelpBox("남은 턴은 해당 캐릭터의 행동 기회 수입니다. 행동 불가도 1회로 셉니다. 확률 해제형에는 최대 턴 제한이 없습니다.", MessageType.Info);
        Field("restrictionType");
        if (serializedObject.FindProperty("restrictionType").enumValueIndex != 0) Field("restrictionChance");
        Field("cureOnDirectDamage");
        Field("atkMultiplier"); Field("defMultiplier"); Field("evaMultiplier"); Field("accMultiplier"); Field("healingReceivedMultiplier");
        Field("dotDamage"); Field("battleDotMaxHpRatio");
        if (serializedObject.FindProperty("durationType").enumValueIndex == (int)EffectDurationType.Persistent)
        {
            Field("explorationStepInterval"); Field("explorationDamage"); Field("explorationMaxHpRatio");
        }
        serializedObject.ApplyModifiedProperties();
    }
    void Field(string name) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
}
