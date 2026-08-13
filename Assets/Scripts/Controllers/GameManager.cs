using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public event Action<eStateGame> StateChangedAction = delegate { };

    public enum eLevelMode
    {
        NORMAL,
        AUTOPLAY,
        AUTO_LOSE,
        TIME_ATTACK
    }

    public enum eStateGame
    {
        SETUP,
        MAIN_MENU,
        GAME_STARTED,
        PAUSE,
        GAME_OVER,
    }

    private eStateGame m_state;
    public eStateGame State
    {
        get { return m_state; }
        private set
        {
            m_state = value;

            StateChangedAction(m_state);
        }
    }


    private GameSettings m_gameSettings;


    private BoardController m_boardController;

    private UIMainManager m_uiMenu;

    private LevelCondition m_levelCondition;

    public bool LastGameWon { get; private set; }

    private void Awake()
    {
        State = eStateGame.SETUP;

        m_gameSettings = Resources.Load<GameSettings>(Constants.GAME_SETTINGS_PATH);

        m_uiMenu = FindObjectOfType<UIMainManager>();
        m_uiMenu.Setup(this);
    }

    void Start()
    {
        State = eStateGame.MAIN_MENU;
    }

    internal void SetState(eStateGame state)
    {
        State = state;

        if(State == eStateGame.PAUSE)
        {
            DOTween.PauseAll();
        }
        else
        {
            DOTween.PlayAll();
        }
    }

    public void LoadLevel(eLevelMode mode)
    {
        ClearLevel();
        LastGameWon = false;

        m_boardController = new GameObject("BoardController").AddComponent<BoardController>();
        m_boardController.StartGame(this, m_gameSettings, mode);
        m_boardController.BoardClearedEvent += GameWin;
        m_boardController.BottomCellsFilledEvent += GameLose;

        if (mode == eLevelMode.TIME_ATTACK)
        {
            SetLevelConditionVisible(true);

            m_levelCondition = this.gameObject.AddComponent<LevelTime>();
            m_levelCondition.Setup(m_gameSettings.LevelTime, m_uiMenu.GetLevelConditionView(), this);
            m_levelCondition.ConditionCompleteEvent += GameLose;
        }
        else
        {
            SetLevelConditionVisible(false);
        }

        State = eStateGame.GAME_STARTED;
    }

    private void SetLevelConditionVisible(bool visible)
    {
        Text levelConditionView = m_uiMenu.GetLevelConditionView();
        if (levelConditionView != null)
        {
            levelConditionView.gameObject.SetActive(visible);
        }
    }

    public void GameWin()
    {
        LastGameWon = true;
        GameOver();
    }

    public void GameLose()
    {
        LastGameWon = false;
        GameOver();
    }

    private void GameOver()
    {
        StartCoroutine(WaitBoardController());
    }

    internal void ClearLevel()
    {
        if (m_boardController)
        {
            m_boardController.BoardClearedEvent -= GameWin;
            m_boardController.BottomCellsFilledEvent -= GameLose;
            m_boardController.Clear();
            Destroy(m_boardController.gameObject);
            m_boardController = null;
        }

        if (m_levelCondition != null)
        {
            m_levelCondition.ConditionCompleteEvent -= GameLose;
            Destroy(m_levelCondition);
            m_levelCondition = null;
        }
    }

    private IEnumerator WaitBoardController()
    {
        while (m_boardController.IsBusy)
        {
            yield return new WaitForEndOfFrame();
        }

        yield return new WaitForSeconds(1f);

        State = eStateGame.GAME_OVER;

        if (m_levelCondition != null)
        {
            m_levelCondition.ConditionCompleteEvent -= GameLose;

            Destroy(m_levelCondition);
            m_levelCondition = null;
        }
    }
}
