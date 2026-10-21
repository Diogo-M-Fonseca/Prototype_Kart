using UnityEngine;
using UnityEngine.InputSystem;

public class KartInputReader : MonoBehaviour
{
    [Header("Actions (formato \"Mapa/Action\")")]
    [SerializeField] private string moveActionName = "Kart/Move";
    [SerializeField] private string brakeActionName = "Kart/Brake";
    [SerializeField] private string driftActionName = "Kart/Drift";

    [Header("Ajustes")]
    [SerializeField, Range(0f, 0.5f)] private float steerDeadzone = 0.05f;

    private InputAction moveAction;
    private InputAction brakeAction;
    private InputAction driftAction;

    public float Steer
    {
        get
        {
            float x = ReadMove().x;
            return Mathf.Abs(x) < steerDeadzone ? 0f : x;
        }
    }

    public float Throttle => ReadMove().y;
    public bool Brake => brakeAction != null && brakeAction.IsPressed();
    public bool Drift => driftAction != null && driftAction.IsPressed();

    private void Awake()
    {
        InputActionAsset actions = InputSystem.actions;

        if (actions == null)
        {
            Debug.LogError("Não há Project-wide Actions definidas em Project Settings > Input System Package.", this);
            return;
        }

        BindActions(actions);
    }

    public void BindActions(InputActionAsset actions)
    {
        moveAction = Find(actions, moveActionName);
        brakeAction = Find(actions, brakeActionName);
        driftAction = Find(actions, driftActionName);
    }

    private Vector2 ReadMove() => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

    private InputAction Find(InputActionAsset actions, string actionName)
    {
        InputAction action = actions.FindAction(actionName);

        if (action == null)
            Debug.LogWarning($"Action \"{actionName}\" não encontrada nas Project-wide Actions.", this);

        return action;
    }
}
