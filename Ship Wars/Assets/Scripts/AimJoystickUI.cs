using FishNet.Object;
using UnityEngine;
using UnityEngine.EventSystems;

public class AimJoystickUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick UI Elements")]
    [SerializeField] private RectTransform joystickBackground;
    [SerializeField] private RectTransform joystickHandle;

    [Header("Joystick Settings")]
    [SerializeField] private float handleRange = 100f;

    private CannonShoot _localCannonShoot;
    private Vector2 _inputVector = Vector2.zero;
    private Vector2 _pointerStartPosition;

    private void Start()
    {
        if (joystickBackground == null)
            joystickBackground = GetComponent<RectTransform>();

        if (joystickHandle == null)
        {
            Transform handleChild = transform.Find("Handle");
            if (handleChild != null)
                joystickHandle = handleChild.GetComponent<RectTransform>();
        }
    }

    private void Update()
    {
        // Auto-cari CannonShoot milik local player setiap frame sampai ketemu
        if (_localCannonShoot == null)
        {
            foreach (CannonShoot cs in FindObjectsByType<CannonShoot>(FindObjectsSortMode.None))
            {
                NetworkBehaviour nb = cs;
                if (nb.IsOwner)
                {
                    _localCannonShoot = cs;
                    Debug.Log("[AimJoystickUI] Linked to local player's CannonShoot.");
                    break;
                }
            }
        }

        // Selama joystick di-drag, kirim input secara kontinu
        if (_inputVector != Vector2.zero && _localCannonShoot != null)
        {
            _localCannonShoot.AimJoystickUpdate(_inputVector);
        }
    }

    /// <summary>Dipakai jika ingin assign manual dari luar (opsional).</summary>
    public void SetCannonShoot(CannonShoot cannon)
    {
        _localCannonShoot = cannon;
    }

    public void OnPointerDown(PointerEventData eventData)
    {

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground,
            eventData.position,
            eventData.pressEventCamera,
            out _pointerStartPosition
        );

        if (_localCannonShoot != null)
        {
            _localCannonShoot.StartAiming();
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

            if (_localCannonShoot != null)
            {
                _localCannonShoot.AimJoystickUpdate(_inputVector);
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

        if (_localCannonShoot != null)
        {
            _localCannonShoot.StopAiming();
        }
    }
}
