using UnityEngine;
using UnityEngine.EventSystems;

public class AimJoystickUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick UI Elements")]
    [SerializeField] private RectTransform joystickBackground;
    [SerializeField] private RectTransform joystickHandle;

    [Header("Joystick Settings")]
    [SerializeField] private float handleRange = 100f;

    private PlayerController _localPlayerController;
    private Vector2 _inputVector = Vector2.zero;
    private Vector2 _pointerStartPosition;

    private void Start()
    {
        if (joystickBackground == null)
        {
            joystickBackground = GetComponent<RectTransform>();
        }

        if (joystickHandle == null)
        {
            // Coba cari child dengan nama "Handle" secara otomatis
            Transform handleChild = transform.Find("Handle");
            if (handleChild != null)
            {
                joystickHandle = handleChild.GetComponent<RectTransform>();
            }
        }
    }

    private void Update()
    {
        // Selama joystick sedang di-hold/drag, kirim input 2D joystick secara kontinu ke PlayerController
        if (_inputVector != Vector2.zero && _localPlayerController != null)
        {
            _localPlayerController.AimJoystickUpdate(_inputVector);
        }
    }

    public void SetPlayer(PlayerController player)
    {
        _localPlayerController = player;
    }

    public void OnPointerDown(PointerEventData eventData)
    {

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground,
            eventData.position,
            eventData.pressEventCamera,
            out _pointerStartPosition
        );

        if (_localPlayerController != null)
        {
            _localPlayerController.StartAiming();
        }

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 currentLocalPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground,
            eventData.position,
            eventData.pressEventCamera,
            out currentLocalPoint))
        {
            Vector2 position = currentLocalPoint - _pointerStartPosition;
            _inputVector = Vector2.ClampMagnitude(position / handleRange, 1.0f);

            if (joystickHandle != null)
            {
                joystickHandle.anchoredPosition = _inputVector * handleRange;
            }

            if (_localPlayerController != null)
            {
                _localPlayerController.AimJoystickUpdate(_inputVector);
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _inputVector = Vector2.zero;

        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = Vector2.zero;
        }

        if (_localPlayerController != null)
        {
            _localPlayerController.ConfirmFire();
        }
    }
}
