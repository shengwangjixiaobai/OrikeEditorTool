# Orike HTN（分层任务网络）

按《游戏人工智能》（Game AI Pro 中文版）第 4.7 节与第 12 章
（Troy Humphreys《通过实例探索 HTN 规划器》）实现的
**全序正向分解（total-order forward decomposition）HTN 规划器**，
配套 Unity UI Toolkit 的结构化域配置编辑器。

```
                 ┌────────────┐   感知游戏世界    ┌─────────────┐
   游戏世界 ────▶ │ 感知器      │ ───写入────▶     │ 世界状态     │
                 │ HTNSensor  │                  │ WorldState  │
                 └────────────┘                  └──────┬──────┘
                                                       │ 读取
                 ┌────────────┐   驱动当前任务     ┌──────▼──────┐
                 │ 操作函数    │ ◀──────────────  │ 规划器       │
                 │ Operator   │                   │ Planner     │
                 └─────┬──────┘                   └──────▲──────┘
                       │ 成功 → 施加效果到世界状态           │ 全序正向分解
                       │ 失败 → 重新规划                    │
                       ▼                            ┌──────┴──────┐
                  游戏世界  ◀────── 计划执行器 ────── │ 任务层级     │
                             （HTNBrain）            │ HTNDomain   │
                                                    └─────────────┘
```

一句话流程：**感知器**把游戏世界编码成**世界状态** → **规划器**从根任务开始把
任务层级分解成一串基元任务（计划）→ **计划执行器**逐个驱动基元任务的
**操作函数**，成功的任务把效果写回世界状态；任何环节失效就重新规划。

---

## 1. 五分钟跑通（树神示例）

1. 菜单 `Orike/HTN/生成树神示例域`，保存为资产（如 `TrunkThumperDomain.asset`）。
2. 菜单 `Orike/HTN Domain` 打开配置窗口，把域拖进左上角「定义域」字段。
   左侧树会显示 ★根任务 BeTrunkThumper → 方法 → 子任务 的完整层级。
3. 底部「规划测试」面板：
   - 把 `WsCanSeeEnemy` 改成 1，点 `▶ 规划` → 得到一串
     NavigateToEnemy → DoTrunkSlam → DoRecovery → …（树干断了会先去拔新树干）；
   - 把 `WsCanSeeEnemy` 改成 0、`WsHasSeenEnemyRecently` 改成 1 再规划 →
     走“去最后见到敌人的位置 + 咆哮”分支（这里用到了**期望效果**）；
   - 勾选 `MTR约束` 可以模拟“当前有计划在运行”时的重规划。
4. 场景搭建：新建空物体挂 `HTNBrain`，指定域，Play，
   Console 会输出每个任务的开始 / 完成（内置 Mock 操作函数：打日志 + 等待）。
5. 再加一个子物体挂 `HTNDebugSensor`（每 2 秒翻转 WsCanSeeEnemy），
   观察 Play 中角色随世界变化自动重规划。

---

## 2. 编辑器（Orike/HTN Domain）

| 区域 | 说明 |
|---|---|
| 工具栏 | 定义域资产 / 新建 / 世界状态属性… / 添加任务 / 保存 |
| 左侧树 | 根任务（★）→ 实现方法 → 子任务；未被引用的任务挂在“未挂接”下 |
| 右侧面板 | 选中任务 / 方法的属性编辑（条件、效果、子任务、操作函数…） |
| 底部面板 | 规划测试：临时世界状态 → 规划 → 计划 + 逐步分解日志 |

**树操作**：

- 左键点击行 → 右侧面板编辑对应对象；
- 右键行：复合任务（设为根 / 重命名 / 删除）、方法（调优先级 / 加子任务 / 删除）；
- 右键空白：添加任务 / 世界状态属性；
- 左键拖拽：任务→方法行＝追加为子任务；任务→复合任务行＝进它的方法0；
  任务→基元子任务行＝插到该位置（向上拖在目标上方、向下拖在目标下方）；
  方法→方法行＝调整方法优先级顺序。

所有编辑都支持 Undo；停止输入 2 秒后自动保存到资产。

**改任何配置都不需要写代码**：条件、效果、方法、任务结构全是资产数据。

---

## 3. 添加一个世界状态属性，需要改感知器代码吗？

**先在编辑器里加属性**：工具栏 `世界状态属性…` → `＋ 属性`，
填名字（建议 `Ws` 前缀）、默认值、备注（值域说明）。

加完之后分三种情况：

