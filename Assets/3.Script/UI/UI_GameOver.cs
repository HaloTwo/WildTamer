using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UI_GameOver : MonoBehaviour
{
    [SerializeField] Button resetBtn;
    [SerializeField] Button restartBtn;

    private void Awake()
    {
        restartBtn.onClick.AddListener(RestartGame);
        resetBtn.onClick.AddListener(ResetGame);
    }

    void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void ResetGame()
    {
        Time.timeScale = 1f;

        // 유저 데이터 초기화
        GameManager.Instance.ResetGameData();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}