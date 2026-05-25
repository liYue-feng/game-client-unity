using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameInput : IInputActionCollection2, IDisposable
{
    private InputActionAsset m_Asset;

    public InputActionAsset asset => m_Asset;

    public GameInput()
    {
        m_Asset = ScriptableObject.CreateInstance<InputActionAsset>();

        // Create gameplay map
        var gameplayMap = m_Asset.AddActionMap("Gameplay");

        // Create actions
        var moveAction = gameplayMap.AddAction("Move", type: InputActionType.Value, binding: "<Gamepad>/leftStick");
        moveAction.AddBinding("<Keyboard>/a,<Keyboard>/d,<Keyboard>/leftArrow,<Keyboard>/rightArrow");

        var attackAction = gameplayMap.AddAction("Attack", type: InputActionType.Button);
        attackAction.AddBinding("<Gamepad>/buttonSouth");
        attackAction.AddBinding("<Mouse>/leftButton");
        attackAction.AddBinding("<Keyboard>/j");

        var dashAction = gameplayMap.AddAction("Dash", type: InputActionType.Button);
        dashAction.AddBinding("<Gamepad>/buttonEast");
        dashAction.AddBinding("<Keyboard>/space");
        dashAction.AddBinding("<Keyboard>/k");

        var parryAction = gameplayMap.AddAction("Parry", type: InputActionType.Button);
        parryAction.AddBinding("<Gamepad>/leftTrigger");
        parryAction.AddBinding("<Keyboard>/l");
        parryAction.AddBinding("<Mouse>/rightButton");

        var pauseAction = gameplayMap.AddAction("Pause", type: InputActionType.Button);
        pauseAction.AddBinding("<Keyboard>/escape");
        pauseAction.AddBinding("<Gamepad>/start");

        var inventoryAction = gameplayMap.AddAction("Inventory", type: InputActionType.Button);
        inventoryAction.AddBinding("<Keyboard>/tab");

        // Create UI map
        var uiMap = m_Asset.AddActionMap("UI");

        var navigateAction = uiMap.AddAction("Navigate", type: InputActionType.Value);
        navigateAction.AddBinding("<Gamepad>/dpad");

        var submitAction = uiMap.AddAction("Submit", type: InputActionType.Button);
        submitAction.AddBinding("<Gamepad>/buttonSouth");
        submitAction.AddBinding("<Keyboard>/enter");

        var cancelAction = uiMap.AddAction("Cancel", type: InputActionType.Button);
        cancelAction.AddBinding("<Gamepad>/buttonEast");
        cancelAction.AddBinding("<Keyboard>/escape");

        // Set up callbacks
        gameplay = new GameplayActions(this);
        uI = new UIActions(this);
    }

    public GameplayActions gameplay { get; private set; }
    public UIActions uI { get; private set; }

    public bool enabled => m_Asset != null;

    public IEnumerable<InputBinding> bindings => m_Asset.bindings;

    public IEnumerable<InputAction> actions => m_Asset.actions;

    public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
    {
        return m_Asset.FindAction(actionNameOrId, throwIfNotFound);
    }

    public int bindingMask
    {
        get => m_Asset.bindingMask;
        set => m_Asset.bindingMask = value;
    }

    public void Enable()
    {
        m_Asset.Enable();
    }

    public void Disable()
    {
        m_Asset.Disable();
    }

    public IEnumerator GetEnumerator()
    {
        return m_Asset.GetEnumerator();
    }

    IEnumerator<InputAction> IEnumerable<InputAction>.GetEnumerator()
    {
        return ((IEnumerable<InputAction>)m_Asset).GetEnumerator();
    }

    public bool Contains(InputAction action)
    {
        return m_Asset.Contains(action);
    }

    public void Dispose()
    {
        if (m_Asset != null)
        {
            UnityEngine.Object.DestroyImmediate(m_Asset);
            m_Asset = null;
        }
    }

    public class GameplayActions
    {
        private GameInput m_Parent;

        public GameplayActions(GameInput parent)
        {
            m_Parent = parent;
        }

        public InputAction Move => m_Parent.FindAction("Gameplay/Move");
        public InputAction Attack => m_Parent.FindAction("Gameplay/Attack");
        public InputAction Dash => m_Parent.FindAction("Gameplay/Dash");
        public InputAction Parry => m_Parent.FindAction("Gameplay/Parry");
        public InputAction Pause => m_Parent.FindAction("Gameplay/Pause");
        public InputAction Inventory => m_Parent.FindAction("Gameplay/Inventory");

        public void SetCallbacks(IGameplayActions instance)
        {
            Move.performed += instance.OnMove;
            Move.canceled += instance.OnMove;
            Attack.performed += instance.OnAttack;
            Attack.canceled += instance.OnAttack;
            Dash.performed += instance.OnDash;
            Dash.canceled += instance.OnDash;
            Parry.performed += instance.OnParry;
            Parry.canceled += instance.OnParry;
            Pause.performed += instance.OnPause;
            Pause.canceled += instance.OnPause;
            Inventory.performed += instance.OnInventory;
            Inventory.canceled += instance.OnInventory;
        }
    }

    public class UIActions
    {
        private GameInput m_Parent;

        public UIActions(GameInput parent)
        {
            m_Parent = parent;
        }

        public InputAction Navigate => m_Parent.FindAction("UI/Navigate");
        public InputAction Submit => m_Parent.FindAction("UI/Submit");
        public InputAction Cancel => m_Parent.FindAction("UI/Cancel");

        public void SetCallbacks(IUIActions instance)
        {
            Navigate.performed += instance.OnNavigate;
            Navigate.canceled += instance.OnNavigate;
            Submit.performed += instance.OnSubmit;
            Submit.canceled += instance.OnSubmit;
            Cancel.performed += instance.OnCancel;
            Cancel.canceled += instance.OnCancel;
        }
    }

    public interface IGameplayActions
    {
        void OnMove(InputAction.CallbackContext context);
        void OnAttack(InputAction.CallbackContext context);
        void OnDash(InputAction.CallbackContext context);
        void OnParry(InputAction.CallbackContext context);
        void OnPause(InputAction.CallbackContext context);
        void OnInventory(InputAction.CallbackContext context);
    }

    public interface IUIActions
    {
        void OnNavigate(InputAction.CallbackContext context);
        void OnSubmit(InputAction.CallbackContext context);
        void OnCancel(InputAction.CallbackContext context);
    }
}