| 这个属性的值由谁改变 | 需要写代码吗 |
|---|---|
| 只由任务的效果驱动（如“疲惫”由重击任务置 1） | **不需要**。任务的效果在规划期模拟、执行期施加，编辑器里配好即可 |
| 由外部世界变化驱动（敌人距离、生命、视野……） | **需要一个感知器**。规划器感知不到任务之外的世界变化（第 12.2.2 节），必须由感知器把现实编码进世界状态 |
| 只是想在编辑器测试里手动改 | 不需要。测试面板会自动出现这个属性的输入框 |

### 写一个感知器（~20 行）

感知器是挂在 `HTNBrain` 同物体或其子物体上的 MonoBehaviour，
每帧被调用一次；**只在值真的变化时返回 true**（返回 true 会触发重规划，
每帧都返回 true 会导致角色不停重规划）：

```csharp
using UnityEngine;

namespace Orike.HTN
{
    // 示例：把生命值编码成三档（0=健康 1=受伤 2=濒死）
    public class HealthLevelSensor : HTNSensor
    {
        // 要写入的世界状态属性名（先在“世界状态属性”里建好）
        public string PropertyName = "WsHealthLevel";

        // 挂在角色身上的组件引用，感知器自己去找数据源
        private UnityEngine.AI.NavMeshAgent _agent;   // 换成你的数据源

        public override bool Sense(HTNBrain brain, HTNWorldState worldState)
        {
            // 1. 从游戏世界取原始数据（生命、距离、视野……）
            float health = brain.GetComponent<HealthComponent>().Value;

            // 2. 编码成决策用的档位（不要塞原始数值！见第 7 节）
            int next = health > 60 ? 0 : health > 25 ? 1 : 2;

            // 3. 变了才写、才返回 true
            worldState.TryGet(PropertyName, out int current);
            if (current == next)
            {
                return false;
            }

            worldState.TrySet(PropertyName, next);
            return true;   // 世界状态变化 → 触发重新规划
        }
    }
}
```

不想写感知器时，可以挂现成的两个示例做原型：
`HTNEnemyRangeSensor`（按与目标 Transform 的距离写三档敌人距离，
属性名 / 阈值可在 Inspector 改）和 `HTNDebugSensor`（按间隔反复施加一个效果，
免代码模拟世界变化）。

---

## 4. 怎么实现操作函数（Operator）

**概念先对齐**（第 12.2.3 节）：基元任务 = 操作函数 + 条件 + 效果。
操作函数是“NPC 真正做的事”（移动、播放动画、开火）；条件决定它能不能进计划；
效果描述它成功后世界状态怎么变。**同一个操作函数可以被多个任务复用**
（如 `MoveTo` 同时服务“冲向敌人”和“走向桥”），意义由任务的条件 / 效果赋予。

运行语义：计划执行器每帧调用当前任务的 `Update`；返回
`InProgress` 继续等、`Success` 则把任务的普通效果施加到世界状态并推进到下一
个任务、`Failure` 则放弃整个计划并重新规划。

### 步骤 1：写一个继承 `HTNOperator` 的类

```csharp
using UnityEngine;

namespace Orike.HTN
{
    // 示例 A：定时类操作（有“逐任务”私有状态，用 OperatorState 暂存）
    public class WaitForSecondsOperator : HTNOperator
    {
        public override HTNOperatorStatus Update(HTNOperatorContext context)
        {
            // OperatorState 是本任务专属的暂存位，换任务时自动清空。
            // 操作函数实例是全局共享的，所以状态必须放这里，不能放成员变量！
            float[] state = context.OperatorState as float[];

            if (state == null)
            {
                state = new float[] { ParseDuration(context.Task.Parameters) };
                context.OperatorState = state;
            }

            state[0] -= context.DeltaTime;

            return state[0] <= 0f
                ? HTNOperatorStatus.Success
                : HTNOperatorStatus.InProgress;
        }

        private static float ParseDuration(string parameters)
        {
            return float.TryParse(parameters, out float t) ? t : 1f;
        }
    }
}
```

```csharp
using UnityEngine;

namespace Orike.HTN
{
    // 示例 B：需要访问游戏组件的操作——把“有状态的部分”放到角色组件里，
    // 操作函数本身保持无状态，只做转发
    public class MoveToOperator : HTNOperator
    {
        public override HTNOperatorStatus Update(HTNOperatorContext context)
        {
            // HTNMover 是项目自己的组件（内部用 NavMeshAgent / Transform 都行），
            // 自己管理导航过程，到达返回 Success、走不过去返回 Failure
            HTNMover mover = context.Brain.GetComponent<HTNMover>();

            return mover != null
                ? mover.MoveTo(context.Task.Parameters, context.DeltaTime)
                : HTNOperatorStatus.Failure;
        }
    }
}
```

`HTNOperatorContext` 提供的东西：

