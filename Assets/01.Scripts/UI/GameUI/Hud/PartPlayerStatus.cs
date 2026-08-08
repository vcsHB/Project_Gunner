using UnityEngine;

public class PartPlayerStatus : MonoBehaviour
{
    [Header("Default Status Displays")]
    [SerializeField] private SimpleGauge _healthGauge;
    [SerializeField] private SimpleGauge _staminaGauge;
    [SerializeField] private SimpleGauge _hungerGauge;
    [SerializeField] private SimpleGauge _thirstyGauge;
    [Header("Additional")]
    [SerializeField] private ConditionDisplayGroup _conditionDisplayGroup;
    



}