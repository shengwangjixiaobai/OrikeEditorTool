using System.Collections.Generic;
using UnityEngine;

namespace Orike.ActionGraph
{
    /// <summary>
    /// Action Graph 自动布局（Sugiyama 分层布局精简版）。
    ///
    /// 只负责计算坐标，不读写视图：
    ///   1. 把 Transition（From -> To）当作有向边，贪心 DFS 破除回边（环），
    ///      得到一个 DAG；
    ///   2. 以入口动作为第 0 层，其余节点按“到入口的最长路径”分层
    ///      （等价于最长路径分层后再整体平移，使入口始终在最左）；
    ///   3. 重心法（barycenter）上下多轮扫，减少层间连线交叉；
    ///   4. 层序号 -> X，层内序号 -> Y（从左到右展开），每层垂直居中。
    ///
    /// Auto 与 Cancel 连线方向都是 From -> To，统一按“流向”参与分层；
    /// 回边（如攻击后取消回 Idle 的环）不会额外拉高层数。
    /// </summary>
    public static class ActionGraphAutoLayout
    {
        /// <summary>层与层之间的水平间距（按节点宽度 + 端口 + 留白估算）。</summary>
        private const float LayerSpacing = 360f;

        /// <summary>同层节点之间的垂直间距（按节点高度 + 留白估算）。</summary>
        private const float NodeSpacing = 200f;

        /// <summary>入口节点与入口动作之间的水平间距。</summary>
        private const float EntrySpacing = 260f;

        /// <summary>重心法减交叉的迭代轮数。</summary>
        private const int CrossingPasses = 3;


