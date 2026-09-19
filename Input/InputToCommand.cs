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
    [SerializeField]private float moveDeadzone = 0.2f;//方向输入死区，低于该长度的输入视为无方向
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

        //8个按键按下
        if (InputManager.Instance.Y.WasPressedThisFrame())
        {
            AddInput(KeyMap.Y_press);
        }

        if (InputManager.Instance.B.WasPressedThisFrame())
        {
            AddInput(KeyMap.B_press);
        }

        if (InputManager.Instance.A.WasPressedThisFrame())
        {
            AddInput(KeyMap.A_press);
        }

        if (InputManager.Instance.X.WasPressedThisFrame())  
        {
            AddInput(KeyMap.X_press);   
        }

        if (InputManager.Instance.RB.WasPressedThisFrame())
        {
            AddInput(KeyMap.RB_press);
        }
        if (InputManager.Instance.LB.WasPressedThisFrame())
        {
            AddInput(KeyMap.LB_press);
        }
        if (InputManager.Instance.RT.WasPressedThisFrame())
        {
            AddInput(KeyMap.RT_press);
        }
        if (InputManager.Instance.LT.WasPressedThisFrame())
        {
            AddInput(KeyMap.LT_press);
        }
        //8个按键松开
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

        if (InputManager.Instance.RB.WasReleasedThisFrame())
        {
            AddInput(KeyMap.RB_release);
        }
        if (InputManager.Instance.LB.WasReleasedThisFrame())
        {
            AddInput(KeyMap.LB_release);
        }
        if (InputManager.Instance.RT.WasReleasedThisFrame())
        {
            AddInput(KeyMap.RT_release);
        }
        if (InputManager.Instance.LT.WasReleasedThisFrame())
        {
            AddInput(KeyMap.LT_release);
        }



        //处理方向输入
        Vector2 move = InputManager.Instance.Move_Value;

        // 死区：摇杆轻微抖动不算有效方向输入
        if (move.sqrMagnitude < moveDeadzone * moveDeadzone)
        {
            AddInput(KeyMap.NoMovementInput);
        }
        else
        {
            AddInput(KeyMap.HasMovementInput);

            // 按角度切分 8 方向（0°=右，逆时针递增）。
            // 不再要求某一轴严格为 0，上下左右更容易触发。
            float angle = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            int sector = Mathf.RoundToInt(angle / 45f) % 8;

            switch (sector)
            {
                case 0: AddInput(KeyMap.Right); break;
                case 1: AddInput(KeyMap.RightUp); break;
                case 2: AddInput(KeyMap.Up); break;
                case 3: AddInput(KeyMap.LeftUp); break;
                case 4: AddInput(KeyMap.Left); break;
                case 5: AddInput(KeyMap.LeftDown); break;
                case 6: AddInput(KeyMap.Down); break;
                case 7: AddInput(KeyMap.RightDown); break;
            }
        }




        timeStamp += Time.deltaTime;
    }

    private void AddInput(KeyMap key)
    {
        input.Add(new KeyRecord{keyMap = key, pressStamp = timeStamp, frame = Time.frameCount});
    }


    //判断一个搓招序列是否成立
    public bool occurCommand(KeyCommand keySequence)
    {
        // timeLimit <= 0：只匹配本帧内按下的按键
        bool currentFrameOnly = keySequence.timeLimit <= 0f;

        float cursor = timeStamp - keySequence.timeLimit;
        bool firstKey = true;
        foreach (KeyMap key in keySequence.key)
        {
            bool found = false;
            foreach (KeyRecord keyRecord in input)
            {
                if (firstKey)
                {
                    if (currentFrameOnly)
                    {
                        if (keyRecord.frame != Time.frameCount) continue;
                    }
                    else if (keyRecord.pressStamp < cursor)
                    {
                        continue;
                    }
                }
                else if (keyRecord.pressStamp <= cursor)
                {
                    continue;
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

    RB_press = 5,
    LB_press = 6,
    RT_press = 7,
    LT_press = 8,   

    //八个方向
    Left = 9,
    LeftUp = 10,
    Up = 11,
    RightUp = 12,   
    Right = 13,
    RightDown = 14,
    Down = 15,
    LeftDown = 16,

    //方向键有输入
    HasMovementInput = 17,
    
    //方向键输入
    NoMovementInput = 18,

    RB_release = 19,
    LB_release = 20,
    RT_release = 21,
    LT_release = 22,   

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
    public int frame;
}

