using System;
using UnityEngine;

[Serializable]
public class KeyCommand
{
    public KeyMap[] key;
    //限制时间
    public float timeLimit;

    /// <summary>
    /// 该按键序列绑定的取消 Tag。
    /// 空 = 作用于所有 Cancel（默认）；
    /// 非空 = 仅当与之配对的 Cancel Tag 匹配时才触发取消。
    /// </summary>
    [Tooltip("绑定的取消 Tag；空表示作用于所有 Cancel。")]
    public string cancelTag;
}

