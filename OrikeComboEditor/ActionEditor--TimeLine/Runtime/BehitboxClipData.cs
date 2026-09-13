using System;

/// <summary>
/// Behitbox Clip（受击框）
///
/// 逻辑与 HitboxClipData 完全一致
/// 只是 ClipType = Behitbox 用于区分轨道
/// </summary>
[Serializable]
public class BehitboxClipData
    : HitboxClipData
{
    public override ClipType Type
    {
        get
        {
            return ClipType.Behitbox;
        }
    }
}