        /// <summary>
        /// 计算全图布局坐标。
        /// </summary>
        /// <param name="graph">要布局的动作图。为空或无节点时只产出空结果。</param>
        /// <param name="positions">每个 Action 的模型坐标（节点左上角）。</param>
        /// <param name="entryPosition">入口节点的模型坐标。</param>
        public static void Compute(
            ActionGraphData graph,
            out Dictionary<Action, Vector2> positions,
            out Vector2 entryPosition)
        {
            positions =
                new Dictionary<Action, Vector2>();

            entryPosition =
                new Vector2(
                    -EntrySpacing,
                    0f);

            if (graph == null)
            {
                return;
            }

            List<Action> nodes =
                new List<Action>();

            if (graph.Actions != null)
            {
                foreach (Action action in graph.Actions)
                {
                    if (action != null)
                    {
                        nodes.Add(action);
                    }
                }
            }

            int n =
                nodes.Count;

            if (n == 0)
            {
                return;
            }

            Dictionary<Action, int> index =
                new Dictionary<Action, int>();

            for (int i = 0;
                 i < n;
                 i++)
            {
                index[nodes[i]] =
                    i;
            }


            // =========================================================
            // 建邻接表（去重有向边，忽略缺失端点与自环）
            // =========================================================

            List<int>[] adj =
                new List<int>[n];

            for (int i = 0;
                 i < n;
                 i++)
            {
                adj[i] =
                    new List<int>();
            }

            HashSet<int> seenEdges =
                new HashSet<int>();

            if (graph.Transitions != null)
            {
                foreach (TransitionData transition in graph.Transitions)
                {
                    if (transition == null ||
                        transition.From == null ||
                        transition.To == null)
                    {
                        continue;
                    }

                    if (!index.TryGetValue(
                            transition.From,
                            out int from) ||
                        !index.TryGetValue(
                            transition.To,
                            out int to))
                    {
                        continue;
                    }

                    if (from == to)
                    {
                        continue;
                    }

                    // from * n + to 在 0 <= to < n 时唯一，避免依赖引用相等
                    int key =
                        from * n +
                        to;

                    if (seenEdges.Add(key))
                    {
                        adj[from].Add(to);
                    }
                }
            }


            // =========================================================
            // 贪心破环：迭代 DFS，标记回边（回溯环上的边）
            // =========================================================

            HashSet<int> backEdges =
                new HashSet<int>();

            {
                int[] state =
                    new int[n]; // 0 未访问 / 1 访问中 / 2 完成

                int[] nextEdge =
                    new int[n];

                Stack<int> stack =
                    new Stack<int>();

                for (int s = 0;
                     s < n;
                     s++)
                {
                    if (state[s] != 0)
                    {
                        continue;
                    }

                    state[s] =
                        1;

                    nextEdge[s] =
                        0;

                    stack.Push(s);

                    while (stack.Count > 0)
                    {
                        int u =
                            stack.Peek();

                        if (nextEdge[u] <
                            adj[u].Count)
                        {
                            int v =
                                adj[u][nextEdge[u]];

                            nextEdge[u]++;

                            if (state[v] == 0)
                            {
                                state[v] =
                                    1;

                                nextEdge[v] =
                                    0;

                                stack.Push(v);
                            }
                            else if (state[v] == 1)
                            {
                                // u -> v 仍在栈上：回边
                                backEdges.Add(
                                    u * n +
                                    v);
                            }
                            // state[v] == 2：横跨 / 前向边，忽略
                        }
                        else
                        {
                            state[u] =
                                2;

                            stack.Pop();
                        }
                    }
                }
            }


            // =========================================================
            // 前向边（去掉回边）组成 DAG，求最长路径深度
            // =========================================================

            List<int>[] forwardOut =
                new List<int>[n];

            List<int>[] forwardIn =
                new List<int>[n];

            int[] inDegree =
                new int[n];

            for (int i = 0;
                 i < n;
                 i++)
            {
                forwardOut[i] =
                    new List<int>();

                forwardIn[i] =
                    new List<int>();
            }

            for (int u = 0;
                 u < n;
                 u++)
            {
                foreach (int v in adj[u])
                {
                    if (backEdges.Contains(
                            u * n +
                            v))
                    {
                        continue;
                    }

                    forwardOut[u].Add(v);

                    forwardIn[v].Add(u);

                    inDegree[v]++;
                }
            }

            int[] depth =
                new int[n];

            int[] remaining =
                new int[n];

            Queue<int> queue =
                new Queue<int>();

            for (int i = 0;
                 i < n;
                 i++)
            {
                remaining[i] =
                    inDegree[i];

                if (inDegree[i] == 0)
                {
                    queue.Enqueue(i);
                }
            }

            while (queue.Count > 0)
            {
                int u =
                    queue.Dequeue();

                foreach (int v in forwardOut[u])
                {
                    if (depth[v] <
                        depth[u] + 1)
                    {
                        depth[v] =
                            depth[u] + 1;
                    }

                    remaining[v]--;

                    if (remaining[v] == 0)
                    {
                        queue.Enqueue(v);
                    }
                }
            }


            // =========================================================
            // 层序号：整体平移，使入口动作固定在第 0 层（最左）。
            // 若图里存在“取消连回起点之前的分支”，会被放到入口左侧（负层），
            // 这是罕见情形，仍是可读的分层结果。
            // =========================================================

            int[] rank =
                new int[n];

            int shift;

            if (graph.EntryAction != null &&
                index.TryGetValue(
                    graph.EntryAction,
                    out int entryIndex))
            {
                shift =
                    depth[entryIndex];
            }
            else
            {
                shift =
                    ComputeMinDepth(depth, n);
            }

            int minRank =
                0;

            int maxRank =
                0;

            for (int i = 0;
                 i < n;
                 i++)
            {
                rank[i] =
                    depth[i] -
                    shift;

                minRank =
                    Mathf.Min(
                        minRank,
                        rank[i]);

                maxRank =
                    Mathf.Max(
                        maxRank,
                        rank[i]);
            }


            // rank 归一化到 >= 0，方便按层建列表
            int rankOffset =
                -minRank;

            int layerCount =
                rankOffset +
                maxRank +
                1;

            List<int>[] layers =
                new List<int>[layerCount];

            for (int l = 0;
                 l < layerCount;
                 l++)
            {
                layers[l] =
                    new List<int>();
            }

            for (int i = 0;
                 i < n;
                 i++)
            {
                layers[rank[i] + rankOffset].Add(i);
            }


            // =========================================================
            // 层内减交叉（重心法，多轮上下扫）
            // =========================================================

            int[] layerPos =
                ReduceCrossings(
                    layers,
                    forwardOut,
                    forwardIn,
                    rank,
                    n);


            // =========================================================
            // 坐标分配：层 -> X，层内序 -> Y，每层垂直居中
            // =========================================================

            Vector2 entryActionPos =
                Vector2.zero;

            bool entryFound =
                false;

            for (int l = 0;
                 l < layers.Length;
                 l++)
            {
                List<int> layer =
                    layers[l];

                if (layer.Count == 0)
                {
                    continue;
                }

                float totalHeight =
                    (layer.Count - 1) *
                    NodeSpacing;

                float offsetY =
                    -totalHeight *
                    0.5f;

                float x =
                    l *
                    LayerSpacing;

                for (int i = 0;
                     i < layer.Count;
                     i++)
                {
                    Vector2 pos =
                        new Vector2(
                            x,
                            offsetY +
                            i *
                            NodeSpacing);

                    positions[nodes[layer[i]]] =
                        pos;

                    if (!entryFound &&
                        graph.EntryAction != null &&
                        nodes[layer[i]] == graph.EntryAction)
                    {
                        entryActionPos =
                            pos;

                        entryFound =
                            true;
                    }
                }
            }

            entryPosition =
                new Vector2(
                    entryActionPos.x -
                    EntrySpacing,
                    entryActionPos.y);
        }


