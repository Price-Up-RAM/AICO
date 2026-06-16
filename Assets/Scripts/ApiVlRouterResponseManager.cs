using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class ApiVlRouterResponseManager : MonoBehaviour
{
    private static ApiVlRouterResponseManager instance;  // Singleton instance
    public static ApiVlRouterResponseManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ApiVlRouterResponseManager>();
            }
            return instance;
        }
    }

    // Handle Router protocol-level tool_call responses.
    public bool TryHandleRouterToolCall(
        JObject eventData,
        JObject data,
        int offsetX,
        int offsetY,
        Func<JObject, bool> showReplyListFromData,
        Action<string, JObject, int, int> executeRouterFunction
    )
    {
        // Ignore non-tool_call data.
        if (!IsToolCall(data))
        {
            return false;
        }

        // Extract Router metadata for trace logs.
        string kind = eventData["kind"]?.Value<string>() ?? "";
        string message = eventData["message"]?.Value<string>() ?? "";
        string routerMatchType = eventData["router"]?["match"]?["type"]?.Value<string>() ?? "";
        string routerTarget = eventData["router"]?["match"]?["target"]?.Value<string>() ?? "";
        string toolTarget = GetToolCallTarget(data);
        string status = GetToolCallStatus(data);

        Debug.Log($"[VlRouterRun] tool_call detected: kind={kind}, status={status}, target={toolTarget}, routerMatchType={routerMatchType}, routerTarget={routerTarget}, message={message}");

        // Alarm tool_call uses result.text h/m/s JSON to create a real relative timer.
        if (TryHandleAlarmMakerToolCall(data, toolTarget, showReplyListFromData))
        {
            return true;
        }

        // Unity-owned tool_call: only execute when the server sends an explicit envelope.
        if (TryExtractEnvelope(data, out string functionName, out JObject parameters))
        {
            string parameterLog = parameters.ToString(Formatting.None);
            Debug.Log($"[VlRouterRun] tool_call unity envelope: function={functionName}, parameters={parameterLog}, offset=({offsetX}, {offsetY})");
            executeRouterFunction(functionName, parameters, offsetX, offsetY);
            TryShowReplyList(data, showReplyListFromData);
            return true;
        }

        // Server-completed tool_call: Unity does not run a second function.
        if (IsSuccessStatus(status))
        {
            Debug.Log($"[VlRouterRun] tool_call server-completed: target={toolTarget}, status={status}. Unity function execution skipped.");
            TryLogResult(data);
            TryShowReplyList(data, showReplyListFromData);
            return true;
        }

        // Failed or unresolved tool_call: log and show reply_list if present.
        Debug.LogWarning($"[VlRouterRun] tool_call failed or unresolved: target={toolTarget}, status={status}");
        TryLogResult(data);
        TryShowReplyList(data, showReplyListFromData);
        return true;
    }

    // Return whether data.action is Router tool_call.
    private bool IsToolCall(JObject data)
    {
        string action = data["action"]?.Value<string>() ?? "";
        if (action == "tool_call")
        {
            return true;
        }

        return false;
    }

    // Return whether the target belongs to alarm maker.
    private bool IsAlarmMakerTarget(string toolTarget)
    {
        if (toolTarget == "tool_alarm_maker")
        {
            return true;
        }

        if (toolTarget == "skill_alarm_counter.md")
        {
            return true;
        }

        return false;
    }

    // Create and start a real AlarmManager relative timer from alarm tool_call data.
    private bool TryHandleAlarmMakerToolCall(JObject data, string toolTarget, Func<JObject, bool> showReplyListFromData)
    {
        if (!IsAlarmMakerTarget(toolTarget))
        {
            return false;
        }

        if (!TryExtractAlarmDurationSeconds(data, out int durationSeconds))
        {
            Debug.LogWarning($"[VlRouterRun] tool_alarm_maker duration parse failed. data={data.ToString(Formatting.None)}");
            TryShowReplyList(data, showReplyListFromData);
            return true;
        }

        AlarmManager alarmManager = FindSceneComponentIncludingInactive<AlarmManager>();
        if (alarmManager == null)
        {
            Debug.LogWarning("[VlRouterRun] tool_alarm_maker failed: AlarmManager not found in scene.");
            TryShowReplyList(data, showReplyListFromData);
            return true;
        }

        string title = GetAlarmTitle(data, durationSeconds);
        AlarmItem alarm = alarmManager.AddRelativeTimer(title, durationSeconds, "default_alarm");
        alarmManager.StartRelativeTimer(alarm.id);
        Debug.Log($"[VlRouterRun] tool_alarm_maker created relative timer: id={alarm.id}, title={title}, seconds={durationSeconds}");

        UIManager.Instance.ShowAlarmMini();
        TryShowReplyList(data, showReplyListFromData);
        return true;
    }

    // Parse h/m/s duration from the alarm tool_call result and return total seconds.
    private bool TryExtractAlarmDurationSeconds(JObject data, out int durationSeconds)
    {
        durationSeconds = 0;

        JObject durationObject = GetAlarmDurationObject(data);
        if (durationObject == null)
        {
            return false;
        }

        int hours = durationObject["h"]?.Value<int>() ?? 0;
        int minutes = durationObject["m"]?.Value<int>() ?? 0;
        int seconds = durationObject["s"]?.Value<int>() ?? 0;
        durationSeconds = hours * 3600 + minutes * 60 + seconds;
        if (durationSeconds <= 0)
        {
            return false;
        }

        return true;
    }

    // Extract the h/m/s object from result fields or result.text.
    private JObject GetAlarmDurationObject(JObject data)
    {
        JObject result = data["result"] as JObject;
        if (result == null)
        {
            return data;
        }

        if (result["h"] != null || result["m"] != null || result["s"] != null)
        {
            return result;
        }

        string resultText = result["text"]?.Value<string>() ?? "";
        if (string.IsNullOrEmpty(resultText))
        {
            return null;
        }

        return ParseJsonObjectFromText(resultText);
    }

    // Parse the first JSON object from text that may include think logs.
    private JObject ParseJsonObjectFromText(string text)
    {
        int startIndex = text.IndexOf("{", StringComparison.Ordinal);
        int endIndex = text.LastIndexOf("}", StringComparison.Ordinal);
        if (startIndex < 0 || endIndex < startIndex)
        {
            return null;
        }

        string json = text.Substring(startIndex, endIndex - startIndex + 1);
        try
        {
            return JObject.Parse(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VlRouterRun] tool_alarm_maker JSON parse failed: {e.Message}, json={json}");
            return null;
        }
    }

    // Build a visible alarm title.
    private string GetAlarmTitle(JObject data, int durationSeconds)
    {
        string goal = data["goal"]?.Value<string>() ?? "";
        if (!string.IsNullOrEmpty(goal))
        {
            return goal;
        }

        int hours = durationSeconds / 3600;
        int minutes = durationSeconds % 3600 / 60;
        int seconds = durationSeconds % 60;
        if (hours > 0)
        {
            return string.Format("Router timer {0}h {1}m {2}s", hours, minutes, seconds);
        }

        if (minutes > 0)
        {
            return string.Format("Router timer {0}m {1}s", minutes, seconds);
        }

        return string.Format("Router timer {0}s", seconds);
    }

    // Find scene components even when their GameObject is inactive.
    private T FindSceneComponentIncludingInactive<T>() where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || component.gameObject == null)
            {
                continue;
            }

            if (!component.gameObject.scene.IsValid())
            {
                continue;
            }

            return component;
        }

        return null;
    }

    // Extract the actual tool target name from common fields.
    private string GetToolCallTarget(JObject data)
    {
        string target = data["target"]?.Value<string>() ?? "";
        if (!string.IsNullOrEmpty(target))
        {
            return target;
        }

        string toolName = data["tool_name"]?.Value<string>() ?? "";
        if (!string.IsNullOrEmpty(toolName))
        {
            return toolName;
        }

        string name = data["name"]?.Value<string>() ?? "";
        return name;
    }

    // Extract tool_call status from data or result.
    private string GetToolCallStatus(JObject data)
    {
        string status = data["status"]?.Value<string>() ?? "";
        if (!string.IsNullOrEmpty(status))
        {
            return status;
        }

        JObject result = data["result"] as JObject;
        if (result != null)
        {
            status = result["status"]?.Value<string>() ?? "";
        }

        return status;
    }

    // Return whether a status means server-completed success.
    private bool IsSuccessStatus(string status)
    {
        if (string.IsNullOrEmpty(status))
        {
            return false;
        }

        if (status == "success" || status == "succeeded" || status == "ok" || status == "done" || status == "completed")
        {
            return true;
        }

        return false;
    }

    // Extract a Unity execution envelope from tool_call data.
    private bool TryExtractEnvelope(JObject data, out string functionName, out JObject parameters)
    {
        functionName = "";
        parameters = null;

        JObject envelope = null;  // Execution body with function_name and parameters.

        // Standard router_action.payload.envelope format.
        JObject routerAction = data["router_action"] as JObject;
        if (routerAction != null)
        {
            JObject payload = routerAction["payload"] as JObject;
            if (payload != null)
            {
                envelope = payload["envelope"] as JObject;
                if (envelope == null && payload["function_name"] != null)
                {
                    envelope = payload;
                }
            }

            if (envelope == null && routerAction["envelope"] != null)
            {
                envelope = routerAction["envelope"] as JObject;
            }

            if (envelope == null && routerAction["function_name"] != null)
            {
                envelope = routerAction;
            }
        }

        // Compatibility format: data.envelope.
        if (envelope == null && data["envelope"] != null)
        {
            envelope = data["envelope"] as JObject;
        }

        // Compatibility format: data itself is an envelope.
        if (envelope == null && data["function_name"] != null)
        {
            envelope = data;
        }

        if (envelope == null)
        {
            return false;
        }

        functionName = envelope["function_name"]?.Value<string>() ?? "";
        parameters = envelope["parameters"] as JObject;
        if (parameters == null)
        {
            parameters = new JObject();
        }

        if (string.IsNullOrEmpty(functionName))
        {
            Debug.LogWarning($"[VlRouterRun] tool_call envelope has no function_name: envelope={envelope.ToString(Formatting.None)}");
            return false;
        }

        return true;
    }

    // Log raw tool_call result.
    private void TryLogResult(JObject data)
    {
        JToken result = data["result"];
        if (result == null)
        {
            return;
        }

        Debug.Log($"[VlRouterRun] tool_call result: {result.ToString(Formatting.None)}");
    }

    // Display tool_call reply_list through the existing Router conversation facade.
    private bool TryShowReplyList(JObject data, Func<JObject, bool> showReplyListFromData)
    {
        JToken replyList = data["reply_list"];
        if (replyList == null || replyList.Type != JTokenType.Array)
        {
            return false;
        }

        bool shown = showReplyListFromData(data);
        if (shown)
        {
            Debug.Log("[VlRouterRun] tool_call reply_list displayed through Router conversation facade.");
        }
        else
        {
            Debug.LogWarning("[VlRouterRun] tool_call reply_list exists but display callback returned false.");
        }

        return shown;
    }
}