| 成员 | 说明 |
|---|---|
| `Brain` | 执行计划的 HTNBrain（`.name`、`.WorldState`、`GetComponent` 都从这走） |
| `Task` | 当前基元任务（`Parameters` 在这，格式你说了算） |
| `DeltaTime` | 帧间隔 |
| `GameObject` | Brain 所在物体 |
| `OperatorState` | 逐任务私有状态暂存位（换任务自动清空），**别用成员变量存状态** |

`context.Task.Parameters` 是自由字符串，格式由你的操作函数约定
（内置 Mock 的约定是 `时长秒|日志文本`，如 `1.5|冲向敌人`）。

### 步骤 2：注册（二选一）

- **编辑器用法（推荐起步）**：什么都不用做。编辑器下拉框会通过 TypeCache
  自动列出项目里所有 `HTNOperator` 子类，**类名就是 ID**；
  运行时在编辑器里也会按类名自动查找实例化。
- **打包构建必须显式注册**（TypeCache 是编辑器专用）：
  在项目里建一个静态注册类，初始化时调用一次：

```csharp
using Orike.HTN;
using UnityEngine;

public static class GameHTNOperators
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterAll()
    {
        HTNOperatorRegistry.Register("WaitForSeconds", new WaitForSecondsOperator());
        HTNOperatorRegistry.Register("MoveTo", new MoveToOperator());
        // 内置的 "Mock" 已默认注册
    }
}
```

### 步骤 3：在编辑器里给基元任务绑操作函数

选中基元任务 → 「操作函数」下拉选择（或手填 ID）→ 「参数」填参数字符串。
条件 / 效果在下面两条列表里配。

---

## 5. 把大脑挂到角色（场景清单）

1. 角色物体挂 `HTNBrain`（会自动收集子物体上的感知器）；
2. 指定 `Domain`；
3. 感知器挂同一物体或子物体；
4. Play。

`HTNBrain` 字段：

| 字段 | 说明 |
|---|---|
| `Domain` | 定义域资产 |
| `UseMtrPriority` | 打开后重规划受当前计划 MTR 约束，只接受优先级相同或更高的新计划（第 12.8 节），避免打断仍合法的行为；MTR 约束下无解会自动放宽重试 |
| `ReplanCooldown` | 重规划最小间隔（规划失败也不会每帧空转） |
| `VerboseLog` | 输出计划生成 / 放弃日志 |
| `Sensors` | 感知器列表（留空自动收集子物体） |

代码接口：事件 `PlanCreated(HTNPlan)` / `PlanAborted(string reason)`；
`RequestReplan(string reason)` 手动触发重规划（如收到受击通知）；
只读查询 `WorldState` / `CurrentPlan` / `PlanIndex`（调试 HUD 用）。

---

## 6. 运行时行为细节（对应书中章节）

| 行为 | 实现 | 章节 |
|---|---|---|
| 正向分解 | TasksToProcess 栈，深度优先，复合任务按方法顺序选第一个条件满足的 | 12.4 |
| 回溯 | 分解历史栈：恢复任务栈 / 计划 / MTR / **工作世界状态**，从下一个方法重试 | 12.4 |
| 期望效果 | `HTNEffect.Expected`：只在规划与校验阶段施加，表达“执行中感知器理应造成的变化” | 12.7 |
| 计划校验 | 执行中用工作世界状态前向模拟剩余任务的条件与效果，失效立即重规划 | 12.5 |
| MTR 优先级 | 计划里记录每个复合任务选中的方法索引；重规划只接受优先级不降的新计划 | 12.8 |
| 递归 | 方法的子任务引用所属复合任务即可（如取新树干后回到 AttackEnemy） | 12.6 |
| 防死循环 | 分解迭代上限 20000，超限返回失败并提示检查递归终止条件 | — |

**重规划触发时机**：计划执行完毕 / 操作函数失败 / 剩余计划校验失败 /
感知器改变了世界状态。触发后受 `ReplanCooldown` 限流。

---

## 7. 设计域的建议与常见坑

- **世界状态建模成抽象档位，不要塞原始数值**：距离用三档枚举而不是米数
  （12.2.1 节），条件 `WsEnemyRange == 0` 远比 `DistanceToEnemy > 3.7` 好维护，
  而且规划器本来也推不动连续值。
- **递归必须有终止路径**：递归方法的效果要让更高优先级的方法在重新分解时
  能命中（示例域里 UprootTrunk 把耐久设回 3，方法0 的 `WsTrunkHealth > 0`
  才能重新成立）。写完递归方法，先在测试面板规划一次验证计划会终止。
- **期望效果别滥用**：只有“执行过程中由感知器达成、但任务本身不直接产生”
  的状态才用期望效果（典型：走到目标附近后“理应能看见 / 理应进入近战距离”）。