        private static int ComputeMinDepth(
            int[] depth,
            int n)
        {
            int min =
                int.MaxValue;

            for (int i = 0;
                 i < n;
                 i++)
            {
                if (depth[i] < min)
                {
                    min =
                        depth[i];
                }
            }

            return min == int.MaxValue
                ? 0
                : min;
        }


        /// <summary>
        /// 重心法计算每层内的节点顺序。
        /// 向下扫用“上一层父节点”的质心排序，向上扫用“下一层子节点”的质心排序。
        /// </summary>
        private static int[] ReduceCrossings(
            List<int>[] layers,
            List<int>[] forwardOut,
            List<int>[] forwardIn,
            int[] rank,
            int n)
        {
            // 层内顺序：初始按入列表顺序
            int[] pos =
                new int[n];

            for (int l = 0;
                 l < layers.Length;
                 l++)
            {
                for (int i = 0;
                     i < layers[l].Count;
                     i++)
                {
                    pos[layers[l][i]] =
                        i;
                }
            }

            float[] bary =
                new float[n];

            for (int pass = 0;
                 pass < CrossingPasses;
                 pass++)
            {
                // 向下：第 l 层按“第 l-1 层父节点”排序
                for (int l = 1;
                     l < layers.Length;
                     l++)
                {
                    SortLayer(
                        layers[l],
                        pos,
                        bary,
                        forwardOut,
                        forwardIn,
                        rank,
                        false);
                }

                // 向上：第 l 层按“第 l+1 层子节点”排序
                for (int l = layers.Length - 2;
                     l >= 0;
                     l--)
                {
                    SortLayer(
                        layers[l],
                        pos,
                        bary,
                        forwardOut,
                        forwardIn,
                        rank,
                        true);
                }
            }

            return pos;
        }


        /// <summary>
        /// 对单层做一次性排序：
        ///   向上 = true  ：按子节点（rank + 1）的质心；
        ///   向上 = false ：按父节点（rank - 1）的质心。
        /// 排序稳定，质心相同时保持原顺序。
        /// </summary>
        private static void SortLayer(
            List<int> layer,
            int[] pos,
            float[] bary,
            List<int>[] forwardOut,
            List<int>[] forwardIn,
            int[] rank,
            bool upward)
        {
            if (layer.Count <= 1)
            {
                return;
            }

            for (int i = 0;
                 i < layer.Count;
                 i++)
            {
                int v =
                    layer[i];

                float sum =
                    0f;

                int count =
                    0;

                if (upward)
                {
                    foreach (int child in forwardOut[v])
                    {
                        if (rank[child] ==
                            rank[v] + 1)
                        {
                            sum +=
                                pos[child];

                            count++;
                        }
                    }
                }
                else
                {
                    foreach (int parent in forwardIn[v])
                    {
                        if (rank[parent] ==
                            rank[v] - 1)
                        {
                            sum +=
                                pos[parent];

                            count++;
                        }
                    }
                }

                bary[v] =
                    count > 0
                        ? sum / count
                        : pos[v];
            }

            // 稳定排序：质心升序，相同时保持原相对顺序
            for (int i = 1;
                 i < layer.Count;
                 i++)
            {
                int key =
                    layer[i];

                float keyBary =
                    bary[key];

                int j =
                    i - 1;

                while (j >= 0 &&
                       bary[layer[j]] >
                       keyBary)
                {
                    layer[j + 1] =
                        layer[j];

                    j--;
                }

                layer[j + 1] =
                    key;
            }

            // 回写新的层内位置
            for (int i = 0;
                 i < layer.Count;
                 i++)
            {
                pos[layer[i]] =
                    i;
            }
        }
    }
}