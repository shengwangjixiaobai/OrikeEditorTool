using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

/// <summary>
/// 搓招输入检测器
/// </summary>
public class InputToCommand : MonoBehaviour
{
    private float timeStamp = 0f;//时间戳
    [SerializeField]private float recordTime = 0.5f;//记录搓招时间窗口的间隔
    private List<KeyRecord> input = new List<KeyRecord>();//记录输入列表



    public void Update()
    {
        //清理过期的按键记录
        int removeIndex = 0;
        while (removeIndex < input.Count)
        {
            if (input[removeIndex].pressStamp < timeStamp - recordTime)
            {
                input.RemoveAt(removeIndex);
            }
            else
            {
                removeIndex++;
            }
        }


        //记录按键输入
        if (InputManagerTest.Instance.Y > 0.5f)
        {
            AddInput(KeyMap.Y);
        }

        if (InputManagerTest.Instance.B > 0.5f)
        {
            AddInput(KeyMap.B);
        }

        if (InputManagerTest.Instance.A > 0.5f)
        {
            AddInput(KeyMap.A);
        }

        if (InputManagerTest.Instance.X > 0.5f)
        {
            AddInput(KeyMap.X);
        }
        //处理方向输入
        Vector2 move = InputManagerTest.Instance.Move;
        if (move != Vector2.zero)
        {

            if (move.x > 0)
            {
                if (move.y > 0) AddInput(KeyMap.RightUp);
                else if(move.y == 0) AddInput(KeyMap.Right);
                else AddInput(KeyMap.RightDown);
            }
            else if(move.x == 0)
            {
                if (move.y > 0) AddInput(KeyMap.Up);
                else if(move.y < 0) AddInput(KeyMap.Down);
            }
            else
            {
                if(move.y > 0)AddInput(KeyMap.LeftUp);
                else if(move.y == 0)AddInput(KeyMap.Left);
                else AddInput(KeyMap.LeftDown);
            }

        }

        timeStamp += Time.deltaTime;
    }

    private void AddInput(KeyMap key)
    {
        input.Add(new KeyRecord{keyMap = key,pressStamp = timeStamp});
    }


    //判断一个搓招序列是否成立
    public bool occurCommand(KeyCommand keySequence)
    {
        float cursor = timeStamp - keySequence.timeLimit;
        bool firstKey = true;
        foreach (KeyMap key in keySequence.key)
        {
            bool found = false;
            foreach (KeyRecord keyRecord in input)
            {
                if (firstKey)
                {
                    if (keyRecord.pressStamp < cursor) continue;
                }
                else
                {
                    if (keyRecord.pressStamp <= cursor) continue;
                }
                if (keyRecord.keyMap == key)
                {
                    //按键匹配成功
                    found = true;
                    cursor = keyRecord.pressStamp;
                    firstKey = false;
                    break;
                }
            }
            if (found) continue;
            return false;
        }

        return true;
    }

}

[Serializable]
public enum KeyMap
{
    Y = 1,
    B = 2,
    A = 3,
    X = 4,

    Left = 5,
    LeftUp = 6,
    Up = 7,
    RightUp = 8,
    Right = 9,
    RightDown = 10,
    Down = 11,
    LeftDown = 12,
}

[Serializable]
public class KeyRecord
{
    public KeyMap keyMap;
    public float pressStamp;

}

