using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Orike.HTN.Editor
{
    /// <summary>
    /// 示例域生成器：按《游戏人工智能》第 12 章的“树神（Trunk Thumper）”
    /// 例子生成一个完整可规划的 HTN 定义域，涵盖：
    ///   - 世界状态属性（视野 / 敌人距离 / 树干耐久 / 疲劳）
    ///   - 根任务 BeTrunkThumper 的两个实现方法（攻击 / 巡逻）
    ///   - AttackEnemy 的高优先级近战与低优先级取新树干（递归）
    ///   - DoTrunkSlam 的耐久条件与递减效果
    /// 生成后可在 HTN Domain 窗口的规划测试面板里直接验证。
    /// </summary>
    public static class HTNExampleDomainBuilder
    {
        [MenuItem(
            "Orike/HTN/生成树神示例域")]
        public static void CreateExampleDomain()
        {
            string selectedPath =
                AssetDatabase.GetAssetPath(
                    Selection.activeObject);

            if (string.IsNullOrEmpty(selectedPath))
            {
                selectedPath =
                    "Assets";
            }
            else
            {
                selectedPath =
                    Directory.Exists(selectedPath)
                        ? selectedPath
                        : Path.GetDirectoryName(selectedPath);
            }

            string path =
                EditorUtility.SaveFilePanelInProject(
                    "生成树神示例域",
                    "TrunkThumperDomain",
                    "asset",
                    "选择保存位置",
                    selectedPath);

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            BuildDomain(path);

            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[HTN] 树神示例域已生成：{path}。" +
                "打开 Orike/HTN Domain 窗口加载它，" +
                "在底部规划测试面板调整世界状态后点“规划”即可验证。");

            EditorGUIUtility.PingObject(
                AssetDatabase.LoadAssetAtPath<HTNDomain>(path));
        }


        private static void BuildDomain(string path)
        {
            HTNDomain domain =
                ScriptableObject.CreateInstance<HTNDomain>();

            AssetDatabase.CreateAsset(
                domain,
                path);

            // ---------------------------------------------------------
            // 世界状态属性
            // ---------------------------------------------------------
            domain.WorldStateProperties =
                new List<HTNWorldStateProperty>
                {
                    new HTNWorldStateProperty
                    {
                        Name = "WsCanSeeEnemy",
                        DefaultValue = 0,
                        Comment = "0=看不到 1=看到（由视觉感知器更新）",
                    },
                    new HTNWorldStateProperty
                    {
                        Name = "WsEnemyRange",
                        DefaultValue = 2,
                        Comment = "0=近战距离 1=攻击距离 2=远离",
                    },
                    new HTNWorldStateProperty
                    {
                        Name = "WsTrunkHealth",
                        DefaultValue = 3,
                        Comment = "树干耐久：每 3 次重击会折断",
                    },
                    new HTNWorldStateProperty
                    {
                        Name = "WsIsTired",
                        DefaultValue = 0,
                        Comment = "0=清醒 1=疲惫（重击后需要恢复）",
                    },
                    new HTNWorldStateProperty
                    {
                        Name = "WsHasSeenEnemyRecently",
                        DefaultValue = 0,
                        Comment = "0=否 1=是（用于追击丢失视野的敌人）",
                    },
                };

            // ---------------------------------------------------------
            // 基元任务
            // ---------------------------------------------------------
            HTNPrimitiveTask navigateToEnemy =
                NewPrimitive(
                    domain,
                    "NavigateToEnemy",
                    "移动到敌人身边。效果里的 WsEnemyRange=0 是期望效果："
                    + "到达近战距离这件事由距离感知器在执行期达成，"
                    + "但规划时需要假定它成立，后面的 DoTrunkSlam 才能通过条件校验。",
                    effects: new List<HTNEffect>
                    {
                        new HTNEffect
                        {
                            Property = "WsEnemyRange",
                            Op = HTNEffectOp.Set,
                            Value = 0,
                            Expected = true,
                        },
                        new HTNEffect
                        {
                            Property = "WsHasSeenEnemyRecently",
                            Op = HTNEffectOp.SetTrue,
                        },
                    });

            HTNPrimitiveTask doTrunkSlam =
                NewPrimitive(
                    domain,
                    "DoTrunkSlam",
                    "树干重击：要求敌人在近战距离且树干有耐久；"
                    + "每次成功攻击耐久 -1，累计三次后疲惫。",
                    conditions: new List<HTNCondition>
                    {
                        new HTNCondition
                        {
                            Property = "WsEnemyRange",
                            Type = HTNConditionType.Equal,
                            Value = 0,
                        },
                        new HTNCondition
                        {
                            Property = "WsTrunkHealth",
                            Type = HTNConditionType.Greater,
                            Value = 0,
                        },
                    },
                    effects: new List<HTNEffect>
                    {
                        new HTNEffect
                        {
                            Property = "WsTrunkHealth",
                            Op = HTNEffectOp.Subtract,
                            Value = 1,
                        },
                        new HTNEffect
                        {
                            Property = "WsIsTired",
                            Op = HTNEffectOp.SetTrue,
                        },
                    });

            HTNPrimitiveTask doRecovery =
                NewPrimitive(
                    domain,
                    "DoRecovery",
                    "恢复：疲惫后播放恢复动画，清醒过来。",
                    conditions: new List<HTNCondition>
                    {
                        new HTNCondition
                        {
                            Property = "WsIsTired",
                            Type = HTNConditionType.Equal,
                            Value = 1,
                        },
                    },
                    effects: new List<HTNEffect>
                    {
                        new HTNEffect
                        {
                            Property = "WsIsTired",
                            Op = HTNEffectOp.SetFalse,
                        },
                    });

            HTNPrimitiveTask findTrunk =
                NewPrimitive(
                    domain,
                    "FindTrunk",
                    "选择一棵合适的新树。",
                    effects: new List<HTNEffect>());

            HTNPrimitiveTask navigateToTrunk =
                NewPrimitive(
                    domain,
                    "NavigateToTrunk",
                    "走到那棵树旁。",
                    effects: new List<HTNEffect>());

            HTNPrimitiveTask uprootTrunk =
                NewPrimitive(
                    domain,
                    "UprootTrunk",
                    "连根拔起树干，耐久恢复到 3。",
                    effects: new List<HTNEffect>
                    {
                        new HTNEffect
                        {
                            Property = "WsTrunkHealth",
                            Op = HTNEffectOp.Set,
                            Value = 3,
                        },
                    });

            HTNPrimitiveTask navToLastEnemyLoc =
                NewPrimitive(
                    domain,
                    "NavToLastEnemyLoc",
                    "移动到上次看到敌人的位置。"
                    + "期望效果假定到达后视觉感知器能看到敌人。",
                    effects: new List<HTNEffect>
                    {
                        new HTNEffect
                        {
                            Property = "WsCanSeeEnemy",
                            Op = HTNEffectOp.SetTrue,
                            Expected = true,
                        },
                    });

            HTNPrimitiveTask roar =
                NewPrimitive(
                    domain,
                    "RegainLOSRoar",
                    "咆哮示威（要求重新看到敌人）。",
                    conditions: new List<HTNCondition>
                    {
                        new HTNCondition
                        {
                            Property = "WsCanSeeEnemy",
                            Type = HTNConditionType.Equal,
                            Value = 1,
                        },
                    },
                    effects: new List<HTNEffect>());

            HTNPrimitiveTask selectNextBridge =
                NewPrimitive(
                    domain,
                    "SelectNextBridge",
                    "选择下一座要巡逻的桥。",
                    effects: new List<HTNEffect>());

            HTNPrimitiveTask navigateToBridge =
                NewPrimitive(
                    domain,
                    "NavigateToBridge",
                    "走到那座桥。",
                    effects: new List<HTNEffect>());

            HTNPrimitiveTask inspectBridgeForEnemies =
                NewPrimitive(
                    domain,
                    "InspectBridgeForEnemies",
                    "检查桥附近是否有敌人（视野感知器会更新 WsCanSeeEnemy）。",
                    effects: new List<HTNEffect>());

            // ---------------------------------------------------------
            // 复合任务
            // ---------------------------------------------------------

            // 攻击敌人（方法优先级从高到低）
            HTNCompoundTask attackEnemy =
                NewCompound(domain, "AttackEnemy");

            // 方法0：有树干 → 冲过去重击（NavigateToEnemy 的期望效果
            // 把敌人距离假定为近战距离，DoTrunkSlam 的条件才能通过校验）
            attackEnemy.Methods.Add(
                new HTNMethod
                {
                    MethodName = "树干重击",
                    Conditions = new List<HTNCondition>
                    {
                        new HTNCondition
                        {
                            Property = "WsCanSeeEnemy",
                            Type = HTNConditionType.Equal,
                            Value = 1,
                        },
                        new HTNCondition
                        {
                            Property = "WsTrunkHealth",
                            Type = HTNConditionType.Greater,
                            Value = 0,
                        },
                    },
                    SubTasks = new List<HTNTask>
                    {
                        navigateToEnemy,
                        doTrunkSlam,
                        doRecovery,
                    },
                });

            // 方法1：树干没了 → 取新树干 → 递归回到 AttackEnemy（12.6 节）。
            // UprootTrunk 会把耐久设回 3，递归重新分解时方法 0 就能命中，
            // 保证递归可以终止
            attackEnemy.Methods.Add(
                new HTNMethod
                {
                    MethodName = "取新树干（递归）",
                    Conditions = new List<HTNCondition>
                    {
                        new HTNCondition
                        {
                            Property = "WsCanSeeEnemy",
                            Type = HTNConditionType.Equal,
                            Value = 1,
                        },
                    },
                    SubTasks = new List<HTNTask>
                    {
                        findTrunk,
                        navigateToTrunk,
                        uprootTrunk,
                        attackEnemy,
                    },
                });

            // 方法2：看不到但最近看到过 → 去最后出现的位置找
            //（期望效果示例，12.7 节）
            attackEnemy.Methods.Add(
                new HTNMethod
                {
                    MethodName = "追击丢失的敌人",
                    Conditions = new List<HTNCondition>
                    {
                        new HTNCondition
                        {
                            Property = "WsHasSeenEnemyRecently",
                            Type = HTNConditionType.Equal,
                            Value = 1,
                        },
                    },
                    SubTasks = new List<HTNTask>
                    {
                        navToLastEnemyLoc,
                        roar,
                    },
                });

            // 巡逻：检查完一座桥后计划结束，由 Brain 的
            // “计划执行完毕 → 重新规划”驱动去下一座桥，
            // 不能在域里自递归（否则规划永远分解不完）
            HTNCompoundTask patrolBridges =
                NewCompound(domain, "PatrolBridges");

            patrolBridges.Methods.Add(
                new HTNMethod
                {
                    MethodName = "巡逻一座桥",
                    Conditions = new List<HTNCondition>(),
                    SubTasks = new List<HTNTask>
                    {
                        selectNextBridge,
                        navigateToBridge,
                        inspectBridgeForEnemies,
                    },
                });

            // 根任务（12.3 节）
            HTNCompoundTask beTrunkThumper =
                NewCompound(domain, "BeTrunkThumper");

            beTrunkThumper.Methods.Add(
                new HTNMethod
                {
                    MethodName = "攻击敌人",
                    Conditions = new List<HTNCondition>
                    {
                        new HTNCondition
                        {
                            Property = "WsCanSeeEnemy",
                            Type = HTNConditionType.Equal,
                            Value = 1,
                        },
                    },
                    SubTasks = new List<HTNTask>
                    {
                        attackEnemy,
                    },
                });

            beTrunkThumper.Methods.Add(
                new HTNMethod
                {
                    MethodName = "巡逻",
                    Conditions = new List<HTNCondition>(),
                    SubTasks = new List<HTNTask>
                    {
                        patrolBridges,
                    },
                });

            domain.RootTask = beTrunkThumper;

            EditorUtility.SetDirty(domain);
        }


        private static HTNPrimitiveTask NewPrimitive(
            HTNDomain domain,
            string taskName,
            string description,
            List<HTNCondition> conditions = null,
            List<HTNEffect> effects = null)
        {
            HTNPrimitiveTask task =
                ScriptableObject.CreateInstance<HTNPrimitiveTask>();

            task.name = taskName;

            task.Description = description;

            task.OperatorId = "Mock";

            task.Parameters = "1.0|" + taskName;

            task.Conditions = conditions ?? new List<HTNCondition>();

            task.Effects = effects ?? new List<HTNEffect>();

            AssetDatabase.AddObjectToAsset(
                task,
                domain);

            domain.Tasks.Add(task);

            return task;
        }


        private static HTNCompoundTask NewCompound(
            HTNDomain domain,
            string taskName)
        {
            HTNCompoundTask task =
                ScriptableObject.CreateInstance<HTNCompoundTask>();

            task.name = taskName;

            AssetDatabase.AddObjectToAsset(
                task,
                domain);

            domain.Tasks.Add(task);

            return task;
        }
    }
}
