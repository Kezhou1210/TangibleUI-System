using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class AffordanceMatching : MonoBehaviour
{
    [Header("Algorithm Weight")]
    [Tooltip("Funtionality Weight")]
    public float functionalityWeight = 0.4f;

    [Tooltip("Ergonomic Weight")]
    public float ergonomicWeight = 0.15f;

    [Tooltip("Intuitive Weight")]
    public float intuitiveWeight = 0.15f;

    [Tooltip("Compact Weight")]
    public float compactWeight = 0.3f;


    [Tooltip("Feedback")]
    [SerializeField]


    private TMPro.TextMeshPro responseText;

    private EnvironmentAnalysis affordanceData;
    private UiComponentList uiComponentData;

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Matching();
        }
    }
    public void Matching()
    {
        responseText.text = "matching...";
        if (!LoadDataFiles())
        {
            Debug.LogError("���ݼ���ʧ�ܣ��޷�����ƥ�䡣");
            return;
        }
        List<MatchResult> bestMatches = FindBestOverallMatches();
        PrintResults(bestMatches);
    }

    private string GetLastestAffordanceFile(string directory)
    {
        var dirInfo = new DirectoryInfo(directory);
        if (!dirInfo.Exists)
        {
            return null;
        }
        return dirInfo.GetFiles("Affordance_Analysis_*.json").OrderByDescending(f => f.LastWriteTime).FirstOrDefault()?.FullName;
    }

    private bool LoadDataFiles()
    {
        string uiComponentsDataPath = Path.Combine(Application.persistentDataPath, "ui_components.json");
        if (File.Exists(uiComponentsDataPath))
        {
            string uiJson = File.ReadAllText(uiComponentsDataPath);
            uiComponentData = JsonUtility.FromJson<UiComponentList>(uiJson);
            responseText.text = "uicomponentData loaded successfully";
            Debug.Log("uicomponentData loaded successfully");

            if (uiComponentData.ui_components.Count == 0)
            {
                Debug.LogError("����UI����б�Ϊ�գ����� ui_components.json �ļ������Ƿ���ȷ�����Ƿ���һ�����б�[]��");
                return false;
            }
        }
        else
        {
            responseText.text = "cannnot find uicomponentdata";
            Debug.Log("cannnot find uicomponentdata");
            return false;
        }

        string affordanceFilePath = GetLastestAffordanceFile(Application.persistentDataPath);
        if (!string.IsNullOrEmpty(affordanceFilePath))
        {
            string affordanceJson = File.ReadAllText(affordanceFilePath);
            affordanceData = JsonUtility.FromJson<EnvironmentAnalysis>(affordanceJson);
            responseText.text = "affordanceData loaded successfully";
            Debug.Log("affordanceData loaded successfully");
        }
        else
        {
            responseText.text = "cannnot find affordanceData";
            Debug.Log("cannnot find affordanceData");
            return false;
        }

        return true;
    }

    private class MatchScore
    {
        public UiComponent uiComponent;
        public DetectedObject targetObject;
        public Affordance targetAffordance;
        public PotentialProfile targetPotentialProfile;
        public float Score;
    }

    private List<MatchResult> FindBestOverallMatches()
    {
        var allPossibleScores = new List<MatchScore>();

        var primaryUiComponents = uiComponentData.ui_components
        .Where(ui => ui.priority == "primary")
        .ToList();

        foreach (var uiComponent in primaryUiComponents)
        {
            foreach (var detectedObject in affordanceData.detected_objects)
            {
                foreach (var affordance in detectedObject.affordances)
                {
                    var potentialProfile = affordance.potential_profiles[0];
                    float score = CalculateMatchScore(uiComponent.required_profile, potentialProfile.profile);
                    if (score > 0)
                    {
                        allPossibleScores.Add(new MatchScore
                        {
                            uiComponent = uiComponent,
                            targetObject = detectedObject,
                            targetAffordance = affordance,
                            targetPotentialProfile = potentialProfile,
                            Score = score
                        });
                    }
                }
            }
        }

        Debug.Log($"�������� {allPossibleScores.Count} �����ܵ���ԡ�");

        var finalMatches = new List<MatchResult>();
        var usedUiComponentIds = new HashSet<string>();
        var usedPhysicalProfileIds = new HashSet<string>();

        allPossibleScores = allPossibleScores.OrderByDescending(s => s.Score).ToList();

        foreach (var potentialMatch in allPossibleScores)
        {
            string physicalProfileId = $"{potentialMatch.targetObject.object_id}-{potentialMatch.targetAffordance.affordance_type}-{potentialMatch.targetPotentialProfile.profile_id}";

            if (usedUiComponentIds.Contains(potentialMatch.uiComponent.component_id) ||
                usedPhysicalProfileIds.Contains(physicalProfileId))
            {
                continue;
            }

            finalMatches.Add(new MatchResult
            {
                uiComponent = potentialMatch.uiComponent,
                targetObject = potentialMatch.targetObject,
                targetAffordance = potentialMatch.targetAffordance,
                targetPotentialProfile = potentialMatch.targetPotentialProfile,
                Score = potentialMatch.Score
            });

            usedUiComponentIds.Add(potentialMatch.uiComponent.component_id);
            usedPhysicalProfileIds.Add(physicalProfileId);
        }

        return finalMatches;
    }

    private float CalculateMatchScore(FunctionProfile uiComponentProfile, FunctionProfile affordanceProfile)
    {
        float score = 0.0f;
        if (uiComponentProfile.semantic_verb == affordanceProfile.semantic_verb) score += 5.0f;
        if (uiComponentProfile.interaction_flow == affordanceProfile.interaction_flow) score += 3.0f;
        if (uiComponentProfile.dimensionality == affordanceProfile.dimensionality) score += 2.0f;
        if (uiComponentProfile.value_type == affordanceProfile.value_type) score += 1.0f;
        if (uiComponentProfile.directionality == affordanceProfile.directionality) score += 1.0f;
        return score;
    }

    private void PrintResults(List<MatchResult> results)
    {
        Debug.Log("===== �㷨ƥ���� =====");
        if (results.Count == 0)
        {
            Debug.LogWarning("û���ҵ��κ���Ч��ƥ���");
            return;
        }
        var sortedResults = results.OrderByDescending(r => r.Score);
        foreach (var result in sortedResults)
        {
            string log = $"UI��� [{result.uiComponent.component_name}] (���ȼ�: {result.uiComponent.priority.ToUpper()}) " +
                         $"=> ƥ�䵽: ���� [{result.targetObject.object_name}] �� " +
                         $"[{result.targetAffordance.affordance_type}] �ɹ���, " +
                         $"�÷�: {result.Score}";
            Debug.Log(log);
        }
        Debug.Log("==========================");
    }
}
