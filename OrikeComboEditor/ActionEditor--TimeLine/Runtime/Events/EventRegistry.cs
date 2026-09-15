using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// 事件注册表
///
/// 自动扫描所有程序集中实现了 IStateEvent / IPointEvent
/// 且带有 [Serializable] 的类，按 Type.FullName 注册。
///
/// 首次访问时执行扫描，之后使用缓存。
/// </summary>
public static class EventRegistry
{
    // =========================================================
    // 缓存
    // =========================================================

    private static bool _scanned;

    private static readonly Dictionary<
        string,
        Type> _stateEventTypes =
        new Dictionary<
            string,
            Type>();

    private static readonly Dictionary<
        string,
        Type> _pointEventTypes =
        new Dictionary<
            string,
            Type>();


    // =========================================================
    // 扫描
    // =========================================================

    private static void Scan()
    {
        if (_scanned)
        {
            return;
        }

        _scanned =
            true;

        Type stateInterface =
            typeof(IStateEvent);

        Type pointInterface =
            typeof(IPointEvent);

        Assembly[] assemblies =
            AppDomain.CurrentDomain
                .GetAssemblies();

        foreach (
            Assembly assembly
            in assemblies)
        {
            Type[] types;

            try
            {
                types =
                    assembly.GetTypes();
            }
            catch
            {
                continue;
            }

            foreach (
                Type type
                in types)
            {
                // 跳过抽象类、接口、泛型
                if (type.IsAbstract ||
                    type.IsInterface ||
                    type.IsGenericType)
                {
                    continue;
                }

                // 必须有 [Serializable]
                if (!type
                    .IsDefined(
                        typeof(SerializableAttribute),
                        false))
                {
                    continue;
                }

                // 持续事件
                if (stateInterface
                    .IsAssignableFrom(type))
                {
                    string key =
                        type.FullName;

                    if (!_stateEventTypes
                        .ContainsKey(key))
                    {
                        _stateEventTypes
                            .Add(
                                key,
                                type);
                    }
                }

                // 点事件
                if (pointInterface
                    .IsAssignableFrom(type))
                {
                    string key =
                        type.FullName;

                    if (!_pointEventTypes
                        .ContainsKey(key))
                    {
                        _pointEventTypes
                            .Add(
                                key,
                                type);
                    }
                }
            }
        }
    }


    // =========================================================
    // 创建持续事件
    // =========================================================

    public static IStateEvent CreateStateEvent(
        string typeName)
    {
        Scan();

        if (string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        if (_stateEventTypes.TryGetValue(
                typeName,
                out Type type))
        {
            return (IStateEvent)
                Activator.CreateInstance(type);
        }

        // 兼容旧资产：尝试按类名（不含命名空间）匹配
        type =
            ResolveByShortName(
                typeName,
                _stateEventTypes);

        if (type != null)
        {
            return (IStateEvent)
                Activator.CreateInstance(type);
        }

        return null;
    }


    // =========================================================
    // 创建点事件
    // =========================================================

    public static IPointEvent CreatePointEvent(
        string typeName)
    {
        Scan();

        if (string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        if (_pointEventTypes.TryGetValue(
                typeName,
                out Type type))
        {
            return (IPointEvent)
                Activator.CreateInstance(type);
        }

        // 兼容旧资产
        type =
            ResolveByShortName(
                typeName,
                _pointEventTypes);

        if (type != null)
        {
            return (IPointEvent)
                Activator.CreateInstance(type);
        }

        return null;
    }


    // =========================================================
    // 是否持续事件类型
    // =========================================================

    public static bool IsStateEventType(
        string typeName)
    {
        Scan();

        return _stateEventTypes
            .ContainsKey(typeName);
    }


    // =========================================================
    // 是否点事件类型
    // =========================================================

    public static bool IsPointEventType(
        string typeName)
    {
        Scan();

        return _pointEventTypes
            .ContainsKey(typeName);
    }


    // =========================================================
    // 获取所有持续事件类型名
    // =========================================================

    public static string[] GetStateEventTypeNames()
    {
        Scan();

        string[] result =
            new string[
                _stateEventTypes.Count];

        int i = 0;

        foreach (
            string key
            in _stateEventTypes.Keys)
        {
            result[i++] =
                key;
        }

        return result;
    }


    // =========================================================
    // 获取所有点事件类型名
    // =========================================================

    public static string[] GetPointEventTypeNames()
    {
        Scan();

        string[] result =
            new string[
                _pointEventTypes.Count];

        int i = 0;

        foreach (
            string key
            in _pointEventTypes.Keys)
        {
            result[i++] =
                key;
        }

        return result;
    }


    // =========================================================
    // 获取持续事件的显示名
    // =========================================================

    public static string GetDisplayName(
        string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return "None";
        }

        // 取类名部分（不含命名空间）
        int lastDot =
            typeName.LastIndexOf('.');

        if (lastDot >= 0 &&
            lastDot < typeName.Length - 1)
        {
            return typeName
                .Substring(lastDot + 1);
        }

        return typeName;
    }


    // =========================================================
    // 按短名匹配（兼容旧资产）
    // =========================================================

    private static Type ResolveByShortName(
        string name,
        Dictionary<string, Type> cache)
    {
        // 旧枚举值可能是 "DebugTestState" 或 "1001"
        foreach (
            KeyValuePair<string, Type> pair
            in cache)
        {
            string shortName =
                GetDisplayName(pair.Key);

            if (shortName == name)
            {
                return pair.Value;
            }
        }

        return null;
    }
}
