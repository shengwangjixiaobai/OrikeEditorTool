using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


public class InputManagerTest : SingleMono<InputManagerTest>
{
    private OrikeInputConfig gameInputAction;


    public Vector2 Move => gameInputAction.Player.Move.ReadValue<Vector2>();
    public Vector2 CameraLook => gameInputAction.Player.CameraLook.ReadValue<Vector2>();
    public float Y => gameInputAction.Player.Y.ReadValue<float>();
    public float B => gameInputAction.Player.B.ReadValue<float>();
    public float A => gameInputAction.Player.A.ReadValue<float>();
    public float X => gameInputAction.Player.X.ReadValue<float>();

    protected override void Awake()
    {
        base.Awake();
        gameInputAction = new OrikeInputConfig();
    }


    private void Update()
    {
        Debug.Log(CameraLook);
    }

    private void EnableInput()
    {
        gameInputAction.Enable();
    }

    private void DisableInput()
    {
        gameInputAction.Disable();
    }
    
    
    private void OnEnable()
    {
        gameInputAction.Enable();
    }

    private void OnDisable()
    {
        gameInputAction.Disable();
    }
}

