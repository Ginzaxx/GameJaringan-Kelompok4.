using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject connectionPanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        // Pastikan menu utama aktif dan menu koneksi mati saat awal mulai
        mainMenuPanel.SetActive(true);
        connectionPanel.SetActive(false);

        playButton.onClick.AddListener(OnPlayClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void OnPlayClicked()
    {
        // Sembunyikan Main Menu, tampilkan layar Host/Client
        mainMenuPanel.SetActive(false);
        connectionPanel.SetActive(true);
    }

    private void OnQuitClicked()
    {

#if UNITY_EDITOR
        // Menghentikan Play Mode jika dijalankan di dalam Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Menutup aplikasi jika dijalankan dari file Build (.exe, .apk, dll)
        Application.Quit();
#endif
    }
}