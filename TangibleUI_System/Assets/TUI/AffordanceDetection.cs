using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Text;
using PassthroughCameraSamples;
using Meta.XR.MRUtilityKit;
using Meta.XR;

public class AffordanceDetection : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("WebCamTextureManager")]
    [SerializeField]
    public WebCamTextureManager webCamTextureManager;

    [Tooltip("EnvironmentManager")]
    [SerializeField]
    public EnvironmentRaycastManager environmentRaycastManager;

    [Tooltip("AffordanceMatching")]
    [SerializeField]
    public AffordanceMatching affordanceMatching;

    [Header("API settings")]
    [Tooltip("API key from Google AI Studio")]
    [SerializeField]
    private string apiKey;

    [Tooltip("Gemini Model")]
    [SerializeField]
    private string modelName = "gemini-2.0-flash";

    //提供一个用以测试的接口可以自己添加图片进行测试
    [Header("input data")]
    [Tooltip("image input")]
    [SerializeField]
    private Texture2D imageToAnalyze;

    private string simplePrompt = @"In bullet-point format, describe in detail all interactable physical objects in this image.
For each object, you MUST describe its main features, such as material, shape, state, and spatial position.
Crucially, for each object, you MUST also provide its normalized 2D bounding box in the format[xmin, ymin, xmax, ymax], where (0, 0) is the bottom-left corner and(1,1) is the top-right corner.

Example:
* Mouse
  * Features: A black ergonomic mouse.
  * Bounding Box: [0.6, 0.2, 0.8, 0.4]
";

    private string prompt = @"
# ROLE
You are a top-tier Human-Computer Interaction (HCI) analysis engine.

# TASK
Analyze the following textual ""Input Description"" of a scene. For each object described, generate a list of its interaction affordances as a JSON object.

