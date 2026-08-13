using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BoardController : MonoBehaviour
{
    private const int BottomCellCount = 5;
    private const float ActionDelay = 0.5f;
    private const float MoveDuration = 0.25f;

    public event Action OnMoveEvent = delegate { };
    public event Action BoardClearedEvent = delegate { };
    public event Action BottomCellsFilledEvent = delegate { };

    public bool IsBusy { get; private set; }

    private Board m_board;
    private GameManager m_gameManager;
    private GameSettings m_gameSettings;
    private Camera m_cam;
    private bool m_gameOver;
    private bool m_timeAttackMode;
    private List<Cell> m_bottomCells = new List<Cell>();
    private Coroutine m_autoPlayCoroutine;

    public void StartGame(GameManager gameManager, GameSettings gameSettings, GameManager.eLevelMode mode)
    {
        m_gameManager = gameManager;
        m_gameSettings = gameSettings;
        m_timeAttackMode = mode == GameManager.eLevelMode.TIME_ATTACK;

        m_gameManager.StateChangedAction += OnGameStateChange;

        m_cam = Camera.main;
        m_board = new Board(this.transform, gameSettings);
        m_board.Fill();
        CreateBottomCells();

        if (mode == GameManager.eLevelMode.AUTOPLAY)
        {
            m_autoPlayCoroutine = StartCoroutine(AutoPlayWinCoroutine());
        }
        else if (mode == GameManager.eLevelMode.AUTO_LOSE)
        {
            m_autoPlayCoroutine = StartCoroutine(AutoPlayLoseCoroutine());
        }
    }

    private void CreateBottomCells()
    {
        GameObject prefabBG = Resources.Load<GameObject>(Constants.PREFAB_CELL_BACKGROUND);
        float startX = -(BottomCellCount - 1) * 0.5f;
        float y = -m_gameSettings.BoardSizeY * 0.5f - 1.25f;

        for (int i = 0; i < BottomCellCount; i++)
        {
            GameObject go = Instantiate(prefabBG, this.transform);
            go.name = "BottomCell_" + i;
            go.transform.position = new Vector3(startX + i, y, 0f);

            Cell cell = go.GetComponent<Cell>();
            cell.Setup(i, -1);
            m_bottomCells.Add(cell);
        }
    }

    private void OnGameStateChange(GameManager.eStateGame state)
    {
        switch (state)
        {
            case GameManager.eStateGame.GAME_STARTED:
                IsBusy = false;
                break;
            case GameManager.eStateGame.PAUSE:
                IsBusy = true;
                break;
            case GameManager.eStateGame.GAME_OVER:
                m_gameOver = true;
                if (m_autoPlayCoroutine != null)
                {
                    StopCoroutine(m_autoPlayCoroutine);
                    m_autoPlayCoroutine = null;
                }
                break;
        }
    }

    public void Update()
    {
        if (m_gameOver || IsBusy) return;

        if (Input.GetMouseButtonDown(0))
        {
            Cell cell = GetCellUnderPointer();
            if (cell == null || cell.IsEmpty) return;

            if (m_bottomCells.Contains(cell))
            {
                TryReturnToBoard(cell);
            }
            else
            {
                TryMoveToBottom(cell);
            }
        }
    }

    private Cell GetCellUnderPointer()
    {
        RaycastHit2D hit = Physics2D.Raycast(m_cam.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
        return hit.collider != null ? hit.collider.GetComponent<Cell>() : null;
    }

    private void TryMoveToBottom(Cell boardCell)
    {
        Cell target = m_bottomCells.FirstOrDefault(cell => cell.IsEmpty);
        if (target == null) return;

        MoveItem(boardCell, target, () =>
        {
            OnMoveEvent();
            StartCoroutine(CheckBottomCellsCoroutine());
        });
    }

    private void TryReturnToBoard(Cell bottomCell)
    {
        if (!m_timeAttackMode) return;

        Cell initialCell = bottomCell.Item.InitialCell;
        if (initialCell == null || !initialCell.IsEmpty) return;

        MoveItem(bottomCell, initialCell, OnMoveEvent);
    }

    private void MoveItem(Cell from, Cell to, Action complete)
    {
        Item item = from.Item;
        if (item == null) return;

        IsBusy = true;
        from.Free();
        to.Assign(item);
        item.SetSortingLayerHigher();
        item.AnimationMoveToPosition(MoveDuration, () =>
        {
            item.SetSortingLayerLower();
            IsBusy = false;
            complete?.Invoke();
        });
    }

    private IEnumerator CheckBottomCellsCoroutine()
    {
        IsBusy = true;
        yield return new WaitForSeconds(0.05f);

        List<Cell> matches = GetBottomMatches();
        if (matches.Count == m_gameSettings.MatchesMin)
        {
            foreach (Cell cell in matches)
            {
                cell.ExplodeItem();
            }

            yield return new WaitForSeconds(0.25f);
        }

        IsBusy = false;
        CheckEndConditions();
    }

    private List<Cell> GetBottomMatches()
    {
        return m_bottomCells
            .Where(cell => !cell.IsEmpty)
            .GroupBy(cell => ((NormalItem)cell.Item).ItemType)
            .Where(group => group.Count() == m_gameSettings.MatchesMin)
            .SelectMany(group => group)
            .ToList();
    }

    private void CheckEndConditions()
    {
        if (m_board.IsClear() && m_bottomCells.All(cell => cell.IsEmpty))
        {
            BoardClearedEvent();
            return;
        }

        if (!m_timeAttackMode && m_bottomCells.All(cell => !cell.IsEmpty))
        {
            BottomCellsFilledEvent();
        }
    }

    private IEnumerator AutoPlayWinCoroutine()
    {
        yield return new WaitForSeconds(ActionDelay);

        while (!m_gameOver && !m_board.IsClear())
        {
            Cell source = FindBestWinningMove();
            if (source == null) yield break;

            TryMoveToBottom(source);
            yield return new WaitUntil(() => !IsBusy);
            yield return new WaitForSeconds(ActionDelay);
        }
    }

    private Cell FindBestWinningMove()
    {
        List<NormalItem.eNormalType> trayTypes = m_bottomCells
            .Where(cell => !cell.IsEmpty)
            .Select(cell => ((NormalItem)cell.Item).ItemType)
            .ToList();

        if (trayTypes.Count > 0)
        {
            NormalItem.eNormalType type = trayTypes
                .GroupBy(itemType => itemType)
                .OrderByDescending(group => group.Count())
                .First()
                .Key;

            Cell matchingCell = m_board.GetFirstOccupiedCellOfType(type);
            if (matchingCell != null) return matchingCell;
        }

        Cell first = m_board.GetFirstOccupiedCell();
        if (first == null) return null;

        NormalItem firstItem = first.Item as NormalItem;
        return m_board.GetFirstOccupiedCellOfType(firstItem.ItemType);
    }

    private IEnumerator AutoPlayLoseCoroutine()
    {
        yield return new WaitForSeconds(ActionDelay);

        while (!m_gameOver && m_bottomCells.Any(cell => cell.IsEmpty))
        {
            Cell source = FindAutoLoseMove();
            if (source == null) yield break;

            TryMoveToBottom(source);
            yield return new WaitUntil(() => !IsBusy);
            yield return new WaitForSeconds(ActionDelay);
        }
    }

    private Cell FindAutoLoseMove()
    {
        HashSet<NormalItem.eNormalType> trayTypes = new HashSet<NormalItem.eNormalType>(
            m_bottomCells.Where(cell => !cell.IsEmpty).Select(cell => ((NormalItem)cell.Item).ItemType));

        Cell different = m_board.Cells.FirstOrDefault(cell =>
        {
            NormalItem item = cell.Item as NormalItem;
            return item != null && !trayTypes.Contains(item.ItemType);
        });

        return different ?? m_board.GetFirstOccupiedCell();
    }

    internal void Clear()
    {
        if (m_gameManager != null) m_gameManager.StateChangedAction -= OnGameStateChange;

        if (m_board != null) m_board.Clear();

        foreach (Cell cell in m_bottomCells)
        {
            if (cell != null)
            {
                cell.Clear();
                Destroy(cell.gameObject);
            }
        }

        m_bottomCells.Clear();
    }
}
