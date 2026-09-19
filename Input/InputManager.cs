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

    public InputAction RB => gameInputAction.Player.RB;
    public InputAction LB => gameInputAction.Player.LB;
    public InputAction RT => gameInputAction.Player.RT;
    public InputAction LT => gameInputAction.Player.LT;

    //===========InputAction Value==================
    public Vector2 Move_Value => gameInputAction.Player.Move.ReadValue<Vector2>();
    public Vector2 CameraLook_Value => gameInputAction.Player.CameraLook.ReadValue<Vector2>();
    public float Y_Value => gameInputAction.Player.Y.ReadValue<float>();
    public float B_Value => gameInputAction.Player.B.ReadValue<float>();
    public float A_Value => gameInputAction.Player.A.ReadValue<float>();
    public float X_Value => gameInputAction.Player.X.ReadValue<float>();
    public float RB_Value => gameInputAction.Player.RB.ReadValue<float>();
    public float LB_Value => gameInputAction.Player.LB.ReadValue<float>();
    public float RT_Value => gameInputAction.Player.RT.ReadValue<float>();
    public float LT_Value => gameInputAction.Player.LT.ReadValue<float>();


    protected override void Awake()
    {
        base.Awake();
        gameInputAction = new OrikeInputConfig();
    }


    private void Update()
    {
        if(InputManager.Instance.RT.WasReleasedThisFrame())
        {
            Debug.Log("RT Released");
        }
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

