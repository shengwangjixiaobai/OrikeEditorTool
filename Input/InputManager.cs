using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


public class InputManager : SingleMono<InputManager>
{
    private OrikeInputConfig gameInputAction;


    //===========InputAction==================
    public InputAction Move => gameInputAction.Player.Move;
    public InputAction CameraLook => gameInputAction.Player.CameraLook;
    public InputAction Y => gameInputAction.Player.Y;
    public InputAction B => gameInputAction.Player.B;
    public InputAction A => gameInputAction.Player.A;
    public InputAction X => gameInputAction.Player.X;

    //===========InputAction Value==================
    public Vector2 Move_Value => gameInputAction.Player.Move.ReadValue<Vector2>();
    public Vector2 CameraLook_Value => gameInputAction.Player.CameraLook.ReadValue<Vector2>();
    public float Y_Value => gameInputAction.Player.Y.ReadValue<float>();
    public float B_Value => gameInputAction.Player.B.ReadValue<float>();
    public float A_Value => gameInputAction.Player.A.ReadValue<float>();
    public float X_Value => gameInputAction.Player.X.ReadValue<float>();


    protected override void Awake()
    {
        base.Awake();
        gameInputAction = new OrikeInputConfig();
    }


    private void Update()
    {
        Debug.Log(CameraLook_Value);
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

