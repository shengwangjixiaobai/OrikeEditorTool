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


        //==============记录按键输入===================

        //四个按键按下
        if (InputManager.Instance.Y.IsPressed())
        {
            AddInput(KeyMap.Y_press);
        }

        if (InputManager.Instance.B.IsPressed())
        {
            AddInput(KeyMap.B_press);
        }

        if (InputManager.Instance.A.IsPressed())
        {
            AddInput(KeyMap.A_press);
        }

        if (InputManager.Instance.X.IsPressed())
        {
            AddInput(KeyMap.X_press);   
        }
        //四个按键松开
        if(InputManager.Instance.Y.WasReleasedThisFrame())
        {
            AddInput(KeyMap.Y_release);
        }
        if(InputManager.Instance.B.WasReleasedThisFrame())
        {
            AddInput(KeyMap.B_release);
        }
        if(InputManager.Instance.A.WasReleasedThisFrame())
        {
            AddInput(KeyMap.A_release);
        }
        if(InputManager.Instance.X.WasReleasedThisFrame())
        {
            AddInput(KeyMap.X_release);
        }

        //处理方向输入
        Vector2 move = InputManager.Instance.Move_Value;
        if (move != Vector2.zero)
        {
            AddInput(KeyMap.HasMovementInput);
            
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

        }else
        {
            AddInput(KeyMap.NoMovementInput);
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

    //四个按键按下
    Y_press = 1,
    B_press = 2,
    A_press = 3,
    X_press = 4,

    //八个方向
    Left = 5,
    LeftUp = 6,
    Up = 7,
    RightUp = 8,
    Right = 9,
    RightDown = 10,
    Down = 11,
    LeftDown = 12,

    //方向键有输入
    HasMovementInput = 13,
    
    //方向键输入
    NoMovementInput = 14,

    //松开四个按键
    Y_release = 23,
    B_release = 24,
    A_release = 25,
    X_release = 26,


}

[Serializable]
public class KeyRecord
{
    public KeyMap keyMap;
    public float pressStamp;

}

