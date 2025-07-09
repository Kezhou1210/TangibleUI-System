using System.Collections.Generic;
using UnityEngine;

//Affordance Detection datatype
[System.Serializable]
public class InlineData
{
    public string mime_type;
    public string data;
}


[System.Serializable]
public class Part
{
    public string text;
    public InlineData inline_data;
}

[System.Serializable]
public class Content
{
    public List<Part> parts;
    public string role;
}

[System.Serializable]
public class GeminiError
{
    public int code;
    public string message;
    public string status;
}

[System.Serializable]
public class Candidate
{
    public Content content;
    public string finishReason;
    public int index;
}

[System.Serializable]
public class TextPart 
{
    public string text;
}

[System.Serializable]
public class TextContent
{
    public List<TextPart> parts;
}

[System.Serializable]
public class GenerationConfig 
{
    public int maxOutputTokens;
    public float temperature;
    public float topP;
    public int topK;
}


[System.Serializable]
public class GeminiTextOnlyRequest 
{
    public List<TextContent> contents;
    public GenerationConfig generationConfig;
}

//Gemini Request
[System.Serializable]
public class GeminiRequest
{
    public List<Content> contents;
}


[System.Serializable]
public class UsageMetadata
{
    public int promptTokenCount;
    public string candidatesTokenCount;
    public int totalTokenCount;
}

//Gemini Response
[System.Serializable]
public class GeminiResponse
{
    public List<Candidate> candidates;
    public UsageMetadata usageMetadata;
    public GeminiError error;
}


[System.Serializable]
public class FunctionProfile
{
    public string interaction_flow;
    public string value_type;
    public string dimensionality;
    public string directionality;
    public string semantic_verb;
}

[System.Serializable]
public class PotentialProfile
{
    public string profile_id;
    public FunctionProfile profile;
    public string reasoning;
}

[System.Serializable]
public class Affordance
{
    public string affordance_type;
    public List<PotentialProfile> potential_profiles;
    public float confidence_score;
}

[System.Serializable]
public class DetectedObject
{
    public int object_id;
    public string object_name;
    public List<Affordance> affordances;
    public float[] bounding_box;
    public SerializableVector3 position;
}

[System.Serializable]
public class EnvironmentAnalysis
{
    public List<DetectedObject> detected_objects;
}

[System.Serializable]
public class FinalAnalysis
{
    public EnvironmentAnalysis environment_analysis;
}


//Affordance Matching DataType

[System.Serializable]
public class UiComponent
{
    public string component_id;
    public string component_name;
    public string priority;
    public FunctionProfile required_profile;
}

[System.Serializable]
public class UiComponentList
{
    public List<UiComponent> ui_components;
}

public class MatchResult
{
    public UiComponent uiComponent;
    public DetectedObject targetObject;
    public Affordance targetAffordance;
    public PotentialProfile targetPotentialProfile;
    public float Score;
}

// Position Data
[System.Serializable]
public struct SerializableVector3
{
    public float x, y, z;

    public SerializableVector3(Vector3 vector)
    {
        x = vector.x;
        y = vector.y;
        z = vector.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}