- **排查规划失败**：看测试面板日志——橙色是“方法条件不满足 / 基元任务被拒”，
  红色是回溯；沿日志从上往下找到第一个被拒的任务，检查它依赖的状态
  在前序任务效果 / 期望效果里有没有被正确设置。
- **任务执行完立刻重规划很正常**：计划走完本来就触发重规划（HTN 是
  “每次决策时规划到足够远”，不是行为树）。如果表现为没执行就反复重规划，
  检查是不是有感知器每帧都返回 true。

---

## 8. FAQ

**Q：添加一个世界状态属性后，要去改感知器的代码吗？**
见第 3 节的三种情况：任务效果驱动的不用；外部世界驱动的要写感知器
（或复用/配置现有感知器）；测试用不用。规划器、编辑器代码任何情况都不用改。

**Q：怎么实现操作函数？**
见第 4 节：写 `HTNOperator` 子类 → 类名即 ID（编辑器自动识别；打包需显式
`Register`）→ 编辑器里给基元任务选 ID 并填参数。有逐任务状态就放
`context.OperatorState`，需要游戏组件就在组件里做、操作函数转发。

**Q：计划里的任务名字和树里对不上 / 有“↻”？**
`↻` 表示递归引用（任务引用了祖先链上的任务，包括自己），只是显示标记，
不影响运行。

**Q：和隔壁 Fluid-HTN 文件夹什么关系？**
独立实现。Fluid-HTN（ptrefall）是参考用的第三方库，本目录按书第 12 章的
设计自研，带配套编辑器；两者没有代码依赖。

---

## 9. 目录结构

```
HTN/
├─ Runtime/                  运行时（不依赖编辑器）
│  ├─ HTNWorldState.cs       世界状态（属性定义 + int 数组 + Clone/Restore）
│  ├─ HTNCondition.cs        条件（属性比较断言）
│  ├─ HTNEffect.cs           效果（Set/Add/… + 期望效果标记）
│  ├─ HTNTask.cs             任务基类（复合 / 基元）
│  ├─ HTNCompoundTask.cs     复合任务 + HTNMethod 实现方法
│  ├─ HTNPrimitiveTask.cs    基元任务（操作函数 + 条件 + 效果）
│  ├─ HTNDomain.cs           定义域（世界状态属性 + 任务集合 + 根任务）
│  ├─ HTNPlanner.cs          规划器（正向分解 / 分解历史回溯 / MTR / 日志）
│  ├─ HTNOperator.cs         操作函数 + 注册表（内置 Mock；编辑器按类名自动识别）
│  ├─ HTNSensor.cs           感知器基类 + 示例（距离档位 / 调试）
│  └─ HTNBrain.cs            Agent：感知 → 规划 → 校验 → 执行
└─ Editor/                   UI Toolkit 编辑器（结构化配置，非节点图）
   ├─ HTNDomainEditorWindow.cs  主窗口（菜单 Orike/HTN Domain）
   ├─ HTNDomainTree.cs          左侧结构树（TreeView：选择 / 右键 / 拖拽）
   ├─ HTNInspectorPanel.cs      右侧任务 / 方法属性面板
   ├─ HTNListEditors.cs         条件 / 效果列表控件
   ├─ HTNPlanTestPanel.cs       底部规划测试面板（含 MTR 约束开关）
   ├─ HTNWorldStateWindow.cs    世界状态属性编辑窗口
   ├─ HTNDomainAssetOps.cs      资产创建 / 删除 / Undo
   └─ HTNExampleDomainBuilder.cs 生成书中的“树神”示例域
```

## 10. 概念对照（书中章节 ↔ 代码）

| 概念 | 实现 |
|---|---|
| 世界状态（12.2.1） | `HTNWorldState`：属性名索引的 int 数组，规划时 Clone 出工作世界状态模拟未来 |
| 感知器（12.2.2） | `HTNSensor`：把游戏世界变化编码进世界状态，变化触发重规划 |
| 基元任务（12.2.3） | `HTNPrimitiveTask` = 操作函数 + 条件 + 效果 |
| 复合任务 / 方法（12.2.4） | `HTNCompoundTask` + `HTNMethod`（条件 + 子任务，按序优先级） |
| 规划器（12.4） | `HTNPlanner`：TasksToProcess 栈 + DecompHistory 回溯 |
| 计划执行（12.5） | `HTNBrain`：逐任务驱动操作函数，成功后施加效果，前向校验剩余计划 |
| 递归（12.6） | 方法子任务引用所属复合任务 |
| 期望效果（12.7） | `HTNEffect.Expected` |
| MTR 优先级（12.8） | `HTNPlan.Mtr` + `HTNBrain.UseMtrPriority` |