# OUTPUT INSTRUCTIONS
- You MUST include the ""bounding_box"" from the input description in your output for each object.
- For each affordance, you must generate a ""Function Profile"".
- All property values in the profile MUST be chosen from the predefined lists below.
- The final output MUST be a single, raw, perfectly valid JSON object and nothing else. Do not add any extra text or markdown ```json wrappers.

# PROPERTY DEFINITIONS
- `affordance_type`: `Surface`, `Edge`, `Grabability`, `Movable_Structure`, `Button_like`, `Sharp_Tip`
- `interaction_flow`: `continuous`, `discrete`
- `value_type`: `binary`, `categorical`, `ranged`, `positional_6d`
- `dimensionality`: `0D`, `1D`, `2D`, `3D`, `6D`
- `directionality`: `none`, `unidirectional`, `bidirectional`
- `semantic_verb`: `SELECT`, `ADJUST`, `Maps`, `TRANSFORM`, `TOGGLE`

#Example
---
## Input description:
This is a white ceramic mug placed on a wooden tabletop. It has a C-shaped handle and an open round mouth.* bounding_box: [0.4, 0.3, 0.6, 0.7]

## OutputJSON:
{
  ""environment_analysis"": {
    ""detected_objects"": [
      {
        ""object_id"": 1,
        ""object_name"": ""Coffee Mug"",
        ""bounding_box"":[0.4, 0.3, 0.6, 0.7],
        ""affordances"": [
          {
            ""affordance_type"": ""Grabability"",
            ""potential_profiles"": [
              {
                ""profile_id"": ""Mug-GrabHandle"",
                ""profile"": { ""interaction_flow"": ""discrete"", ""value_type"": ""positional_6d"", ""dimensionality"": ""6D"", ""directionality"": ""none"", ""semantic_verb"": ""TRANSFORM"" },
                ""reasoning"": ""The C-shaped handle is designed to be grasped by a hand to lift and move the entire object.""
              }
            ]
          }
        ]
      }
    ]
  }
}
---

# START OF TASK

## Input Description:
{description}

## Output JSON:
";

    [Tooltip("Gemini Feedback")]
    [SerializeField]
    private TMPro.TextMeshPro responseText;

    private string apiURL;

    private Vector2Int nativeResolution;

    private Pose cameraPoseAtCapture;

    void Start()
    {
        apiURL = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

    }


    private void Update()
    {
        if (Input.GetKeyDown("space"))
        {
            OnSubmit();
        }

        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            OnSubmit();
        }
    }

    //触发affordance提取的步骤
    public void OnSubmit()
    {
        if (imageToAnalyze == null)
        {
            responseText.tag = "please input image!!";
            return;
        }

        responseText.text = "思考中...";
        StartCoroutine(StartPipeline());
    }

    private GeminiRequest CreateRequestData(string prompt, string base64Image)
    {
        GeminiRequest request = new GeminiRequest
        {
            contents = new List<Content>
            {
                new Content
                {
                    parts = new List<Part>
                    {
                        new Part {text = prompt},
                        new Part
                        {
                            inline_data = new InlineData
                            {
                                mime_type = "image/jpeg",
                                data = base64Image
                            }
                        }
                    }
                }
            }
        };

        return request;
    }
    
    //编码图片信息
    private string EncodeTextureToBase64(Texture2D texture)
    {
        if (!texture.isReadable)
        {
            responseText.text = "this image is unreable!!!";
            return null;
        }
        byte[] imageBytes = texture.EncodeToJPG(75);
        float sizeInKB = imageBytes.Length / 1024.0f;
        Debug.Log($"[Image Size Check] 编码为JPG后的数据大小: {sizeInKB:F2} KB");
        return System.Convert.ToBase64String(imageBytes);
    }

    //检查是否是因为prompt过于复杂导致的超时问题，分为两个一个是简单的文字prompt，一个是简单的图片描述，如果发现Step1_GetImageDescription运行超时，可通过这两个测试检查是否是prompt的原因。
    //同时记得检查梯子问题，如果梯子爆了就会发生SSL connection cannot complete的问题。
    private IEnumerator AskSimpleTextCoroutine()
    {
        responseText.text = "正在发送一个简单的测试请求...";
        Debug.Log("正在发送一个简单的测试请求...");

        // 1. 构建一个最简单的JSON请求体
        string jsonBody = $@"{{
            ""contents"": [
                {{
                    ""parts"": [
                        {{ ""text"": ""Hello, what time is it now?"" }}
                    ]
                }}
            ]
        }}";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest webRequest = new UnityWebRequest(apiURL, "POST"))
        {
            webRequest.timeout = 60; // 保持60秒超时

            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            // 3. 处理响应
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                // 如果这里出错，打印错误信息
                responseText.text = $"简单测试失败: {webRequest.error}";
                Debug.LogError($"[简单测试] 失败: {webRequest.error}\n服务器响应: {webRequest.downloadHandler.text}");
            }
            else
            {
                // 如果这里成功，打印成功信息
                responseText.text = "简单测试成功！请查看控制台。";
                Debug.Log($"[简单测试] 成功! 响应: {webRequest.downloadHandler.text}");
            }
        }
    }
    private IEnumerator RunSimpleImageTestCoroutine()
    {
        responseText.text = "[简单测试] 正在准备...";
        Debug.Log("[简单测试] 启动...");

        if (imageToAnalyze == null)
        {
            responseText.text = "[简单测试] 错误: 未指定图像。";
            yield break;
        }

        // 1. 准备图像和Prompt
        string base64Image = EncodeTextureToBase64(imageToAnalyze);
        if (base64Image == null)
        {
            yield break;
        }

        string simplePrompt = "please describe the image";
        string escapedPrompt = simplePrompt.Replace("\"", "\\\"");

        // 2. 构建JSON请求体
        string jsonBody = $@"{{
            ""contents"": [{{
                ""parts"": [
                    {{ ""text"": ""{escapedPrompt}"" }},
                    {{ ""inline_data"": {{ ""mime_type"": ""image/jpeg"", ""data"": ""{base64Image}"" }} }}
                ]
            }}]
        }}";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        responseText.text = "[简单测试] 正在发送请求...";

        // 3. 发送网络请求
        using (UnityWebRequest webRequest = new UnityWebRequest(apiURL, "POST"))
        {
            webRequest.timeout = 120;
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            // 4. 处理响应
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                responseText.text = $"[简单测试] 失败: {webRequest.error}";
                Debug.LogError($"[简单测试] 失败: {webRequest.error}\n服务器响应: {webRequest.downloadHandler.text}");
            }
            else
            {
                string jsonResponse = webRequest.downloadHandler.text;
                GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(jsonResponse);

                if (response.error != null && !string.IsNullOrEmpty(response.error.message))
                {
                    responseText.text = $"[简单测试] API 错误: {response.error.message}";
                }
                else if (response.candidates != null && response.candidates.Count > 0)
                {
                    string description = response.candidates[0].content.parts[0].text;
                    responseText.text = $"[简单测试] 成功:\n{description}";
                    Debug.Log($"[简单测试] 成功获取描述: {description}");
                }
                else
                {
                    responseText.text = "[简单测试] 未能获取有效回复。";
                }
            }
        }
    }
    private IEnumerator AskGeminiCoroutine()
    {
        string base64Image = EncodeTextureToBase64(imageToAnalyze);
        if (base64Image == null)
        {
            yield break;
        }

        string escapedPrompt = this.prompt
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");


        string jsonBody = $@"{{
            ""contents"": [
                {{
                    ""parts"": [
                        {{ ""text"": ""{escapedPrompt}"" }},
                        {{
                            ""inline_data"": {{
                                ""mime_type"": ""image/jpeg"",
                                ""data"": ""{base64Image}""
                            }}
                        }}
                    ]
                }}
            ]
        }}";
        Debug.Log("JSON body being sent: " + jsonBody);

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest webRequest = new UnityWebRequest(apiURL, "POST"))
        {
            webRequest.timeout = 120;

            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            responseText.text = "sending request ...";
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Network Error: {webRequest.error}\n服务器响应: {webRequest.downloadHandler.text}");
                responseText.text = $"Network Error: {webRequest.error}";
            }
            else
            {
                responseText.text = "receive request, parsing...";

                string jsonResponse = webRequest.downloadHandler.text;
                ProcessAndParseResponse(jsonResponse);
            }
        }
    }

    private IEnumerator StartPipeline()
    {
        responseText.text = "starting... ";

        if (webCamTextureManager == null)
        {
            responseText.text = "error: webCamTextureManager not set";
            yield break;
        }

        responseText.text = "waiting for the permission and initialization of the camera";

        float waitTimer = 0f;
        yield return new WaitUntil(() =>
        {
            waitTimer += Time.deltaTime;
            return (webCamTextureManager.WebCamTexture != null &&
                    webCamTextureManager.WebCamTexture.isPlaying &&
                    webCamTextureManager.WebCamTexture.width > 100) || waitTimer > 15f;
        });

        if (webCamTextureManager.WebCamTexture == null || !webCamTextureManager.WebCamTexture.isPlaying)
        {
            responseText.text = "error: camera start failed";
            yield break;
        }

        responseText.text = "camera is ready";

        nativeResolution = PassthroughCameraUtils.GetCameraIntrinsics(webCamTextureManager.Eye).Resolution;
        Debug.Log(nativeResolution);

        cameraPoseAtCapture = PassthroughCameraUtils.GetCameraPoseInWorld(webCamTextureManager.Eye);

        Debug.Log(cameraPoseAtCapture.position);
        Debug.Log("save the position of the camera");


        imageToAnalyze = CaptureImage();

        yield return StartCoroutine(Step1_GetImageDescription());

    }
    public Texture2D CaptureImage()
    {
        responseText.text = "capturing image...";

        RenderTexture rt = RenderTexture.GetTemporary(256, 256, 0);

        Graphics.Blit(webCamTextureManager.WebCamTexture, rt);

        Texture2D finalTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false);

        RenderTexture.active = rt;
        finalTexture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        finalTexture.Apply();
        RenderTexture.active = null;

        RenderTexture.ReleaseTemporary(rt);
        responseText.text = "image acquirement complete!";

        return finalTexture;
    }
    
    //这里是正式的affordance提取的环节，将prompt分成了两步
    private IEnumerator Step1_GetImageDescription()
    {
        responseText.text = "acquring the description of the image...";
        string base64Image = EncodeTextureToBase64(imageToAnalyze);
        if (base64Image == null)
        {
            yield break;
        }

        string escapedPrompt = simplePrompt.Replace("\"", "\\\"");

        string jsonBody = $@"{{
            ""contents"":[{{""parts"":[
                {{""text"":""{escapedPrompt}""}},
                {{""inline_data"":{{""mime_type"":""image/jpeg"",""data"":""{base64Image}""}}}}
            ]}}],
            ""generationConfig"": {{
                ""maxOutputTokens"": 4096,
                ""temperature"": 0.4,
                ""topP"": 1,
                ""topK"": 32
            }}
        }}";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest webRequest = new UnityWebRequest(apiURL, "POST"))
        { 
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                responseText.text = $"step1 failed: {webRequest.error}";
                Debug.LogError($"step1 failed: {webRequest.error}\nserver: {webRequest.downloadHandler.text}");
            }
            else
            {
                var response = JsonUtility.FromJson<GeminiResponse>(webRequest.downloadHandler.text);
                if (response.candidates != null && response.candidates.Count > 0)
                {
                    string description = response.candidates[0].content.parts[0].text;
                    StartCoroutine(Step2_GenerateJsonFromDescription(description));
                }
                else
                {
                    responseText.text = "step1 failed no valid description";
                    Debug.LogError($"step1 failed no valid description{webRequest.downloadHandler.text}");
                }
            }
        }
    }

    private IEnumerator Step2_GenerateJsonFromDescription(string description)
    {
        responseText.text = "step2 starting...";
        string finalPrompt = prompt.Replace("{description}", description);

        string fileName = $"FinalPrompt_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(filePath, finalPrompt);
        string escapedPrompt = finalPrompt.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

        var requestData = new GeminiTextOnlyRequest
        {
            contents = new List<TextContent>
        {
            new TextContent
            {
                parts = new List<TextPart>
                {
                    new TextPart { text = finalPrompt }
                }
            }
        },
            generationConfig = new GenerationConfig
            {
                maxOutputTokens = 8192,
                temperature = 0.2f,
                topP = 1.0f,
                topK = 32
            }
        };
        string jsonBody = JsonUtility.ToJson(requestData);

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest webRequest = new UnityWebRequest(apiURL, "POST"))
        {
            webRequest.timeout = 240;
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                responseText.text = $"step2 failed: {webRequest.error}";
                Debug.LogError($"step2 failed:  {webRequest.error}\nserver: {webRequest.downloadHandler.text}");
            }
            else
            {
                ProcessAndParseResponse(webRequest.downloadHandler.text);
            }
        }
    }


    private void ProcessAndParseResponse(string jsonResponse)
    {
        Debug.Log("Raw JSON Response from Server: " + jsonResponse);

        GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(jsonResponse);

        if (response.error != null && !string.IsNullOrEmpty(response.error.message))
        {
            responseText.text = $"API error : {response.error.message}";
            Debug.LogError(response.error.message);
            return;
        }

        if (response.candidates != null && response.candidates.Count > 0)
        {
            string outputJsonString = response.candidates[0].content.parts[0].text;
            string cleanedOutputJsonString = CleanJsonString(outputJsonString);

            responseText.text = "paring the output ...";
            Debug.Log("cleanedOutputJsonStirng: " + cleanedOutputJsonString);

            FinalAnalysis analysis = JsonUtility.FromJson<FinalAnalysis>(cleanedOutputJsonString);

            EnvironmentAnalysis environmentAnalysis = analysis.environment_analysis;

            foreach(var detectedObject in environmentAnalysis.detected_objects)
            {
                float[] bbox = detectedObject.bounding_box;

                var normalizedCenterX = (bbox[0] + bbox[2]) / 2;
                var normalizedCenterY = (bbox[1] + bbox[3]) / 2;

                Vector2Int targetPixel = new Vector2Int((int)(normalizedCenterX * nativeResolution.x), (int)(normalizedCenterY * nativeResolution.y));

                var localray = PassthroughCameraUtils.ScreenPointToRayInCamera(webCamTextureManager.Eye, targetPixel);

                Vector3 worldRayOrigin = cameraPoseAtCapture.position;

                Vector3 worldRayDirection = cameraPoseAtCapture.rotation * localray.direction;

                var ray = new Ray(worldRayOrigin, worldRayDirection);

                if (environmentRaycastManager.Raycast(ray, out EnvironmentRaycastHit hitinfo))
                {
                    Vector3 worldPosition = hitinfo.point;

                    SerializableVector3 serializablePosition = new SerializableVector3
                    {
                        x = worldPosition.x,
                        y = worldPosition.y,
                        z = worldPosition.z,
                    };
                    detectedObject.position = serializablePosition;
                }
            }

            ProcessAnalysis(analysis.environment_analysis);
        }
        else
        {
            responseText.text = "no valid answer! No valid candidate!!";
        }


    }

    private string CleanJsonString(string jsonString)
    {
        string cleanedJsonString = jsonString.Trim();

        if (cleanedJsonString.StartsWith("```json"))
        {
            cleanedJsonString = cleanedJsonString.Substring(7).Trim(); 
        }
        else if (cleanedJsonString.StartsWith("```"))
        {
            cleanedJsonString = cleanedJsonString.Substring(3).Trim();
        }

        if (cleanedJsonString.EndsWith("```"))
        {
            cleanedJsonString = cleanedJsonString.Substring(0, cleanedJsonString.Length - 3).Trim();
        }
        return cleanedJsonString;
    }

    private void ProcessAnalysis(EnvironmentAnalysis analysis)
    {
        if (analysis == null || analysis.detected_objects == null)
        {
            responseText.text = "empty analysis!!!";
            return;
        }

        responseText.text = "Analysis complete!";

        string jsonToSave = JsonUtility.ToJson(analysis, true);
        string fileName = $"Affordance_Analysis_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(filePath, jsonToSave);

        responseText.text = "saved to localPath!!!";

        affordanceMatching.Matching();
    }
}
